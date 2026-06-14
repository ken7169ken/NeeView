using NeeView.Properties;
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

        public override string ExecuteMessage(object? sender, CommandContext e)
        {
            return "ブックマークを追加しました";
        }

        public override bool CanExecute(object? sender, CommandContext e)
        {
            return BookOperation.Current.BookControl.CanBookmark();
        }

        [MethodArgument("CreateCommand.Execute.Remarks")]
        /*
        public override void Execute(object? sender, CommandContext e)
        {
            BookOperation.Current.BookControl.SetBookmark(true, GetFolderPath(e));
        }
        */
        public override void Execute(object? sender, CommandContext e)
        {
            var openPageMode =
                Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
                    ? BookmarkOpenPageMode.Fixed
                    : BookmarkOpenPageMode.Resume;

            BookOperation.Current.BookControl.SetBookmark(true, GetFolderPath(e), openPageMode);
        }

        private string? GetFolderPath(CommandContext e)
        {
            //return e.Parameter.Cast<ToggleBookmarkCommandParameter>().Folder;

            var folder = e.Parameter.Cast<ToggleBookmarkCommandParameter>().Folder;
            if (!string.IsNullOrEmpty(folder))
            {
                return folder;
            }

            if (BookmarkFolderList.Current.FolderCollection is BookmarkFolderCollection c)
            {
                return c.Place.ToString();
            }

            return null;
        }
    }
}
