using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace NeeView
{
    public static class PanelListTextTools
    {
        private const string Ellipsis = "...";
        private const double ThumbnailTextWidthFactor = 1.85;
        private const int TailLength = 9;
        private const int MaxHeadLength = 20;

        public static string CreateThumbnailMiddleEllipsis(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var width = Config.Current.Panels.ThumbnailItemProfile.ShapeWidth * ThumbnailTextWidthFactor;
            var fontSize = FontParameters.Current.PaneFontSize;

            return CreateMiddleEllipsis(text, width, fontSize);
        }

        private static string CreateMiddleEllipsis(string text, double availableWidth, double fontSize)
        {
            if (Measure(text, fontSize) <= availableWidth)
            {
                return text;
            }

            var tail = text.Length > TailLength ? text[^TailLength..] : text;

            for (int headLength = Math.Min(MaxHeadLength, text.Length - tail.Length); headLength >= 1; headLength--)
            {
                var candidate = text[..headLength] + Ellipsis + tail;

                if (Measure(candidate, fontSize) <= availableWidth)
                {
                    return candidate;
                }
            }

            return Ellipsis + tail;
        }

        private static double Measure(string text, double fontSize)
        {
            var dpi = Application.Current?.MainWindow != null
                ? VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip
                : 1.0;

            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                fontSize,
                Brushes.Black,
                dpi);

            return formattedText.WidthIncludingTrailingWhitespace;
        }
    }
}