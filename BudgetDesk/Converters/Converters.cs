using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using static BudgetDesk.Converters.ConverterBrushes;

namespace BudgetDesk.Converters;

public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && hex.StartsWith('#'))
        {
            try { return FrozenBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { }
        }
        return FrozenBrush(Colors.Gray);
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string text)
            return string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string text)
            return string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class PositiveNegativeColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
            return FrozenBrush(d >= 0 ? Color.FromRgb(20, 184, 166) : Color.FromRgb(239, 68, 68));
        return FrozenBrush(Colors.Gray);
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class BudgetPressureColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double pct)
        {
            if (pct > 100) return FrozenBrush(Color.FromRgb(239, 68, 68));
            if (pct > 80) return FrozenBrush(Color.FromRgb(249, 115, 22));
            return FrozenBrush(Color.FromRgb(59, 130, 246));
        }
        return FrozenBrush(Colors.Gray);
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class TransactionAmountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Models.Transaction t)
        {
            var prefix = t.Type == Models.TransactionType.Income ? "+" : "-";
            return $"{prefix}{t.Amount.ToString("C2", CultureInfo.GetCultureInfo("en-US"))}";
        }
        return "$0.00";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class TransactionAmountColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Models.TransactionType t && t == Models.TransactionType.Income)
            return FrozenBrush(Color.FromRgb(59, 130, 246));
        return FrozenBrush(Colors.White);
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (value is true && parameter is string s && enumType.IsEnum && Enum.TryParse(enumType, s, out var result))
            return result;
        return Binding.DoNothing;
    }
}

public class StringEqualsToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string text && parameter is string target && text.Equals(target, StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && parameter is string target ? target : Binding.DoNothing;
}

public class NonRecurringVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Models.TransactionSource.Recurring ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

static class ConverterBrushes
{
    public static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
