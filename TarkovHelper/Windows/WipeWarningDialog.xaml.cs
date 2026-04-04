using System.Diagnostics;
using System.Windows;
using TarkovHelper.Services;

namespace TarkovHelper.Windows;

/// <summary>
/// Wipe warning dialog window.
/// Warns users about potential issues when syncing after an account wipe.
/// </summary>
public partial class WipeWarningDialog : Window
{
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private readonly string _logPath;

    /// <summary>
    /// Gets whether the user chose to continue with the sync.
    /// </summary>
    public bool ShouldContinue { get; private set; }

    /// <summary>
    /// Gets whether the user chose to hide this warning in the future.
    /// </summary>
    public bool DontShowAgain { get; private set; }

    public WipeWarningDialog(string logPath)
    {
        InitializeComponent();
        _logPath = logPath;
        TxtLogPath.Text = logPath;
        UpdateLocalizedText();
    }

    /// <summary>
    /// Show the wipe warning dialog and return whether to continue.
    /// </summary>
    /// <param name="logPath">The log folder path to display.</param>
    /// <param name="owner">Optional owner window for centering.</param>
    /// <returns>True if the user chose to continue, false otherwise.</returns>
    public static bool ShowWarning(string logPath, Window? owner = null)
    {
        var dialog = new WipeWarningDialog(logPath);
        if (owner != null)
        {
            dialog.Owner = owner;
        }
        dialog.ShowDialog();

        // Save preference if user chose to hide warning
        if (dialog.DontShowAgain)
        {
            SettingsService.Instance.HideWipeWarning = true;
        }

        return dialog.ShouldContinue;
    }

    /// <summary>
    /// Update localized text based on current language.
    /// </summary>
    private void UpdateLocalizedText()
    {
        TxtTitle.Text = "퀘스트 동기화 전 확인";
        TxtMessage.Text = "최근 계정 초기화(와이프)를 진행하셨나요?";
        TxtDescription.Text = "계정 초기화 후 동기화를 진행하면 이전 시즌의 로그가 섞여 퀘스트 진행 상태가 올바르지 않게 표시될 수 있습니다.";
        TxtLogFolderLabel.Text = "📁 로그 폴더 위치:";
        TxtRecommendation.Text = "💡 권장 조치: 계정 초기화 이전 날짜의 로그 폴더를 삭제하거나 다른 위치로 백업해 주세요.";
        ChkDontShowAgain.Content = "이 안내를 다시 보지 않기";
        BtnOpenFolder.Content = "폴더 열기";
        BtnContinue.Content = "계속 진행";
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        ShouldContinue = false;
        DontShowAgain = ChkDontShowAgain.IsChecked == true;
        Close();
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_logPath))
        {
            return;
        }

        try
        {
            Process.Start("explorer.exe", _logPath);
        }
        catch (Exception)
        {
            // Copy path to clipboard if can't open
            try
            {
                Clipboard.SetText(_logPath);
                MessageBox.Show(
                    "폴더를 열 수 없습니다. 경로가 클립보드에 복사되었습니다.",
                    "알림",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch
            {
                // Ignore clipboard errors
            }
        }
    }

    private void BtnContinue_Click(object sender, RoutedEventArgs e)
    {
        ShouldContinue = true;
        DontShowAgain = ChkDontShowAgain.IsChecked == true;
        Close();
    }
}
