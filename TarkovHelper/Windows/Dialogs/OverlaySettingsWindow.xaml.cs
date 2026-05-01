using System.Windows;
using TarkovHelper.Models.Map;
using TarkovHelper.Services;

namespace TarkovHelper.Windows.Dialogs;

/// <summary>
/// Overlay settings window
/// </summary>
public partial class OverlaySettingsWindow : Window
{
    private readonly OverlayMiniMapSettings _settings;
    private readonly OverlayMiniMapWindow? _overlayWindow;
    private bool _isInitializing = true;

    /// <summary>
    /// Settings applied event
    /// </summary>
    public event Action<OverlayMiniMapSettings>? SettingsApplied;

    public OverlaySettingsWindow(OverlayMiniMapSettings settings, OverlayMiniMapWindow? overlayWindow)
    {
        InitializeComponent();

        _settings = settings.Clone();
        _overlayWindow = overlayWindow;

        LoadSettings();
        _isInitializing = false;
    }

    private void LoadSettings()
    {
        SliderOpacity.Value = _settings.Opacity * 100;
        SliderZoom.Value = _settings.ZoomLevel * 100;
        SliderMarkerSize.Value = _settings.PlayerMarkerSize * 100;

        RbFixed.IsChecked = _settings.ViewMode == MiniMapViewMode.Fixed;
        RbTracking.IsChecked = _settings.ViewMode == MiniMapViewMode.PlayerTracking;

        ChkClickThrough.IsChecked = _settings.ClickThrough;

        UpdateDisplays();
    }

    private void UpdateDisplays()
    {
        if (TxtOpacity != null)
            TxtOpacity.Text = $"{(int)SliderOpacity.Value}%";
        if (TxtZoom != null)
            TxtZoom.Text = $"{SliderZoom.Value / 100:F2}x";
        if (TxtMarkerSize != null)
            TxtMarkerSize.Text = $"{SliderMarkerSize.Value / 100:F1}x";
    }

    private void ApplySettings()
    {
        if (_isInitializing) return;

        _settings.Opacity = SliderOpacity.Value / 100.0;
        _settings.ZoomLevel = SliderZoom.Value / 100.0;
        _settings.PlayerMarkerSize = SliderMarkerSize.Value / 100.0;
        _settings.ViewMode = RbTracking.IsChecked == true ? MiniMapViewMode.PlayerTracking : MiniMapViewMode.Fixed;

        _settings.ClickThrough = ChkClickThrough.IsChecked == true;

        SettingsApplied?.Invoke(_settings);

        // Apply to overlay window immediately
        ApplyToOverlay();
    }

    private void ApplyToOverlay()
    {
        if (_overlayWindow == null) return;

        // Update opacity directly and update zoom/center
        _overlayWindow.Dispatcher.Invoke(() =>
        {
            if (_overlayWindow.FindName("MainBorder") is System.Windows.Controls.Border border)
            {
                border.Opacity = _settings.Opacity;
            }
            _overlayWindow.SetZoomLevel(_settings.ZoomLevel);
        });
    }

    #region Event Handlers

    private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateDisplays();
        ApplySettings();
    }

    private void SliderZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateDisplays();
        ApplySettings();
    }

    private void SliderMarkerSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateDisplays();
        ApplySettings();
    }

    private void ViewMode_Changed(object sender, RoutedEventArgs e)
    {
        ApplySettings();
    }



    private void ClickThrough_Changed(object sender, RoutedEventArgs e)
    {
        ApplySettings();
        _overlayWindow?.ToggleClickThrough();
    }

    private void BtnCenterPlayer_Click(object sender, RoutedEventArgs e)
    {
        _overlayWindow?.ToggleViewMode();

        // Update UI to reflect the change
        if (_overlayWindow != null)
        {
            RbTracking.IsChecked = true;
        }
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        _settings.ResetToDefaults();
        _isInitializing = true;
        LoadSettings();
        _isInitializing = false;
        ApplySettings();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion
}
