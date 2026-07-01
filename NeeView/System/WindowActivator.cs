using NeeView.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace NeeView
{
   /// <summary>
   /// ウィンドウ切り替え
   /// </summary>
   public class WindowActivator
   {
      private static bool _isAppActive;

      public static void NextActivate(int direction)
      {
         var changed = NextSubWindow(direction, false);
         if (changed) return;

         var process = ProcessActivator.NextActivate(direction);
         if (process != null) return;

         if (GetSubWindows().Any())
         {
            Application.Current.MainWindow.Activate();
         }
      }

      public static void BringAllToFront(Window? activeWindow)
      {
         var windows = new List<Window>() { Application.Current.MainWindow };
         windows.AddRange(GetSubWindows());

         foreach (var window in windows.Where(e => e.IsVisible && e != activeWindow))
         {
            WindowTools.SetWindowZOrder(window, Application.Current.MainWindow, true);
         }

         if (activeWindow is not null && activeWindow.IsVisible)
         {
            WindowTools.SetWindowZOrder(activeWindow, Application.Current.MainWindow, true);
         }
      }

      public static void BringAllToFrontFromOutside(Window activeWindow)
      {
         if (_isAppActive) return;

         _isAppActive = true;
         BringAllToFront(activeWindow);
      }

      public static bool IsAnyNeeViewWindowActive()
      {
         var windows = new List<Window>() { Application.Current.MainWindow };
         windows.AddRange(GetSubWindows());

         return windows.Any(e => e.IsActive);
      }

      public static void Deactivated(Window window)
      {
         App.Current.Dispatcher.BeginInvoke(() =>
         {
            _isAppActive = IsAnyNeeViewWindowActive();
         });
      }

      public static void SyncSubWindowState(WindowState state)
      {
         foreach (var window in GetSubWindows().Where(e => e.IsVisible))
         {
            window.WindowState = state;
         }
      }

      private static IEnumerable<Window> GetSubWindows()
      {
         var viewWindow = MainViewManager.Current.Window;
         var layoutPanelWindows = CustomLayoutPanelManager.Current.Windows.Windows.Cast<Window>();

         return viewWindow != null ? layoutPanelWindows.Prepend(viewWindow) : layoutPanelWindows;
      }
      private static bool NextSubWindow(int direction, bool allowLoop)
      {
         var windows = new List<Window>() { Application.Current.MainWindow };
         var subWindows = GetSubWindows();
         windows.AddRange(direction > 0 ? subWindows : subWindows.Reverse());

         if (!allowLoop && windows.Last().IsActive) return false;

         var activeWindow = windows.FirstOrDefault(e => e.IsActive);
         if (activeWindow is null)
         {
            var isActive = ActivateSubWindow(windows.First());
            //Debug.WriteLine($"Activate: {isActive}: {windows.First().Title}");
         }
         else
         {
            var index = (windows.IndexOf(activeWindow) + 1) % windows.Count;
            var isActive = ActivateSubWindow(windows[index]);
            //Debug.WriteLine($"Activate: {isActive}: {windows[index].Title}");
         }

         return true;
      }


      private static bool ActivateSubWindow(Window window)
      {
         if (window.WindowState == WindowState.Minimized)
         {
            SystemCommands.RestoreWindow(window);
         }

         return window.Activate();
      }


   }

}
