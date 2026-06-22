using CommunityToolkit.Mvvm.Input;
using NeeLaboratory.ComponentModel;
using NeeLaboratory.Linq;
using NeeView.Collections.Generic;
using NeeView.Properties;
using NeeView.Windows;
using NeeView.Windows.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NeeView
{
    /// <summary>
    /// FolderListBox.xaml の相互作用ロジック
    /// </summary>
    public partial class FolderListBox : UserControl, IPageListPanel, IDisposable
    {
        private readonly FolderListBoxViewModel        _vm;
        private readonly FolderListBoxInsertDropAssist _dropAssist;
        private ListBoxThumbnailLoader?                _thumbnailLoader;
        private PageThumbnailJobClient?                _jobClient;
        private FolderItem?                            _clickItem;
        private CancellationTokenSource?               _realizeTokenSource;
        
        private bool                                   _isAltShiftScroll;
        private Point                                  _altShiftScrollStartPoint;
        private double                                 _altShiftScrollStartOffset;
        private ScrollViewer?                          _gestureScrollViewer ;
        private bool                                   _bookmarkClipboardIsCut;

        static FolderListBox()
        {
            InitializeCommandStatic();
        }


        public FolderListBox(FolderListBoxViewModel vm)
        {
            InitializeComponent();

            _vm = vm;
            this.DataContext = vm;

            InitializeCommand();

            // タッチスクロール操作の終端挙動抑制
            this.ListBox.ManipulationBoundaryFeedback += SidePanelFrame.Current.ScrollViewer_ManipulationBoundaryFeedback;
            this.ListBox.PreviewMouseUpWithSelectionChanged += ListBox_PreviewMouseUpWithSelectionChanged;

            this.Loaded += FolderListBox_Loaded;
            this.Unloaded += FolderListBox_Unloaded;

            if (_vm.FolderCollection is BookmarkFolderCollection)
            {
                var menu = new ContextMenu();
                menu.Items.Add(new MenuItem() { Header = TextResources.GetString("FolderTree.Menu.AddBookmark"), Command = AddBookmarkCommand });
                menu.Items.Add(new MenuItem() { Header = TextResources.GetString("Word.NewFolder"), Command = NewFolderCommand });
                this.ListBox.ContextMenu = menu;
                this.ListBox.ContextMenuOpening += FolderList_ContextMenuOpening;
            }

            _dropAssist = new FolderListBoxInsertDropAssist(this.ListBox, _vm);
            this.ListBox.PreviewDragEnter += ListBox_PreviewDragEnter;
            this.ListBox.PreviewDragLeave += ListBox_PreviewDragLeave;
            this.ListBox.PreviewDragOver += ListBox_PreviewDragOver;
            this.ListBox.DragOver += ListBox_DragOver;
            this.ListBox.Drop += ListBox_Drop;
        }

        private void FolderList_ContextMenuOpening(object? sender, ContextMenuEventArgs e)
        {
            if (_vm.FolderCollection is BookmarkFolderCollection)
            {
                var menu = new ContextMenu();
                menu.Items.Add(new MenuItem() { Header = TextResources.GetString("Word.NewFolder"), Command = NewFolderCommand });
                menu.Items.Add(new Separator());
                menu.Items.Add(new MenuItem() { Header = "ブックマークを貼り付け", Command = PasteBookmarkCommand });
                menu.Items.Add(new MenuItem() { Header = "エリアスをここに貼り付け", Command = PasteBookmarkAliasCommand });
                this.ListBox.ContextMenu = menu;
            }
            _ = 0; // BP
        }
        // フォーカス可能フラグ
        public bool IsFocusEnabled { get; set; } = true;


        #region IDisposable Support
        private bool _disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _jobClient?.Dispose();
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region IPanelListBox Support

        public ListBox PageCollectionListBox => this.ListBox;

        // サムネイルが表示されている？
        public bool IsThumbnailVisible => _vm.IsThumbnailVisible;

        public IEnumerable<IHasPage> CollectPageList(IEnumerable<object> enums) => enums.OfType<IHasPage>();

        #endregion

        #region Commands

        public static readonly RoutedCommand LoadWithRecursiveCommand     = new("LoadWithRecursiveCommand",     typeof(FolderListBox));
        public static readonly RoutedCommand OpenCommand                  = new("OpenCommand",                  typeof(FolderListBox));
        public static readonly RoutedCommand OpenBookCommand              = new("OpenBookCommand",              typeof(FolderListBox));
        public static readonly RoutedCommand OpenExplorerCommand          = new("OpenExplorerCommand",          typeof(FolderListBox));
        public static readonly RoutedCommand OpenExternalAppCommand       = new("OpenExternalAppCommand",       typeof(FolderListBox));
        public static readonly RoutedCommand CutCommand                   = new("CutCommand",                   typeof(FolderListBox));
        public static readonly RoutedCommand CopyCommand                  = new("CopyCommand",                  typeof(FolderListBox));
        public static readonly RoutedCommand CopyToFolderCommand          = new("CopyToFolderCommand",          typeof(FolderListBox));
        public static readonly RoutedCommand MoveToFolderCommand          = new("MoveToFolderCommand",          typeof(FolderListBox));
        public static readonly RoutedCommand RemoveCommand                = new("RemoveCommand",                typeof(FolderListBox));
        public static readonly RoutedCommand RenameCommand                = new("RenameCommand",                typeof(FolderListBox));
        public static readonly RoutedCommand RemoveHistoryCommand         = new("RemoveHistoryCommand",         typeof(FolderListBox));
        public static readonly RoutedCommand OpenDestinationFolderCommand = new("OpenDestinationFolderCommand", typeof(FolderListBox));
        public static readonly RoutedCommand OpenExternalAppDialogCommand = new("OpenExternalAppDialogCommand", typeof(FolderListBox));
        public static readonly RoutedCommand OpenInPlaylistCommand        = new("OpenInPlaylistCommand",        typeof(FolderListBox));
        public static readonly RoutedCommand RegenerateThumbnailCommand   = new("RegenerateThumbnailCommand",   typeof(FolderListBox));
        public static readonly RoutedCommand SetThumbnailCommand          = new("SetThumbnailCommand",          typeof(FolderListBox));
        public static readonly RoutedCommand EditTagColorCommand          = new("EditTagColorCommand",          typeof(FolderListBox));
        //ここから追加
        public static readonly RoutedCommand CreateBookmarkCommand        = new("CreateBookmarkCommand",        typeof(FolderListBox));
        public static readonly RoutedCommand MoveToHomeFolderCommand      = new("MoveToHomeFolderCommand",      typeof(FolderListBox));
        public static readonly RoutedCommand CopyBookmarkCommand          = new("CopyBookmarkCommand",          typeof(FolderListBox));
        public static readonly RoutedCommand PasteBookmarkCommand         = new("PasteBookmarkCommand",         typeof(FolderListBox));
        public static readonly RoutedCommand CutBookmarkCommand           = new("CutBookmarkCommand",           typeof(FolderListBox));
        public static readonly RoutedCommand CopyBookmarkAliasCommand     = new("CopyBookmarkAliasCommand",     typeof(FolderListBox));
        public static readonly RoutedCommand PasteBookmarkAliasCommand    = new("PasteBookmarkAliasCommand",    typeof(FolderListBox));

        private static List<TreeListNode<IBookmarkEntry>> _bookmarkClipboard = new();
        private static TreeListNode<IBookmarkEntry>?      _bookmarkAliasClipboard;

        private static void InitializeCommandStatic()
        {
            OpenCommand.            InputGestures.Add( new KeyGesture(Key.Down, ModifierKeys.Alt));
            OpenBookCommand.        InputGestures.Add( new KeyGesture(Key.Enter));
            CutCommand.             InputGestures.Add( new KeyGesture(Key.X, ModifierKeys.Control));
            CopyCommand.            InputGestures.Add( new KeyGesture(Key.C, ModifierKeys.Control));
            RemoveCommand.          InputGestures.Add( new KeyGesture(Key.Delete));
            RenameCommand.          InputGestures.Add( new KeyGesture(Key.F2));
            //ここから追加
            //CreateBookmarkCommand.InputGestures.Add(new KeyGesture(Key.D, ModifierKeys.Control));
            CreateBookmarkCommand.  InputGestures.Add( new KeyGesture(Key.D, ModifierKeys.Control));
            CreateBookmarkCommand.  InputGestures.Add( new KeyGesture(Key.D, ModifierKeys.Control | ModifierKeys.Shift));
            MoveToHomeFolderCommand.InputGestures.Add( new KeyGesture(Key.G, ModifierKeys.Control));
        }

        private void InitializeCommand()
        {
            this.ListBox.CommandBindings.Add(new CommandBinding(LoadWithRecursiveCommand,     LoadWithRecursive_Executed, LoadWithRecursive_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(OpenCommand,                  Open_Executed));
            this.ListBox.CommandBindings.Add(new CommandBinding(OpenBookCommand,              OpenBook_Executed));
            this.ListBox.CommandBindings.Add(new CommandBinding(OpenExplorerCommand,          OpenExplorer_Executed, OpenExplorer_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(OpenExternalAppCommand,       OpenExternalApp_Executed, OpenExternalApp_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(CutCommand,                   Cut_Executed, Cut_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(CopyCommand,                  Copy_Executed, Copy_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(CopyToFolderCommand,          CopyToFolder_Execute, CopyToFolder_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(MoveToFolderCommand,          MoveToFolder_Execute, MoveToFolder_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(RemoveCommand,                Remove_Executed, Remove_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(RenameCommand,                Rename_Executed, Rename_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(RemoveHistoryCommand,         RemoveHistory_Executed, RemoveHistory_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(OpenDestinationFolderCommand, OpenDestinationFolderDialog_Execute));
            this.ListBox.CommandBindings.Add(new CommandBinding(OpenExternalAppDialogCommand, OpenExternalAppDialog_Execute));
            this.ListBox.CommandBindings.Add(new CommandBinding(OpenInPlaylistCommand,        OpenInPlaylistCommand_Execute));
            this.ListBox.CommandBindings.Add(new CommandBinding(RegenerateThumbnailCommand,   RegenerateThumbnailCommand_Execute));
            this.ListBox.CommandBindings.Add(new CommandBinding(SetThumbnailCommand,          SetThumbnailCommand_Execute, SetThumbnailCommand_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(EditTagColorCommand,          EditTagColor_Executed));
            //ここから追加
            this.ListBox.CommandBindings.Add(new CommandBinding(CreateBookmarkCommand,        CreateBookmark_Executed));
            this.ListBox.CommandBindings.Add(new CommandBinding(MoveToHomeFolderCommand,      MoveToHomeFolder_Execute, MoveToFolder_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(CopyBookmarkCommand,          CopyBookmark_Executed, CopyBookmark_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(PasteBookmarkCommand,         PasteBookmark_Executed, PasteBookmark_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(CutBookmarkCommand,           CutBookmark_Executed, CopyBookmark_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(CopyBookmarkAliasCommand,     CopyBookmarkAlias_Executed, CopyBookmarkAlias_CanExecute));
            this.ListBox.CommandBindings.Add(new CommandBinding(PasteBookmarkAliasCommand,    PasteBookmarkAlias_Executed, PasteBookmarkAlias_CanExecute));
        }

        ///######################################################################################################################
        ///######################################################################################################################
        ///######################################################################################################################
        // ここから追加。(20260607_1139_16 Start)
        private void CreateBookmark_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            var openPageMode = Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
                ? BookmarkOpenPageMode.Fixed
                : BookmarkOpenPageMode.Resume;

            var bookshelfPanel = (FolderPanel)CustomLayoutPanelManager.Current.GetPanel(nameof(FolderPanel));
            var bookshelfItems = bookshelfPanel.Presenter.FolderListBox?.GetSelectedItems();

            _ = 0;
            if (openPageMode == BookmarkOpenPageMode.Fixed
                && bookshelfItems is { Count: > 1 })
            {
                ToastService.Current.Show( new Toast("Fixedモードでのブックマーク作成は複数選択をサポートしていません。", "", ToastIcon.Warning) );
                return;
            }

            _ = 0;
            if (openPageMode == BookmarkOpenPageMode.Resume)
            {
                var parent = BookmarkFolderList.Current.GetBookmarkPlace();
                if (parent is null) return;

                _ = 0;
                var queries = bookshelfItems? .Select(x => x.EntityPath).Where(x => x.Scheme == QueryScheme.File).ToList()?? new List<QueryPath>();
                
                if (queries.Count == 0)
                {
                    var book = BookOperation.Current.Book;
                    if (book is null) return;

                    queries.Add(new QueryPath(book.Path));
                }

                foreach (var query in queries)
                {
                    _ = 0;
                    BookmarkCollectionService.AddTo(
                        query,
                        parent,
                        null,
                        new BookmarkAddOptions()
                        {
                            AllowDuplicate = true,
                            OpenPageMode = BookmarkOpenPageMode.Resume,
                        });
                }
            }
            else
            {
                var book = BookOperation.Current.Book;
                if (book is null) return;

                QueryPath query = new QueryPath(book.Path);

                BookmarkCollectionService.Add(
                    query,
                    null,
                    new BookmarkAddOptions()
                    {
                        AllowDuplicate = true,
                        OpenPageMode = openPageMode,
                    });
            }
            e.Handled = true;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        private void CopyBookmark_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.ListBox.SelectedItems
                .Cast<FolderItem>()
                .Any(x => x.Source is TreeListNode<IBookmarkEntry> node && node.Value is Bookmark);
        }

        private void CopyBookmark_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            _bookmarkClipboardIsCut = false;

            _bookmarkClipboard = this.ListBox.SelectedItems
                .Cast<FolderItem>()
                .Select(x => x.Source as TreeListNode<IBookmarkEntry>)
                .WhereNotNull()
                .Where(x => x.Value is Bookmark)
                .Select(x =>
                {
                    var copiedEntry = (IBookmarkEntry)((Bookmark)x.Value).Clone();
                    return new TreeListNode<IBookmarkEntry>(copiedEntry);
                })
                .ToList();
        }

        private void PasteBookmark_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute =
                _bookmarkClipboard.Count > 0 &&
                _vm.FolderCollection is BookmarkFolderCollection;
        }

        private void PasteBookmark_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            if (_vm.FolderCollection is not BookmarkFolderCollection bookmarkFolderCollection)
            {
                return;
            }

            var target = bookmarkFolderCollection.BookmarkPlace;
            if (target is null)
            {
                return;
            }

            foreach (var item in _bookmarkClipboard.ToList())
            {
                if (_bookmarkClipboardIsCut)
                {
                    BookmarkCollection.Current.MoveToChild(item, target);
                }
                else
                {
                    BookmarkCollection.Current.CopyBookmarkToChild(item, target);
                }
            }

            if (_bookmarkClipboardIsCut)
            {
                _bookmarkClipboard.Clear();
                _bookmarkClipboardIsCut = false;
            }
        }

        private void CutBookmark_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            _bookmarkClipboardIsCut = true;
            _bookmarkClipboard.Clear();

            foreach (var selectedItem in this.ListBox.SelectedItems)
            {
                if (selectedItem is not FolderItem folderItem)
                {
                    continue;
                }

                if (folderItem.Source is not TreeListNode<IBookmarkEntry> node)
                {
                    continue;
                }

                if (node.Value is not Bookmark)
                {
                    continue;
                }

                _bookmarkClipboard.Add(node);
                folderItem.IsCut = true;
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        private void CopyBookmarkAlias_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute =
                this.ListBox.SelectedItem is FolderItem item &&
                item.Source is TreeListNode<IBookmarkEntry> node &&
                node.Value is BookmarkFolder &&
                node.Parent is not null;
        }

        private void CopyBookmarkAlias_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            if (this.ListBox.SelectedItem is not FolderItem item) return;
            if (item.Source is not TreeListNode<IBookmarkEntry> node) return;
            if (node.Value is not BookmarkFolder) return;

            _bookmarkAliasClipboard = node;
        }

        private void PasteBookmarkAlias_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute =
                _bookmarkAliasClipboard is not null &&
                _vm.FolderCollection is BookmarkFolderCollection;
        }

        private void PasteBookmarkAlias_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            if (_bookmarkAliasClipboard is null) return;

            if (_vm.FolderCollection is not BookmarkFolderCollection bookmarkFolderCollection)
            {
                return;
            }

            var target = bookmarkFolderCollection.BookmarkPlace;
            if (target is null)
            {
                return;
            }

            BookmarkCollection.Current.AddAliasFolder(_bookmarkAliasClipboard, target);
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //if (Keyboard.Modifiers != (ModifierKeys.Alt | ModifierKeys.Shift)) return;
            //if (Keyboard.Modifiers != ModifierKeys.Alt || !Keyboard.IsKeyDown(Key.F1)) return;
            if (!Keyboard.IsKeyDown(Key.F13)) return;

            _gestureScrollViewer  = VisualTreeUtility.FindVisualChild<ScrollViewer>(this.ListBox);
            if (_gestureScrollViewer  is null) return;

            _isAltShiftScroll = true;
            _altShiftScrollStartPoint = e.GetPosition(this.ListBox);
            _altShiftScrollStartOffset = _gestureScrollViewer .VerticalOffset;

            this.ListBox.CaptureMouse();
            e.Handled = true;
        }

        private void ListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isAltShiftScroll) return;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndAltShiftScroll();
                return;
            }

            if (_gestureScrollViewer  is null) return;

            var current = e.GetPosition(this.ListBox);
            var deltaY = current.Y - _altShiftScrollStartPoint.Y;

            _gestureScrollViewer .ScrollToVerticalOffset(_altShiftScrollStartOffset + deltaY * 6.5);

            e.Handled = true;
        }

        private void ListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isAltShiftScroll) return;

            EndAltShiftScroll();
            e.Handled = true;
        }

        private void EndAltShiftScroll()
        {
            _isAltShiftScroll = false;
            _gestureScrollViewer  = null;

            if (this.ListBox.IsMouseCaptured)
            {
                this.ListBox.ReleaseMouseCapture();
            }
        }

        ///######################################################################################################################
        ///######################################################################################################################
        ///######################################################################################################################
        // ここからオリジナルのコード
        /// <summary>
        /// ブックマーク登録/解除可能？
        /// </summary>
        private void ToggleBookmark_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = sender is ListBox { SelectedItem: FolderItem item } && item.IsFileSystem() && !item.EntityPath.SimplePath.StartsWith(Temporary.Current.TempDirectory, StringComparison.Ordinal);
        }

        /// <summary>
        /// ブックマーク登録/解除
        /// </summary>
        private void ToggleBookmark_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is FolderItem item)
            {
                var paths = this.ListBox.SelectedItems.Cast<FolderItem>().Select(e => e.EntityPath).ToList();

                if (BookmarkCollection.Current.Contains(item.EntityPath.SimplePath))
                {
                    foreach (var path in paths)
                    {
                        BookmarkCollectionService.Remove(path);
                    }
                }
                else
                {
                    foreach (var path in paths)
                    {
                        BookmarkCollectionService.Add(path, null, new BookmarkAddOptions());
                    }
                }
            }
        }

        /// <summary>
        /// 履歴から削除できる？
        /// </summary>
        private void RemoveHistory_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        /// <summary>
        /// 履歴から削除
        /// </summary>
        private void RemoveHistory_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            BookHistoryCollection.Current.Remove(this.ListBox.SelectedItems.Cast<FolderItem>().Select(e => e.TargetPath.SimplePath));
        }

        /// <summary>
        /// サブフォルダーを読み込む？
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadWithRecursive_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = sender is ListBox { SelectedItem: FolderItem item }
                && !item.Attributes.AnyFlagFast(FolderItemAttribute.Drive | FolderItemAttribute.Empty)
                && (Config.Current.System.ArchiveRecursiveMode == ArchiveEntryCollectionMode.IncludeSubArchives
                    ? (item.Attributes & (FolderItemAttribute.Directory | FolderItemAttribute.Playlist)) != 0
                    : ArchiveManager.Current.GetSupportedType(item.TargetPath.SimplePath).IsRecursiveSupported());
        }


        /// <summary>
        /// サブフォルダーを読み込む
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadWithRecursive_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            if ((sender as ListBox)?.SelectedItem is not FolderItem item) return;

            // サブフォルダー読み込み状態を反転する
            var option = item.IsRecursive ? BookLoadOption.NotRecursive : BookLoadOption.Recursive;
            _vm.Model.LoadBook(item, option, ArchiveHint.None);
        }

        /// <summary>
        /// ファイル系コマンド実行可能判定
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = sender is ListBox { SelectedItem: FolderItem item } && item.IsEditable && Config.Current.System.IsFileWriteAccessEnabled;
        }

        /// <summary>
        /// 切り取りコマンド実行可能判定
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cut_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            var items = this.ListBox.SelectedItems.Cast<FolderItem>();
            e.CanExecute = Config.Current.System.IsFileWriteAccessEnabled && items != null && items.All(x => x.IsEditable && x.IsFileSystem());
        }

        /// <summary>
        /// 切り取りコマンド実行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Cut_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            var items = this.ListBox.SelectedItems.Cast<FolderItem>();
            if (items != null && items.Any())
            {
                CutToClipboard(items);
            }
        }

        /// <summary>
        /// クリップボードに切り取り
        /// </summary>
        private static void CutToClipboard(IEnumerable<FolderItem> items)
        {
            var selectedItems = items.Where(e => !e.IsEmpty() && e.IsEditable && e.IsFileSystem()).ToList();
            if (selectedItems.Count == 0)
            {
                return;
            }

            var collection = new System.Collections.Specialized.StringCollection();
            selectedItems.ForEach(e => collection.Add(e.EntityPath.SimplePath));

            var data = new DataObject();
            var guid = Guid.NewGuid();
            data.SetGuid(guid);
            data.SetFileDropList(collection);
            data.SetPreferredDropEffect(DragDropEffects.Move);
            Clipboard.SetDataObject(data);

            PendingItemManager.Current.AddRange(guid, selectedItems);
        }

        /// <summary>
        /// コピーコマンド実行可能判定
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Copy_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            var items = this.ListBox.SelectedItems.Cast<FolderItem>();
            e.CanExecute = items != null;
        }

        /// <summary>
        /// コピーコマンド実行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void Copy_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            var items = this.ListBox.SelectedItems.Cast<FolderItem>();
            if (items != null && items.Any())
            {
                try
                {
                    this.Cursor = Cursors.Wait;
                    _realizeTokenSource?.Cancel();
                    _realizeTokenSource = new CancellationTokenSource();
                    await CopyToClipboard(items, _realizeTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    this.Cursor = null;
                }
            }
        }

        /// <summary>
        /// クリップボードにコピー
        /// </summary>
        private static async Task CopyToClipboard(IEnumerable<FolderItem> items, CancellationToken token)
        {
            var collection = new System.Collections.Specialized.StringCollection();
            foreach (var item in items.Where(e => !e.IsEmpty()).Select(e => e.EntityPath.SimplePath).Where(e => new QueryPath(e).Scheme == QueryScheme.File))
            {
                try
                {
                    var entry = await ArchiveEntryUtility.CreateAsync(item, ArchiveHint.None, true, token);
                    var path = await entry.RealizeAsync(token);
                    collection.Add(path);
                }
                catch (FileNotFoundException)
                {
                    collection.Add(item);
                }
            }

            if (collection.Count == 0)
            {
                return;
            }

            var data = new DataObject();
            data.SetFileDropList(collection);
            Clipboard.SetDataObject(data);
        }

        /// <summary>
        /// フォルダーにコピーコマンド用
        /// </summary>
        private void CopyToFolder_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CopyToFolder_CanExecute();
        }

        private bool CopyToFolder_CanExecute()
        {
            var items = this.ListBox.SelectedItems.Cast<FolderItem>();
            return items != null; // && items.All(x => x.IsEditable);
        }

        public async void CopyToFolder_Execute(object? sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is not DestinationFolder folder) return;

            try
            {
                if (!FileIO.DirectoryExists(folder.Path))
                {
                    throw new DirectoryNotFoundException();
                }

                var items = this.ListBox.SelectedItems.Cast<FolderItem>();
                if (items != null && items.Any())
                {
                    ////Debug.WriteLine($"CopyToFolder: to {folder.Path}");
                    await FileIO.SHCopyToFolderAsync(items.Select(x => x.TargetPath.SimplePath), folder.Path, CancellationToken.None);
                    GC.KeepAlive(items);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ToastService.Current.Show(new Toast(ex.Message, TextResources.GetString("Bookshelf.CopyToFolderFailed"), ToastIcon.Error));
            }
        }

        /// <summary>
        /// フォルダーに移動コマンド用
        /// </summary>
        private void MoveToFolder_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = MoveToFolder_CanExecute();
        }

        private bool MoveToFolder_CanExecute()
        {
            var items = this.ListBox.SelectedItems.Cast<FolderItem>();
            return Config.Current.System.IsFileWriteAccessEnabled && items != null && items.All(x => x.IsEditable && x.IsFileSystem());
        }

        private async Task MoveSelectedItemsToFolderAsync(DestinationFolder folder)
        {
            if (!FileIO.DirectoryExists(folder.Path))
            {
                throw new DirectoryNotFoundException();
            }

            var items = this.ListBox.SelectedItems.Cast<FolderItem>().ToList();
            if (items.Any())
            {
                await FileIO.SHMoveToFolderAsync(
                    items.Select(x => x.TargetPath.SimplePath),
                    folder.Path,
                    CancellationToken.None);

                GC.KeepAlive(items);
            }
        }

        public async void MoveToHomeFolder_Execute(object? sender, ExecutedRoutedEventArgs e)
        {
            var folders = Config.Current.System.DestinationFolderCollection;
            if (folders.Count <= 0) return;

            try
            {
                await MoveSelectedItemsToFolderAsync(folders[0]);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ToastService.Current.Show(new Toast(ex.Message, TextResources.GetString("Bookshelf.Message.MoveToFolderFailed"), ToastIcon.Error));
            }
        }

        public async void MoveToFolder_Execute(object? sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is not DestinationFolder folder) return;

            try
            {
                await MoveSelectedItemsToFolderAsync(folder);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ToastService.Current.Show(new Toast(ex.Message, TextResources.GetString("Bookshelf.Message.MoveToFolderFailed"), ToastIcon.Error));
            }
        }


        public void Remove_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            var items = this.ListBox.SelectedItems.Cast<FolderItem>();
            e.CanExecute = items != null && _vm.FolderCollection is not PlaylistFolderCollection && items.All(x => x.CanRemove());
        }

        public async void Remove_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            var items = this.ListBox.SelectedItems.Cast<FolderItem>().ToList();
            await _vm.RemoveAsync(items);
            FocusSelectedItem(true);
        }


        public void Rename_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            if ((sender as ListBox)?.SelectedItem is not FolderItem item) return;
            if (_vm.FolderCollection is PlaylistFolderCollection) return;

            e.CanExecute = item.CanRename();
        }

        public async void Rename_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            if (sender != this.ListBox) return;

            await RenameAsync();
        }

        private async Task RenameAsync()
        {
            var listBox = this.ListBox;
            if (listBox.SelectedItem is not FolderItem item) return;

            var renamer = new FolderItemRenamer(listBox, _vm.DetailToolTip);

            if (_vm.SyncBookOnRename)
            {
                renamer.SelectedItemChanged += (s, e) =>
                {
                    if (listBox.SelectedItem is FolderItem item)
                    {
                        _vm.Model.LoadBook(item);
                    }
                };
            }

            await renamer.RenameAsync(item);
        }


        /// <summary>
        /// エクスプローラーで開くコマンド
        /// </summary>
        private void OpenExplorer_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (sender as ListBox)?.SelectedItem is FolderItem;
        }

        public void OpenExplorer_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: FolderItem item })
            {
                var path = item.TargetPath.SimplePath;
                path = item.Attributes.AnyFlagFast(FolderItemAttribute.Bookmark | FolderItemAttribute.ArchiveEntry | FolderItemAttribute.Empty) ? ArchiveManager.Current.GetExistPathName(path) : path;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    ExternalProcess.OpenWithFileManager(path);
                }
            }
        }

        /// <summary>
        /// 外部アプリで開く
        /// </summary>
        private void OpenExternalApp_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CopyToFolder_CanExecute();
        }

        private bool OpenExternalApp_CanExecute()
        {
            var items = this.ListBox.SelectedItems.Cast<FolderItem>();
            return items != null && items.All(x => x.IsEditable);
        }

        public void OpenExternalApp_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is not ExternalApp externalApp) return;

            var items = this.ListBox.SelectedItems.Cast<FolderItem>();
            if (items != null && items.Any())
            {
                var paths = items.Select(x => x.TargetPath.SimplePath).ToList();
                externalApp.Execute(paths);
            }
        }

        public void Open_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: FolderItem item })
            {
                _vm.MoveToSafety(item);
            }
        }

        public void OpenBook_Executed(object? sender, ExecutedRoutedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: FolderItem item } && !item.IsEmpty())
            {
                _vm.Model.LoadBook(item);
            }
        }

        private void OpenDestinationFolderDialog_Execute(object? sender, ExecutedRoutedEventArgs e)
        {
            DestinationFolderDialog.ShowDialog(Window.GetWindow(this));
        }

        private void OpenExternalAppDialog_Execute(object? sender, ExecutedRoutedEventArgs e)
        {
            ExternalAppDialog.ShowDialog(Window.GetWindow(this));
        }

        private void OpenInPlaylistCommand_Execute(object? sender, ExecutedRoutedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: FolderItem item } && item.IsPlaylist)
            {
                Config.Current.Playlist.CurrentPlaylist = item.EntityPath.SimplePath;
                SidePanelFrame.Current.IsVisiblePlaylist = true;
            }
        }

        private void RegenerateThumbnailCommand_Execute(object? sender, ExecutedRoutedEventArgs e)
        {
            if (sender is not ListBox listBox) return;

            var items = listBox.SelectedItems.OfType<FolderItem>().Where(e => !e.IsEmpty());
            foreach (var item in items)
            {
                item.ClearThumbnailCache();
            }

            _thumbnailLoader?.Load();
        }

        private void SetThumbnailCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: FolderItem item } && item.CanThumbnail())
            {
                var page = BookOperation.Current.Book?.CurrentPage;
                // 画像ページと、登録解除用にページなしを許可
                e.CanExecute = page is null || (page.ArchiveEntry.IsImage(false) && !page.ArchiveEntry.IsMedia());
            }
            else
            {
                e.CanExecute = false;
            }
        }

        private void SetThumbnailCommand_Execute(object? sender, ExecutedRoutedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: FolderItem item } && item.CanThumbnail())
            {
                try
                {
                    // 現在のページをサムネイルとして登録。画像でない場合は解除。
                    var page = BookOperation.Current.Book?.CurrentPage;
                    var target = (
                            page is not null                 &&
                            page.ArchiveEntry.IsImage(false) &&
                            !page.ArchiveEntry.IsMedia())    ?
                        page.EntryFullName : null;
                    FolderConfigTools.SetThumbnailTarget(item.TargetPath.SimplePath, target);
                }
                catch (Exception ex)
                {
                    ToastService.Current.Show(new Toast(ex.Message, "Thumbnail error", ToastIcon.Error));
                }
                // サムネイル更新
                item.ClearThumbnailCache();
                _thumbnailLoader?.Load(true);
            }
        }

        private void EditTagColor_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is ListBox { SelectedItem: BookmarkFolderFolderItem item })
            {
                var vm = new TagColorDialogViewModel(item.BookmarkNode);
                var dialog = new TagColorDialog(vm);
                dialog.Owner = Window.GetWindow(this);
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.ShowDialog();
            }
        }

        [RelayCommand]
        private void NewFolder()
        {
            _vm.Model.NewFolder();
        }

        [RelayCommand]
        private void AddBookmark()
        {
            _vm.Model.AddBookmark();
        }

        [RelayCommand]
        private void OpenBookmarkFolder(TagItem tag)
        {
            var path = new QueryPath(QueryScheme.Bookmark, tag.Node.Path);

            SidePanelFrame.Current.SetVisibleBookmarkList(true, true, false);
            var select = tag.SelectedItem is not null ? new FolderItemPosition(tag.SelectedItem) : null;
            BookmarkFolderList.Current.RequestPlace(path, select, FolderSetPlaceOption.None);
        }

        private void TagButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is TagItem tag)
            {
                OpenBookmarkFolder(tag);
                e.Handled = true;
            }
        }

        #endregion

        #region DragDrop

        public async Task DragStartBehavior_DragBeginAsync(object? sender, DragStartEventArgs e, CancellationToken token)
        {
            var items = this.ListBox.SelectedItems
                .Cast<FolderItem>()
                .Where(x => !x.Attributes.HasFlag(FolderItemAttribute.Empty))
                .ToList();

            if (!items.Any()
                    && e.DragItem is ListBoxItem listBoxItem
                    && listBoxItem.DataContext is FolderItem folderItem
                    && !folderItem.Attributes.HasFlag(FolderItemAttribute.Empty)
            ){
                items.Add(folderItem);
            }

            if (!items.Any())
            {
                e.Cancel = true;
                return;
            }

            if (items.Any(e => e.Type == FolderItemType.ParentDirectory))
            {
                e.Cancel = true;
                return;
            }

            // List<QueryPath>
            e.Data.SetQueryPathCollection(items.Select(x => x.TargetPath));

            // bookmark?
            if (items.Any(x => x.Attributes.AnyFlagFast(FolderItemAttribute.Bookmark)))
            {
                var collection = items.Select(x => x.Source).OfType<TreeListNode<IBookmarkEntry>>().ToBookmarkNodeCollection();
                e.Data.SetData(collection);
                e.AllowedEffects |= DragDropEffects.Move;
            }
            // files only, no archive path
            else if (items.All(e => System.IO.Path.Exists(e.TargetPath.SimplePath)))
            {
                var collection = new System.Collections.Specialized.StringCollection();
                foreach (var path in items.Where(x => x.IsFileSystem()).Select(x => x.TargetPath.SimplePath).Distinct())
                {
                    collection.Add(path);
                }
                if (collection.Count > 0)
                {
                    e.Data.SetFileDropList(collection);

                    // 右クリックドラッグは移動を許可
                    if (Config.Current.System.IsFileWriteAccessEnabled && e.MouseEventArgs.RightButton == MouseButtonState.Pressed)
                    {
                        e.AllowedEffects |= DragDropEffects.Move;
                    }
                }
            }

            // text
            if (Config.Current.System.TextCopyPolicy != TextCopyPolicy.None)
            {
                var text = string.Join(System.Environment.NewLine, items.Select(e => e.TargetPath.SimplePath));
                e.Data.SetText(text);
            }
        }

        private void ListBox_PreviewDragEnter(object sender, DragEventArgs e)
        {
            _dropAssist.OnDragEnter(sender, e);

            ListBox_PreviewDragOver(sender, e);
            if (e.Handled) return;

            ListBox_DragOver(sender, e);
        }

        private void ListBox_PreviewDragLeave(object sender, DragEventArgs e)
        {
            _dropAssist.OnDragLeave(sender, e);
        }

        private void ListBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Handled) return;

            var scrolled = DragDropHelper.AutoScroll(sender, e);
            if (scrolled)
            {
                _dropAssist.HideAdorner();
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void ListBox_DragOver(object sender, DragEventArgs e)
        {
            var target = _dropAssist.OnDragOver(sender, e);

            if (!AcceptDrop(e, target))
            {
                _dropAssist.HideAdorner();
                return;
            }

            e.Handled = true;
        }

        private void ListBox_Drop(object sender, DragEventArgs e)
        {
            var target = _dropAssist.OnDrop(sender, e);

            if (!AcceptDrop(e, target))
            {
                return;
            }

            if (_vm.FolderCollection is not BookmarkFolderCollection bookmarkFolderCollection)
            {
                return;
            }

            var bookmarkNode = GetTargetBookmarkNode(target);
            var delta = target.Delta;
            ///*
            if (bookmarkNode is not null && bookmarkNode.Value is not BookmarkFolder)
            {
                bookmarkNode = bookmarkNode.Parent;
            }
            //*/
            if (bookmarkNode is null)
            {
                bookmarkNode = bookmarkFolderCollection.BookmarkPlace;
                delta = 0;
            }

            var bookmarkEntries = e.Data.GetData<BookmarkNodeCollection>();
            if (bookmarkEntries is not null)
            {
                var copyMaybe = e.Effects.HasFlag(DragDropEffects.Copy);
                var entries = copyMaybe
                    ? bookmarkEntries.Select(x => x.Clone()).ToList()
                    : bookmarkEntries.ToList();

                DropToBookmark(sender, e, entries, bookmarkNode, delta);
                e.Handled = true;
                return;
            }

            var queries = e.Data.GetQueryPathCollection();
            if (queries is not null)
            {
                var addTargetNode = bookmarkFolderCollection.BookmarkPlace;

                if (bookmarkNode is not null
                    && bookmarkNode.Value is BookmarkFolder
                    && target.IsOver)
                {
                    addTargetNode = bookmarkNode;
                }

                var options = new BookmarkAddOptions()
                {
                    AllowDuplicate = true,
                    OpenPageMode = e.KeyStates.HasFlag(DragDropKeyStates.ControlKey)
                        ? BookmarkOpenPageMode.Fixed
                        : BookmarkOpenPageMode.Resume,
                };

                foreach (var query in queries)
                {
                    BookmarkCollectionService.Add(query, addTargetNode, null, options);
                }

                e.Handled = true;
                return;
            }
        }

        private bool AcceptDrop(DragEventArgs e, DropTargetItem target)
        {
            if (_vm.FolderCollection is not BookmarkFolderCollection)
            {
                return false;
            }

            if (_vm.FolderCollection.Place.Search is not null)
            {
                return false;
            }

            var entries = e.Data.GetData<BookmarkNodeCollection>();
            if (entries is not null)
            {
                e.Effects = Keyboard.Modifiers == ModifierKeys.Control ? DragDropEffects.Copy : DragDropEffects.Move;

                var destination = GetTargetBookmarkNode(target);
                if (destination is null)
                {
                    destination = ((target.Item as ListBoxItem)?.Content as FolderItem)?.Source as TreeListNode<IBookmarkEntry>;
                }

                if (destination is null)
                {
                    return _vm.FolderCollection.ValidCount == 0;
                }

                return !entries.Contains(destination) && entries.All(e => !destination.ParentContains(e));
            }

            var queries = e.Data.GetQueryPathCollection();
            if (queries is not null)
            {
                e.Effects = DragDropEffects.Copy;
                return queries.Any();
            }

            var files = e.Data.GetNormalizedFileDrop();
            if (files is not null)
            {
                e.Effects = DragDropEffects.Copy;
                return files.Any();
            }

            return false;
        }

        private static TreeListNode<IBookmarkEntry>? GetTargetBookmarkNode(DropTargetItem target)
        {
            if (target.Item is ListBoxItem listBoxItem && listBoxItem.Content is FolderItem folderItem)
            {
                if (folderItem.Type == FolderItemType.ParentDirectory && target.IsOver)
                {
                    return BookmarkCollection.Current.FindNode(folderItem.TargetPath);
                }

                return folderItem.Source as TreeListNode<IBookmarkEntry>;
            }

            return null;
        }

        private List<TreeListNode<IBookmarkEntry>>? GetBookmarkEntryCollection(DragEventArgs e, bool copyMaybe)
        {
            var entries = e.Data.GetData<BookmarkNodeCollection>();
            if (entries is not null)
            {
                if (copyMaybe)
                {
                    return entries.Select(e => e.Clone()).ToList();
                }
                else
                {
                    return entries;
                }
            }

            var queries = e.Data.GetQueryPathCollection();
            if (queries is not null)
            {
                return queries.Select(e => BookmarkCollectionService.CreateBookmarkNode(e)).WhereNotNull().ToList();
            }

            var files = e.Data.GetNormalizedFileDrop();
            if (files is not null)
            {
                return files.Select(e => BookmarkCollectionService.CreateBookmarkNode(new QueryPath(e))).WhereNotNull().ToList();
            }

            return null;
        }

        private void DropToBookmark(object? sender, DragEventArgs e, IEnumerable<TreeListNode<IBookmarkEntry>>? dropItems, TreeListNode<IBookmarkEntry> targetItem, int delta)
        {
            if (dropItems == null || !dropItems.Any())
            {
                return;
            }

            // 複数の移動では順番を維持する
            if (dropItems.Count() > 1)
            {
                // NOTE: 新規のエントリの場合は Index=0 なのでソートされない
                dropItems = dropItems.OrderBy(e => e.GetIndex()).ToList();
            }

            // 処理後の選択項目
            List<FolderItem> selectedItems;

            // フォルダーに移動/登録
            if (delta == 0)
            {
                if (targetItem.Value is not BookmarkFolder)
                {
                    // 対象がフォルダーでない場合は現在の場所への移動とみなす
                    targetItem = targetItem.Parent ?? throw new InvalidOperationException("No parent");
                }

                foreach (var dropItem in dropItems)
                {
                    DropToBookmark(dropItem, targetItem, 0);
                }

                selectedItems = new[] { FindFolderItem(targetItem) }.WhereNotNull().ToList();
            }

            // ドロップ位置に移動/登録
            else
            {
                foreach (var dropItem in dropItems)
                {
                    var isSuccess = DropToBookmark(dropItem, targetItem, delta);
                    if (isSuccess)
                    {
                        // 複数登録の場合の整列
                        Debug.Assert(dropItem.Parent == targetItem.Parent);
                        targetItem = dropItem;
                        delta = +1;
                    }
                }
                selectedItems = dropItems.Select(e => FindFolderItem(e)).WhereNotNull().ToList();
            }

            // フォーカス調整
            if (selectedItems.Any())
            {
                this.ListBox.SetSelectedItems(selectedItems);
            }
        }

        private bool DropToBookmark(TreeListNode<IBookmarkEntry> dropItem, TreeListNode<IBookmarkEntry> targetItem, int delta)
        {
            if (delta == 0)
            {
                _vm.Model.SelectBookmark(targetItem, true);
                return BookmarkCollection.Current.MoveToChild(dropItem, targetItem);
            }
            else if (CanInsertBookmark())
            {
                if (dropItem == targetItem) return false;
                if (targetItem.Parent is null) return false;

                var index = GetDeltaNodeIndex(dropItem, targetItem, delta);
                return BookmarkCollection.Current.Move(targetItem.Parent, dropItem, index);
            }

            return false;
        }

        private bool CanInsertBookmark()
        {
            return _vm.FolderCollection is BookmarkFolderCollection bookmarkFolderCollection && bookmarkFolderCollection.FolderOrder.IsEntryCategory();
        }

        private FolderItem? FindFolderItem(TreeListNode<IBookmarkEntry> items)
        {
            var collection = _vm.FolderCollection;
            if (collection is null) return null;

            var item = collection.FirstOrDefault(e => e.Source == items);
            if (item is not null) return item;

            if (items.Value is not Bookmark bookmark) return null;
            return collection.FirstOrDefault(e => ((e.Source as TreeListNode<IBookmarkEntry>)?.Value as Bookmark)?.Path == bookmark.Path);
        }

        private int GetDeltaNodeIndex(TreeListNode<IBookmarkEntry> dropItems, TreeListNode<IBookmarkEntry> targetItem, int delta)
        {
            var index = targetItem.GetIndex();
            var direction = _vm.FolderOrder.IsDescending() ? -delta : delta;

            if (dropItems.Parent == targetItem.Parent && dropItems.GetIndex() < index)
            {
                return (direction < 0 && targetItem.Previous is not null) ? index - 1 : index;
            }
            else
            {
                return (direction > 0) ? index + 1 : index;
            }
        }

        #endregion DragDrop


        private void FolderListBox_Loaded(object? sender, RoutedEventArgs e)
        {
            _jobClient = new PageThumbnailJobClient("FolderList", JobCategories.BookThumbnailCategory);
            _thumbnailLoader = new ListBoxThumbnailLoader(this, _jobClient);

            _vm.SelectedChanged += ViewModel_SelectedChanged;
            _vm.BusyChanged += ViewModel_BusyChanged;

            Config.Current.Panels.ContentItemProfile.PropertyChanged += PanelListItemProfile_PropertyChanged;
            Config.Current.Panels.BannerItemProfile.PropertyChanged += PanelListItemProfile_PropertyChanged;
            _vm.ThumbnailItemProfile.PropertyChanged += PanelListItemProfile_PropertyChanged;
        }

        private void FolderListBox_Unloaded(object? sender, RoutedEventArgs e)
        {
            _jobClient?.Dispose();

            _vm.SelectedChanged -= ViewModel_SelectedChanged;
            _vm.BusyChanged -= ViewModel_BusyChanged;

            Config.Current.Panels.ContentItemProfile.PropertyChanged -= PanelListItemProfile_PropertyChanged;
            Config.Current.Panels.BannerItemProfile.PropertyChanged -= PanelListItemProfile_PropertyChanged;
            _vm.ThumbnailItemProfile.PropertyChanged -= PanelListItemProfile_PropertyChanged;
        }

        /// <summary>
        /// サムネイルパラメーターが変化したらアイテムをリフレッシュする
        /// </summary>
        private void PanelListItemProfile_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            this.ListBox.Items?.Refresh();
        }

        /// <summary>
        /// フォーカス取得
        /// </summary>
        /// <param name="isFocus"></param>
        public void FocusSelectedItem(bool isFocus)
        {
            if (!this.ListBox.IsVisible)
            {
                return;
            }

            if (this.ListBox.SelectedIndex < 0)
            {
                this.ListBox.SelectedIndex = 0;
            }

            var needToFocus = (isFocus && this.IsFocusEnabled) || _vm.IsFocusAtOnce;

            if (this.ListBox.SelectedIndex < 0 && needToFocus)
            {
                _vm.IsFocusAtOnce = false;
                this.ListBox.Focus();
                return;
            }

            // 選択項目が表示されるようにスクロール
            this.ListBox.ScrollIntoView(this.ListBox.SelectedItem);

            if (needToFocus)
            {
                _vm.IsFocusAtOnce = false;
                ListBoxItem lbi = (ListBoxItem)(this.ListBox.ItemContainerGenerator.ContainerFromIndex(this.ListBox.SelectedIndex));
                lbi?.Focus();
            }
        }


        public async void ViewModel_SelectedChanged(object? sender, FolderListSelectedChangedEventArgs e)
        {
            this.ListBox.ScrollIntoView(this.ListBox.SelectedItem);
            this.ListBox.UpdateLayout();
            this.ListBox.FocusSelectedItem(false);

            _thumbnailLoader?.Load();

            if (e.IsNewFolder && this.ListBox.SelectedItem is BookmarkFolderFolderItem)
            {
                await RenameAsync();
            }
        }

        private void FolderList_Loaded(object? sender, RoutedEventArgs e)
        {
            _ = 0;
        }

        private async void FolderList_IsVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue == true)
            {
                _vm.IsVisibleChanged(true);
                // NOTE: ListBoxItemの表示を確定？
                await Task.Yield();
                FocusSelectedItem(false);
            }
        }

        private void FolderList_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                Key key = e.Key == Key.System ? e.SystemKey : e.Key;

                if (key == Key.Home)
                {
                    _vm.MoveToHome();
                    e.Handled = true;
                }
                else if (key == Key.Up)
                {
                    _vm.MoveToUp();
                    e.Handled = true;
                }
                else if (key == Key.Down)
                {
                    if (sender is ListBox { SelectedItem: FolderItem item })
                    {
                        _vm.MoveToSafety(item);
                        e.Handled = true;
                    }
                }
                else if (key == Key.Left)
                {
                    _vm.MoveToPrevious();
                    e.Handled = true;
                }
                else if (key == Key.Right)
                {
                    _vm.MoveToNext();
                    e.Handled = true;
                }
            }
        }

        private void FolderList_KeyDown(object? sender, KeyEventArgs e)
        {
            if (this.ListBox.IsSimpleTextSearchEnabled)
            {
                KeyExGesture.AddFilter(KeyExGestureFilter.TextKey);
            }

            bool isLRKeyEnabled = _vm.IsLRKeyEnabled();
            var columns = Math.Max(1, (int)(this.ListBox.ActualWidth / _vm.ThumbnailItemSize.Width));

            var focused = Keyboard.FocusedElement as DependencyObject;
            var item = FindVisualParent<ListBoxItem>(focused);

            if (isLRKeyEnabled && e.Key == Key.Left) // ←
            {
                _vm.MoveToUp();
                e.Handled = true;
            }
            else if (TryArrowMoveWithModifiedKey(e.Key, e))
            {
                return;
            }
        }

        private bool TryArrowMoveWithModifiedKey(Key key, KeyEventArgs e)
        {
            if (key != Key.Right && key != Key.Left)
            {
                return false;
            }

            var focused = Keyboard.FocusedElement as DependencyObject;
            var item = FindVisualParent<ListBoxItem>(focused);

            if (item == null)
            {
                return false;
            }

            var index = this.ListBox.ItemContainerGenerator.IndexFromContainer(item);
            var destIndex =
                key == Key.Right ? index + 1 :
                key == Key.Left ? index - 1 :
                index;

            if (destIndex < 0 || destIndex >= this.ListBox.Items.Count)
            {
                e.Handled = true;
                return true;
            }

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                var next = this.ListBox.ItemContainerGenerator.ContainerFromIndex(destIndex) as ListBoxItem;
                next?.Focus();

                e.Handled = true;
                return true;
            }

            this.ListBox.SelectedIndex = destIndex;
            FocusSelectedItem(true);

            e.Handled = true;
            return true;
        }

        private static T? FindVisualParent<T>(DependencyObject? obj) where T : DependencyObject
        {
            while (obj != null)
            {
                if (obj is T t)
                {
                    return t;
                }

                obj = VisualTreeHelper.GetParent(obj);
            }

            return null;
        }

        private void FolderList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            _ = 0;
        }

        // 項目クリック (複数選択解除)
        private void ListBox_PreviewMouseUpWithSelectionChanged(object? sender, MouseButtonEventArgs e)
        {
            if (this.ListBox.SelectedItems.Count != 1) return;

            if (this.ListBox.SelectedItem is FolderItem item)
            {
                ClickToLoadBook(item);
            }
        }

        // 項目クリック
        private void FolderListItem_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem { Content: FolderItem item })
            {
                _clickItem = item;
            }
        }

        private void FolderListItem_MouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem { Content: FolderItem item })
            {
                if (_clickItem == item)
                {
                    ClickToLoadBook(item);
                }
            }
            _clickItem = null;
        }

        private void ClickToLoadBook(FolderItem item)
        {
            if (Keyboard.Modifiers != ModifierKeys.None) return;

            if (!Config.Current.Panels.OpenWithDoubleClick && !item.IsEmpty())
            {
                _vm.Model.LoadBook(item);
            }
        }

        // 項目ダブルクリック
        private void FolderListItem_MouseDoubleClick(object? sender, MouseButtonEventArgs e)
        {
            var item = (sender as ListBoxItem)?.Content as FolderItem;
            if (Config.Current.Panels.OpenWithDoubleClick && item != null && !item.IsEmpty())
            {
                _vm.Model.LoadBook(item);
            }

            _vm.MoveToSafety(item);

            e.Handled = true;
        }

        //
        private void FolderListItem_KeyDown(object? sender, KeyEventArgs e)
        {
            bool isLRKeyEnabled = _vm.IsLRKeyEnabled();
            if ((sender as ListBoxItem)?.Content is not FolderItem item) return;

            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                /*
                if (e.Key == Key.Return)
                {
                    _vm.Model.LoadBook(item);
                    e.Handled = true;
                }
                */
                if (e.Key == Key.Return)
                {
                    if (item.CanOpenFolder()) //「ブックマーク」でフォルダーの中へエンター
                    {
                        _vm.MoveToSafety(item);
                    }
                    else
                    {
                        _vm.Model.LoadBook(item);
                    }

                    e.Handled = true;
                }
                else if (isLRKeyEnabled && e.Key == Key.Right) // →
                {
                    _vm.MoveToSafety(item);
                    e.Handled = true;
                }
                else if (isLRKeyEnabled && e.Key == Key.Left) // ←
                {
                    _vm.MoveToUp();
                    e.Handled = true;
                }
            }
        }


        private void FolderListItem_MouseDown(object? sender, MouseButtonEventArgs e)
        {
        }

        private void FolderListItem_MouseUp(object? sender, MouseButtonEventArgs e)
        {
        }

        private void FolderListItem_MouseMove(object? sender, MouseEventArgs e)
        {
        }

        private class OpenTagDialogCommand : ICommand
        {
            private readonly FolderListBox _owner;

            public OpenTagDialogCommand(FolderListBox owner)
            {
                _owner = owner;
            }

            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter)
            {
                new MessageDialog("タグ", "タグ編集ダイアログ予定地")
                    .ShowDialog(Window.GetWindow(_owner));
            }
        }

        /// <summary>
        /// コンテキストメニュー開始前イベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FolderListItem_ContextMenuOpening(object? sender, ContextMenuEventArgs e)
        {
            if (sender is not ListBoxItem container)
            {
                return;
            }

            if (container.Content is not FolderItem item)
            {
                return;
            }

            if (this.ListBox.SelectedItem is not FolderItem selectedItem)
            {
                return;
            }

            // サブフォルダー読み込みの状態を更新
            var isDefaultRecursive = _vm.FolderCollection != null && _vm.FolderCollection.FolderParameter.IsFolderRecursive;
            item.UpdateIsRecursive(isDefaultRecursive);

            // コンテキストメニュー生成
            var contextMenu = container.ContextMenu;
            if (contextMenu == null)
            {
                return;
            }

            contextMenu.Items.Clear();


            if (item.Attributes.HasFlag(FolderItemAttribute.System))
            {
                contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Open"), Command = OpenCommand });
            }
            else if (item.Attributes.HasFlag(FolderItemAttribute.Bookmark))
            {
                if (item.IsDirectory)
                {
                    contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Open"), Command = OpenCommand });
                    contextMenu.Items.Add(new Separator());
                    contextMenu.Items.Add(new MenuItem() { Header = "エリアスをC/Bに作成", Command = CopyBookmarkAliasCommand });
                    contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Delete"), Command = RemoveCommand });
                    contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Rename"), Command = RenameCommand });
                    contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.EditColor"), Command = EditTagColorCommand });
                }
                else
                {
                    contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.OpenBook"), Command = OpenBookCommand });
                    contextMenu.Items.Add(new Separator());
                    contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Explorer"), Command = OpenExplorerCommand });
                    contextMenu.Items.Add(ExternalAppCollectionUtility.CreateExternalAppItem(TextResources.GetString("BookshelfItem.Menu.OpenExternalApp"), OpenExternalApp_CanExecute(), OpenExternalAppCommand, OpenExternalAppDialogCommand));
                    contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Cut"), Command = CutCommand });
                    contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Copy"), Command = CopyCommand });
                    contextMenu.Items.Add(DestinationFolderCollectionUtility.CreateDestinationFolderItem(TextResources.GetString("BookshelfItem.Menu.CopyToFolder"), CopyToFolder_CanExecute(), CopyToFolderCommand, OpenDestinationFolderCommand));
                    contextMenu.Items.Add(DestinationFolderCollectionUtility.CreateDestinationFolderItem(TextResources.GetString("BookshelfItem.Menu.MoveToFolder"), false, MoveToFolderCommand, OpenDestinationFolderCommand));
                    contextMenu.Items.Add(new Separator());
                    contextMenu.Items.Add(new MenuItem() { Header = "ブックマークを切り取り", Command = CutBookmarkCommand });
                    contextMenu.Items.Add(new MenuItem() { Header = "ブックマークをコピー", Command = CopyBookmarkCommand });
                    contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.DeleteBookmark"), Command = RemoveCommand });
                    contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Rename"), Command = RenameCommand });
                }
            }
            else if (item.Attributes.HasFlag(FolderItemAttribute.Empty))
            {
                bool canExplorer = _vm.FolderCollection is not BookmarkFolderCollection;
                contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Explorer"), Command = OpenExplorerCommand, IsEnabled = canExplorer });
                contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Copy"), Command = CopyCommand, IsEnabled = false });
            }
            else if (item.IsFileSystem())
            {
                if (item.IsDirectory || Config.Current.System.ArchiveRecursiveMode != ArchiveEntryCollectionMode.IncludeSubArchives)
                {
                    contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Open"), Command = OpenCommand });
                    contextMenu.Items.Add(new Separator());
                }
                contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.OpenBook"), Command = OpenBookCommand });
                contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Subfolder"), Command = LoadWithRecursiveCommand, IsChecked = item.IsRecursive });
                contextMenu.Items.Add(new Separator());
                //contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("Word.Bookmark"), Command = ToggleBookmarkCommand, IsChecked = BookmarkCollection.Current.Contains(selectedItem.EntityPath.SimplePath) });
                contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("Word.Bookmark"), Command = CreateBookmarkCommand, IsChecked = BookmarkCollection.Current.Contains(selectedItem.EntityPath.SimplePath) });
                contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.DeleteHistory"), Command = RemoveHistoryCommand });
                contextMenu.Items.Add(new Separator());

                if (item.Tags != null && item.Tags.Count > 0)
                {
                    foreach (var tag in item.Tags)
                    {
                        contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetFormatString("BookshelfItem.Menu.OpenBookmarkFolder", tag.Name), Command = OpenBookmarkFolderCommand, CommandParameter = tag });
                    }
                    contextMenu.Items.Add(new Separator());
                }

                contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Explorer"), Command = OpenExplorerCommand });
                contextMenu.Items.Add(ExternalAppCollectionUtility.CreateExternalAppItem(TextResources.GetString("OpenExternalAppAsCommand.Menu"), OpenExternalApp_CanExecute(), OpenExternalAppCommand, OpenExternalAppDialogCommand));
                contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Cut"), Command = CutCommand });
                contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Copy"), Command = CopyCommand });
                contextMenu.Items.Add(DestinationFolderCollectionUtility.CreateDestinationFolderItem(TextResources.GetString("BookshelfItem.Menu.CopyToFolder"), CopyToFolder_CanExecute(), CopyToFolderCommand, OpenDestinationFolderCommand));
                contextMenu.Items.Add(DestinationFolderCollectionUtility.CreateDestinationFolderItem(TextResources.GetString("BookshelfItem.Menu.MoveToFolder"), MoveToFolder_CanExecute(), MoveToFolderCommand, OpenDestinationFolderCommand));
                contextMenu.Items.Add(new Separator());
                contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Delete"), Command = RemoveCommand });
                contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Rename"), Command = RenameCommand });
                contextMenu.Items.Add(new Separator());
                var menu = new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.Thumbnail"), IsEnabled = item.CanThumbnail() };
                menu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.SetThumbnail"), Command = SetThumbnailCommand });
                menu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.RegenerateThumbnail"), Command = RegenerateThumbnailCommand });
                contextMenu.Items.Add(menu);

                if (item.IsPlaylist)
                {
                    contextMenu.Items.Add(new Separator());
                    contextMenu.Items.Add(new MenuItem() { Header = TextResources.GetString("BookshelfItem.Menu.OpenInPlaylist"), Command = OpenInPlaylistCommand });
                }
            }
        }

        /// <summary>
        /// リスト更新中
        /// </summary>
        private void ViewModel_BusyChanged(object? sender, ReferenceCounterChangedEventArgs e)
        {
            this.BusyFade.IsBusy = e.IsActive;
            if (e.IsActive)
            {
                RenameManager.GetRenameManager(this)?.CloseAll(false, false);
            }
        }

        public void Refresh()
        {
            this.ListBox.Items.Refresh();
        }


        #region UI Accessor

        public List<FolderItem> GetItems()
        {
            return _vm.Model.FolderCollection?.Items.ToList() ?? new();
        }

        public List<FolderItem> GetSelectedItems()
        {
            // ListBox 生成直後でプロパティが不定の場合、モデルデータの値を返す
            if (this.ListBox.SelectedItem is null)
            {
                if (_vm.Model.SelectedItem is null)
                {
                    return new();
                }
                else
                {
                    return new() { _vm.Model.SelectedItem };
                }
            }

            return this.ListBox.SelectedItems.Cast<FolderItem>().ToList();
        }

        public void SetSelectedItems(IEnumerable<FolderItem> selectedItems)
        {
            var items = selectedItems?.Intersect(GetItems()).ToList() ?? new List<FolderItem>();
            this.ListBox.SetSelectedItems(items);
            this.ListBox.ScrollItemsIntoView(items);

            // ListBox 生成直後でプロパティが不定の場合、モデルデータにも反映
            // 個数 0 は未初期化とみなされるらしい
            if (items.Count == 0)
            {
                _vm.Model.SelectedItem = null;
            }
        }

        #endregion UI Accessor
    }


    public class FolderItemToNoteConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is FolderItem item && values[1] is FolderOrder order)
            {
                return item.GetNote(order);
            }

            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
