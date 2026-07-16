using CommunityToolkit.Mvvm.ComponentModel;
using NeeLaboratory.Generators;
//using NeeLaboratory.IO.Search;
using NeeLaboratory.Linq;
using NeeView.Collections.Generic;
using NeeView.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;

//using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
//using System.Windows.Controls;
using System.Windows.Media;

namespace NeeView {
    /// /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public partial class BookmarkCollection : ObservableObject
    {
        static BookmarkCollection() => Current = new BookmarkCollection();
        public static BookmarkCollection Current { get; }

        private TreeListNode<IBookmarkEntry> _items          ;
        private readonly IndexSearcher       _searcher       ;
        private readonly BookmarkIndexes     _BookmarkIndexes;
        private readonly BookmarkTreeRules   _treeRules      ;
        private readonly BookmarkMoveService _moveService    ;

        private BookmarkCollection()
        {
            _items     = CreateEmptyTree();
            _BookmarkIndexes   = new BookmarkIndexes  (() => Items);
            _searcher          = new IndexSearcher    (() => Items, () => _BookmarkIndexes.GetBookPathIndex());
            _treeRules         = new BookmarkTreeRules(() => Items, RaiseBookmarkChangedEvent);
            _moveService       = new BookmarkMoveService(() => Items                    ,
                                                               FindNode                 ,
                                                               Merge                    ,
                                                               Remove                   ,
                                                               _treeRules               ,
                                                               RaiseBookmarkChangedEvent);
    
            BookmarkChanged += BookmarkCollection_BookmarkChanged;
        }


        [Subscribable]
        public event EventHandler<BookmarkCollectionChangedEventArgs>? BookmarkChanged;

        [Subscribable]
        public event EventHandler? Validated;

        //private uint _itemsGetCount = 0;
        public TreeListNode<IBookmarkEntry> Items
        {
            get {
                //_itemsGetCount++;
                //if ((_itemsGetCount % 100) == 0)
                //{
                //    Debug.WriteLine($"Items.get = {_itemsGetCount}");
                //}
                return _items;
            }
            set { SetProperty(ref _items, value); }
        }

        // 空のルートノードを1個作る
        private static TreeListNode<IBookmarkEntry> CreateEmptyTree () { return new TreeListNode<IBookmarkEntry>(new BookmarkFolder()); }
        public void RaiseBookmarkChangedEvent (BookmarkCollectionChangedEventArgs e) { BookmarkChanged?.Invoke(this, e); }
        public void Load (TreeListNode<IBookmarkEntry> nodes, IEnumerable<BookMemento> books)
        {
            foreach (var book in books) BookMementoCollection.Current.Set(book);
            Items = nodes;

            // ルートの名前は空
            if (Items.Value is BookmarkFolder folder) folder.Name = null;
            BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Reset));
        }

        ///======================================================================================================================
        // ここから追加。
        public Bookmark?                          Find(string path)                           { return _searcher.Find(path);      }
        public BookMementoUnit?                   FindUnit(string place)                      { return _searcher.FindUnit(place); }
        public TreeListNode<IBookmarkEntry>?      FindNode(string path)                       { return _searcher.FindNode(path);  }
        public TreeListNode<IBookmarkEntry>?      FindNode(QueryPath path)                    { return _searcher.FindNode(path);  }
        public bool                               Contains(string place)                      { return _searcher.Contains(place); }
        public bool                               Contains(TreeListNode<IBookmarkEntry> node) { return _searcher.Contains(node); }
        public List<TreeListNode<IBookmarkEntry>> Collect(string path)                        { return _searcher.Collect(path);   }
        public List<TreeListNode<IBookmarkEntry>> FindTagEntriesByBookPath(string path)       { return _searcher.FindTagEntriesByBookPath(path); }
        public bool                               ChangeFolderKind(TreeListNode<IBookmarkEntry> node, TagGroupEntryKind kind)
                                                                                              { return _treeRules.ChangeFolderKind(node, kind); }
        public void                               AddAliasFolder(TreeListNode<IBookmarkEntry> source, TreeListNode<IBookmarkEntry> target)
                                                                                              { _treeRules.AddAliasFolder(source, target); }

        private static TagGroupEntryKind? GetEntryKind(IBookmarkEntry entry)
        {
            return entry switch
            {
                Bookmark              => TagGroupEntryKind.Bookmark,
                TagAliasFolder        => TagGroupEntryKind.Alias,
                BookmarkFolder folder => folder.FolderKind,
                _                     => null,
            };
        }
        // ここまで。
        ///======================================================================================================================
        private void BookmarkCollection_BookmarkChanged(object? sender, BookmarkCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case EntryCollectionChangedAction.CreatedNewNode:
                case EntryCollectionChangedAction.Add:
                    _BookmarkIndexes.AddSubtree(e.Item);
                    _BookmarkIndexes.InvalidateTagIndexes();
                    break;

                case EntryCollectionChangedAction.Remove:
                    _BookmarkIndexes.RemoveSubtree(e.Item);
                    _BookmarkIndexes.InvalidateTagIndexes();
                    break;

                case EntryCollectionChangedAction.Move:
                    _BookmarkIndexes.InvalidateTagIndexes();
                    break;

                case EntryCollectionChangedAction.Update:
                    break;

                case EntryCollectionChangedAction.RenameBookmarkNode:
                case EntryCollectionChangedAction.RenameBookPath:
                default:
                    _BookmarkIndexes.Invalidate();
                    break;
            }
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        // TODO: 重複チェックをここで行う
        public void AddNewChild(TreeListNode<IBookmarkEntry> node, TreeListNode<IBookmarkEntry>? parent)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            parent = parent ?? Items.Root;

            if (node.Value is Bookmark && !TagGroupFolderKindTools.CanCreateChild((parent.Value as BookmarkFolder)?.FolderKind, TagGroupEntryKind.Bookmark))
            {
                ToastService.Current.Show( new Toast("ブックマークはタグまたは分類フォルダーにのみ作成できます。", null, ToastIcon.Warning) );
                return;
            }
            else if ( node.Value is TagAliasFolder                                                                                   &&
                      !TagGroupFolderKindTools.CanCreateChild((parent.Value as BookmarkFolder)?.FolderKind, TagGroupEntryKind.Alias) )
            {
                var result = MessageBox.Show(
                    "エイリアスは中継フォルダーとEdgeフォルダーにしか作成できません。\nルートに作成します。",
                    "エイリアスの作成",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;
                parent = Items.Root;
            }

            parent.Add(node);
            if (node.Value is Bookmark bookmark)
            {
                var bookmarkCount = parent.Children.Count(child => child.Value is Bookmark item && item.SortGroup == bookmark.SortGroup);

                var message = bookmarkCount > 1 ?
                    $"「{bookmark.Name}」をブックマークしました。（同じ本：{bookmarkCount}件）" :
                    $"「{bookmark.Name}」をブックマークしました。";

                ToastService.Current.Show(new Toast(message, null, ToastIcon.Information));
            }

            _treeRules.PromoteParentToEdgeIfNeeded(parent, GetEntryKind(node.Value));
            BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.CreatedNewNode, node.Parent, node));

        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        /// <summary>
        /// 新しいフォルダーを追加
        /// </summary>
        public TreeListNode<IBookmarkEntry>? AddNewFolder (TreeListNode<IBookmarkEntry> target          ,
                                                           string?                      name            ,
                                                           bool                         isExpand = true ,
                                                           TagGroupEntryKind?           childKind = null)
        {
            if (target != Items && target.Value is not BookmarkFolder) return null;

            var parentFolder = target.Value as BookmarkFolder;
            var parentKind = parentFolder?.FolderKind;
            var ruleParentKind = target == Items ? TagGroupEntryKind.Edge : parentKind;
            var actualChildKind = childKind ?? TagGroupEntryKind.Edge;

            // 空Edge直下に普通フォルダーを作る時だけ、
            // 親Edgeを中継(null)に戻して、子をEdgeとして作る。
            if (childKind is null && target != Items && parentKind == TagGroupEntryKind.Edge)
            {
                if (!IsEmptyEdge(target)) return null;

                parentFolder!.FolderKind = null;
                parentKind = null;
            }
            else if (childKind is TagGroupEntryKind.Tag && target == Items && parentKind == null)
            {
                if (!TagGroupFolderKindTools.CanCreateChild(ruleParentKind, actualChildKind)) return null;
            }

            var ignoreNames = target.WithLock(e => e.Children
                                                    .Where(e => e.Value is BookmarkFolder)
                                                    .Select(e => e.Value.Name)
                                                    .WhereNotNull()
                                                    .ToList());

            var validName = GetValidateFolderName(ignoreNames, name, TextResources.GetString("Word.NewFolder"));

            var folder = new BookmarkFolder(validName, null, DateTime.Now) { FolderKind = actualChildKind };

            var node = new TreeListNode<IBookmarkEntry>(folder);

            target.Add(node);

            _treeRules.PromoteParentToEdgeIfNeeded(target, actualChildKind);

            if (isExpand) target.IsExpanded = true;

            BookmarkChanged?.Invoke(
                this,
                new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.CreatedNewNode, node.Parent, node)
            );

            return node;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        /// <summary>
        /// 既存ノードを指定した移動先へ移動する
        /// </summary>
        public bool MoveNode(TreeListNode<IBookmarkEntry> item, TreeListNode<IBookmarkEntry> target, int newIndex = 0)
        {
            return _moveService.MoveNode(item, target, newIndex);
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        public bool Remove(TreeListNode<IBookmarkEntry>? node)
        {
            if (node == null) return false;
            if (node.Parent is null) return false;
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

        ///----- - ----- -
        private static bool IsEmptyEdge(TreeListNode<IBookmarkEntry> node)
        {
            return node.Value is BookmarkFolder { FolderKind: TagGroupEntryKind.Edge }
                && node.Children.Count == 0;
        }


        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        public bool CopyBookmarkToChild(TreeListNode<IBookmarkEntry> item, TreeListNode<IBookmarkEntry> target)
        {
            if (item?.Value is not Bookmark bookmark) return false;
            if (target?.Value is not BookmarkFolder folder) throw new ArgumentException("target must be BookmarkFolder");
            if (!TagGroupFolderKindTools.CanCreateChild(folder.FolderKind, TagGroupEntryKind.Bookmark))
            {
                ToastService.Current.Show(new Toast(
                    "ブックマークはタグまたは分類フォルダーにのみコピーできます。",
                    null,
                    ToastIcon.Warning));

                return false;
            }

            var copiedEntry = (IBookmarkEntry)bookmark.Clone();
            var copiedNode = new TreeListNode<IBookmarkEntry>(copiedEntry);

            AddNewChild(copiedNode, target);
            target.IsExpanded = true;

            return true;
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
                _treeRules.PromoteParentToEdgeIfNeeded(target, GetEntryKind(child.Value));
                BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Add, target, child));
            }

            return true;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        //　本棚の本が名前変更されたら呼び出された
        public void RenameBookPath(string src, string dst)
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
                BookmarkChanged?.Invoke(this, new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.RenameBookPath, item.Parent, item));
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
        SubTag,
        Alias,
        Category,
        Bookmark
    }

    ///##########################################################################################################################
    ///##########################################################################################################################
    public static class TagGroupFolderKindTools
    {
        public static bool CanCreateChild(TagGroupEntryKind? parentKind, TagGroupEntryKind? childKind)
        {
            return parentKind switch
            {
                null                       => childKind is null or TagGroupEntryKind.Edge   or TagGroupEntryKind.Alias,                                  // 中継フォルダーに適用されるルール
                TagGroupEntryKind.Edge     => childKind is         TagGroupEntryKind.Tag    or TagGroupEntryKind.Alias,                                  // 中継終端フォルダーに適用されるルール
                TagGroupEntryKind.Tag      => childKind is         TagGroupEntryKind.SubTag or TagGroupEntryKind.Category or TagGroupEntryKind.Bookmark, // タグ・フォルダーに適用されるルール
                TagGroupEntryKind.SubTag   => childKind is         TagGroupEntryKind.Bookmark,                                                           // サブ・タグ・フォルダーに適用されるルール
                TagGroupEntryKind.Category => childKind is         TagGroupEntryKind.Bookmark,                                                           // 分類フォルダーに適用されるルール
                TagGroupEntryKind.Alias    => false,                                                                                                     // エリアス・フォルダーに適用されるルール
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
                var bookmarkFolder = new BookmarkFolder(){ Name       = source.Name      ,
                                                           Color      = source.Color     ,  
                                                           EntryTime  = source.EntryTime ,  
                                                           FolderKind = folderKind       };
                var node = new TreeListNode<IBookmarkEntry>(bookmarkFolder);
                if (source.Children is not null)
                {
                    foreach (var child in source.Children)
                    {
                        var childNode = ConvertToTreeListNode(child);
                        if (childNode is not null) node.Add(childNode);
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
                var bookmark = new Bookmark(source.Path){ Name          = source.Name ?? ""   ,
                                                          IsUnlinked    = source.Invalid      ,  
                                                          BookmarkPage  = source.Page         ,  
                                                          BookmarkProps = source.Props        ,  
                                                          OpenPageMode  = source.OpenPageMode ,  
                                                          SortGroup     = source.SortGroup    ,  
                                                          SortIndex     = source.SortIndex    ,  
                                                          Thumb         = source.Thumb        };
                
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
