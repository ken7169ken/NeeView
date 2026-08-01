using System.Windows;

namespace NeeView
{
    public enum DescendantBookFilterMode
    { Include, Exclude, }

    public sealed record DescendantBookFilterResult(string TargetFolderPath, DescendantBookFilterMode Mode);

    public partial class DescendantBookFilterDialog : Window
    {
        private readonly DescendantBookFilterDialogViewModel _vm;

        public DescendantBookFilterDialog(string targetFolderPath)
        {
            InitializeComponent();
            _vm         = new DescendantBookFilterDialogViewModel(targetFolderPath);
            DataContext = _vm;
        }

        public DescendantBookFilterResult Result
            => new(_vm.TargetFolderPath, _vm.IsIncludeMode ? DescendantBookFilterMode.Include : DescendantBookFilterMode.Exclude);

        private void OkButton_Click(object sender, RoutedEventArgs e)
        { DialogResult = true; }
    }
}