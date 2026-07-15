using NeeView.Collections.Generic;
using System;
using System.Collections.Generic;

namespace NeeView
{
    internal sealed class BookmarkIndexes
    {
        private readonly Func<TreeListNode<IBookmarkEntry>>             _getItems;
        private Dictionary<string, List<TreeListNode<IBookmarkEntry>>>? _bookPathToBookmarkEntries;
        private readonly TagIndexes                                     _tagIndex;  

        public BookmarkIndexes (Func<TreeListNode<IBookmarkEntry>> getItems)
        {
            _getItems = getItems ?? throw new ArgumentNullException(nameof(getItems));
            _tagIndex = new TagIndexes(_getItems);
        }

        public Dictionary<string, List<TreeListNode<IBookmarkEntry>>> GetBookPathIndex()
        {
            Ensure();
            return _bookPathToBookmarkEntries!;
        }

        public void Ensure()
        {
            if (_bookPathToBookmarkEntries is not null)
                return;

            _getItems().WithLock(root =>
            {
                var bookPathToBookmarks = new Dictionary<string, List<TreeListNode<IBookmarkEntry>>>();

                Build(root, bookPathToBookmarks);
                _bookPathToBookmarkEntries = bookPathToBookmarks;
                return 0;
            });
        }

        private static void Build (TreeListNode<IBookmarkEntry>                           node               ,
                                   Dictionary<string, List<TreeListNode<IBookmarkEntry>>> bookPathToBookmarks)
        {
            foreach (var child in node.Children)
            {
                if (child.Value is Bookmark bookmark && !string.IsNullOrEmpty(bookmark.Path))
                    AddEntry(bookPathToBookmarks, bookmark.Path, child);
                Build(child, bookPathToBookmarks);
            }
        }

        public void Invalidate ()
        {
            _bookPathToBookmarkEntries = null;
            _tagIndex.Invalidate();
        }

        public void InvalidateTagIndexes()
        {
            _tagIndex.Invalidate();
        }
        
        public void Add (TreeListNode<IBookmarkEntry>? parent,
                         TreeListNode<IBookmarkEntry>? node  )
        {
            // インデックスがまだ一度も構築されていないなら、
            // わざわざ今回の追加だけで構築しない
            if (_bookPathToBookmarkEntries is null || node?.Value is not Bookmark bookmark) return;

            AddBookmark(node, bookmark);
        }

        private void AddBookmark (TreeListNode<IBookmarkEntry> node, Bookmark bookmark)
        {
            if (string.IsNullOrEmpty(bookmark.Path)) return;

            // 本Path索引にはBookmarkノードそのものを登録
            AddEntry(_bookPathToBookmarkEntries!, bookmark.Path, node);
        }

        private static void AddEntry (Dictionary<string, List<TreeListNode<IBookmarkEntry>>> index,
                                      string                                                 key,
                                      TreeListNode<IBookmarkEntry>                           entry)
        {
            if (!index.TryGetValue(key, out var entries))
            {
                entries = new List<TreeListNode<IBookmarkEntry>>();
                index[key] = entries;
            }

            entries.Add(entry);
        }

        public void AddSubtree(TreeListNode<IBookmarkEntry>? node)
        {
            if (_bookPathToBookmarkEntries is null || node is null) return;

            if (node.Value is Bookmark bookmark)
                AddBookmark(node, bookmark);

            foreach (var child in node.WalkChildren())
            {
                if (child.Value is Bookmark childBookmark)
                    AddBookmark(child, childBookmark);
            }
        }
        
        public void Remove(TreeListNode<IBookmarkEntry>? parent,
                             TreeListNode<IBookmarkEntry>? node)
        {
            if (_bookPathToBookmarkEntries is null || node is null)
                return;

            if (node.Value is Bookmark bookmark)
            {
                RemoveBookmark(node, bookmark);
                return;
            }

            foreach (var child in node.WalkChildren())
            {
                if (child.Value is Bookmark childBookmark)
                    RemoveBookmark(child, childBookmark);
            }
        }

        public void RemoveSubtree(TreeListNode<IBookmarkEntry>? node)
        {
            if (_bookPathToBookmarkEntries is null || node is null)
                return;

            if (node.Value is Bookmark bookmark)
            {
                RemoveBookmark(node, bookmark);
            }

            foreach (var child in node.WalkChildren())
            {
                if (child.Value is Bookmark childBookmark)
                {
                    RemoveBookmark(child, childBookmark);
                }
            }
        }
        
        private void RemoveBookmark (TreeListNode<IBookmarkEntry> node, Bookmark bookmark)
        {
            if (string.IsNullOrEmpty(bookmark.Path)) return;

            if (_bookPathToBookmarkEntries!
                .TryGetValue(bookmark.Path, out var bookmarks))
            {
                bookmarks.Remove(node);

                if (bookmarks.Count == 0)
                    _bookPathToBookmarkEntries.Remove(bookmark.Path);
            }
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        public bool ContainsTagName(string name)
        {
            return _tagIndex.ContainsName(name);
        }

        public IReadOnlyList<TreeListNode<IBookmarkEntry>> FindTagsByName(string name)
        {
            return _tagIndex.FindByName(name);
        }

        public bool TryGetTagById(
            Guid id,
            out TreeListNode<IBookmarkEntry>? node)
        {
            return _tagIndex.TryGetById(id, out node);
        }
    }
}