using NeeView.Properties;

namespace NeeView
{
    public class FocusMainViewCommand : CommandElement
    {
        public FocusMainViewCommand()
        {
            this.Group = TextResources.GetString("CommandGroup.Panel");
            this.IsShowMessage = false;

            this.ParameterSource = new CommandParameterSource(new FocusMainViewCommandParameter());
        }

        public override void Execute(object? sender, CommandContext e)
        {
            var window = MainViewManager.Current.GetWindowContainingMainView();
            if (window is not null)
            {
                window.Activate();
                window.Focus();
            }

            MainViewManager.Current.FocusMainView(e.Parameter.Cast<FocusMainViewCommandParameter>());
        }
    }

}
