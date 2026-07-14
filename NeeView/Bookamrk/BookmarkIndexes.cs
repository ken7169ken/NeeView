using NeeView.Collections.Generic;
using System;
using System.Collections.Generic;

namespace NeeView
{
    internal sealed class BookmarkIndexes
    {
        private readonly Func<TreeListNode<IBookmarkEntry>>             _getItems;
        private Dictionary<string, List<TreeListNode<IBookmarkEntry>>>? _bookPathToBookmarkEntries;
        private Dictionary<string, List<TreeListNode<IBookmarkEntry>>>? _tagPathToBookEntries;


        public BookmarkIndexes (Func<TreeListNode<IBookmarkEntry>> getItems)
        {
            _getItems = getItems ?? throw new ArgumentNullException(nameof(getItems));
        }


        public Dictionary<string, List<TreeListNode<IBookmarkEntry>>> GetBookPathIndex()
        {
            Ensure();
            return _bookPathToBookmarkEntries!;
        }


        public Dictionary<string, List<TreeListNode<IBookmarkEntry>>> GetTagPathIndex()
        {
            Ensure();
            return _tagPathToBookEntries!;
        }


        public void Ensure()
        {
            if (_bookPathToBookmarkEntries is not null && _tagPathToBookEntries is not null)
                return;

            _getItems().WithLock(root =>
            {
                var bookPathToBookmarks = new Dictionary<string, List<TreeListNode<IBookmarkEntry>>>();
                var tagPathToBooks      = new Dictionary<string, List<TreeListNode<IBookmarkEntry>>>();
                                          

                Build(root, bookPathToBookmarks, tagPathToBooks);

                _bookPathToBookmarkEntries = bookPathToBookmarks;
                _tagPathToBookEntries      = tagPathToBooks;

                return 0;
            });
        }

        private static void Build (TreeListNode<IBookmarkEntry> node,
                                   Dictionary<string, List<TreeListNode<IBookmarkEntry>>> bookPathToBookmarks,
                                   Dictionary<string, List<TreeListNode<IBookmarkEntry>>> tagPathToBooks,
                                   TreeListNode<IBookmarkEntry>?                          currentTag = null)
        {
            foreach (var child in node.Children)
            {
                var nextTag = currentTag;

                if (child.Value is BookmarkFolder folder &&
                    folder.FolderKind is TagGroupEntryKind.Tag or TagGroupEntryKind.SubTag)
                {
                    nextTag = child;
                }
                else if (child.Value is Bookmark bookmark && !string.IsNullOrEmpty(bookmark.Path))
                {
                    AddEntry(bookPathToBookmarks, bookmark.Path, child);

                    if (nextTag is not null)
                    {
                        AddEntry(tagPathToBooks, nextTag.CreateQuery().SimplePath, child);
                    }
                }

                Build(child, bookPathToBookmarks, tagPathToBooks, nextTag);
            }
        }

        private static void AddEntry (Dictionary<string, List<TreeListNode<IBookmarkEntry>>> index,
                                      string                                                 key,
                                      TreeListNode<IBookmarkEntry>                           entry)
        {
            if (!index.TryGetValue(key, out var entries))
            {
                entries    = new List<TreeListNode<IBookmarkEntry>>();
                index[key] = entries;
            }

            entries.Add(entry);
        }


        public void Invalidate ()
        {
            _bookPathToBookmarkEntries = null;
            _tagPathToBookEntries      = null;
        }

        public void Add (TreeListNode<IBookmarkEntry>? parent,
                         TreeListNode<IBookmarkEntry>? node)
        {
            // インデックスがまだ一度も構築されていないなら、
            // わざわざ今回の追加だけで構築しない
            if (_bookPathToBookmarkEntries is null || _tagPathToBookEntries is null || node is null) return;

            if (node.Value is Bookmark bookmark)
            {
                AddBookmark(parent, node, bookmark);
                return;
            }

            foreach (var child in node.WalkChildren())
            {
                if (child.Value is Bookmark childBookmark)
                    AddBookmark(child.Parent, child, childBookmark);
            }
        }


        private void AddBookmark (TreeListNode<IBookmarkEntry>? parent,
                                  TreeListNode<IBookmarkEntry> node,
                                  Bookmark bookmark)
        {
            if (string.IsNullOrEmpty(bookmark.Path)) return;

            // 本Path索引にはBookmarkノードそのものを登録
            AddEntry(_bookPathToBookmarkEntries!, bookmark.Path, node);

            var tag = GetTagNode(parent);

            if (tag is not null)
                AddEntry(_tagPathToBookEntries!, tag.CreateQuery().SimplePath, node);
        }


        public void Remove(TreeListNode<IBookmarkEntry>? parent,
                             TreeListNode<IBookmarkEntry>? node)
        {
            if (_bookPathToBookmarkEntries is null || _tagPathToBookEntries is null || node is null)
                return;

            if (node.Value is Bookmark bookmark)
            {
                RemoveBookmark(parent, node, bookmark);
                return;
            }

            foreach (var child in node.WalkChildren())
            {
                if (child.Value is Bookmark childBookmark)
                    RemoveBookmark(child.Parent, child, childBookmark);
            }
        }


        private void RemoveBookmark (TreeListNode<IBookmarkEntry>? parent,
                                     TreeListNode<IBookmarkEntry> node,
                                     Bookmark bookmark)
        {
            if (string.IsNullOrEmpty(bookmark.Path)) return;

            if (_bookPathToBookmarkEntries!
                .TryGetValue(bookmark.Path, out var bookmarks))
            {
                bookmarks.Remove(node);

                if (bookmarks.Count == 0)
                    _bookPathToBookmarkEntries.Remove(bookmark.Path);
            }

            var tag = GetTagNode(parent);

            if (tag is null) return;

            var tagPath = tag.CreateQuery().SimplePath;

            if (_tagPathToBookEntries!.TryGetValue(tagPath, out var taggedBooks))
            {
                taggedBooks.Remove(node);

                if (taggedBooks.Count == 0)
                    _tagPathToBookEntries.Remove(tagPath);
            }
        }


        private static TreeListNode<IBookmarkEntry>? GetTagNode (TreeListNode<IBookmarkEntry>? parent)
        {
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