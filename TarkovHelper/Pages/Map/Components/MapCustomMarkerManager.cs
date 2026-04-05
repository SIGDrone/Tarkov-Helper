using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TarkovHelper.Models.Map;
using TarkovHelper.Services;
using TarkovHelper.Services.Map;
using TarkovHelper.Windows.Dialogs;

namespace TarkovHelper.Pages.Map.Components;

/// <summary>
/// 사용자가 추가한 커스텀 마커를 관리하는 컴포넌트
/// </summary>
public class MapCustomMarkerManager
{
    private readonly Canvas _container;
    private readonly MapTrackerService _trackerService;
    private readonly IMapCoordinateTransformer _transformer;
    private readonly LocalizationService _loc;
    private readonly UserDataDbService _userDataDb = UserDataDbService.Instance;

    private string? _currentMapKey;
    private string? _currentFloorId;
    private double _zoomLevel = 1.0;
    public ObservableCollection<CustomMapMarker> Markers { get; } = new();
    private readonly List<FrameworkElement> _markerElements = new();

    public event Action<string>? StatusUpdated;

    public MapCustomMarkerManager(
        Canvas container,
        MapTrackerService trackerService,
        IMapCoordinateTransformer transformer,
        LocalizationService loc)
    {
        _container = container;
        _trackerService = trackerService;
        _transformer = transformer;
        _loc = loc;
    }

    public void SetParameters(string? mapKey, string? floorId, double zoomLevel)
    {
        _currentMapKey = mapKey;
        _currentFloorId = floorId;
        _zoomLevel = zoomLevel;
    }

    public async Task RefreshMarkersAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_currentMapKey))
        {
            ClearMarkers();
            return;
        }

        try
        {
            var markers = await _userDataDb.LoadCustomMarkersAsync(_currentMapKey).WaitAsync(ct);
            Markers.Clear();
            foreach (var m in markers) Markers.Add(m);
            UpdateMarkerDisplay();
        }
        catch (OperationCanceledException)
        {
            // 중단됨
        }
        catch (Exception ex)
        {
            StatusUpdated?.Invoke($"커스텀 마커 로드 실패: {ex.Message}");
        }
    }

    private void ClearMarkers()
    {
        foreach (var element in _markerElements)
        {
            _container.Children.Remove(element);
        }
        _markerElements.Clear();
    }

    public void UpdateMarkerDisplay()
    {
        ClearMarkers();

        if (string.IsNullOrEmpty(_currentMapKey)) return;

        var mapMarkers = Markers.Where(m => 
            (string.IsNullOrEmpty(m.FloorId) && string.IsNullOrEmpty(_currentFloorId)) || 
            (m.FloorId == _currentFloorId)
        ).ToList();

        foreach (var marker in mapMarkers)
        {
            var element = CreateMarkerElement(marker);
            if (element != null)
            {
                _markerElements.Add(element);
                _container.Children.Add(element);
            }
        }
    }

    private FrameworkElement? CreateMarkerElement(CustomMapMarker marker)
    {
        if (!_transformer.TryTransform(_currentMapKey!, marker.X, marker.Z, null, out var screenPos) || screenPos == null)
            return null;

        var size = marker.Size > 0 ? marker.Size : 24;
        var container = new Grid
        {
            Width = size,
            Height = size,
            RenderTransform = new TranslateTransform(screenPos.X - (size / 2.0), screenPos.Y - (size / 2.0)),
            Cursor = Cursors.Hand,
            ToolTip = marker.Name,
            Opacity = marker.Opacity * (SettingsService.Instance.MapMarkerOpacity / 100.0),
            Tag = marker
        };

        // 마커 배경 (원형)
        var ellipse = new Ellipse
        {
            Fill = GetColorBrush(marker.Color ?? "#FFD700"), // 기본 금색
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 4, Opacity = 0.5, ShadowDepth = 1 }
        };
        container.Children.Add(ellipse);



        // 우클릭 메뉴
        var contextMenu = new ContextMenu();
        var editItem = new MenuItem { Header = "마커 수정" };
        editItem.Click += async (s, e) => await EditMarkerAsync(marker);
        contextMenu.Items.Add(editItem);

        var deleteItem = new MenuItem { Header = "마커 삭제" };
        deleteItem.Click += async (s, e) => await DeleteMarkerAsync(marker);
        contextMenu.Items.Add(deleteItem);
        container.ContextMenu = contextMenu;

        return container;
    }

    public async Task DeleteMarkerAsync(CustomMapMarker marker)
    {
        var result = MessageBox.Show($"'{marker.Name}' 마커를 삭제하시겠습니까?", "마커 삭제", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            await _userDataDb.DeleteCustomMarkerAsync(marker.Id);
            Markers.Remove(marker);
            UpdateMarkerDisplay();
            StatusUpdated?.Invoke("마커가 삭제되었습니다.");
        }
        catch (Exception ex)
        {
            StatusUpdated?.Invoke($"마커 삭제 실패: {ex.Message}");
        }
    }

    public async Task EditMarkerAsync(CustomMapMarker marker)
    {
        var editor = new CustomMarkerEditorWindow(marker);
        editor.Owner = Window.GetWindow(_container);

        if (editor.ShowDialog() == true && editor.ResultMarker != null)
        {
            var updatedMarker = editor.ResultMarker;
            await _userDataDb.SaveCustomMarkerAsync(updatedMarker);
            
            // 컬렉션 업데이트
            var index = Markers.IndexOf(marker);
            if (index >= 0)
            {
                Markers[index] = updatedMarker;
            }
            
            UpdateMarkerDisplay();
            StatusUpdated?.Invoke("마커가 수정되었습니다.");
        }
    }

    private Brush GetColorBrush(string? colorHex)
    {
        if (string.IsNullOrEmpty(colorHex)) return Brushes.Gold;
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(colorHex)!;
        }
        catch
        {
            return Brushes.Gold;
        }
    }

    public async Task AddMarkerAsync(double screenX, double screenY)
    {
        try
        {
            if (string.IsNullOrEmpty(_currentMapKey))
            {
                StatusUpdated?.Invoke("오류: 선택된 맵이 없습니다.");
                return;
            }

            if (!_transformer.TryTransformToWorld(_currentMapKey, screenX, screenY, out var worldPos) || worldPos == null)
            {
                StatusUpdated?.Invoke("좌표 변환 실패: 지도의 유효 범위 내에서 시도해 주세요.");
                return;
            }

            // 임시 마커 생성 후 에디터 호출
            var tempMarker = new CustomMapMarker(_currentMapKey, "New Marker", worldPos.X, worldPos.Y, worldPos.Z ?? 0, _currentFloorId);
            var editor = new CustomMarkerEditorWindow(tempMarker);
            editor.Owner = Window.GetWindow(_container);

            if (editor.ShowDialog() == true && editor.ResultMarker != null)
            {
                var newMarker = editor.ResultMarker;
                await _userDataDb.SaveCustomMarkerAsync(newMarker);
                
                Markers.Add(newMarker);
                UpdateMarkerDisplay();
                StatusUpdated?.Invoke("새 마커가 성공적으로 생성되었습니다.");
            }
        }
        catch (Exception ex)
        {
            StatusUpdated?.Invoke($"마커 추가 실패: {ex.Message}");
            MessageBox.Show($"마커 추가 중 오류 발생:\n{ex.Message}", "마커 추가 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
