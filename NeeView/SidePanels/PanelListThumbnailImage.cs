using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace NeeView
{
    public partial class PanelListThumbnailImage : Control
    {
        static PanelListThumbnailImage()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PanelListThumbnailImage), new FrameworkPropertyMetadata(typeof(PanelListThumbnailImage)));
        }


        public IThumbnail? Thumbnail
        {
            get { return (IThumbnail)GetValue(ThumbnailProperty); }
            set { SetValue(ThumbnailProperty, value); }
        }

        public static readonly DependencyProperty ThumbnailProperty =
            DependencyProperty.Register("Thumbnail", typeof(IThumbnail), typeof(PanelListThumbnailImage), new PropertyMetadata(null));
        
        public PanelListItemProfile? Profile
        {
            get => (PanelListItemProfile?)GetValue(ProfileProperty);
            set => SetValue(ProfileProperty, value);
        }

        public static readonly DependencyProperty ProfileProperty =
            DependencyProperty.Register(nameof(Profile), typeof(PanelListItemProfile),
            typeof(PanelListThumbnailImage), new PropertyMetadata(null, OnProfileChanged));

        private static void OnProfileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PanelListThumbnailImage self)
            {
                if (e.OldValue is PanelListItemProfile oldProfile)
                {
                    oldProfile.PropertyChanged -= self.Profile_PropertyChanged;
                }

                if (e.NewValue is PanelListItemProfile newProfile)
                {
                    newProfile.PropertyChanged += self.Profile_PropertyChanged;
                }

                self.UpdateSize();
            }
        }

        private void UpdateSize()
        {
            Debug.WriteLine($"UpdateSize: {ProfileOrDefault.ShapeWidth} x {ProfileOrDefault.ShapeHeight}");

            Width = ProfileOrDefault.ShapeWidth;
            Height = ProfileOrDefault.ShapeHeight;
        }

        private void Profile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PanelListItemProfile.ShapeWidth)
                || e.PropertyName == nameof(PanelListItemProfile.ShapeHeight)
                || e.PropertyName == nameof(PanelListItemProfile.ImageWidth)
                || e.PropertyName == nameof(PanelListItemProfile.ImageShape))
            {
                UpdateSize();
            }
        }

        public PanelListItemProfile ProfileOrDefault => Profile ?? Config.Current.Panels.ThumbnailItemProfile;
    }




    public class BooleanToThumbnailStretchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
            {
                return Config.Current.Panels.ThumbnailItemProfile.ImageStretch;
            }
            else
            {
                return System.Windows.Media.Stretch.Uniform;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    public class BooleanToThumbnailViewboxConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
            {
                return Config.Current.Panels.ThumbnailItemProfile.Viewbox;
            }
            else
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    public class BooleanToThumbnailAlignmentYConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
            {
                return Config.Current.Panels.ThumbnailItemProfile.AlignmentY;
            }
            else
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    public class ThumbnailBackgroundBrushConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Brush brush)
            {
                if (brush is SolidColorBrush solidColorBrush && solidColorBrush.Color.A != 0)
                {
                    return brush;
                }
                else
                {
                    return Config.Current.Panels.ThumbnailItemProfile.Background;
                }
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ThumbnailProfileToolTopEnableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
            {
                return Config.Current.Panels.ThumbnailItemProfile.IsImagePopupEnabled;
            }
            else
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
