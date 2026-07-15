using NeeView.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeeView
{
    public sealed class IndexSearcher
    {
        private readonly Func<                        TreeListNode<IBookmarkEntry  >> _getItems;
        private readonly Func<Dictionary<string, List<TreeListNode<IBookmarkEntry>>>> _getBookPathIndex;

        public IndexSearcher (Func<                        TreeListNode<IBookmarkEntry  >> getItems,
                              Func<Dictionary<string, List<TreeListNode<IBookmarkEntry>>>> getBookPathIndex)
        {
            _getItems = getItems                 ?? throw new ArgumentNullException(nameof(getItems));
            _getBookPathIndex = getBookPathIndex ?? throw new ArgumentNullException(nameof(getBookPathIndex));
        }

        public TreeListNode<IBookmarkEntry>? FindBookmarkNodeByPath(string bookPath)
        {
            if (bookPath is null) return null;

            var index = _getBookPathIndex();

            return index.TryGetValue(bookPath, out var entries)
                   ? entries.FirstOrDefault()
                   : null;
        }

        public Bookmark? Find(string path)
        { return FindBookmarkNodeByPath(path)?.Value as Bookmark; }

        public BookMementoUnit? FindUnit(string place)
        { return Find(place)?.Unit; }

        public TreeListNode<IBookmarkEntry>? FindNode(string path)
        {
            if (path is null) return null;
            return FindNode(new QueryPath(path));
        }

        public TreeListNode<IBookmarkEntry>? FindNode(QueryPath path)
        {
            if (path is null) return null;

            if (path.Scheme == QueryScheme.Bookmark)
            {
                var items = _getItems();
                if (path.Path is null) return items;

                return FindNode(items, path.Path.Split(LoosePath.Separators));
            }
            if (path.Scheme == QueryScheme.File) return FindBookmarkNodeByPath(path.SimplePath);
            return null;
        }

        private TreeListNode<IBookmarkEntry>? FindNode(TreeListNode<IBookmarkEntry> node,
                                                        IEnumerable<string> pathTokens)
        {
            if (pathTokens is null) return null;

            using var enumerator = pathTokens.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                return node;
            }

            var name = enumerator.Current;

            var child = node.WithLock(e =>
                e.Children.FirstOrDefault(x =>
                    x.Value.Name == name &&
                    x.Value is not TagAliasFolder));

            if (child is null)
            {
                return null;
            }

            var remainingTokens = pathTokens.Skip(1);

            return FindNode(child, remainingTokens);
        }

        public bool Contains(string place)
        {
            if (place is null) return false;

            return _getBookPathIndex().ContainsKey(place);
        }

        public bool Contains(TreeListNode<IBookmarkEntry> node)
        {
            if (node is null) return false;

            return _getItems() == node.Root;
        }

        public List<TreeListNode<IBookmarkEntry>> Collect(string path)
        {
            if (path is null) return new();

            var index = _getBookPathIndex();
            return index.TryGetValue(path, out var entries) ? entries.ToList() : new();
        }

        public List<TreeListNode<IBookmarkEntry>> FindTagEntriesByBookPath(string path)
        {
            return Collect(path).Select(GetTagNode)
                                .OfType<TreeListNode<IBookmarkEntry>>()
                                .Distinct()
                                .ToList();
        }

        private static TreeListNode<IBookmarkEntry>? GetTagNode (TreeListNode<IBookmarkEntry> bookmarkNode)
        {
            var parent = bookmarkNode.Parent;
            var parentKind = (parent?.Value as BookmarkFolder)?.FolderKind;

            return parentKind switch
            {
                TagGroupEntryKind.Tag      => parent,
                TagGroupEntryKind.SubTag   => parent,
                TagGroupEntryKind.Category => parent?.Parent,
                _                          => null,
            };
        }
    }
}