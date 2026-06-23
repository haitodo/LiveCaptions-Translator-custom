using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LiveCaptionsTranslator.Utils
{
    /// <summary>
    /// フォントサイズの値とパラメーターで指定された倍率を元に、適切な Thickness (Margin/Padding) を算出するコンバーター。
    /// </summary>
    public class FontSizeToThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double fontSize = 18.0;
            if (value is int intVal)
            {
                fontSize = intVal;
            }
            else if (value is double doubleVal)
            {
                fontSize = doubleVal;
            }

            string paramStr = parameter as string ?? "1.0";
            string[] parts = paramStr.Split(',');

            double leftMul = 0, topMul = 0, rightMul = 0, bottomMul = 0;

            if (parts.Length == 1)
            {
                if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                {
                    leftMul = topMul = rightMul = bottomMul = val;
                }
            }
            else if (parts.Length == 2)
            {
                if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double horizontal) &&
                    double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double vertical))
                {
                    leftMul = rightMul = horizontal;
                    topMul = bottomMul = vertical;
                }
            }
            else if (parts.Length == 4)
            {
                double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out leftMul);
                double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out topMul);
                double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out rightMul);
                double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out bottomMul);
            }

            // 余白が小さくなりすぎないよう、非ゼロの辺は最小で1ピクセル以上を確保しつつ四捨五入します。
            double left = leftMul == 0 ? 0 : Math.Max(1, Math.Round(fontSize * leftMul));
            double top = topMul == 0 ? 0 : Math.Max(1, Math.Round(fontSize * topMul));
            double right = rightMul == 0 ? 0 : Math.Max(1, Math.Round(fontSize * rightMul));
            double bottom = bottomMul == 0 ? 0 : Math.Max(1, Math.Round(fontSize * bottomMul));

            return new Thickness(left, top, right, bottom);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
