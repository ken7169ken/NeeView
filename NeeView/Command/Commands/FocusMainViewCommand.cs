using NeeView.Properties;

namespace NeeView
{
    public class FocusMainViewCommand : CommandElement
    {
        public FocusMainViewCommand()
        {
            this.Group = TextResources.GetString("CommandGroup.Panel");
            this.IsShowMessage = false;
            this.ShortCutKey = new ShortcutKey("Ctrl+1");

            this.ParameterSource = new CommandParameterSource(new FocusMainViewCommandParameter());
        }

        public override void Execute(object? sender, CommandContext e)
        {
            var window = MainViewManager.Current.GetWindowContainingMainView();
            //ToastService.Current.Show(new Toast($"MainView window = {window?.GetType().Name ?? "null"}"));
            if (window is not null)
            {
                window.Activate();
                window.Focus();
            }

            MainViewManager.Current.FocusMainView(e.Parameter.Cast<FocusMainViewCommandParameter>());
        }
    }

}
