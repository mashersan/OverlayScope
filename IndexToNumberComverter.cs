using System;
using System.Globalization;
using System.Windows.Data;

namespace OverlayScope
{
    /// <summary>
    /// 0から始まるインデックスを1から始まる番号に変換するコンバーター
    /// </summary>
    public class IndexToNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return index + 1;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}