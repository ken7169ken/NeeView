using NeeView.Properties;
using System.Linq;
using System.Windows;
using System.Windows.Input;


namespace NeeView
{
    public class CreateBookmarkCommand : CommandElement
    {
        public CreateBookmarkCommand()
        {
            this.Group = TextResources.GetString("CommandGroup.Bookmark");
            this.ShortCutKey = new ShortcutKey("Ctrl+D, Ctrl+Shift+D");
            this.IsShowMessage = true;

            this.ParameterSource = new CommandParameterSource(new ToggleBookmarkCommandParameter());
        }

        //public override string ExecuteMessage(object? sender, CommandContext e)
        //{
        //    return "ブックマークを追加しました";
        //}

        public override bool CanExecute(object? sender, CommandContext e)
        {
            return BookOperation.Current.BookControl.CanBookmark();
        }

        [MethodArgument("CreateCommand.Execute.Remarks")]
        public override void Execute(object? sender, CommandContext e)
        {
            var openPageMode = Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) ?
                 BookmarkOpenPageMode.Fixed : 
                 BookmarkOpenPageMode.Resume;

            if (openPageMode == BookmarkOpenPageMode.Fixed)
            {
                var book = BookOperation.Current.Book;
                if (book is null) return;

                var parent = BookmarkPanel.Current.DstFixedBookmarkFolder;
                if (parent is null)
                {
                    MessageBox.Show(
                        "Fixedブックマーク作成フォルダーが登録されていません。",
                        "Fixedブックマーク作成フォルダー",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }

                BookmarkCollectionService.AddTo(
                    new QueryPath(book.Path),
                    parent,
                    null,
                    new BookmarkAddOptions()
                    {
                        AllowDuplicate = true,
                        OpenPageMode = BookmarkOpenPageMode.Fixed,
                    }
                );
            }
            else if (openPageMode == BookmarkOpenPageMode.Resume)
            {
                var book = BookOperation.Current.Book;
                if (book is null) return;

                var parent = BookmarkFolderList.Current.GetBookmarkPlace();
                if (parent is null) return;

                BookmarkCollectionService.AddTo(
                    new QueryPath(book.Path),
                    parent,
                    null,
                    new BookmarkAddOptions()
                    {
                        AllowDuplicate = true,
                        OpenPageMode = BookmarkOpenPageMode.Resume,
                    }
                );
            }
        }
    }
}
