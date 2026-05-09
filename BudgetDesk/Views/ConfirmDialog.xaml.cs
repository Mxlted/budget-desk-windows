using System.Windows;
using System.Windows.Input;

namespace BudgetDesk.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
        // Allow click-and-drag on the chrome since we removed the title bar.
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        };
    }

    public static bool Show(
        Window? owner,
        string title,
        string body,
        string confirmLabel,
        string? backupHint = null,
        bool destructive = true)
    {
        var dlg = new ConfirmDialog
        {
            Owner = owner ?? Application.Current?.MainWindow,
        };
        dlg.TitleText.Text = title;
        dlg.BodyText.Text = body;
        dlg.ConfirmBtn.Content = confirmLabel;

        if (!string.IsNullOrWhiteSpace(backupHint))
        {
            dlg.BackupHintText.Text = backupHint;
            dlg.BackupHint.Visibility = Visibility.Visible;
        }

        if (!destructive)
        {
            // Soft / informational variant: blue accent and accent button.
            dlg.IconBadge.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "PositiveBadgeBgBrush");
            dlg.IconGlyph.Text = "?";
            dlg.IconGlyph.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "AccentBlueBrush");
            if (Application.Current?.TryFindResource("AccentButton") is Style accent)
                dlg.ConfirmBtn.Style = accent;
        }

        return dlg.ShowDialog() == true;
    }

    void ConfirmBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
