using CommunityToolkit.Mvvm.ComponentModel;
using NeeLaboratory.Generators;
using NeeLaboratory.Linq;
using NeeView.Collections.Generic;
using NeeView.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace NeeView {
    /// /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //public partial class BookmarkCollection
    //public class BookmarkCollectionMemento
    //public class BookmarkNode
    //public static class BookmarkNodeConverter
    //public static class TreeListNodeExtensions
    //public static class BookmarkTreeListNodeExtensions

    ///##########################################################################################################################
    ///##########################################################################################################################
    public partial class BookmarkCollection : ObservableObject
    {
        static BookmarkCollection() => Current = new BookmarkCollection();
        public static BookmarkCollection Current { get; }

        private TreeListNode<IBookmarkEntry> _items;


        private BookmarkCollection()
        {
            _items = CreateEmptyTree();

            BookmarkChanged += BookmarkCollection_BookmarkChanged;
        }


        [Subscribable]
        public event EventHandler<BookmarkCollectionChangedEventArgs>? BookmarkChanged;

        [Subscribable]
        public event EventHandler? Validated;


        public TreeListNode<IBookmarkEntry> Items
        {
            get { return _items; }
            set { SetProperty(ref _items, value); }
        }

        private void BookmarkCollection_BookmarkChanged(object? sender, BookmarkCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case EntryCollectionChangedAction.Add:
                    AddTagIndexEntry(e.Parent, e.Item);
                    break;

                case EntryCollectionChangedAction.Remove:
                    RemoveTagIndexEntry(e.Parent, e.Item);
                    break;

                case EntryCollectionChangedAction.Move:
                    // 同一フォルダー内の並び替えなのでタグインデックスは変更不要
                    break;

                case EntryCollectionChangedAction.Update:
                    // ページ更新だけならタグ関係は変わらない
                    break;

                default:
                    InvalidateTagIndexes();
                    break;
            }
        }

        private static TreeListNode<IBookmarkEntry> CreateEmptyTree()
        {
            return new TreeListNode<IBookmarkEntry>(new BookmarkFolder());
        }

        public void RaiseBookmarkChangedEvent(BookmarkCollectionChangedEventArgs e)
        {
            BookmarkChanged?.Invoke(this, e);
        }

        public void Load(TreeListNode<IBookmarkEntry> nodes, IEnumerable<BookMemento> books)
        {
            foreach (var book in books)
            {
                BookMementoCollection.Current.Set(book);
            }

            Items = nodes;

            // ルートの名前は空
            if (Items.Value is BookmarkFolder folder)
            {
                folder.Name = null;
            }

            BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Reset));
        }

        public Bookmark? Find(string path)
        {
            if (path == null) return null;

            return Items.WithLock(e => e.WalkChildren()
                .Select(e => e.Value)
                .OfType<Bookmark>()
                .FirstOrDefault(e => e.Path == path));
        }

        public BookMementoUnit? FindUnit(string place)
        {
            if (place == null) return null;

            return Find(place)?.Unit;
        }

        public TreeListNode<IBookmarkEntry>? FindNode(string path)
        {
            if (path == null) return null;

            return FindNode(new QueryPath(path));
        }

        public TreeListNode<IBookmarkEntry>? FindNode(QueryPath path)
        {
            if (path is null) return null;
            if (path.Scheme == QueryScheme.Bookmark)
            {
                if (path.Path == null)
                {
                    return Items;
                }
                return FindNode(Items, path.Path.Split(LoosePath.Separators));
            }
            else if (path.Scheme == QueryScheme.File)
            {
                return Items.WithLock(e => e.WalkChildren().FirstOrDefault(e => e.Value is Bookmark bookmark && bookmark.Path == path.SimplePath));
            }
            else
            {
                return null;
            }
        }

        private TreeListNode<IBookmarkEntry>? FindNode(TreeListNode<IBookmarkEntry> node, IEnumerable<string> pathTokens)
        {
            if (pathTokens == null) return null;

            if (!pathTokens.Any()) return node;

            var name = pathTokens.First();
            var child = node.WithLock(e => e.Children.FirstOrDefault(e => e.Value.Name == name));
            if (child != null) return FindNode(child, pathTokens.Skip(1));

            return null;
        }

        public bool Contains(string place)
        {
            if (place == null) return false;

            return Find(place) != null;
        }

        public bool Contains(TreeListNode<IBookmarkEntry> node)
        {
            return Items == node.Root;
        }

        public List<TreeListNode<IBookmarkEntry>> Collect(string path)
        {
            if (path == null) return new();

            return Items.WithLock(e => e.WalkChildren()
                .Where(e => e.Value is Bookmark bookmark && bookmark.Path == path)
                .ToList());
        }

        ///======================================================================================================================
        // ここから追加。
        private Dictionary<string, List<TreeListNode<IBookmarkEntry>>>? _bookPathToTagEntries;
        private Dictionary<string, List<TreeListNode<IBookmarkEntry>>>? _tagPathToBookEntries;

        ///----- - ----- -
        public List<TreeListNode<IBookmarkEntry>> ManageTagEntries(string path)
        {
            EnsureTagIndexes();

            return _bookPathToTagEntries!.TryGetValue(path, out var entries) ? entries : new();
        }

        ///----- - ----- -
        //int count = 0;
        private void EnsureTagIndexes()
        {
            if (_bookPathToTagEntries != null && _tagPathToBookEntries != null) return;

            Items.WithLock(root =>
            {
                var bookPathToTags = new Dictionary<string, List<TreeListNode<IBookmarkEntry>>>();
                var tagPathToBooks = new Dictionary<string, List<TreeListNode<IBookmarkEntry>>>();

                BuildTagIndexes(root, bookPathToTags, tagPathToBooks);

                _bookPathToTagEntries = bookPathToTags;
                _tagPathToBookEntries = tagPathToBooks;

                return 0;
            });
        }

        ///----- - ----- -
        // 本→タグ、本←タグの双方向インデックスをそれぞれ作る。
        int bookmarkCount = 0;
        int aliasCount = 0;
        private void BuildTagIndexes(
            TreeListNode<IBookmarkEntry>                           node,
            Dictionary<string, List<TreeListNode<IBookmarkEntry>>> bookPathToTags,
            Dictionary<string, List<TreeListNode<IBookmarkEntry>>> tagPathToBooks,
            TreeListNode<IBookmarkEntry> ?                         currentTag = null)
        {
            foreach (var child in node.Children)
            {
                var nextTag = currentTag;

                if (child.Value is TagAliasFolder alias)
                {
                    AddAliasTagEntries(bookPathToTags, tagPathToBooks, child, alias);
                    continue;
                }
                else if (child.Value is BookmarkFolder folder && folder.FolderKind == TagGroupEntryKind.Tag)
                {
                    nextTag = child;
                }
                else if (child.Value is Bookmark bookmark && bookmark.Path != null && nextTag != null)
                {
                    AddTagEntry(bookPathToTags, bookmark.Path, nextTag);
                    AddTagEntry(tagPathToBooks, nextTag.CreateQuery().SimplePath, child);
                }

                BuildTagIndexes(child, bookPathToTags, tagPathToBooks, nextTag);
            }
        }

        ///----- - ----- -
        private void AddTagEntry(
            Dictionary<string, List<TreeListNode<IBookmarkEntry>>> index,
            string                                                 path,
            TreeListNode<IBookmarkEntry>                           entry)
        {
            if (!index.TryGetValue(path, out var list))
            {
                list = new List<TreeListNode<IBookmarkEntry>>();
                index[path] = list;
            }

            list.Add(entry);
        }

        ///----- - ----- -
        private void AddAliasTagEntries(
            Dictionary<string, List<TreeListNode<IBookmarkEntry>>> bookPathToTags,
            Dictionary<string, List<TreeListNode<IBookmarkEntry>>> tagPathToBooks,
            TreeListNode<IBookmarkEntry>                           aliasNode,
            TagAliasFolder                                    alias)
        {
            if (alias.AliasTarget is null) return;

            var targetNode = FindAliasTargetNode(alias.AliasTarget);
            if (targetNode == null) return;

            foreach (var targetChild in targetNode.WalkChildren())
            {
                if (targetChild.Value is Bookmark bookmark && bookmark.Path != null)
                {
                    AddTagEntry(bookPathToTags, bookmark.Path, aliasNode);
                    AddTagEntry(tagPathToBooks, aliasNode.CreateQuery().SimplePath, targetChild);
                }
            }
        }

        ///----- - ----- -
        private TreeListNode<IBookmarkEntry>? FindAliasTargetNode(string? aliasTarget)
        {
            if (aliasTarget is null) return null;
            var path = new QueryPath(aliasTarget);

            if (path.Scheme != QueryScheme.Bookmark || path.Path is null) return null;
            return FindNode(Items, path.Path.Split(LoosePath.Separators));
        }

        ///----- - ----- -
        private void InvalidateTagIndexes()
        {
            _bookPathToTagEntries = null;
            _tagPathToBookEntries = null;
        }

        ///----- - ----- - ----- ----- - ----- - ----- ----- - ----- - ----- ----- - ----- - ----- ----- - ----- - ----- ----- -
        ///----- - ----- - ----- ----- - ----- - ----- ----- - ----- - ----- ----- - ----- - ----- ----- - ----- - ----- ----- -
        private void AddTagIndexEntry(TreeListNode<IBookmarkEntry>? parent, TreeListNode<IBookmarkEntry>? node)
        {
            if (_bookPathToTagEntries is null || _tagPathToBookEntries is null) return;
            if (node is null)                                                   return;

            if (node.Value is Bookmark bookmark)
            {
                AddBookmarkToTagIndexes(parent, node, bookmark);
                return;
            }

            foreach (var child in node.WalkChildren())
            {
                if (child.Value is Bookmark childBookmark)
                    AddBookmarkToTagIndexes(child.Parent, child, childBookmark);
            }
        }

        ///----- - ----- -
        private void AddBookmarkToTagIndexes(TreeListNode<IBookmarkEntry>? parent, TreeListNode<IBookmarkEntry> node, Bookmark bookmark)
        {
            if (string.IsNullOrEmpty(bookmark.Path)) return;

            var parentKind = (parent?.Value as BookmarkFolder)?.FolderKind;

            if (!TagGroupkFolderKindTools.CanCreateChild(parentKind, TagGroupEntryKind.Bookmark)) return;

            var tag = parentKind switch
            {
                TagGroupEntryKind.Tag => parent,
                TagGroupEntryKind.Category => parent!.Parent,
                _ => null,
            };

            if (tag is null) return;

            AddTagEntry(_bookPathToTagEntries!, bookmark.Path, tag);
            AddTagEntry(_tagPathToBookEntries!, tag.CreateQuery().SimplePath, node);
        }

        ///----- - ----- - ----- ----- - ----- - ----- ----- - ----- - ----- ----- - ----- - ----- ----- - ----- - ----- ----- -
        ///----- - ----- - ----- ----- - ----- - ----- ----- - ----- - ----- ----- - ----- - ----- ----- - ----- - ----- ----- -
        private void RemoveTagIndexEntry(TreeListNode<IBookmarkEntry>? parent, TreeListNode<IBookmarkEntry>? node)
        {
            if (_bookPathToTagEntries is null || _tagPathToBookEntries is null) return;
            if (node is null)                                                   return;

            if (node.Value is Bookmark bookmark)
            {
                RemoveBookmarkFromTagIndexes(parent, node, bookmark);
                return;
            }

            foreach (var child in node.WalkChildren())
            {
                if (child.Value is Bookmark childBookmark)
                    RemoveBookmarkFromTagIndexes(child.Parent, child, childBookmark);
            }
        }

        ///----- - ----- -
        private void RemoveBookmarkFromTagIndexes(TreeListNode<IBookmarkEntry>? parent, TreeListNode<IBookmarkEntry> node, Bookmark bookmark)
        {
            if (string.IsNullOrEmpty(bookmark.Path)) return;

            var parentKind = (parent?.Value as BookmarkFolder)?.FolderKind;

            if (!TagGroupkFolderKindTools.CanCreateChild(parentKind, TagGroupEntryKind.Bookmark)) return;

            var tag = parentKind switch
            {
                TagGroupEntryKind.Tag => parent,
                TagGroupEntryKind.Category => parent!.Parent,
                _ => null,
            };

            if (tag is null) return;

            if (_bookPathToTagEntries!.TryGetValue(bookmark.Path, out var tags))
            {
                tags.Remove(tag);
                if (tags.Count == 0)
                    _bookPathToTagEntries.Remove(bookmark.Path);
            }

            var tagPath = tag.CreateQuery().SimplePath;

            if (_tagPathToBookEntries!.TryGetValue(tagPath, out var books))
            {
                books.Remove(node);
                if (books.Count == 0)
                    _tagPathToBookEntries.Remove(tagPath);
            }
        }

        ///----- - ----- -
        public void AddAliasFolder(TreeListNode<IBookmarkEntry> source, TreeListNode<IBookmarkEntry> target)
        {
            if (source.Value is TagAliasFolder) return;
            if (source.Value is not BookmarkFolder sourceFolder) return;
            if (target.Value is not BookmarkFolder) return;

            var alias = new TagAliasFolder(sourceFolder.Name, source.CreateQuery().SimplePath, DateTime.Now)
            {
                FolderKind = TagGroupEntryKind.Alias
            };
            var node = new TreeListNode<IBookmarkEntry>(alias);

            target.Add(node);

            BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Add, node.Parent, node));
        }
        // ここまで。
        ///======================================================================================================================
        public bool CopyBookmarkToChild(TreeListNode<IBookmarkEntry> item, TreeListNode<IBookmarkEntry> target)
        {
            if (item?.Value is not Bookmark bookmark) return false;
            if(target?.Value is not BookmarkFolder folder) throw new ArgumentException("target must be BookmarkFolder");
            if (!TagGroupkFolderKindTools.CanCreateChild(folder.FolderKind, TagGroupEntryKind.Bookmark))
            {
                ToastService.Current.Show(new Toast(
                    "ブックマークはタグまたは分類フォルダーにのみコピーできます。",
                    null,
                    ToastIcon.Warning));

                return false;
            }

            var copiedEntry = (IBookmarkEntry)bookmark.Clone();
            var copiedNode = new TreeListNode<IBookmarkEntry>(copiedEntry);

            AddToChild(copiedNode, target);
            target.IsExpanded = true;

            return true;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        // TODO: 重複チェックをここで行う
        public void AddToChild(TreeListNode<IBookmarkEntry> node, TreeListNode<IBookmarkEntry> parent)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            parent = parent ?? Items.Root;

            if (node.Value is Bookmark && !TagGroupkFolderKindTools.CanCreateChild((parent.Value as BookmarkFolder)?.FolderKind, TagGroupEntryKind.Bookmark))
            {
                ToastService.Current.Show( new Toast("ブックマークはタグまたは分類フォルダーにのみ作成できます。", null, ToastIcon.Warning) );
                return;
            }
            else if (node.Value is TagAliasFolder && !TagGroupkFolderKindTools.CanCreateChild((parent.Value as BookmarkFolder)?.FolderKind, TagGroupEntryKind.Alias))
            {
                ToastService.Current.Show(new Toast("エイリアスは中継フォルダーにのみ作成できます。", null, ToastIcon.Warning));
                return;
            }

            parent.Add(node);
            BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Add, node.Parent, node));
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        public void Restore(TreeListNodeMemento<IBookmarkEntry> memento)
        {
            if (memento == null) throw new ArgumentNullException(nameof(memento));

            if (!Contains(memento.Parent))
            {
                return;
            }

            var index = memento.Index > memento.Parent.Count ? memento.Parent.Count : memento.Index;
            memento.Parent.Insert(index, memento.Node);
            BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Add, memento.Node.Parent, memento.Node));
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        public bool Remove(TreeListNode<IBookmarkEntry>? node)
        {
            if (node == null)            return false;
            if (node.Parent is null)     return false;
            if (node.Root != Items.Root) throw new InvalidOperationException();

            var parent = node.Parent;

            if (!node.RemoveSelf()) return false;

            BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Remove, parent, node));
            return true;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        /// <summary>
        /// ブックマークの修復
        /// </summary>
        /// <remarks>
        /// リンク切れのブックマークの修復を試み、それでも失敗する場合はリンク切れフラグをつける
        /// </remarks>
        /// <param name="progress"></param>
        /// <param name="token"></param>
        /// <returns>リンク切れ項目数</returns>
        public async Task<int> ResolveUnlinkedAsync(IProgress<ProgressContext>? progress, CancellationToken token)
        {
            var nodes = Items.WithLock(e => e.WalkChildren().Where(e => e.Value is Bookmark).ToList());

            var progressContext = new ProgressContext("", 0.0, true);

            int count = 0;
            int unlinkedCount = 0;
            foreach (var node in nodes)
            {
                count++;

                var bookmark = (Bookmark)node.Value;

                progressContext.Message = node.Name;
                progressContext.ProgressValue = (double)count / nodes.Count;
                progress?.Report(progressContext);

                bookmark.IsUnlinked = false;
                if (!await ArchiveEntryUtility.ExistsAsync(bookmark.Path, false, token))
                {
                    var resolved = FileResolver.Current.ResolveArchivePath(bookmark.Path);
                    if (resolved != null)
                        bookmark.Path = resolved.Path;
                    else                  
                    {
                        bookmark.IsUnlinked = true;
                        unlinkedCount++;
                    }
                }
            }

            BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Replace));

            return unlinkedCount;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        /// <summary>
        /// Unlinked フラグのたったブックマークを収集
        /// </summary>
        /// <returns></returns>
        public List<TreeListNode<IBookmarkEntry>> CollectUnlinked()
        {
            return Items.WithLock(e => e.WalkChildren().Where(e => e.Value is Bookmark bookmark && bookmark.IsUnlinked).ToList());
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        // ・ルート/中継の下に作る → null 中継フォルダー
        // ・Tag の下に作る → Category
        // ・Category の下 → 禁止
        // ・Alias の下 → 禁止
        /// <summary>
        /// 新しいフォルダーを追加
        /// </summary>
        public TreeListNode<IBookmarkEntry>? AddNewFolder(
            TreeListNode<IBookmarkEntry> target,
            string?                      name,
            bool                         isExpand = true,
            TagGroupEntryKind ?         childKind = null
        ){
            if (target == Items || target.Value is BookmarkFolder)
            {
                var parentKind = (target.Value as BookmarkFolder)?.FolderKind;

                if (!TagGroupkFolderKindTools.CanCreateChild(parentKind, childKind))
                    return null;

                var ignoreNames = 
                    target.WithLock(e => e.Children.Where(e => e.Value is BookmarkFolder).Select(e => e.Value.Name).WhereNotNull().ToList());

                var validName = GetValidateFolderName(ignoreNames, name, TextResources.GetString("Word.NewFolder"));
                var folder = new BookmarkFolder(validName, null, DateTime.Now){ FolderKind = childKind };
                var node = new TreeListNode<IBookmarkEntry>(folder);
                target.Add(node);

                if (isExpand) target.IsExpanded = true;

                BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Add, node.Parent, node));

                return node;
            }

            return null;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        /// <summary>
        /// 移動 (汎用)
        /// </summary>
        /// <remarks>
        /// 同階層の移動だけでなく、異なる階層の移動や新規挿入にも対応している。
        /// </remarks>
        /// <param name="parent">移動先階層</param>
        /// <param name="item">移動元項目</param>
        /// <param name="newIndex">移動位置</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public bool Move(TreeListNode<IBookmarkEntry> parent, TreeListNode<IBookmarkEntry> item, int newIndex)
        {
            if (item.Value is Bookmark && !TagGroupkFolderKindTools.CanCreateChild((parent.Value as BookmarkFolder)?.FolderKind, TagGroupEntryKind.Bookmark))
            {
                ToastService.Current.Show( new Toast(
                    "ブックマークはタグまたは分類フォルダーにのみ移動できます。",
                    null,
                    ToastIcon.Warning));

                return false;
            }

            newIndex = Math.Clamp(newIndex, 0, parent.Count);

            // 親がいないときは挿入
            if (item.Parent is null)
            {
                // 親がいないのは新しいエントリなので重複を除外する
                var itemPath = (item.Value as Bookmark)?.Path;
                if (itemPath is null) return false;

                var node = parent.WithLock(e => e.Children.FirstOrDefault(e => e.Value is Bookmark bookmark && bookmark.Path == itemPath));
                if (node is not null) return false;

                // 新しい項目として挿入する
                parent.Insert(newIndex, item);
                BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Add, item.Parent, item) { NewIndex = item.GetIndex() });
                return true;
            }

            if (parent == item || parent.ParentContains(item))// 親を子には移動できない
                throw new InvalidOperationException("Can't move a parent to a child.");

            var isChangeDirectory = item.Parent != parent;
            if (isChangeDirectory)
            {
                var oldParent = item.Parent;
                if (item.RemoveSelf()) BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Remove, oldParent, item));

                parent.Insert(newIndex, item);
                BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Add, item.Parent, item) { NewIndex = item.GetIndex() });

                return true;
            }
            else
            {
                var oldIndex = item.GetIndex();
                Move(parent, oldIndex, newIndex);
                return true;
            }
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        private void Move(TreeListNode<IBookmarkEntry> parent, int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex) return;

            var item = parent[oldIndex];
            var target = parent[newIndex];
            parent.Move(oldIndex, newIndex);

            BookmarkChanged?.Invoke(
                this,
                new BookmarkCollectionChangedEventArgs(
                    EntryCollectionChangedAction.Move, item.Parent, item
                ) { Target = target, OldIndex = oldIndex, NewIndex = newIndex }
            );
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        public bool MoveToChild(TreeListNode<IBookmarkEntry> item, TreeListNode<IBookmarkEntry> target)
        {
            if (target != Items && target.Value is not BookmarkFolder) return false;
            if (item.Parent == target) return false;

            // AliasへのD&Dは実体TagへのD&Dとして扱う
            if (target.Value is TagAliasFolder alias)
            {
                var real = FindNode(new QueryPath(alias.AliasTarget));
                if (real is null) return false;

                target = real;
            }

            if (item.Value is BookmarkFolder folder)
            {
                if (!TagGroupkFolderKindTools.CanCreateChild((target.Value as BookmarkFolder)?.FolderKind, folder.FolderKind))
                {
                    ToastService.Current.Show(new Toast(
                        $"{GetFolderKindText((target.Value as BookmarkFolder)?.FolderKind)}に{GetFolderKindText(folder.FolderKind)}を移動することは禁則事項で出来ません。",
                        null,
                        ToastIcon.Warning));
                    return false;
                }
                if (target.ParentContains(item))
                    return false;

                var conflict = target.WithLock(e =>
                    e.Children.FirstOrDefault(e => folder.IsEqual(e.Value)));

                if (conflict != null) return Merge(item, conflict);
                else return MoveToChildInner(item, target);
            }
            else if (item.Value is Bookmark bookmark)
            {
                var conflict = target.WithLock(e => e.Children.FirstOrDefault(e => bookmark.IsEqual(e.Value)));

                if (conflict != null) return Remove(item);
                else return MoveToChildInner(item, target);
            }

            return false;
        }

        ///----- - ----- - 
        private static string GetFolderKindText(TagGroupEntryKind? kind)
        {
            return kind switch
            {
                null => "中継フォルダー",
                TagGroupEntryKind.Tag => "タグフォルダー",
                TagGroupEntryKind.Category => "分類フォルダー",
                TagGroupEntryKind.Alias => "エイリアス",
                _ => kind.ToString() ?? "不明"
            };
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        private bool MoveToChildInner(TreeListNode<IBookmarkEntry> item, TreeListNode<IBookmarkEntry> target)
        {
            if (item == target)              return false;
            if (target.ParentContains(item)) return false; // TODO: 例外にすべき？
            if (item.Value is Bookmark && !TagGroupkFolderKindTools.CanCreateChild((target.Value as BookmarkFolder)?.FolderKind, TagGroupEntryKind.Bookmark))
            {
                ToastService.Current.Show(new Toast(
                    "ブックマークはタグまたは分類フォルダー、アリエス・フォルダーにのみ移動できます。",
                    null,
                    ToastIcon.Warning));

                return false;
            }
            var parent    = item.Parent;
            var isRemoved = item.RemoveSelf();
            if (isRemoved) BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Remove, parent, item));

            target.Insert(0, item);
            target.IsExpanded = true;
            BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Add, item.Parent, item));

            return true;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        public bool Merge(TreeListNode<IBookmarkEntry> item, TreeListNode<IBookmarkEntry> target)
        {
            if (item?.Value is not BookmarkFolder) throw new ArgumentException("item must be BookmarkFolder");
            if (target?.Value is not BookmarkFolder) throw new ArgumentException("target must be BookmarkFolder");

            var parent = item.Parent;
            if (item.RemoveSelf())
            {
                BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Remove, parent, item));
            }

            foreach (var child in item.CloneChildren())
            {
                child.RemoveSelf();
                if (child.Value is BookmarkFolder folder)
                {
                    var conflict = target.WithLock(e => e.Children.FirstOrDefault(e => folder.IsEqual(e.Value)));
                    if (conflict != null)
                    {
                        Merge(child, conflict);
                        continue;
                    }
                }
                else if (child.Value is Bookmark bookmark)
                {
                    var conflict = target.WithLock(e => e.Children.FirstOrDefault(e => bookmark.IsEqual(e.Value)));
                    if (conflict != null) continue;
                }

                target.Add(child);
                BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Add, target, child));
            }

            return true;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        public void Rename(string src, string dst)
        {
            List<TreeListNode<IBookmarkEntry>> renames = new();

            Items.WithLock(e =>
            {
                foreach (var item in e.WalkChildren())
                {
                    if (item.Value is Bookmark bookmark && bookmark.Path == src)
                    {
                        bookmark.Path = dst;
                        renames.Add(item);
                    }
                }
            });

            foreach (var item in renames)
                BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Rename, item.Parent, item));
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        private static string GetValidateFolderName(IEnumerable<string> names, string? name, string defaultName)
        {
            name = BookmarkTools.GetValidateName(name);
            if (string.IsNullOrWhiteSpace(name)) name = defaultName;

            if (names.Contains(name))
            {
                int count = 1;
                string newName;
                do newName = $"{name} ({++count})";
                while (names.Contains(newName));

                name = newName;
            }

            return name;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        private void ValidateFolderName(TreeListNode<IBookmarkEntry> node)
        {
            var names = new List<string>();

            foreach (var child in node.WithLock(e => e.Children.Where(e => e.Value is BookmarkFolder).ToList()))
            {
                ValidateFolderName(child);

                var folder = ((BookmarkFolder)child.Value);

                var name = BookmarkTools.GetValidateName(folder.Name);
                if (string.IsNullOrWhiteSpace(name)) name = "_";
                if (names.Contains(name))
                {
                    int count = 1;
                    string newName = name;
                    do newName = $"{name} ({++count})";
                    while (names.Contains(newName));

                    name = newName;
                }
                names.Add(name);
                folder.Name = name;
            }
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        /// <summary>
        /// 情報更新
        /// </summary>
        /// <param name="memento">新しい情報</param>
        /// <param name="isForce">変更がなくても更新する</param>
        public void Update(BookMemento memento, bool isForce)
        {
            var node = FindNode(memento.Path);
            if (node is null) return;
            if (node.Value is not Bookmark bookmark) return;
            if (!isForce && bookmark.Unit.Memento.IsEquals(memento)) return;

            bookmark.Unit.Memento = memento;

            if(bookmark.OpenPageMode == BookmarkOpenPageMode.Resume)
            {
                bookmark.BookmarkPage = memento.Page;
                bookmark.BookmarkProps = memento.ToPropertiesString();
            }
            BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Update, node.Parent, node));
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        #region Memento
        // memento作成
        public BookmarkCollectionMemento CreateMemento()
        {
            var memento = new BookmarkCollectionMemento();
            memento.Nodes = BookmarkNodeConverter.ConvertFrom(Items);

            return memento;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        // memento適用
        public RestoreResult Restore(BookmarkCollectionMemento? memento)
        {
            if (memento is null) return RestoreResult.None;

            RestoreResult result = RestoreResult.None;

            if (memento.QuickAccessLegacy is not null)
            {
                QuickAccessCollection.Current.Restore(memento.QuickAccessLegacy);
                result |= RestoreResult.RestoreQuickAccess;
            }

            if (memento.Nodes is not null)
            {
                var nodes = BookmarkNodeConverter.ConvertToTreeListNode(memento.Nodes) ?? CreateEmptyTree();
                var books = memento.Nodes.Walk().Where(e => e.IsBookmark).Select(e => BookMemento.ParseWithProperties(e.Path!, e.Page, e.Props)).WhereNotNull().ToList();
                this.Load(nodes, books);

                result |= RestoreResult.RestoreBookmark;

                // 互換用 : FileResolver 登録
                if (books.Count != 0 && memento.Format?.CompareTo(new FormatVersion(BookmarkCollectionMemento.FormatName, VersionNumber.Ver45_Alpha4)) <= 0)
                {
                    var files = books.Select(e => e.Path).ToList();
                    ProcessJobEngine.Current.AddJob("Processing bookmarks",
                        () =>
                        {
                            FileResolver.Current.AddRangeArchivePath(files);
                            Validated?.Invoke(this, EventArgs.Empty);
                        });

                    result |= RestoreResult.AddToFileResolver;
                }
            }

            return result;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        [Flags]
        public enum RestoreResult
        {
            None,
            RestoreBookmark = 1 << 0,
            RestoreQuickAccess = 1 << 1,
            AddToFileResolver = 1 << 2,
        }

        #endregion
    }

    ///##########################################################################################################################
    ///##########################################################################################################################
    [Memento]
    public class BookmarkCollectionMemento
    {
        public static string FormatName => Environment.SolutionName + ".Bookmark";

        public FormatVersion? Format { get; set; }

        public BookmarkNode? Nodes { get; set; }

        #region Obsolete

        [Obsolete] // v46.0
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
        public List<BookMemento>? Books { get; set; }

        [JsonPropertyName("QuickAccess")] // v46.0
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
        public QuickAccessCollectionMemento? QuickAccessLegacy { get; set; }

        #endregion

        public BookmarkCollectionMemento()
        {
            Nodes = new BookmarkNode();
        }


        public void Save(string path, string? backupFileName)
        {
            Format = new FormatVersion(FormatName);

            var json = JsonSerializer.SerializeToUtf8Bytes(this, UserSettingTools.GetSerializeOptions());
            FileIO.WriteAllBytesDurable(path, json, backupFileName);
        }

        public static BookmarkCollectionMemento Load(string path)
        {
            using var stream = FileIO.OpenReadShared(path);
            return Load(stream);
        }

        public static BookmarkCollectionMemento Load(Stream stream)
        {
            var memento = JsonSerializer.Deserialize<BookmarkCollectionMemento>(stream, UserSettingTools.GetDeserializeOptions());
            if (memento is null) throw new FormatException();
            return memento.Validate();
        }
    }

    ///##########################################################################################################################
    ///##########################################################################################################################
    public enum TagGroupEntryKind
    {
        Edge,
        Tag,
        Alias,
        Category,
        Bookmark
    }

    ///##########################################################################################################################
    ///##########################################################################################################################
    public static class TagGroupkFolderKindTools
    {
        public static bool CanCreateChild(TagGroupEntryKind? parentKind, TagGroupEntryKind? childKind)
        {
            return parentKind switch
            {
                null                       => childKind is null or TagGroupEntryKind.Edge or TagGroupEntryKind.Tag or TagGroupEntryKind.Alias, // 中継フォルダーに適用されるルール
                TagGroupEntryKind.Edge     => childKind is TagGroupEntryKind.Tag or TagGroupEntryKind.Alias,                                   // 中継終端フォルダーに適用されるルール
                TagGroupEntryKind.Tag      => childKind is TagGroupEntryKind.Category or TagGroupEntryKind.Bookmark,                           // タグ・フォルダーに適用されるルール
                TagGroupEntryKind.Category => childKind is TagGroupEntryKind.Bookmark,                                                         // 分類フォルダーに適用されるルール
                TagGroupEntryKind.Alias    => false,                                                                                           // エリアス・フォルダーに適用されるルール
                _                          => false,
            };
        }
    }

    ///##########################################################################################################################
    ///##########################################################################################################################
    public class BookmarkNode
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Path { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Color? Color { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime EntryTime { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] //追加。JSON保存用。
        public string? Page { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] //追加。JSON保存用。
        public string? Props { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] //追加。JSON保存用。
        public string? SortGroup { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] //追加。JSON保存用。
        public int SortIndex { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]  //追加。JSON保存用。
        public BookmarkOpenPageMode OpenPageMode { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] //追加。JSON保存用。
        public string? Thumb { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] //追加。JSON保存用。
        public string? AliasTarget { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] //追加。JSON保存用。
        public string? FolderKind { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Invalid { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<BookmarkNode>? Children { get; set; }

        public bool IsFolder => Children != null;
        public bool IsAlias => FolderKind == nameof(TagGroupEntryKind.Alias) || AliasTarget != null;
        public bool IsBookmark => Path != null;


        public IEnumerable<BookmarkNode> Walk()
        {
            yield return this;

            if (Children == null) yield break;
            foreach     (var child in Children)
                foreach (var subChild in child.Walk())
                    yield return subChild;
        }
    }

    ///##########################################################################################################################
    ///##########################################################################################################################
    public static class BookmarkNodeConverter
    {
        public static BookmarkNode ConvertFrom(TreeListNode<IBookmarkEntry> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var node = new BookmarkNode();

            if (source.Value is TagAliasFolder alias)
            {
                node.Name         = alias.Name;
                node.FolderKind   = TagGroupEntryKind.Alias.ToString();
                node.AliasTarget  = alias.AliasTarget;
                node.EntryTime    = alias.EntryTime;
            }
            else if (source.Value is BookmarkFolder folder)
            {
                node.Name         = folder.Name;
                node.Color        = folder.Color;
                node.FolderKind   = folder.FolderKind?.ToString();
                node.EntryTime    = folder.EntryTime;
                node.Children     = new List<BookmarkNode>();
                
                foreach (var child in source) node.Children.Add(ConvertFrom(child));
            }
            else if (source.Value is Bookmark bookmark)
            {
                node.Name         = bookmark.RawName;
                node.Path         = bookmark.Path;
                node.Page         = bookmark.BookmarkPage;
                node.Props        = bookmark.BookmarkProps;
                node.OpenPageMode = bookmark.OpenPageMode;
                node.SortGroup    = bookmark.SortGroup;
                node.SortIndex    = bookmark.SortIndex;
                node.Invalid      = bookmark.IsUnlinked;
                node.Thumb        = bookmark.Thumb;
            }
            else throw new NotSupportedException();

            return node;
        }

        // ConvertToTreeListNode() は JSON → 実行時ノード なので、ここに Alias 復元を入れる。
        public static TreeListNode<IBookmarkEntry>? ConvertToTreeListNode(BookmarkNode source)
        {
            var folderKind = Enum.TryParse<TagGroupEntryKind>(source.FolderKind, out var kind) ? kind : (TagGroupEntryKind?)null;

            if (source.IsAlias)
            {
                var alias = new TagAliasFolder(source.Name, source.AliasTarget, source.EntryTime);
                alias.FolderKind = TagGroupEntryKind.Alias;

                return new TreeListNode<IBookmarkEntry>(alias);
            }
            else if (source.IsFolder)
            {
                var bookmarkFolder = new BookmarkFolder()
                {
                    Name       = source.Name,
                    Color      = source.Color,
                    EntryTime  = source.EntryTime,
                    FolderKind = folderKind
                };
                var node = new TreeListNode<IBookmarkEntry>(bookmarkFolder);
                if (source.Children is not null)
                {
                    foreach (var child in source.Children)
                    {
                        var childNode = ConvertToTreeListNode(child);
                        if (childNode is not null)
                            node.Add(childNode);
                    }
                }
                return node;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(source.Path))
                {
                    return null;
                }
                var bookmark = new Bookmark(source.Path)
                {
                    Name          = source.Name ?? "",
                    IsUnlinked    = source.Invalid,

                    BookmarkPage  = source.Page,
                    BookmarkProps = source.Props,
                    OpenPageMode  = source.OpenPageMode,
                    SortGroup     = source.SortGroup,
                    SortIndex     = source.SortIndex,
                    Thumb         = source.Thumb,
                };
                var node = new TreeListNode<IBookmarkEntry>(bookmark);
                return node;
            }
        }
    }

    ///##########################################################################################################################
    ///##########################################################################################################################
    public static class TreeListNodeExtensions
    {
        public static QueryPath CreateQuery<T>(this TreeListNode<T> node, QueryScheme scheme)
            where T : ITreeListNode
        {
            var path = string.Join("\\", node.Hierarchy.Select(e => e.Value).Skip(1).OfType<T>().Select(e => e.Name));
            return new QueryPath(scheme, path, null);
        }

        /// <summary>
        /// Bookmark用パス等価判定
        /// </summary>
        public static bool IsEqual(this TreeListNode<IBookmarkEntry> node, QueryPath path)
        {
            if (node is null || path is null)
            {
                return false;
            }

            if (path.Scheme == QueryScheme.Bookmark)
            {
                return node.CreateQuery(QueryScheme.Bookmark) == path;
            }
            else if (path.Scheme == QueryScheme.File)
            {
                if (node.Value is Bookmark bookmark)
                {
                    return bookmark.Path == path.SimplePath;
                }
            }

            return false;
        }
    }

    ///##########################################################################################################################
    ///##########################################################################################################################
    /// <summary>
    /// TreeListNode&lt;IBookmarkEntry&rt; 拡張関数
    /// </summary>
    public static class BookmarkTreeListNodeExtensions
    {
        /// <summary>
        /// Query生成
        /// </summary>
        public static QueryPath CreateQuery(this TreeListNode<IBookmarkEntry> node)
        {
            return node.CreateQuery(QueryScheme.Bookmark);
        }
    }
}
