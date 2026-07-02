using System.Windows;
using NeeView.Properties;

namespace NeeView
{
    public class FocusHistoryCommand : CommandElement
    {
        public FocusHistoryCommand()
        {
            this.Group = TextResources.GetString("CommandGroup.Panel");
            this.IsShowMessage = false;
            this.ShortCutKey = new ShortcutKey("Ctrl+4");
        }

        public override void Execute(object? sender, CommandContext e)
        {
            var target = HistoryPanel.Current.Presenter.HistoryListBox;
            var window = target is not null ? Window.GetWindow(target) : null;

            if (window is not null)
            {
                window.Activate();
                window.Focus();
            }

            HistoryPanel.Current.Presenter.FocusAtOnce();
        }
    }
}