using System.Windows;

namespace NeeView
{
    public static class Misc
    {
        public static void ShowWarning(string msg)
        {
            MessageBox.Show(msg,
                　　　　　　　"警告",
                　　　　　　　MessageBoxButton.OK,
                　　　　　　　MessageBoxImage.Warning);
        }
    }
}