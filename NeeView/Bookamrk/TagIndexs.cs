using NeeView.Collections.Generic;
using System;
using System.Collections.Generic;

namespace NeeView
{
    ///======================================================================================================================
    internal sealed class TagIndexes
    {
        private readonly Func<TreeListNode<IBookmarkEntry>> _getItems;

        private TagName2TagNode      ? _nameIndex;
        private TagRuntimeId2TagNode ? _idIndex;

        public TagIndexes(Func<TreeListNode<IBookmarkEntry>> getItems)
        {
            _getItems = getItems ?? throw new ArgumentNullException(nameof(getItems));
        }

        public bool ContainsName(string name)
        {
            Ensure();
            return _nameIndex!.Contains(name);
        }

        public IReadOnlyList<TreeListNode<IBookmarkEntry>> FindByName(string name)
        {
            Ensure();
            return _nameIndex!.Find(name);
        }

        public bool TryGetById(Guid id, out TreeListNode<IBookmarkEntry>? node)
        {
            Ensure();
            return _idIndex!.TryGet(id, out node);
        }

        public void Ensure()
        {
            if (_nameIndex is not null && _idIndex is not null) return;

            var nameIndex = new TagName2TagNode();
            var idIndex   = new TagRuntimeId2TagNode();

            _getItems().WithLock(root =>
            {
                foreach (var node in root.WalkChildren())
                {
                    if (!TryGetTagFolder(node, out var folder)) continue;

                    nameIndex.Add(folder.Name,      node);
                    idIndex  .Add(node.RuntimeGuid, node);
                }

                return 0;
            });

            _nameIndex = nameIndex;
            _idIndex   = idIndex;
        }


        public void Invalidate()
        {
            _nameIndex = null;
            _idIndex = null;
        }

        private static bool TryGetTagFolder (TreeListNode<IBookmarkEntry> node, out BookmarkFolder folder)
        {
            if (node.Value is BookmarkFolder value                                    &&
                value.FolderKind is TagGroupEntryKind.Tag or TagGroupEntryKind.SubTag )
            {
                folder = value;
                return true;
            }

            folder = null!;
            return false;
        }
    }

    ///======================================================================================================================
    internal sealed class TagRuntimeId2TagNode
    {
        private readonly Dictionary<Guid, TreeListNode<IBookmarkEntry>>
            _items = new();

        public void Add(
            Guid id,
            TreeListNode<IBookmarkEntry> node)
        {
            _items.Add(id, node);
        }

        public bool TryGet(Guid id, out TreeListNode<IBookmarkEntry>? node)
        { return _items.TryGetValue(id, out node); }
    }


    ///======================================================================================================================
    internal sealed class TagName2TagNode
    {
        private readonly Dictionary<string, List<TreeListNode<IBookmarkEntry>>> _items = new(StringComparer.CurrentCultureIgnoreCase);

        public void Add(string? name, TreeListNode<IBookmarkEntry> node)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            if (!_items.TryGetValue(name, out var nodes))
            {
                nodes = new List<TreeListNode<IBookmarkEntry>>();
                _items.Add(name, nodes);
            }
            nodes.Add(node);
        }

        public bool Contains(string name)
        { return _items.ContainsKey(name); }


        public IReadOnlyList<TreeListNode<IBookmarkEntry>> Find(string name)
        {
            return _items.TryGetValue(name, out var nodes)
                   ? nodes
                   : Array.Empty<TreeListNode<IBookmarkEntry>>();
        }
    }
}