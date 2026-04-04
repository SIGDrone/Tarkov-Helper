using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TarkovHelper.Debug;
using TarkovHelper.Models;
using TarkovHelper.Models.Map;

namespace TarkovHelper.Services;

/// <summary>
/// 사용자 데이터를 SQLite DB (user_data.db)에 저장/로드하는 서비스.
/// 퀘스트 진행, 목표 완료, 하이드아웃 진행, 아이템 인벤토리 등을 관리합니다.
/// </summary>
public sealed class UserDataDbService
{
    private static UserDataDbService? _instance;
    public static UserDataDbService Instance => _instance ??= new UserDataDbService();

    private readonly string _databasePath;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;
    public string DatabasePath => _databasePath;

    /// <summary>
    /// 마이그레이션 진행 상황 이벤트
    /// </summary>
    public event Action<string>? MigrationProgress;

    /// <summary>
    /// 마이그레이션이 필요한지 확인
    /// </summary>
    public bool NeedsMigration()
    {
        var v2Path = Path.Combine(AppEnv.ConfigPath, "quest_progress_v2.json");
        var v1Path = Path.Combine(AppEnv.ConfigPath, "quest_progress.json");
        var objPath = Path.Combine(AppEnv.ConfigPath, "objective_progress.json");
        var hideoutPath = Path.Combine(AppEnv.ConfigPath, "hideout_progress.json");
        var inventoryPath = Path.Combine(AppEnv.ConfigPath, "item_inventory.json");

        return File.Exists(v2Path) || File.Exists(v1Path) || File.Exists(objPath) ||
               File.Exists(hideoutPath) || File.Exists(inventoryPath);
    }

    private void ReportProgress(string message)
    {
        MigrationProgress?.Invoke(message);
        System.Diagnostics.Debug.WriteLine($"[UserDataDbService] {message}");
    }

    private UserDataDbService()
    {
        _databasePath = Path.Combine(AppEnv.ConfigPath, "user_data.db");
    }

    /// <summary>
    /// DB 초기화 (테이블 생성)
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            // 백업 먼저 수행
            await BackupDatabaseAsync();

            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

            var connectionString = $"Data Source={_databasePath}";
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            await CreateTablesAsync(connection);

            _isInitialized = true;
            System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Initialized: {_databasePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Initialization failed: {ex.Message}");
            throw;
        }
    }

    private async Task CreateTablesAsync(SqliteConnection connection)
    {
        // First, check for schema migration to ProfileType system
        await MigrateToProfileSystemAsync(connection);

        var createTablesSql = @"
            -- 퀘스트 진행 상태
            CREATE TABLE IF NOT EXISTS QuestProgress (
                Id TEXT,
                ProfileType INTEGER NOT NULL DEFAULT 0,
                NormalizedName TEXT,
                Status TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                PRIMARY KEY (Id, ProfileType)
            );

            -- 퀘스트 목표 진행 상태
            CREATE TABLE IF NOT EXISTS ObjectiveProgress (
                Id TEXT,
                ProfileType INTEGER NOT NULL DEFAULT 0,
                QuestId TEXT,
                IsCompleted INTEGER NOT NULL DEFAULT 0,
                UpdatedAt TEXT NOT NULL,
                PRIMARY KEY (Id, ProfileType)
            );

            -- 아이템 인벤토리
            CREATE TABLE IF NOT EXISTS ItemInventory (
                ItemNormalizedName TEXT,
                ProfileType INTEGER NOT NULL DEFAULT 0,
                FirQuantity INTEGER NOT NULL DEFAULT 0,
                NonFirQuantity INTEGER NOT NULL DEFAULT 0,
                UpdatedAt TEXT NOT NULL,
                PRIMARY KEY (ItemNormalizedName, ProfileType)
            );

            -- 하이드아웃 진행
            CREATE TABLE IF NOT EXISTS HideoutProgress (
                StationId TEXT,
                ProfileType INTEGER NOT NULL DEFAULT 0,
                Level INTEGER NOT NULL DEFAULT 0,
                UpdatedAt TEXT NOT NULL,
                PRIMARY KEY (StationId, ProfileType)
            );

            -- 사용자 설정 (일부 설정은 프로필별로 관리)
            CREATE TABLE IF NOT EXISTS UserSettings (
                Key TEXT,
                ProfileType INTEGER NOT NULL DEFAULT 0,
                Value TEXT NOT NULL,
                PRIMARY KEY (Key, ProfileType)
            );

            -- 레이드 히스토리
            CREATE TABLE IF NOT EXISTS RaidHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RaidId TEXT,
                SessionId TEXT,
                ShortId TEXT,
                ProfileId TEXT,
                RaidType INTEGER NOT NULL DEFAULT 0,
                GameMode INTEGER NOT NULL DEFAULT 0,
                MapName TEXT,
                MapKey TEXT,
                ServerIp TEXT,
                ServerPort INTEGER,
                IsParty INTEGER NOT NULL DEFAULT 0,
                PartyLeaderAccountId TEXT,
                StartTime TEXT,
                EndTime TEXT,
                DurationSeconds INTEGER,
                Rtt REAL,
                PacketLoss REAL,
                PacketsSent INTEGER,
                PacketsReceived INTEGER,
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            -- 커스텀 맵 마커
            CREATE TABLE IF NOT EXISTS CustomMapMarkers (
                Id TEXT,
                ProfileType INTEGER NOT NULL DEFAULT 0,
                MapKey TEXT NOT NULL,
                Name TEXT,
                X REAL NOT NULL,
                Y REAL NOT NULL,
                Z REAL NOT NULL,
                FloorId TEXT,
                Color TEXT,
                Size REAL NOT NULL DEFAULT 24.0,
                Opacity REAL NOT NULL DEFAULT 1.0,
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (Id, ProfileType)
            );

            -- 인덱스
            CREATE INDEX IF NOT EXISTS idx_quest_progress_normalized ON QuestProgress(NormalizedName);
            CREATE INDEX IF NOT EXISTS idx_objective_progress_quest ON ObjectiveProgress(QuestId);
            CREATE INDEX IF NOT EXISTS idx_raid_history_start_time ON RaidHistory(StartTime);
            CREATE INDEX IF NOT EXISTS idx_raid_history_map_key ON RaidHistory(MapKey);
            CREATE INDEX IF NOT EXISTS idx_raid_history_raid_type ON RaidHistory(RaidType);
            CREATE INDEX IF NOT EXISTS idx_custom_markers_map_key ON CustomMapMarkers(MapKey);
        ";

        await using var cmd = new SqliteCommand(createTablesSql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 기존 단일 프로필 시스템에서 PVP/PVE 통합 프로필 시스템으로 마이그레이션합니다.
    /// SQLite는 ALTER TABLE로 PK를 변경할 수 없으므로 테이블 재생성 방식을 사용합니다.
    /// </summary>
    private async Task MigrateToProfileSystemAsync(SqliteConnection connection)
    {
        var tablesToMigrate = new Dictionary<string, string>
        {
            { "QuestProgress", "Id, 0 as ProfileType, NormalizedName, Status, UpdatedAt" },
            { "ObjectiveProgress", "Id, 0 as ProfileType, QuestId, IsCompleted, UpdatedAt" },
            { "ItemInventory", "ItemNormalizedName, 0 as ProfileType, FirQuantity, NonFirQuantity, UpdatedAt" },
            { "HideoutProgress", "StationId, 0 as ProfileType, Level, UpdatedAt" },
            { "UserSettings", "Key, 0 as ProfileType, Value" },
            { "CustomMapMarkers", "Id, 0 as ProfileType, MapKey, Name, X, Y, Z, FloorId, Color, Size, Opacity, CreatedAt" }
        };

        foreach (var entry in tablesToMigrate)
        {
            var tableName = entry.Key;
            var columns = entry.Value;

            try
            {
                // 테이블 존재 여부 확인
                var checkTableSql = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tableName}'";
                await using var checkTableCmd = new SqliteCommand(checkTableSql, connection);
                if (Convert.ToInt32(await checkTableCmd.ExecuteScalarAsync()) == 0) continue;

                // 이미 고유 PK(ProfileType 포함)가 설정되어 있는지 확인
                var checkPkSql = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE pk > 0 AND name = 'ProfileType'";
                await using var checkPkCmd = new SqliteCommand(checkPkSql, connection);
                var hasProfileTypeInPk = Convert.ToInt32(await checkPkCmd.ExecuteScalarAsync()) > 0;

                if (!hasProfileTypeInPk)
                {
                    System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Reconstructing {tableName} for PVE/PVP PK change...");

                    // 1. 기존 테이블 이름 변경
                    var renameSql = $"ALTER TABLE {tableName} RENAME TO {tableName}_old";
                    await using (var cmd = new SqliteCommand(renameSql, connection)) await cmd.ExecuteNonQueryAsync();

                    // 2. 새 테이블 생성 (CreateTablesAsync가 나중에 호출되므로 여기서는 직접 명령 실행 대신 
                    // CreateTablesAsync에서 사용할 SQL과 동일한 구조의 테이블을 미리 생성)
                    await RecreateTableWithNewPkAsync(tableName, connection);

                    // 3. 데이터 복사 (기존 데이터는 PVP인 0으로 매핑)
                    // 필드 목록에 ProfileType 컬럼이 포함되어 있는지 확인 후 데이터 부어넣기
                    var insertSql = $@"
                        INSERT INTO {tableName} 
                        SELECT * FROM (
                            SELECT {columns} FROM {tableName}_old
                        )";
                    
                    try {
                        await using (var cmd = new SqliteCommand(insertSql, connection)) await cmd.ExecuteNonQueryAsync();
                        // 4. 기존 테이블 삭제
                        var dropSql = $"DROP TABLE {tableName}_old";
                        await using (var cmd = new SqliteCommand(dropSql, connection)) await cmd.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"[UserDataDbService] {tableName} reconstruction success");
                    } catch (Exception ex) {
                        System.Diagnostics.Debug.WriteLine($"[UserDataDbService] {tableName} data copy failed: {ex.Message}. Rolling back rename...");
                        var rollbackSql = $"ALTER TABLE {tableName}_old RENAME TO {tableName}";
                        await using (var cmd = new SqliteCommand(rollbackSql, connection)) await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Migration failed for {tableName}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 테이블별 새 스키마로 재생성
    /// </summary>
    private async Task RecreateTableWithNewPkAsync(string tableName, SqliteConnection connection)
    {
        string sql = tableName switch
        {
            "QuestProgress" => @"
                CREATE TABLE QuestProgress (
                    Id TEXT, ProfileType INTEGER NOT NULL DEFAULT 0, NormalizedName TEXT, Status TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
                    PRIMARY KEY (Id, ProfileType))",
            "ObjectiveProgress" => @"
                CREATE TABLE ObjectiveProgress (
                    Id TEXT, ProfileType INTEGER NOT NULL DEFAULT 0, QuestId TEXT, IsCompleted INTEGER NOT NULL DEFAULT 0, UpdatedAt TEXT NOT NULL,
                    PRIMARY KEY (Id, ProfileType))",
            "ItemInventory" => @"
                CREATE TABLE ItemInventory (
                    ItemNormalizedName TEXT, ProfileType INTEGER NOT NULL DEFAULT 0, FirQuantity INTEGER NOT NULL DEFAULT 0, NonFirQuantity INTEGER NOT NULL DEFAULT 0, UpdatedAt TEXT NOT NULL,
                    PRIMARY KEY (ItemNormalizedName, ProfileType))",
            "HideoutProgress" => @"
                CREATE TABLE HideoutProgress (
                    StationId TEXT, ProfileType INTEGER NOT NULL DEFAULT 0, Level INTEGER NOT NULL DEFAULT 0, UpdatedAt TEXT NOT NULL,
                    PRIMARY KEY (StationId, ProfileType))",
            "UserSettings" => @"
                CREATE TABLE UserSettings (
                    Key TEXT, ProfileType INTEGER NOT NULL DEFAULT 0, Value TEXT NOT NULL,
                    PRIMARY KEY (Key, ProfileType))",
            "CustomMapMarkers" => @"
                CREATE TABLE CustomMapMarkers (
                    Id TEXT, ProfileType INTEGER NOT NULL DEFAULT 0, MapKey TEXT NOT NULL, Name TEXT, X REAL NOT NULL, Y REAL NOT NULL, Z REAL NOT NULL, FloorId TEXT, Color TEXT, Size REAL NOT NULL DEFAULT 24.0, Opacity REAL NOT NULL DEFAULT 1.0, CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (Id, ProfileType))",
            _ => throw new ArgumentException($"Unknown table: {tableName}")
        };

        if (!string.IsNullOrEmpty(sql))
        {
            await using var cmd = new SqliteCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task BackupDatabaseAsync()
    {
        try
        {
            if (File.Exists(_databasePath))
            {
                var backupPath = _databasePath + ".bak";
                File.Copy(_databasePath, backupPath, true);
                System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Database backup created: {backupPath}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Backup failed: {ex.Message}");
        }
    }



    #region Quest Progress

    /// <summary>
    /// 모든 퀘스트 진행 상태 로드
    /// </summary>
    public async Task<Dictionary<string, QuestStatus>> LoadQuestProgressAsync()
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var result = new Dictionary<string, QuestStatus>(StringComparer.OrdinalIgnoreCase);

        var connectionString = $"Data Source={_databasePath};Mode=ReadOnly";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "SELECT Id, NormalizedName, Status FROM QuestProgress WHERE ProfileType = @profileType";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            var normalizedName = reader.IsDBNull(1) ? null : reader.GetString(1);
            var statusStr = reader.GetString(2);

            if (Enum.TryParse<QuestStatus>(statusStr, out var status))
            {
                // NormalizedName을 키로 사용 (기존 호환성)
                var key = normalizedName ?? id;
                result[key] = status;
            }
        }

        return result;
    }

    /// <summary>
    /// 퀘스트 진행 상태 저장
    /// </summary>
    public async Task SaveQuestProgressAsync(string id, string? normalizedName, QuestStatus status)
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO QuestProgress (Id, ProfileType, NormalizedName, Status, UpdatedAt)
            VALUES (@id, @profileType, @normalizedName, @status, @updatedAt)
            ON CONFLICT(Id, ProfileType) DO UPDATE SET
                NormalizedName = @normalizedName,
                Status = @status,
                UpdatedAt = @updatedAt";

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        cmd.Parameters.AddWithValue("@normalizedName", normalizedName ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@status", status.ToString());
        cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 여러 퀘스트 진행 상태를 배치로 저장 (트랜잭션 사용)
    /// </summary>
    public async Task SaveQuestProgressBatchAsync(IEnumerable<(string Id, string? NormalizedName, QuestStatus Status)> progressItems)
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var sql = @"
                INSERT INTO QuestProgress (Id, ProfileType, NormalizedName, Status, UpdatedAt)
                VALUES (@id, @profileType, @normalizedName, @status, @updatedAt)
                ON CONFLICT(Id, ProfileType) DO UPDATE SET
                    NormalizedName = @normalizedName,
                    Status = @status,
                    UpdatedAt = @updatedAt";

            var updatedAt = DateTime.UtcNow.ToString("o");

            foreach (var item in progressItems)
            {
                await using var cmd = new SqliteCommand(sql, connection, (SqliteTransaction)transaction);
                cmd.Parameters.AddWithValue("@id", item.Id);
                cmd.Parameters.AddWithValue("@profileType", (int)profileType);
                cmd.Parameters.AddWithValue("@normalizedName", item.NormalizedName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@status", item.Status.ToString());
                cmd.Parameters.AddWithValue("@updatedAt", updatedAt);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 퀘스트 진행 상태 삭제 (리셋)
    /// </summary>
    public async Task DeleteQuestProgressAsync(string id)
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "DELETE FROM QuestProgress WHERE (Id = @id OR NormalizedName = @id) AND ProfileType = @profileType";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 모든 퀘스트 진행 상태 삭제
    /// </summary>
    public async Task ClearAllQuestProgressAsync()
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = new SqliteCommand("DELETE FROM QuestProgress WHERE ProfileType = @profileType", connection);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        await cmd.ExecuteNonQueryAsync();
    }

    #endregion

    #region Objective Progress

    /// <summary>
    /// 모든 목표 진행 상태 로드
    /// </summary>
    public async Task<Dictionary<string, bool>> LoadObjectiveProgressAsync()
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        var connectionString = $"Data Source={_databasePath};Mode=ReadOnly";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "SELECT Id, IsCompleted FROM ObjectiveProgress WHERE ProfileType = @profileType";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            var isCompleted = reader.GetInt32(1) == 1;
            result[id] = isCompleted;
        }

        return result;
    }

    /// <summary>
    /// 목표 진행 상태 저장
    /// </summary>
    public async Task SaveObjectiveProgressAsync(string id, string? questId, bool isCompleted)
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO ObjectiveProgress (Id, ProfileType, QuestId, IsCompleted, UpdatedAt)
            VALUES (@id, @profileType, @questId, @isCompleted, @updatedAt)
            ON CONFLICT(Id, ProfileType) DO UPDATE SET
                QuestId = @questId,
                IsCompleted = @isCompleted,
                UpdatedAt = @updatedAt";

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        cmd.Parameters.AddWithValue("@questId", questId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@isCompleted", isCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 목표 진행 상태 삭제
    /// </summary>
    public async Task DeleteObjectiveProgressAsync(string id)
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "DELETE FROM ObjectiveProgress WHERE Id = @id AND ProfileType = @profileType";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 퀘스트의 모든 목표 진행 상태 삭제
    /// </summary>
    public async Task DeleteObjectiveProgressByQuestAsync(string questId)
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "DELETE FROM ObjectiveProgress WHERE (QuestId = @questId OR Id LIKE @pattern) AND ProfileType = @profileType";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@questId", questId);
        cmd.Parameters.AddWithValue("@pattern", $"{questId}:%");
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 모든 목표 진행 상태 삭제
    /// </summary>
    public async Task ClearAllObjectiveProgressAsync()
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = new SqliteCommand("DELETE FROM ObjectiveProgress WHERE ProfileType = @profileType", connection);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        await cmd.ExecuteNonQueryAsync();
    }

    #endregion

    #region Hideout Progress

    /// <summary>
    /// 모든 하이드아웃 진행 상태 로드
    /// </summary>
    public async Task<Dictionary<string, int>> LoadHideoutProgressAsync()
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var connectionString = $"Data Source={_databasePath};Mode=ReadOnly";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "SELECT StationId, Level FROM HideoutProgress WHERE ProfileType = @profileType";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var stationId = reader.GetString(0);
            var level = reader.GetInt32(1);
            result[stationId] = level;
        }

        return result;
    }

    /// <summary>
    /// 하이드아웃 진행 상태 저장
    /// </summary>
    public async Task SaveHideoutProgressAsync(string stationId, int level)
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        // 레벨이 0이면 삭제
        if (level == 0)
        {
            var deleteSql = "DELETE FROM HideoutProgress WHERE StationId = @stationId AND ProfileType = @profileType";
            await using var deleteCmd = new SqliteCommand(deleteSql, connection);
            deleteCmd.Parameters.AddWithValue("@stationId", stationId);
            deleteCmd.Parameters.AddWithValue("@profileType", (int)profileType);
            await deleteCmd.ExecuteNonQueryAsync();
            return;
        }

        var sql = @"
            INSERT INTO HideoutProgress (StationId, ProfileType, Level, UpdatedAt)
            VALUES (@stationId, @profileType, @level, @updatedAt)
            ON CONFLICT(StationId, ProfileType) DO UPDATE SET
                Level = @level,
                UpdatedAt = @updatedAt";

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@stationId", stationId);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        cmd.Parameters.AddWithValue("@level", level);
        cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 모든 하이드아웃 진행 상태 삭제
    /// </summary>
    public async Task ClearAllHideoutProgressAsync()
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = new SqliteCommand("DELETE FROM HideoutProgress WHERE ProfileType = @profileType", connection);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        await cmd.ExecuteNonQueryAsync();
    }

    #endregion

    #region Item Inventory

    /// <summary>
    /// 모든 아이템 인벤토리 로드
    /// </summary>
    public async Task<Dictionary<string, (int FirQuantity, int NonFirQuantity)>> LoadItemInventoryAsync()
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var result = new Dictionary<string, (int FirQuantity, int NonFirQuantity)>(StringComparer.OrdinalIgnoreCase);

        var connectionString = $"Data Source={_databasePath};Mode=ReadOnly";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "SELECT ItemNormalizedName, FirQuantity, NonFirQuantity FROM ItemInventory WHERE ProfileType = @profileType";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var itemName = reader.GetString(0);
            var firQty = reader.GetInt32(1);
            var nonFirQty = reader.GetInt32(2);
            result[itemName] = (firQty, nonFirQty);
        }

        return result;
    }

    /// <summary>
    /// 아이템 인벤토리 저장
    /// </summary>
    public async Task SaveItemInventoryAsync(string itemNormalizedName, int firQuantity, int nonFirQuantity)
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        // 둘 다 0이면 삭제
        if (firQuantity == 0 && nonFirQuantity == 0)
        {
            var deleteSql = "DELETE FROM ItemInventory WHERE ItemNormalizedName = @itemName AND ProfileType = @profileType";
            await using var deleteCmd = new SqliteCommand(deleteSql, connection);
            deleteCmd.Parameters.AddWithValue("@itemName", itemNormalizedName);
            deleteCmd.Parameters.AddWithValue("@profileType", (int)profileType);
            await deleteCmd.ExecuteNonQueryAsync();
            return;
        }

        var sql = @"
            INSERT INTO ItemInventory (ItemNormalizedName, ProfileType, FirQuantity, NonFirQuantity, UpdatedAt)
            VALUES (@itemName, @profileType, @firQty, @nonFirQty, @updatedAt)
            ON CONFLICT(ItemNormalizedName, ProfileType) DO UPDATE SET
                FirQuantity = @firQty,
                NonFirQuantity = @nonFirQty,
                UpdatedAt = @updatedAt";

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@itemName", itemNormalizedName);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        cmd.Parameters.AddWithValue("@firQty", firQuantity);
        cmd.Parameters.AddWithValue("@nonFirQty", nonFirQuantity);
        cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 모든 아이템 인벤토리 삭제
    /// </summary>
    public async Task ClearAllItemInventoryAsync()
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = new SqliteCommand("DELETE FROM ItemInventory WHERE ProfileType = @profileType", connection);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        await cmd.ExecuteNonQueryAsync();
    }

    #endregion

    #region JSON Migration

    /// <summary>
    /// 기존 JSON 파일들을 DB로 마이그레이션
    /// </summary>
    public async Task<bool> MigrateFromJsonAsync()
    {
        if (!NeedsMigration())
        {
            return false;
        }

        ReportProgress("데이터 마이그레이션을 시작합니다...");
        var migrated = false;

        // Quest Progress 마이그레이션
        ReportProgress("퀘스트 진행 데이터 마이그레이션 중...");
        migrated |= await MigrateQuestProgressJsonAsync();

        // Objective Progress 마이그레이션
        ReportProgress("목표 진행 데이터 마이그레이션 중...");
        migrated |= await MigrateObjectiveProgressJsonAsync();

        // Hideout Progress 마이그레이션
        ReportProgress("하이드아웃 진행 데이터 마이그레이션 중...");
        migrated |= await MigrateHideoutProgressJsonAsync();

        // Item Inventory 마이그레이션
        ReportProgress("아이템 인벤토리 데이터 마이그레이션 중...");
        migrated |= await MigrateItemInventoryJsonAsync();

        if (migrated)
        {
            ReportProgress("데이터 마이그레이션 완료!");
        }

        return migrated;
    }

    private async Task<bool> MigrateQuestProgressJsonAsync()
    {
        // V2 파일 먼저 확인
        var v2Path = Path.Combine(AppEnv.ConfigPath, "quest_progress_v2.json");
        var v1Path = Path.Combine(AppEnv.ConfigPath, "quest_progress.json");

        if (File.Exists(v2Path))
        {
            try
            {
                var json = await File.ReadAllTextAsync(v2Path);
                var v2Data = JsonSerializer.Deserialize<QuestProgressDataV2>(json);

                if (v2Data != null)
                {
                    await InitializeAsync();

                    foreach (var entry in v2Data.CompletedQuests)
                    {
                        if (entry.IsValid)
                        {
                            await SaveQuestProgressAsync(
                                entry.Id ?? entry.NormalizedName!,
                                entry.NormalizedName,
                                QuestStatus.Done);
                        }
                    }

                    foreach (var entry in v2Data.FailedQuests)
                    {
                        if (entry.IsValid)
                        {
                            await SaveQuestProgressAsync(
                                entry.Id ?? entry.NormalizedName!,
                                entry.NormalizedName,
                                QuestStatus.Failed);
                        }
                    }

                    // 마이그레이션 완료 후 파일 삭제
                    File.Delete(v2Path);
                    System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Migrated and deleted: {v2Path}");

                    // V1 파일도 있으면 삭제
                    if (File.Exists(v1Path))
                    {
                        File.Delete(v1Path);
                        System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Deleted legacy: {v1Path}");
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserDataDbService] V2 migration failed: {ex.Message}");
            }
        }
        else if (File.Exists(v1Path))
        {
            try
            {
                var json = await File.ReadAllTextAsync(v1Path);
                var v1Data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (v1Data != null)
                {
                    await InitializeAsync();

                    foreach (var kvp in v1Data)
                    {
                        if (Enum.TryParse<QuestStatus>(kvp.Value, out var status))
                        {
                            await SaveQuestProgressAsync(kvp.Key, kvp.Key, status);
                        }
                    }

                    // 마이그레이션 완료 후 파일 삭제
                    File.Delete(v1Path);
                    System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Migrated and deleted: {v1Path}");

                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserDataDbService] V1 migration failed: {ex.Message}");
            }
        }

        return false;
    }

    private async Task<bool> MigrateObjectiveProgressJsonAsync()
    {
        var filePath = Path.Combine(AppEnv.ConfigPath, "objective_progress.json");

        if (!File.Exists(filePath))
            return false;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);

            if (data != null)
            {
                await InitializeAsync();

                foreach (var kvp in data)
                {
                    // 키 형식: "questName:index" 또는 "id:objectiveId"
                    string? questId = null;
                    if (kvp.Key.Contains(':'))
                    {
                        var parts = kvp.Key.Split(':');
                        if (parts[0] != "id")
                        {
                            questId = parts[0];
                        }
                    }

                    await SaveObjectiveProgressAsync(kvp.Key, questId, kvp.Value);
                }

                // 마이그레이션 완료 후 파일 삭제
                File.Delete(filePath);
                System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Migrated and deleted: {filePath}");

                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Objective migration failed: {ex.Message}");
        }

        return false;
    }

    private async Task<bool> MigrateHideoutProgressJsonAsync()
    {
        var filePath = Path.Combine(AppEnv.ConfigPath, "hideout_progress.json");

        if (!File.Exists(filePath))
            return false;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            Dictionary<string, int>? modules = null;

            // Try new format first: {"version": 1, "lastUpdated": "...", "modules": {...}}
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("modules", out var modulesElement))
                {
                    modules = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in modulesElement.EnumerateObject())
                    {
                        if (prop.Value.TryGetInt32(out var level))
                        {
                            modules[prop.Name] = level;
                        }
                    }
                }
            }
            catch
            {
                // Fall back to old format: {"stationId": level, ...}
                modules = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            }

            if (modules != null && modules.Count > 0)
            {
                await InitializeAsync();

                foreach (var kvp in modules)
                {
                    await SaveHideoutProgressAsync(kvp.Key, kvp.Value);
                }

                // 마이그레이션 완료 후 파일 삭제
                File.Delete(filePath);
                System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Migrated and deleted: {filePath}");

                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Hideout migration failed: {ex.Message}");
        }

        return false;
    }

    private async Task<bool> MigrateItemInventoryJsonAsync()
    {
        var filePath = Path.Combine(AppEnv.ConfigPath, "item_inventory.json");

        if (!File.Exists(filePath))
            return false;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var data = JsonSerializer.Deserialize<ItemInventoryData>(json, options);

            if (data != null && data.Items.Count > 0)
            {
                await InitializeAsync();

                foreach (var kvp in data.Items)
                {
                    var inventory = kvp.Value;
                    await SaveItemInventoryAsync(
                        kvp.Key,
                        inventory.FirQuantity,
                        inventory.NonFirQuantity);
                }

                // 마이그레이션 완료 후 파일 삭제
                File.Delete(filePath);
                System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Migrated and deleted: {filePath}");

                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserDataDbService] ItemInventory migration failed: {ex.Message}");
        }

        return false;
    }

    #endregion

    #region User Settings

    /// <summary>
    /// 설정 값 조회
    /// </summary>
    public async Task<string?> GetSettingAsync(string key)
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath};Mode=ReadOnly";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "SELECT Value FROM UserSettings WHERE Key = @key AND ProfileType = @profileType";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);

        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    /// <summary>
    /// 설정 값 저장
    /// </summary>
    public async Task SetSettingAsync(string key, string value)
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO UserSettings (Key, ProfileType, Value)
            VALUES (@key, @profileType, @value)
            ON CONFLICT(Key, ProfileType) DO UPDATE SET Value = @value";

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        cmd.Parameters.AddWithValue("@value", value);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 설정 값 삭제
    /// </summary>
    public async Task DeleteSettingAsync(string key)
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "DELETE FROM UserSettings WHERE Key = @key AND ProfileType = @profileType";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 모든 설정 조회
    /// </summary>
    public async Task<Dictionary<string, string>> GetAllSettingsAsync()
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var connectionString = $"Data Source={_databasePath};Mode=ReadOnly";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "SELECT Key, Value FROM UserSettings WHERE ProfileType = @profileType";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var key = reader.GetString(0);
            var value = reader.GetString(1);
            result[key] = value;
        }

        return result;
    }

    /// <summary>
    /// 동기 버전: 설정 값 조회 (초기화 시 사용)
    /// </summary>
    public string? GetSetting(string key)
    {
        if (!_isInitialized)
        {
            InitializeAsync().GetAwaiter().GetResult();
        }
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath};Mode=ReadOnly";
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var sql = "SELECT Value FROM UserSettings WHERE Key = @key AND ProfileType = @profileType";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);

        var result = cmd.ExecuteScalar();
        return result as string;
    }

    /// <summary>
    /// 동기 버전: 설정 값 저장 (초기화 시 사용)
    /// </summary>
    public void SetSetting(string key, string value)
    {
        if (!_isInitialized)
        {
            InitializeAsync().GetAwaiter().GetResult();
        }
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var sql = @"
            INSERT INTO UserSettings (Key, ProfileType, Value)
            VALUES (@key, @profileType, @value)
            ON CONFLICT(Key, ProfileType) DO UPDATE SET Value = @value";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        cmd.Parameters.AddWithValue("@value", value);

        cmd.ExecuteNonQuery();
    }

    #endregion

    #region Raid History

    /// <summary>
    /// 레이드 히스토리 저장
    /// </summary>
    public async Task SaveRaidHistoryAsync(Models.EftRaidInfo raid)
    {
        await InitializeAsync();

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO RaidHistory (
                RaidId, SessionId, ShortId, ProfileId, RaidType, GameMode,
                MapName, MapKey, ServerIp, ServerPort, IsParty, PartyLeaderAccountId,
                StartTime, EndTime, DurationSeconds, Rtt, PacketLoss, PacketsSent, PacketsReceived
            ) VALUES (
                @raidId, @sessionId, @shortId, @profileId, @raidType, @gameMode,
                @mapName, @mapKey, @serverIp, @serverPort, @isParty, @partyLeaderId,
                @startTime, @endTime, @durationSeconds, @rtt, @packetLoss, @packetsSent, @packetsReceived
            )";

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@raidId", raid.RaidId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@sessionId", raid.SessionId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@shortId", raid.ShortId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@profileId", raid.ProfileId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@raidType", (int)raid.RaidType);
        cmd.Parameters.AddWithValue("@gameMode", (int)raid.GameMode);
        cmd.Parameters.AddWithValue("@mapName", raid.MapName ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@mapKey", raid.MapKey ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@serverIp", raid.ServerIp ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@serverPort", raid.ServerPort);
        cmd.Parameters.AddWithValue("@isParty", raid.IsParty ? 1 : 0);
        cmd.Parameters.AddWithValue("@partyLeaderId", raid.PartyLeaderAccountId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@startTime", raid.StartTime?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@endTime", raid.EndTime?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@durationSeconds", raid.Duration?.TotalSeconds ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@rtt", raid.Rtt ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@packetLoss", raid.PacketLoss ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@packetsSent", raid.PacketsSent ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@packetsReceived", raid.PacketsReceived ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 레이드 히스토리 조회 (최근 N개)
    /// </summary>
    public async Task<List<Models.EftRaidInfo>> GetRaidHistoryAsync(int limit = 100, Models.RaidType? raidType = null, string? mapKey = null)
    {
        await InitializeAsync();

        var result = new List<Models.EftRaidInfo>();

        var connectionString = $"Data Source={_databasePath};Mode=ReadOnly";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var whereConditions = new List<string>();
        if (raidType.HasValue)
            whereConditions.Add("RaidType = @raidType");
        if (!string.IsNullOrEmpty(mapKey))
            whereConditions.Add("MapKey = @mapKey");

        var whereClause = whereConditions.Count > 0 ? $"WHERE {string.Join(" AND ", whereConditions)}" : "";

        var sql = $@"
            SELECT RaidId, SessionId, ShortId, ProfileId, RaidType, GameMode,
                   MapName, MapKey, ServerIp, ServerPort, IsParty, PartyLeaderAccountId,
                   StartTime, EndTime, Rtt, PacketLoss, PacketsSent, PacketsReceived
            FROM RaidHistory
            {whereClause}
            ORDER BY StartTime DESC
            LIMIT @limit";

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@limit", limit);
        if (raidType.HasValue)
            cmd.Parameters.AddWithValue("@raidType", (int)raidType.Value);
        if (!string.IsNullOrEmpty(mapKey))
            cmd.Parameters.AddWithValue("@mapKey", mapKey);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var raid = new Models.EftRaidInfo
            {
                RaidId = reader.IsDBNull(0) ? null : reader.GetString(0),
                SessionId = reader.IsDBNull(1) ? null : reader.GetString(1),
                ShortId = reader.IsDBNull(2) ? null : reader.GetString(2),
                ProfileId = reader.IsDBNull(3) ? null : reader.GetString(3),
                RaidType = (Models.RaidType)reader.GetInt32(4),
                GameMode = (Models.GameMode)reader.GetInt32(5),
                MapName = reader.IsDBNull(6) ? null : reader.GetString(6),
                MapKey = reader.IsDBNull(7) ? null : reader.GetString(7),
                ServerIp = reader.IsDBNull(8) ? null : reader.GetString(8),
                ServerPort = reader.GetInt32(9),
                IsParty = reader.GetInt32(10) == 1,
                PartyLeaderAccountId = reader.IsDBNull(11) ? null : reader.GetString(11),
                StartTime = reader.IsDBNull(12) ? null : DateTime.Parse(reader.GetString(12)),
                EndTime = reader.IsDBNull(13) ? null : DateTime.Parse(reader.GetString(13)),
                Rtt = reader.IsDBNull(14) ? null : reader.GetDouble(14),
                PacketLoss = reader.IsDBNull(15) ? null : reader.GetDouble(15),
                PacketsSent = reader.IsDBNull(16) ? null : reader.GetInt64(16),
                PacketsReceived = reader.IsDBNull(17) ? null : reader.GetInt64(17)
            };
            result.Add(raid);
        }

        return result;
    }

    /// <summary>
    /// 레이드 통계 조회
    /// </summary>
    public async Task<(int TotalRaids, int PmcRaids, int ScavRaids, int PartyRaids)> GetRaidStatisticsAsync(DateTime? since = null)
    {
        await InitializeAsync();

        var connectionString = $"Data Source={_databasePath};Mode=ReadOnly";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var whereClause = since.HasValue ? "WHERE StartTime >= @since" : "";

        var sql = $@"
            SELECT
                COUNT(*) as TotalRaids,
                SUM(CASE WHEN RaidType = 1 THEN 1 ELSE 0 END) as PmcRaids,
                SUM(CASE WHEN RaidType = 2 THEN 1 ELSE 0 END) as ScavRaids,
                SUM(CASE WHEN IsParty = 1 THEN 1 ELSE 0 END) as PartyRaids
            FROM RaidHistory
            {whereClause}";

        await using var cmd = new SqliteCommand(sql, connection);
        if (since.HasValue)
            cmd.Parameters.AddWithValue("@since", since.Value.ToString("o"));

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return (
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3)
            );
        }

        return (0, 0, 0, 0);
    }

    /// <summary>
    /// 오래된 레이드 히스토리 삭제
    /// </summary>
    public async Task CleanupRaidHistoryAsync(int keepDays = 30)
    {
        await InitializeAsync();

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var cutoffDate = DateTime.Now.AddDays(-keepDays).ToString("o");

        var sql = "DELETE FROM RaidHistory WHERE StartTime < @cutoff";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@cutoff", cutoffDate);

        var deleted = await cmd.ExecuteNonQueryAsync();
        System.Diagnostics.Debug.WriteLine($"[UserDataDbService] Cleaned up {deleted} old raid history entries");
    }

    #endregion

    #region Custom Map Markers

    /// <summary>
    /// 특정 맵의 모든 커스텀 마커 로드
    /// </summary>
    public async Task<List<CustomMapMarker>> LoadCustomMarkersAsync(string mapKey)
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var result = new List<CustomMapMarker>();

        var connectionString = $"Data Source={_databasePath};Mode=ReadOnly";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "SELECT Id, MapKey, Name, X, Y, Z, FloorId, Color, Size, Opacity, CreatedAt FROM CustomMapMarkers WHERE MapKey = @mapKey AND ProfileType = @profileType";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@mapKey", mapKey);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var marker = new CustomMapMarker
            {
                Id = reader.GetString(0),
                MapKey = reader.GetString(1),
                Name = reader.IsDBNull(2) ? "" : reader.GetString(2),
                X = reader.GetDouble(3),
                Y = reader.GetDouble(4),
                Z = reader.GetDouble(5),
                FloorId = reader.IsDBNull(6) ? null : reader.GetString(6),
                Color = reader.IsDBNull(7) ? null : reader.GetString(7),
                Size = reader.GetDouble(8),
                Opacity = reader.IsDBNull(9) ? 1.0 : reader.GetDouble(9),
                CreatedAt = DateTime.Parse(reader.GetString(10))
            };
            result.Add(marker);
        }

        return result;
    }

    /// <summary>
    /// 커스텀 마커 저장/업데이트
    /// </summary>
    public async Task SaveCustomMarkerAsync(CustomMapMarker marker)
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO CustomMapMarkers (Id, ProfileType, MapKey, Name, X, Y, Z, FloorId, Color, Size, Opacity, CreatedAt)
            VALUES (@id, @profileType, @mapKey, @name, @x, @y, @z, @floorId, @color, @size, @opacity, @createdAt)
            ON CONFLICT(Id, ProfileType) DO UPDATE SET
                MapKey = @mapKey,
                Name = @name,
                X = @x,
                Y = @y,
                Z = @z,
                FloorId = @floorId,
                Color = @color,
                Size = @size,
                Opacity = @opacity,
                CreatedAt = @createdAt";

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", marker.Id);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);
        cmd.Parameters.AddWithValue("@mapKey", marker.MapKey);
        cmd.Parameters.AddWithValue("@name", marker.Name ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@x", marker.X);
        cmd.Parameters.AddWithValue("@y", marker.Y);
        cmd.Parameters.AddWithValue("@z", marker.Z);
        cmd.Parameters.AddWithValue("@floorId", marker.FloorId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@color", marker.Color ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@size", marker.Size);
        cmd.Parameters.AddWithValue("@opacity", marker.Opacity);
        cmd.Parameters.AddWithValue("@createdAt", marker.CreatedAt.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 커스텀 마커 삭제
    /// </summary>
    public async Task DeleteCustomMarkerAsync(string id)
    {
        await InitializeAsync();
        var profileType = ProfileService.Instance.CurrentProfile;

        var connectionString = $"Data Source={_databasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "DELETE FROM CustomMapMarkers WHERE Id = @id AND ProfileType = @profileType";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@profileType", (int)profileType);

        await cmd.ExecuteNonQueryAsync();
    }

    #endregion
}
