using System;
using System.Globalization;
using System.Windows.Data;

namespace LiveCaptionsTranslator.Utils
{
    /// <summary>
    /// フォントサイズ(第1引数)と行間倍率(第2引数)から LineHeight を算出するマルチコンバーター。
    /// </summary>
    public class FontSizeToLineHeightMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double fontSize = 18.0;
            double multiplier = 1.25;

            if (values.Length > 0)
            {
                if (values[0] is int intVal) fontSize = intVal;
                else if (values[0] is double doubleVal) fontSize = doubleVal;
            }
            if (values.Length > 1)
            {
                if (values[1] is int intMul) multiplier = intMul;
                else if (values[1] is double doubleMul) multiplier = doubleMul;
            }

            return Math.Round(fontSize * multiplier);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
