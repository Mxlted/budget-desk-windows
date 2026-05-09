using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using BudgetDesk.Models;
using BudgetDesk.ViewModels;

namespace BudgetDesk;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    MainViewModel VM => (MainViewModel)DataContext;

    void TransactionsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (TransactionsGrid.SelectedItem is Transaction t)
            VM.StartEditTransactionCommand.Execute(t);
    }

    void CategoryLimit_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is BudgetCategory cat)
        {
            if (double.TryParse(tb.Text, NumberStyles.Number, CultureInfo.GetCultureInfo("en-US"), out var limit)
                || double.TryParse(tb.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out limit))
                VM.UpdateCategoryLimit(cat.CategoryName, limit);
        }
    }
}

[MarkupExtensionReturnType(typeof(IValueConverter))]
public class TabVisibilityConverter : MarkupExtension, IValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string tab && parameter is string target)
            return tab.Equals(target, StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

[MarkupExtensionReturnType(typeof(IMultiValueConverter))]
public class GoalPercentConverter : MarkupExtension, IMultiValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is double saved && values[1] is double target && target > 0)
            return Math.Clamp(saved / target * 100, 0, 100);
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => Binding.DoNothing).ToArray();
}

[MarkupExtensionReturnType(typeof(IValueConverter))]
public class CountVisConverter : MarkupExtension, IValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
