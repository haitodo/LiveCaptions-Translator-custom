using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ColorEnum = LiveCaptionsTranslator.Utils.Color;

namespace LiveCaptionsTranslator.Utils
{
    public class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ColorEnum colorEnum)
            {
                switch (colorEnum)
                {
                    case ColorEnum.White: return Brushes.White;
                    case ColorEnum.Yellow: return Brushes.Yellow;
                    case ColorEnum.LimeGreen: return Brushes.LimeGreen;
                    case ColorEnum.Aqua: return Brushes.Aqua;
                    case ColorEnum.Blue: return Brushes.Blue;
                    case ColorEnum.DeepPink: return Brushes.DeepPink;
                    case ColorEnum.Red: return Brushes.Red;
                    case ColorEnum.Black: return Brushes.Black;
                    case ColorEnum.Default:
                    default:
                        if (parameter is Brush defaultBrush)
                        {
                            return defaultBrush;
                        }
                        return DependencyProperty.UnsetValue;
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
