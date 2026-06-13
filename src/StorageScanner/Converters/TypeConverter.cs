using System;
using System.Globalization;
using System.Windows.Data;

namespace StorageScanner.Converters;

public class TypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isDir)
            return isDir ? "📁" : "📄";
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
