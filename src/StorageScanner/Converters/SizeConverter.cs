using System.Globalization;
using System.Windows.Data;
using StorageScanner.Utils;

namespace StorageScanner.Converters;

public class SizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long bytes)
            return SizeFormatter.FormatBytes(bytes);
        return "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
