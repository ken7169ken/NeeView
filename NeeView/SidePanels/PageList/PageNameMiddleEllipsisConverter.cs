using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace NeeView
{
    public class PageNameMiddleEllipsisConverter : IMultiValueConverter
    {
        private readonly PageNameFormatConverter _pageNameFormatConverter = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.WriteLine("[PageNameMiddleEllipsisConverter] called");

            var text = _pageNameFormatConverter.Convert(
                new object[]
                {
                    values.Length > 0 ? values[0] : null!,
                    values.Length > 1 ? values[1] : null!,
                    values.Length > 2 ? values[2] : null!,
                },
                targetType,
                parameter,
                culture
            ) as string ?? "";

            var width = values.Length > 3 && values[3] is double d ? d : 128.0;

            return PanelListTextTools.CreateMiddleEllipsis(
                text,
                width,
                FontParameters.Current.PaneFontSize);
            //return "ABC ... XYZ";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
