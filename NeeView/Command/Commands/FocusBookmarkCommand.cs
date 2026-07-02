using System.Windows;
using NeeView.Properties;

namespace NeeView
{
    public class FocusBookmarkCommand : CommandElement
    {
        public FocusBookmarkCommand()
        {
            this.Group = TextResources.GetString("CommandGroup.Panel");
            this.IsShowMessage = false;
            this.ShortCutKey = new ShortcutKey("Ctrl+3");
        }

        public override void Execute(object? sender, CommandContext e)
        {
            var target = BookmarkPanel.Current.Presenter.FolderListBox;
            var window = target is not null ? Window.GetWindow(target) : null;

            if (window is not null)
            {
                window.Activate();
                window.Focus();
            }

            BookmarkFolderList.Current.FocusAtOnce();
        }
    }
}