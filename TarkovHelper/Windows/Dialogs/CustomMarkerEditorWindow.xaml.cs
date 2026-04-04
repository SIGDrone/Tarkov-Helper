using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TarkovHelper.Models.Map;

namespace TarkovHelper.Windows.Dialogs;

public partial class CustomMarkerEditorWindow : Window
{
    private CustomMapMarker _marker;
    public CustomMapMarker ResultMarker { get; private set; }

    public CustomMarkerEditorWindow(CustomMapMarker marker)
    {
        InitializeComponent();
        _marker = marker;
        ResultMarker = null;

        // 초기값 설정
        TxtName.Text = marker.Name;
        SliderSize.Value = marker.Size > 0 ? marker.Size : 24;
        TxtSizeValue.Text = SliderSize.Value.ToString();
        TxtSizeValue.Text = SliderSize.Value.ToString();

        // 색상 선택
        if (!string.IsNullOrEmpty(marker.Color))
        {
            foreach (RadioButton rb in ColorPanel.Children.OfType<RadioButton>())
            {
                if (rb.Tag?.ToString()?.Equals(marker.Color, StringComparison.OrdinalIgnoreCase) == true)
                {
                    rb.IsChecked = true;
                    break;
                }
            }
        }
    }

    private void SliderSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtSizeValue != null)
        {
            TxtSizeValue.Text = Math.Round(e.NewValue).ToString();
        }
    }


    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string selectedColor = "#FFD700"; // 기본값
        foreach (RadioButton rb in ColorPanel.Children.OfType<RadioButton>())
        {
            if (rb.IsChecked == true)
            {
                selectedColor = rb.Tag?.ToString() ?? "#FFD700";
                break;
            }
        }

        ResultMarker = new CustomMapMarker(_marker.MapKey, TxtName.Text.Trim(), _marker.X, _marker.Y, _marker.Z, _marker.FloorId)
        {
            Id = _marker.Id, // 기존 ID 유지
            Color = selectedColor,
            Size = SliderSize.Value,
            Opacity = 1.0,
            CreatedAt = _marker.CreatedAt
        };

        if (string.IsNullOrEmpty(ResultMarker.Name))
        {
            ResultMarker.Name = "New Marker";
        }

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
