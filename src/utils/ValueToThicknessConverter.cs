using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LiveCaptionsTranslator.Utils
{
    /// <summary>
    /// 数値(double/int)の値とパラメーターを元に Thickness (Margin/Padding) を作成するコンバーター。
    /// </summary>
    public class ValueToThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double val = 0.0;
            if (value is int intVal) val = intVal;
            else if (value is double doubleVal) val = doubleVal;

            string paramStr = parameter as string ?? "1";
            string[] parts = paramStr.Split(',');

            double leftMul = 0, topMul = 0, rightMul = 0, bottomMul = 0;

            if (parts.Length == 1)
            {
                if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double m))
                {
                    leftMul = topMul = rightMul = bottomMul = m;
                }
            }
            else if (parts.Length == 4)
            {
                double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out leftMul);
                double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out topMul);
                double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out rightMul);
                double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out bottomMul);
            }

            return new Thickness(
                Math.Round(val * leftMul),
                Math.Round(val * topMul),
                Math.Round(val * rightMul),
                Math.Round(val * bottomMul)
            );
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
