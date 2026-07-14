using NeeView.Collections.Generic;
using System;

namespace NeeView
{
    internal sealed class BookmarkTreeRules
    {
        private readonly Func<TreeListNode<IBookmarkEntry>>         _getItems;
        private readonly Action<BookmarkCollectionChangedEventArgs> _raiseBookmarkChanged;


        public BookmarkTreeRules (Func<TreeListNode<IBookmarkEntry>>         getItems,
                                  Action<BookmarkCollectionChangedEventArgs> raiseBookmarkChanged)
        {
            _getItems = getItems                         ?? throw new ArgumentNullException(nameof(getItems));
            _raiseBookmarkChanged = raiseBookmarkChanged ?? throw new ArgumentNullException(nameof(raiseBookmarkChanged));
        }


        public bool ChangeFolderKind(
            TreeListNode<IBookmarkEntry> node,
            TagGroupEntryKind kind)
        {
            if (node.Value is not BookmarkFolder folder)                                           return false;
            if (folder.FolderKind is not (TagGroupEntryKind.Category or TagGroupEntryKind.SubTag)) return false;
            if (kind is not (TagGroupEntryKind.Category or TagGroupEntryKind.SubTag))              return false;

            folder.FolderKind = kind;

            _raiseBookmarkChanged(new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Reset));

            return true;
        }


        public void AddAliasFolder (TreeListNode<IBookmarkEntry> source,
                                    TreeListNode<IBookmarkEntry> target)
        {
            if (source.Value is TagAliasFolder)                  return;
            if (source.Value is not BookmarkFolder sourceFolder) return;
            if (target.Value is not BookmarkFolder targetFolder) return;
            if (!CanCreateAliasAt(targetFolder.FolderKind))      return;
            if (!CanAliasTarget(sourceFolder.FolderKind))        return;

            var alias = new TagAliasFolder(sourceFolder.Name,
                                           source.CreateQuery().SimplePath,
                                           DateTime.Now
                            ){ FolderKind = TagGroupEntryKind.Alias };


            var node = new TreeListNode<IBookmarkEntry>(alias);

            target.Add(node);

            _raiseBookmarkChanged(new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Add,
                                                                         node.Parent,
                                                                         node));
        }


        public void PromoteParentToEdgeIfNeeded(TreeListNode<IBookmarkEntry> parent,
                                                TagGroupEntryKind? childKind)
        {
            if (parent == _getItems())                                               return;
            if (childKind is not (TagGroupEntryKind.Tag or TagGroupEntryKind.Alias)) return;
            if (parent.Value is not BookmarkFolder parentFolder)                     return;
            if (parentFolder.FolderKind is not null)                                 return;

            parentFolder.FolderKind = TagGroupEntryKind.Edge;
        }


        public static TagGroupEntryKind? GetEntryKind(
            IBookmarkEntry entry)
        {
            return entry switch
            {
                Bookmark              => TagGroupEntryKind.Bookmark,
                TagAliasFolder        => TagGroupEntryKind.Alias,
                BookmarkFolder folder => folder.FolderKind,
                _                     => null,
            };
        }


        private static bool CanCreateAliasAt(TagGroupEntryKind? parentKind)
        {
            return parentKind is null or TagGroupEntryKind.Edge;
        }


        private static bool CanAliasTarget(TagGroupEntryKind? targetKind)
        {
            return targetKind is TagGroupEntryKind.Edge  or
                                 TagGroupEntryKind.Tag   or
                                 TagGroupEntryKind.SubTag;
        }
    }
}