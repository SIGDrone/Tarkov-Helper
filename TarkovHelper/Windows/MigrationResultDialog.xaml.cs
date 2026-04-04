using System.Windows;
using System.Windows.Media;
using TarkovHelper.Services;

namespace TarkovHelper.Windows;

/// <summary>
/// Migration result dialog window.
/// Displays the results of a data migration operation.
/// </summary>
public partial class MigrationResultDialog : Window
{
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public MigrationResultDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Show the dialog with migration results.
    /// </summary>
    /// <param name="result">The migration result to display.</param>
    /// <param name="owner">Optional owner window for centering.</param>
    public static void Show(ConfigMigrationService.MigrationResult result, Window? owner = null)
    {
        var dialog = new MigrationResultDialog();
        if (owner != null)
        {
            dialog.Owner = owner;
        }
        dialog.SetResult(result);
        dialog.ShowDialog();
    }

    /// <summary>
    /// Set the migration result to display.
    /// </summary>
    private void SetResult(ConfigMigrationService.MigrationResult result)
    {
        UpdateLocalizedText(result);

        // Update counts
        TxtMigrationQuestCount.Text = result.QuestProgressCount.ToString();
        TxtMigrationHideoutCount.Text = result.HideoutProgressCount.ToString();
        TxtMigrationInventoryCount.Text = result.ItemInventoryCount.ToString();
        TxtMigrationSettingsCount.Text = result.SettingsCount.ToString();

        TxtMigrationTotalCount.Text = $"{result.TotalCount}개 항목";

        // Show warnings if any
        if (result.HasWarnings || result.HasErrors)
        {
            var allMessages = result.Errors.Concat(result.Warnings).Cast<object>().ToList();
            MigrationWarningsList.ItemsSource = allMessages;
            MigrationWarningsSection.Visibility = Visibility.Visible;

            TxtMigrationWarningsHeader.Text = result.HasErrors ? "오류 및 경고" : "경고";

            TxtMigrationWarningsHeader.Foreground = result.HasErrors
                ? new SolidColorBrush(Color.FromRgb(239, 83, 80)) // Red
                : new SolidColorBrush(Color.FromRgb(255, 167, 38)); // Orange
        }
        else
        {
            MigrationWarningsSection.Visibility = Visibility.Collapsed;
        }

        // Update icon based on result
        if (result.HasErrors)
        {
            TxtMigrationResultIcon.Text = "";
            TxtMigrationResultTitle.Foreground = new SolidColorBrush(Color.FromRgb(239, 83, 80));
        }
        else if (result.HasWarnings)
        {
            TxtMigrationResultIcon.Text = "";
            TxtMigrationResultTitle.Foreground = new SolidColorBrush(Color.FromRgb(255, 167, 38));
        }
        else
        {
            TxtMigrationResultIcon.Text = "";
            TxtMigrationResultTitle.Foreground = (Brush)FindResource("AccentBrush");
        }
    }

    /// <summary>
    /// Update localized text based on current language.
    /// </summary>
    private void UpdateLocalizedText(ConfigMigrationService.MigrationResult result)
    {
        TxtMigrationResultTitle.Text = result.HasErrors ? "마이그레이션 실패" : "마이그레이션 완료";
        TxtMigrationQuestLabel.Text = "퀘스트 진행";
        TxtMigrationHideoutLabel.Text = "하이드아웃 진행";
        TxtMigrationInventoryLabel.Text = "아이템 인벤토리";
        TxtMigrationSettingsLabel.Text = "설정";
        TxtMigrationTotalLabel.Text = "총 가져온 항목: ";
        BtnOk.Content = "확인";
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
