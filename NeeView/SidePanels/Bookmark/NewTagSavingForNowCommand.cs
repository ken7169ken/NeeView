using NeeView.Collections.Generic;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace NeeView
{
    public class NewTagSavingForNowCommand
    {
        private readonly BookmarkListView _owner;

        public static readonly RoutedCommand SavingForNowCommand = new(nameof(SavingForNowCommand), typeof(BookmarkListView));

        static NewTagSavingForNowCommand()
        {
            SavingForNowCommand.InputGestures.Add(new KeyGesture(Key.F, ModifierKeys.Control | ModifierKeys.Shift));
        }

        public NewTagSavingForNowCommand(BookmarkListView owner) { _owner = owner; }

        public CommandBinding CreateCommandBinding()
        {
            return new CommandBinding(
            SavingForNowCommand,
            SavingForNow_Executed,
            SavingForNow_CanExecute
            );
        }

        private void SavingForNow_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!_owner.IsVisible || !_owner.IsKeyboardFocusWithin)
            {
                e.CanExecute = false;
                return;
            }

            try   { e.CanExecute = Clipboard.ContainsText(); }
            catch { e.CanExecute = false;                    }
        }

        private void SavingForNow_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            string name;

            try { name = Clipboard.GetText()?.Trim() ?? ""; }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return;
            }

            if (string.IsNullOrWhiteSpace(name)) return;

            var parent = FindBookmarkFolder(
            "01. 作者",
            "(6) とりあえず保留");

            if (parent is null)
            {
                ToastService.Current.Show(
                    new Toast("保存先のEdgeフォルダーが見つかりません。", null, ToastIcon.Warning)
                );

                return;
            }

            var existing = parent.WithLock(node => node.Children.FirstOrDefault(child =>
                child.Value is BookmarkFolder folder                                      &&
                folder.FolderKind == TagGroupEntryKind.Tag                                &&
                string.Equals(folder.Name, name, StringComparison.CurrentCultureIgnoreCase)));
            

            if (existing is not null)
            {
                OpenBookmarkFolder(existing);
                _owner.FocusFolderListSelectedItem();

                e.Handled = true;
                return;
            }

            var newTag = BookmarkCollection.Current.AddNewFolder(parent, name, true, TagGroupEntryKind.Tag);

            if (newTag is null)
            {
                ToastService.Current.Show(new Toast(
                $"Tag「{name}」を作成できませんでした。",
                null,
                ToastIcon.Warning));

                return;
            }

            OpenBookmarkFolder(newTag);
            _owner.FocusFolderListSelectedItem();
            e.Handled = true;
        }

        private static TreeListNode<IBookmarkEntry>? FindBookmarkFolder(params string[] names)
        {
            var current = BookmarkCollection.Current.Items;

            if (current is null) return null;

            foreach (var name in names)
            {
            var next = current.WithLock(node =>
                node.Children.FirstOrDefault(child =>
                    child.Value is BookmarkFolder folder &&
                    folder.Name == name)
            );

            if (next is null) return null;
            current = next;
            }

            return current;
        }

        private static void OpenBookmarkFolder(TreeListNode<IBookmarkEntry> node)
        {
            BookmarkFolderList.Current.RequestPlace(
            node.CreateQuery(),
            null,
            FolderSetPlaceOption.None
            );
        }
    }
}