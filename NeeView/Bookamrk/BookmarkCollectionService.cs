using NeeView.Collections.Generic;
using NeeView.Properties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace NeeView
{
    /// <summary>
    /// ブックマークリスト用のブックマーク登録管理
    /// </summary>
    public static class BookmarkCollectionService
    {
        private static CancellationTokenSource? _removeUnlinkedCancellationTokenSource;

        /// <summary>
        /// ブックマークを追加する
        /// </summary>
        public static TreeListNode<IBookmarkEntry>? Add(
            QueryPath                     query,
            TreeListNode<IBookmarkEntry>? parent,
            string?                       name,
            BookmarkAddOptions            options)
        {
            if     (parent is not null    ) return AddTo(query, parent, name, options                          );
            else if(options.AllowDuplicate) return AddTo(query, BookmarkCollection.Current.Items, name, options);
            else                            return Add  (query, name, options                                  );
        }

        /// <summary>
        /// 現在開いているフォルダーリストの場所を優先してブックマークを追加する
        /// </summary>
        public static TreeListNode<IBookmarkEntry>? Add(
            QueryPath          query,
            string?            name,
            BookmarkAddOptions options)
        {
            if (!BookmarkFolderList.Current.AddBookmark(query, false, options))
            {
                return AddTo(query, BookmarkCollection.Current.Items, name, options);
            }
            return null;
        }

        private static string CreateBookmarkNameWithPageNumber(string bookName, int pageNumber)
        {
            return $"{System.IO.Path.GetFileNameWithoutExtension(bookName)}({pageNumber:00}){System.IO.Path.GetExtension(bookName)}";
        }

        /// <summary>
        /// ブックマークフォルダーを指定してブックマークを追加する
        /// </summary>
        // 2026-06-07#KKRev_Start(001)
        public static TreeListNode<IBookmarkEntry>? AddTo(
            QueryPath                    query,
            TreeListNode<IBookmarkEntry> parent,
            string?                      name,
            BookmarkAddOptions           options)
        {
            if (query.Scheme != QueryScheme.File)    return null;

            var unit        = BookMementoCollection.Current.Set(query.SimplePath);
            var currentPage = BookOperation.Current.Book?.CurrentPage;
            var page        = currentPage?.EntryName ?? unit.Memento.Page;

            if (!options.AllowDuplicate && FindBookmark(parent, query, page) != null) return null;

            var pageNumber = currentPage?.IndexPlusOne;
            _ = 0;//BP事後評価用

            var bookmark = new Bookmark(unit)
            {
                BookmarkPage  = page,
                BookmarkProps = unit.Memento.ToPropertiesString(),
                OpenPageMode  = options.OpenPageMode,
                SortGroup     = query.SimplePath,
                SortIndex     = options.OpenPageMode == BookmarkOpenPageMode.Resume ? 0 : pageNumber ?? 0,
                Thumb = options.OpenPageMode == BookmarkOpenPageMode.Fixed ? currentPage?.EntryFullName : null,
            };

            //ここから追加。(20260607_1712_43 Start)
            _ = 0;//BP事後評価用
            if (name is not null)
            {
                bookmark.Name = name;
            }
            else if (options.OpenPageMode == BookmarkOpenPageMode.Fixed && pageNumber is not null)
            {
                bookmark.Name = CreateBookmarkNameWithPageNumber(unit.Memento.Name, pageNumber.Value);
            }
            var node = new TreeListNode<IBookmarkEntry>(bookmark);
            BookmarkCollection.Current.AddToChild(node, parent);
            //ここまで。(20260607_1712_43 End)

            return node;
        }

        /// <summary>
        /// ブックマークを削除する
        /// </summary>
        public static bool Remove(QueryPath query, TreeListNode<IBookmarkEntry>? parent)
        {
            if (parent is not null)
            {
                return RemoveFrom(query, parent);
            }
            else
            {
                return Remove(query);
            }
        }

        /// <summary>
        /// 現在開いているフォルダーリストを優先してブックマークを削除する
        /// </summary>
        public static bool Remove(QueryPath query)
        {
            if (BookshelfFolderList.Current.FolderCollection is BookmarkFolderCollection bookmarkFolderCollection)
            {
                var node = bookmarkFolderCollection.BookmarkPlace.WithLock(e => e.Children.FirstOrDefault(e => e.IsEqual(query)));
                if (node != null)
                {
                    return BookmarkCollection.Current.Remove(node);
                }
            }

            return BookmarkCollection.Current.Remove(BookmarkCollection.Current.FindNode(query));
        }

        /// <summary>
        /// 指定したフォルダーからブックマークを削除する
        /// </summary>
        public static bool RemoveFrom(QueryPath query, TreeListNode<IBookmarkEntry> parent)
        {
            var node = FindChildBookmark(query, parent);
            if (node != null)
            {
                return BookmarkCollection.Current.Remove(node);
            }
            else
            {
                return false;
            }
        }

        public static int RemoveAll(QueryPath query)
        {
            if (query is null || query.Scheme != QueryScheme.File)
                return 0;

            var nodes = BookmarkCollection.Current.Collect(query.SimplePath);
            var count = 0;

            foreach (var node in nodes)
            {
                if (BookmarkCollection.Current.Remove(node))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// ブックマークを削除
        /// </summary>
        /// <param name="node"></param>
        public static void Remove(TreeListNode<IBookmarkEntry> node)
        {
            BookmarkCollection.Current.Remove(node);
        }

        /// <summary>
        /// ブックマークを他のフォルダーに移動
        /// </summary>
        /// <param name="node"></param>
        /// <param name="parent"></param>
        public static void MoveToChild(TreeListNode<IBookmarkEntry> node, TreeListNode<IBookmarkEntry> parent)
        {
            BookmarkCollection.Current.MoveToChild(node, parent);
        }

        /// <summary>
        /// フォルダー内でブックマークを検索する
        /// </summary>
        /// <param name="path"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static TreeListNode<IBookmarkEntry>? FindChildBookmark(QueryPath path, TreeListNode<IBookmarkEntry>? parent)
        {
            if (path is null || path.Scheme != QueryScheme.File)
            {
                return null;
            }

            if (parent is null)
            {
                return BookmarkCollection.Current.FindNode(path);
            }
            else
            {
                return parent.WithLock(e => e.Children.FirstOrDefault(e => e.Value is Bookmark bookmark && bookmark.Path == path.SimplePath));
            }
        }

        /// <summary>
        /// ブックマークの名前変更とそれに伴う統合を行う
        /// </summary>
        public static bool Rename(TreeListNode<IBookmarkEntry> node, string newName)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (node.Value is BookmarkFolder folder)
            {
                newName = BookmarkTools.GetValidateName(newName);
                var oldName = folder.Name;

                if (string.IsNullOrEmpty(newName))
                {
                    return false;
                }

                if (newName != oldName)
                {
                    var conflict = node.Parent?.WithLock(e => e.Children.FirstOrDefault(e => e != node && e.Value is BookmarkFolder && e.Value.Name == newName));
                    if (conflict != null)
                    {
                        var dialog = new MessageDialog(TextResources.GetString("MergeFolderDialog.Title"), string.Format(CultureInfo.InvariantCulture, TextResources.GetString("MergeFolderDialog.Message"), newName));
                        dialog.Commands.Add(UICommands.Yes);
                        dialog.Commands.Add(UICommands.No);
                        var result = dialog.ShowDialog();

                        if (result.Command == UICommands.Yes)
                        {
                            BookmarkCollection.Current.Merge(node, conflict);
                            return true;
                        }
                    }
                    else
                    {
                        folder.Name = newName;
                        BookmarkCollection.Current.RaiseBookmarkChangedEvent(new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Rename, node.Parent, node) { OldName = oldName });
                        return true;
                    }
                }
                return false;
            }
            else if (node.Value is Bookmark bookmark)
            {
                newName = BookmarkTools.GetValidateName(newName);
                var oldName = bookmark.Name;
                bookmark.Name = newName;
                if (bookmark.Name != oldName)
                {
                    BookmarkCollection.Current.RaiseBookmarkChangedEvent(new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Rename, node.Parent, node) { OldName = oldName });
                }
                return true;
            }

            throw new InvalidOperationException($"Cannot rename node of type {node.Value.GetType().Name}.");
        }

        /// <summary>
        /// ブックマークフォルダ―に色を設定
        /// </summary>
        public static bool SetColor(TreeListNode<IBookmarkEntry> node, Color? color)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (node.Value is not BookmarkFolder folder)
            {
                return false;
            }

            if (folder.Color == color)
            {
                return false;
            }

            folder.Color = color;
            BookmarkCollection.Current.RaiseBookmarkChangedEvent(new BookmarkCollectionChangedEventArgs(EntryCollectionChangedAction.Update, node.Parent, node));
            return true;
        }

        /// <summary>
        /// Query から BookmarkNode を生成する。親ノードはまだ未定。
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static TreeListNode<IBookmarkEntry>? CreateBookmarkNode(QueryPath query)
        {
            if (!IsBookPath(query)) return null;

            var unit = BookMementoCollection.Current.Set(query.SimplePath);
            var bookmark = new Bookmark(unit);
            return new TreeListNode<IBookmarkEntry>(bookmark);
        }

        /// <summary>
        /// 有効なブックパスであるかを判定する
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static bool IsBookPath(QueryPath query)
        {
            if (query.Search != null)
            {
                return false;
            }

            return query.Scheme switch
            {
                QueryScheme.File => ArchiveManager.Current.IsSupported(query.SimplePath, true, true) || FileIO.DirectoryExists(query.SimplePath),
                _ => false,
            };
        }

        /// <summary>
        /// ブックマークフォルダーを検索する
        /// </summary>
        /// <param name="path">ブックマークフォルダ―パス</param>
        /// <returns>ブックマークノード。見つからなかったら null</returns>
        public static TreeListNode<IBookmarkEntry>? FindBookmarkFolder(QueryPath path)
        {
            var node = BookmarkCollection.Current.FindNode(path);
            return node != null && node.Value is BookmarkFolder ? node : null;
        }

        /// <summary>
        /// ブックマークフォルダ―からブックマークを検索する
        /// </summary>
        /// <param name="parent">検索するブックマークフォルダ―</param>
        /// <param name="query">ブックマークのターゲットパス</param>
        /// <param name="name">ブックマークの名前。null の場合はブックマークの名前を比較しない</param>
        /// <returns>ブックマークノード。見つからなかったら null</returns>
        public static TreeListNode<IBookmarkEntry>? FindBookmark(
            TreeListNode<IBookmarkEntry> parent,
            QueryPath                    query,
            //string?                      name)
            string?                      page)
        {
            return parent.WithLock(e => e.Children.FirstOrDefault(
                e => e.Value is Bookmark bookmark &&
                bookmark.Path == query.SimplePath &&
                bookmark.BookmarkPage == page
            ));
            /*
            if (name is null)
            {
                return parent.WithLock(e => e.Children.FirstOrDefault(e => e.Value is Bookmark bookmark && bookmark.Path == query.SimplePath));
            }
            else
            {
                return parent.WithLock(e => e.Children.FirstOrDefault(e => e.Value is Bookmark bookmark && bookmark.Path == query.SimplePath && bookmark.Name == name));
            }
            */
        }

        /// <summary>
        /// 現在開いているフォルダーリストを優先してブックマークを検索する
        /// </summary>
        public static TreeListNode<IBookmarkEntry>? FindBookmark(QueryPath query, FolderCollection? folderCollection)
        {
            if (folderCollection is BookmarkFolderCollection bookmarkFolderCollection)
            {
                var node = bookmarkFolderCollection.BookmarkPlace.WithLock(e => e.Children.FirstOrDefault(e => e.IsEqual(query)));
                if (node != null)
                {
                    return node;
                }
            }

            return BookmarkCollection.Current.FindNode(query);
        }

        /// <summary>
        /// 無効なブックマークを削除
        /// </summary>
        /// <returns></returns>
        public static async Task DeleteInvalidBookmark(CancellationToken token)
        {
            // Job としてブックマーク修復を実行
            var jobOperation = ProcessJobEngine.Current.AddJob(Resolve);
            int unlinkedCount = await jobOperation.WaitAsync(token);

            // 削除確認ダイアログと削除
            if (unlinkedCount > 0)
            {
                var dialog = new MessageDialog(TextResources.GetString("DeleteInvalidBookmarkDialog.Title"), TextResources.GetFormatString("DeleteItemsDialog.Message", unlinkedCount));
                dialog.Commands.AddRange(UICommands.OKCancel);
                var result = dialog.ShowDialog();
                if (result.IsPossible)
                {
                    RemoveBookmark(BookmarkCollection.Current.CollectUnlinked(), 1);
                }
            }
            else
            {
                var dialog = new MessageDialog(TextResources.GetString("NoDeleteInvalidBookmarkDialog.Title"), TextResources.GetString("NoDeleteItemsDialog.Message"));
                dialog.ShowDialog();
            }

            static async Task<int> Resolve(IProgress<ProgressContext>? progress, CancellationToken token)
            {
                // 直前の命令はキャンセル
                _removeUnlinkedCancellationTokenSource?.Cancel();
                _removeUnlinkedCancellationTokenSource = new CancellationTokenSource();

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, _removeUnlinkedCancellationTokenSource.Token);
                return await BookmarkCollection.Current.ResolveUnlinkedAsync(progress, tokenSource.Token);
            }
        }

        /// <summary>
        /// ブックマーク削除。復元トースト通知あり
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="recoveryCount"></param>
        /// <returns></returns>
        public static bool RemoveBookmark(IEnumerable<TreeListNode<IBookmarkEntry>> nodes, int recoveryCount = 2)
        {
            if (!nodes.Any())
            {
                return false;
            }

            var mementos = new List<TreeListNodeMemento<IBookmarkEntry>>();
            int count = 0;

            foreach (var node in nodes)
            {
                var memento = new TreeListNodeMemento<IBookmarkEntry>(node);

                bool isRemoved = BookmarkCollection.Current.Remove(node);
                if (isRemoved)
                {
                    mementos.Add(memento);

                    if (node.Value is BookmarkFolder)
                    {
                        count += node.WalkChildren().Count(e => e.Value is Bookmark);
                    }
                    else
                    {
                        count++;
                    }
                }
            }

            if (count >= recoveryCount)
            {
                mementos.Reverse();
                var toast = new Toast(TextResources.GetFormatString("BookmarkFolderDelete.Message", count), null, ToastIcon.Information, TextResources.GetString("Word.Restore"),
                    () => { foreach (var memento in mementos) BookmarkCollection.Current.Restore(memento); });
                ToastService.Current.Show("BookmarkList", toast);
            }

            return (count > 0);
        }
    }
}
