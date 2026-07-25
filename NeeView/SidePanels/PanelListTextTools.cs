using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace NeeView
{
    public static class PanelListTextTools
    {
        private const string Ellipsis = "...";
        private const double ThumbnailTextWidthFactor = 1.75;
        private const int TailLength = 9;
        private const int MaxHeadLength = 20;

        public static string CreateThumbnailMiddleEllipsis(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var width = Config.Current.Panels.BookshelfThumbnailItemProfile.ShapeWidth;
            var fontSize = FontParameters.Current.PaneFontSize;

            return ContainsJapanese(text)
                ? CreateTwoLineMiddleEllipsis(text, width, fontSize)
                : CreateMiddleEllipsis(text, width * ThumbnailTextWidthFactor, fontSize);
        }

        public static string CreateMiddleEllipsis(string text, double availableWidth, double fontSize)
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

        private static string CreateTwoLineMiddleEllipsis(string text, double availableWidth, double fontSize)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 全文が1行に収まる
            if (Measure(text, fontSize) <= availableWidth) return text;

            //
            // 1行目
            //
            // 先頭から1文字ずつ増やし、
            // availableWidthを超える直前で確定する。
            //
            var firstLineEnd = 0;
            while (firstLineEnd < text.Length)
            {
                var candidate = text[..(firstLineEnd + 1)];
                if (Measure(candidate, fontSize) > availableWidth) break;
                firstLineEnd++;
            }

            // 念のため、1文字も収まらない場合
            if (firstLineEnd == 0) return Ellipsis + (text.Length > TailLength ? text[^TailLength..] : text);

            var firstLine = text[..firstLineEnd];
            var remainingText = text[firstLineEnd..];

            //
            // 残り全部が2行目へ収まる場合は、省略しない。
            //
            if (Measure(remainingText, fontSize) <= availableWidth)return firstLine + System.Environment.NewLine + remainingText;

            //
            // ここからMiddle Ellipsis。
            // 末尾9文字を必ず残す。
            //
            var tailStart = Math.Max(firstLineEnd, text.Length - TailLength);
            var tail = text[tailStart..];

            //
            // 2行目
            //
            // 「...」と末尾9文字の幅を最初から含め、
            // その手前へ1行目の続きを可能な限り詰める。
            //
            var secondLineHeadEnd = firstLineEnd;
            var reservedText = Ellipsis + tail;

            while (secondLineHeadEnd < tailStart)
            {
                var candidate =
                    text[firstLineEnd..(secondLineHeadEnd + 1)] +
                    reservedText;

                if (Measure(candidate, fontSize) > availableWidth)
                {
                    break;
                }

                secondLineHeadEnd++;
            }
            var secondLineHead = text[firstLineEnd..secondLineHeadEnd];
            var secondLine = secondLineHead + Ellipsis + tail;

            return firstLine + System.Environment.NewLine + secondLine;
        }

        private static bool ContainsJapanese(string text)
        {
            foreach (var c in text)
            {
                if (c is >= '\u3040' and <= '\u309F' || // ひらがな
                    c is >= '\u30A0' and <= '\u30FF' || // カタカナ
                    c is >= '\u3400' and <= '\u4DBF' || // CJK統合漢字拡張A
                    c is >= '\u4E00' and <= '\u9FFF' || // CJK統合漢字
                    c is >= '\uF900' and <= '\uFAFF' || // CJK互換漢字
                    c is >= '\uFF66' and <= '\uFF9F')   // 半角カタカナ
                {
                    return true;
                }
            }

            return false;
        }

        private static double Measure(string text, double fontSize)
        {
            var dpi = Application.Current?.MainWindow != null
                ? VisualTreeHelper
                    .GetDpi(Application.Current.MainWindow)
                    .PixelsPerDip
                : 1.0;

            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    SystemFonts.MessageFontFamily,
                    FontStyles.Normal,
                    FontWeights.Normal,
                    FontStretches.Normal),
                fontSize,
                Brushes.Black,
                dpi);

            return formattedText.WidthIncludingTrailingWhitespace;
        }
    }
}
