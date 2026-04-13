using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TarkovDBEditor.Models;
using TarkovDBEditor.Services;

namespace TarkovDBEditor.ViewModels;

public enum ObjectiveFilterMode
{
    All,
    PendingApproval,
    Approved,
    HasCoordinates,
    NoCoordinates
}

public class QuestObjectiveEditorViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _db = DatabaseService.Instance;
    private readonly ApiMarkerService _apiMarkerService = ApiMarkerService.Instance;

    private ObservableCollection<string> _availableMaps = new();
    private ObservableCollection<QuestObjectiveItem> _allObjectives = new();
    private ObservableCollection<QuestObjectiveItem> _filteredObjectives = new();
    private ObservableCollection<ApiMarker> _apiMarkersForMap = new();

    private string? _selectedMapKey;
    private QuestObjectiveItem? _selectedObjective;
    private bool _isEditingLocationPoints = true;
    private ObjectiveFilterMode _filterMode = ObjectiveFilterMode.All;
    private string _searchText = "";
    private bool _hideApproved = false;
    private bool _nearPlayerOnly = false;
    private double _nearPlayerRadius = 50.0;
    private EftPosition? _playerPosition;
    private MapConfig? _currentMapConfig;

    public ObservableCollection<string> AvailableMaps
    {
        get => _availableMaps;
        set { _availableMaps = value; OnPropertyChanged(); }
    }

    public ObservableCollection<QuestObjectiveItem> FilteredObjectives
    {
        get => _filteredObjectives;
        set { _filteredObjectives = value; OnPropertyChanged(); }
    }

    public ObservableCollection<ApiMarker> ApiMarkersForMap
    {
        get => _apiMarkersForMap;
        set { _apiMarkersForMap = value; OnPropertyChanged(); }
    }

    public string? SelectedMapKey
    {
        get => _selectedMapKey;
        set
        {
            _selectedMapKey = value;
            OnPropertyChanged();
            _ = LoadObjectivesForMapAsync();
            _ = LoadApiMarkersForMapAsync();
        }
    }

    public QuestObjectiveItem? SelectedObjective
    {
        get => _selectedObjective;
        set
        {
            _selectedObjective = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedObjective));
        }
    }

    public bool IsEditingLocationPoints
    {
        get => _isEditingLocationPoints;
        set
        {
            _isEditingLocationPoints = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEditingOptionalPoints));
        }
    }

    public bool IsEditingOptionalPoints => !_isEditingLocationPoints;

    public ObjectiveFilterMode FilterMode
    {
        get => _filterMode;
        set
        {
            _filterMode = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public bool HideApproved
    {
        get => _hideApproved;
        set
        {
            _hideApproved = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public bool NearPlayerOnly
    {
        get => _nearPlayerOnly;
        set
        {
            _nearPlayerOnly = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public double NearPlayerRadius
    {
        get => _nearPlayerRadius;
        set
        {
            _nearPlayerRadius = value;
            OnPropertyChanged();
            if (_nearPlayerOnly) ApplyFilter();
        }
    }

    public EftPosition? PlayerPosition
    {
        get => _playerPosition;
        set
        {
            _playerPosition = value;
            OnPropertyChanged();
            if (_nearPlayerOnly) ApplyFilter();
        }
    }

    public MapConfig? CurrentMapConfig
    {
        get => _currentMapConfig;
        set
        {
            _currentMapConfig = value;
            OnPropertyChanged();
        }
    }

    public bool HasSelectedObjective => _selectedObjective != null;

    public int ApprovedCount => _allObjectives.Count(o => o.IsApproved);
    public int TotalCount => _allObjectives.Count;
    public double ProgressPercent => TotalCount > 0 ? (double)ApprovedCount / TotalCount * 100 : 0;

    public async Task LoadAvailableMapsAsync()
    {
        if (!_db.IsConnected) return;

        var maps = new HashSet<string>();
        var connectionString = $"Data Source={_db.DatabasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        // Get maps from QuestObjectives.MapName
        var sql1 = "SELECT DISTINCT MapName FROM QuestObjectives WHERE MapName IS NOT NULL AND MapName != ''";
        await using var cmd1 = new SqliteCommand(sql1, connection);
        await using var reader1 = await cmd1.ExecuteReaderAsync();
        while (await reader1.ReadAsync())
        {
            maps.Add(reader1.GetString(0));
        }

        // Get maps from Quests.Location
        var sql2 = "SELECT DISTINCT Location FROM Quests WHERE Location IS NOT NULL AND Location != ''";
        await using var cmd2 = new SqliteCommand(sql2, connection);
        await using var reader2 = await cmd2.ExecuteReaderAsync();
        while (await reader2.ReadAsync())
        {
            maps.Add(reader2.GetString(0));
        }

        _availableMaps.Clear();
        foreach (var map in maps.OrderBy(m => m))
        {
            _availableMaps.Add(map);
        }
    }

    public async Task LoadObjectivesForMapAsync()
    {
        _allObjectives.Clear();
        _filteredObjectives.Clear();

        if (!_db.IsConnected || string.IsNullOrEmpty(_selectedMapKey)) return;

        var connectionString = $"Data Source={_db.DatabasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT o.Id, o.QuestId, o.SortOrder, o.ObjectiveType, o.Description,
                   o.TargetType, o.TargetCount, o.ItemId, o.ItemName, o.RequiresFIR,
                   o.MapName, o.LocationName, o.LocationPoints, o.OptionalPoints,
                   o.Conditions, o.IsApproved, o.ApprovedAt,
                   q.Name as QuestName, q.NameEN, q.Location as QuestLocation, q.WikiPageLink
            FROM QuestObjectives o
            JOIN Quests q ON o.QuestId = q.Id
            WHERE o.MapName = @MapKey
               OR ((o.MapName IS NULL OR o.MapName = '') 
                   AND (q.Location = @MapKey 
                        OR q.Location LIKE @MapKey + ', %' 
                        OR q.Location LIKE '%, ' + @MapKey 
                        OR q.Location LIKE '%, ' + @MapKey + ', %'))
            ORDER BY q.Name, o.SortOrder";

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@MapKey", _selectedMapKey);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var obj = new QuestObjectiveItem
            {
                Id = reader.GetString(0),
                QuestId = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                ObjectiveType = reader.IsDBNull(3) ? "Custom" : reader.GetString(3),
                Description = reader.IsDBNull(4) ? "" : reader.GetString(4),
                TargetType = reader.IsDBNull(5) ? null : reader.GetString(5),
                TargetCount = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                ItemId = reader.IsDBNull(7) ? null : reader.GetString(7),
                ItemName = reader.IsDBNull(8) ? null : reader.GetString(8),
                RequiresFIR = !reader.IsDBNull(9) && reader.GetInt64(9) != 0,
                MapName = reader.IsDBNull(10) ? null : reader.GetString(10),
                LocationName = reader.IsDBNull(11) ? null : reader.GetString(11),
                LocationPointsJson = reader.IsDBNull(12) ? null : reader.GetString(12),
                OptionalPointsJson = reader.IsDBNull(13) ? null : reader.GetString(13),
                Conditions = reader.IsDBNull(14) ? null : reader.GetString(14),
                IsApproved = !reader.IsDBNull(15) && reader.GetInt64(15) != 0,
                ApprovedAt = reader.IsDBNull(16) ? null : DateTime.TryParse(reader.GetString(16), out var dt) ? dt : null,
                QuestName = reader.IsDBNull(17) ? null : reader.GetString(17),
                QuestNameEN = reader.IsDBNull(18) ? null : reader.GetString(18),
                QuestLocation = reader.IsDBNull(19) ? null : reader.GetString(19),
                WikiPageLink = reader.IsDBNull(20) ? null : reader.GetString(20)
            };

            _allObjectives.Add(obj);
        }

        ApplyFilter();
        OnPropertyChanged(nameof(ApprovedCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ProgressPercent));
    }

    public async Task LoadApiMarkersForMapAsync()
    {
        _apiMarkersForMap.Clear();

        if (string.IsNullOrEmpty(_selectedMapKey)) return;

        var markers = await _apiMarkerService.GetByMapKeyAsync(_selectedMapKey);
        // Only show Quest-related API markers
        foreach (var marker in markers.Where(m => m.Category == "Quests"))
        {
            _apiMarkersForMap.Add(marker);
        }
    }

    public void ApplyFilter()
    {
        _filteredObjectives.Clear();

        var filtered = _allObjectives.AsEnumerable();

        // Filter by HideApproved checkbox
        if (_hideApproved)
        {
            filtered = filtered.Where(o => !o.IsApproved);
        }

        // Filter by Near Player
        if (_nearPlayerOnly && _playerPosition != null && _currentMapConfig != null)
        {
            // Convert player position to screen coordinates
            var (playerScreenX, playerScreenY) = _currentMapConfig.GameToScreenForPlayer(_playerPosition.X, _playerPosition.Z);

            // Radius in screen pixels (approximate: 1 game unit ≈ some screen pixels)
            // Use a scale factor based on map config
            var screenRadius = _nearPlayerRadius * 3.0; // Approximate scale factor
            var screenRadiusSq = screenRadius * screenRadius;

            // Get API markers near player (using screen coordinates)
            // API 마커는 PlayerMarkerTransform 사용 (marker.Z = gameX, marker.X = gameZ)
            var nearbyMarkers = _apiMarkersForMap
                .Where(m =>
                {
                    var (markerScreenX, markerScreenY) = _currentMapConfig.GameToScreenForPlayer(m.Z, m.X);
                    return DistanceSquared(playerScreenX, playerScreenY, markerScreenX, markerScreenY) <= screenRadiusSq;
                })
                .ToList();

            // Collect quest names from nearby markers (for matching)
            var nearbyQuestNames = nearbyMarkers
                .Where(m => !string.IsNullOrEmpty(m.QuestNameEn))
                .Select(m => m.QuestNameEn!.ToLowerInvariant())
                .ToHashSet();

            // Also collect quest BSG IDs
            var nearbyQuestBsgIds = nearbyMarkers
                .Where(m => !string.IsNullOrEmpty(m.QuestBsgId))
                .Select(m => m.QuestBsgId!)
                .ToHashSet();

            filtered = filtered.Where(o =>
            {
                // Check if objective's quest matches by BSG ID first
                // (QuestObjectiveItem doesn't have BsgId directly, so we use quest name matching)

                // Check if objective's quest matches any nearby marker's quest
                var questNameLower = o.QuestNameEN?.ToLowerInvariant() ?? "";
                var questNameMatch = !string.IsNullOrEmpty(questNameLower) && nearbyQuestNames.Contains(questNameLower);

                // Also try partial match for quest name
                var questNamePartialMatch = nearbyQuestNames.Any(n =>
                    !string.IsNullOrEmpty(n) && !string.IsNullOrEmpty(questNameLower) &&
                    (questNameLower.Contains(n) || n.Contains(questNameLower)));

                return questNameMatch || questNamePartialMatch;
            });
        }

        // Filter by mode
        filtered = _filterMode switch
        {
            ObjectiveFilterMode.PendingApproval => filtered.Where(o => !o.IsApproved),
            ObjectiveFilterMode.Approved => filtered.Where(o => o.IsApproved),
            ObjectiveFilterMode.HasCoordinates => filtered.Where(o => o.HasCoordinates || o.HasOptionalPoints),
            ObjectiveFilterMode.NoCoordinates => filtered.Where(o => !o.HasCoordinates && !o.HasOptionalPoints),
            _ => filtered
        };

        // Filter by search text
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var search = _searchText.ToLowerInvariant();
            filtered = filtered.Where(o =>
                (o.Description?.ToLowerInvariant().Contains(search) ?? false) ||
                (o.QuestName?.ToLowerInvariant().Contains(search) ?? false) ||
                (o.ItemName?.ToLowerInvariant().Contains(search) ?? false) ||
                (o.ObjectiveType?.ToLowerInvariant().Contains(search) ?? false));
        }

        foreach (var obj in filtered)
        {
            _filteredObjectives.Add(obj);
        }
    }

    private static double DistanceSquared(double x1, double z1, double x2, double z2)
    {
        var dx = x2 - x1;
        var dz = z2 - z1;
        return dx * dx + dz * dz;
    }

    /// <summary>
    /// Get set of quest names (lowercase) that have all their objectives on the current map approved.
    /// Only checks objectives for the currently selected map, not all objectives across all maps.
    /// </summary>
    public HashSet<string> GetFullyApprovedQuestNamesForCurrentMap()
    {
        var approvedQuests = new HashSet<string>();

        // _allObjectives already contains only objectives for the current map
        // Group objectives by quest name
        var questGroups = _allObjectives
            .Where(o => !string.IsNullOrEmpty(o.QuestNameEN))
            .GroupBy(o => o.QuestNameEN!.ToLowerInvariant());

        foreach (var group in questGroups)
        {
            if (group.All(o => o.IsApproved))
            {
                approvedQuests.Add(group.Key);
            }
        }

        return approvedQuests;
    }

    public async Task UpdateObjectiveApprovalAsync(string objectiveId, bool isApproved)
    {
        if (!_db.IsConnected || string.IsNullOrEmpty(objectiveId)) return;

        var connectionString = $"Data Source={_db.DatabasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = isApproved
            ? "UPDATE QuestObjectives SET IsApproved = 1, ApprovedAt = @ApprovedAt WHERE Id = @Id"
            : "UPDATE QuestObjectives SET IsApproved = 0, ApprovedAt = NULL WHERE Id = @Id";

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", objectiveId);
        if (isApproved)
        {
            cmd.Parameters.AddWithValue("@ApprovedAt", DateTime.UtcNow.ToString("o"));
        }
        await cmd.ExecuteNonQueryAsync();

        // Update local model
        var obj = _allObjectives.FirstOrDefault(o => o.Id == objectiveId);
        if (obj != null)
        {
            obj.IsApproved = isApproved;
            obj.ApprovedAt = isApproved ? DateTime.UtcNow : null;
        }

        OnPropertyChanged(nameof(ApprovedCount));
        OnPropertyChanged(nameof(ProgressPercent));
    }

    public async Task SaveLocationPointsAsync(QuestObjectiveItem objective)
    {
        if (!_db.IsConnected || objective == null) return;

        var connectionString = $"Data Source={_db.DatabasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "UPDATE QuestObjectives SET LocationPoints = @LocationPoints, UpdatedAt = @UpdatedAt WHERE Id = @Id";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", objective.Id);
        cmd.Parameters.AddWithValue("@LocationPoints", (object?)objective.LocationPointsJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveOptionalPointsAsync(QuestObjectiveItem objective)
    {
        if (!_db.IsConnected || objective == null) return;

        var connectionString = $"Data Source={_db.DatabasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "UPDATE QuestObjectives SET OptionalPoints = @OptionalPoints, UpdatedAt = @UpdatedAt WHERE Id = @Id";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", objective.Id);
        cmd.Parameters.AddWithValue("@OptionalPoints", (object?)objective.OptionalPointsJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public void AddLocationPoint(double gameX, double gameZ, string? floorId)
    {
        if (_selectedObjective == null) return;

        _selectedObjective.LocationPoints.Add(new LocationPoint
        {
            X = gameX,
            Y = 0,
            Z = gameZ,
            FloorId = floorId
        });
    }

    public void AddOptionalPoint(double gameX, double gameZ, string? floorId)
    {
        if (_selectedObjective == null) return;

        _selectedObjective.OptionalPoints.Add(new LocationPoint
        {
            X = gameX,
            Y = 0,
            Z = gameZ,
            FloorId = floorId
        });
    }

    public void RemoveLastLocationPoint()
    {
        if (_selectedObjective == null || _selectedObjective.LocationPoints.Count == 0) return;
        _selectedObjective.LocationPoints.RemoveAt(_selectedObjective.LocationPoints.Count - 1);
    }

    public void RemoveLastOptionalPoint()
    {
        if (_selectedObjective == null || _selectedObjective.OptionalPoints.Count == 0) return;
        _selectedObjective.OptionalPoints.RemoveAt(_selectedObjective.OptionalPoints.Count - 1);
    }

    public void ClearLocationPoints()
    {
        _selectedObjective?.LocationPoints.Clear();
    }

    public void ClearOptionalPoints()
    {
        _selectedObjective?.OptionalPoints.Clear();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
