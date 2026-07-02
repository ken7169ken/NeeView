using System.Windows;
using NeeView.Properties;

namespace NeeView
{
    public class FocusBookshelfCommand : CommandElement
    {
        public FocusBookshelfCommand()
        {
            this.Group = TextResources.GetString("CommandGroup.Panel");
            this.IsShowMessage = false;
            this.ShortCutKey = new ShortcutKey("Ctrl+2");
        }

        public override void Execute(object? sender, CommandContext e)
        {
            var target = FolderPanel.Current.Presenter.FolderListBox;
            var window = target is not null ? Window.GetWindow(target) : null;

            if (window is not null)
            {
                window.Activate();
                window.Focus();
            }

            BookshelfFolderList.Current.FocusAtOnce();
        }
    }
}
