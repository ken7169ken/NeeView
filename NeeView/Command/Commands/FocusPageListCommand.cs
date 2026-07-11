using System.Windows;
using NeeView.Properties;

namespace NeeView
{
    public class FocusPageListCommand : CommandElement
    {
        public FocusPageListCommand()
        {
            this.Group = TextResources.GetString("CommandGroup.Panel");
            this.IsShowMessage = false;
            //this.ShortCutKey = new ShortcutKey("Ctrl+5");
        }

        public override void Execute(object? sender, CommandContext e)
        {
            var target = PageListPanel.Current.Presenter.PageListBox;
            var window = target is not null ? Window.GetWindow(target) : null;

            if (window is not null)
            {
                window.Activate();
                window.Focus();
            }

            target?.Dispatcher.BeginInvoke(() =>
            {
                target.FocusSelectedItem(true);
            });
        }
    }
}