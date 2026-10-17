using System.Globalization;
using System.Windows.Data;

namespace TcpMessenger.App.Converters;

/// <summary>Negates a boolean in both directions (used for the Client radio button).</summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : Binding.DoNothing;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : Binding.DoNothing;
}
