using TarkovHelper.Debug;
using TarkovHelper.Models;
using TarkovHelper.Services.Logging;

namespace TarkovHelper.Services
{
    /// <summary>
    /// Service for managing user's item inventory quantities (FIR/Non-FIR)
    /// </summary>
    public class ItemInventoryService
    {
        private static readonly ILogger _log = Log.For<ItemInventoryService>();
        private static ItemInventoryService? _instance;
        public static ItemInventoryService Instance => _instance ??= new ItemInventoryService();

        /// <summary>
        /// 싱글톤 인스턴스를 파괴하여 캐시된 인벤토리 데이터를 완전히 비웁니다.
        /// </summary>
        public static void ResetInstance()
        {
            _instance = null;
        }
        private ProfileType _loadedProfile = ProfileType.Pvp;

        private readonly UserDataDbService _userDataDb = UserDataDbService.Instance;

        private ItemInventoryData _inventoryData = new();
        private readonly object _lock = new();

        // Debounce save timer
        private System.Timers.Timer? _saveTimer;
        private readonly HashSet<string> _pendingSaves = new(StringComparer.OrdinalIgnoreCase);

        public event EventHandler? InventoryChanged;

        private ItemInventoryService()
        {
            // 초기화는 외부에서 비동기적으로 호출되어야 합니다.
            InitializeSaveTimer();
        }

        private void InitializeSaveTimer()
        {
            _saveTimer = new System.Timers.Timer(500); // 500ms debounce
            _saveTimer.AutoReset = false;
            _saveTimer.Elapsed += (s, e) =>
            {
                SavePendingItems();
            };
        }

        private void SavePendingItems()
        {
            List<string> itemsToSave;
            lock (_lock)
            {
                if (_pendingSaves.Count == 0) return;
                itemsToSave = _pendingSaves.ToList();
                _pendingSaves.Clear();
            }

            _ = Task.Run(async () =>
            {
                foreach (var itemName in itemsToSave)
                {
                    try
                    {
                        int firQty, nonFirQty;
                        lock (_lock)
                        {
                            if (_inventoryData.Items.TryGetValue(itemName, out var inv))
                            {
                                firQty = inv.FirQuantity;
                                nonFirQty = inv.NonFirQuantity;
                            }
                            else
                            {
                                firQty = 0;
                                nonFirQty = 0;
                            }
                        }
                        await _userDataDb.SaveItemInventoryAsync(itemName, firQty, nonFirQty, _loadedProfile);
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Save failed for {itemName}: {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// Get inventory for a specific item
        /// </summary>
        public ItemInventory GetInventory(string itemNormalizedName)
        {
            lock (_lock)
            {
                if (_inventoryData.Items.TryGetValue(itemNormalizedName, out var inventory))
                {
                    return inventory;
                }

                return new ItemInventory { ItemNormalizedName = itemNormalizedName };
            }
        }

        /// <summary>
        /// Get FIR quantity for an item
        /// </summary>
        public int GetFirQuantity(string itemNormalizedName)
        {
            return GetInventory(itemNormalizedName).FirQuantity;
        }

        /// <summary>
        /// Get Non-FIR quantity for an item
        /// </summary>
        public int GetNonFirQuantity(string itemNormalizedName)
        {
            return GetInventory(itemNormalizedName).NonFirQuantity;
        }

        /// <summary>
        /// Get total quantity for an item
        /// </summary>
        public int GetTotalQuantity(string itemNormalizedName)
        {
            return GetInventory(itemNormalizedName).TotalQuantity;
        }

        /// <summary>
        /// Set FIR quantity for an item
        /// </summary>
        public void SetFirQuantity(string itemNormalizedName, int quantity)
        {
            quantity = Math.Max(0, quantity);

            lock (_lock)
            {
                if (!_inventoryData.Items.TryGetValue(itemNormalizedName, out var inventory))
                {
                    inventory = new ItemInventory { ItemNormalizedName = itemNormalizedName };
                    _inventoryData.Items[itemNormalizedName] = inventory;
                }

                if (inventory.FirQuantity != quantity)
                {
                    inventory.FirQuantity = quantity;
                    CleanupEmptyInventory(itemNormalizedName);
                    ScheduleSave(itemNormalizedName);
                    InventoryChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Set Non-FIR quantity for an item
        /// </summary>
        public void SetNonFirQuantity(string itemNormalizedName, int quantity)
        {
            quantity = Math.Max(0, quantity);

            lock (_lock)
            {
                if (!_inventoryData.Items.TryGetValue(itemNormalizedName, out var inventory))
                {
                    inventory = new ItemInventory { ItemNormalizedName = itemNormalizedName };
                    _inventoryData.Items[itemNormalizedName] = inventory;
                }

                if (inventory.NonFirQuantity != quantity)
                {
                    inventory.NonFirQuantity = quantity;
                    CleanupEmptyInventory(itemNormalizedName);
                    ScheduleSave(itemNormalizedName);
                    InventoryChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Adjust FIR quantity by delta (can be positive or negative)
        /// </summary>
        public void AdjustFirQuantity(string itemNormalizedName, int delta)
        {
            var current = GetFirQuantity(itemNormalizedName);
            SetFirQuantity(itemNormalizedName, current + delta);
        }

        /// <summary>
        /// Adjust Non-FIR quantity by delta (can be positive or negative)
        /// </summary>
        public void AdjustNonFirQuantity(string itemNormalizedName, int delta)
        {
            var current = GetNonFirQuantity(itemNormalizedName);
            SetNonFirQuantity(itemNormalizedName, current + delta);
        }

        /// <summary>
        /// Remove inventory entry if both quantities are 0
        /// </summary>
        private void CleanupEmptyInventory(string itemNormalizedName)
        {
            if (_inventoryData.Items.TryGetValue(itemNormalizedName, out var inventory))
            {
                if (inventory.FirQuantity == 0 && inventory.NonFirQuantity == 0)
                {
                    _inventoryData.Items.Remove(itemNormalizedName);
                }
            }
        }

        /// <summary>
        /// Calculate fulfillment info for an item
        /// </summary>
        public ItemFulfillmentInfo GetFulfillmentInfo(string itemNormalizedName, int requiredTotal, int requiredFir)
        {
            var inventory = GetInventory(itemNormalizedName);

            return new ItemFulfillmentInfo
            {
                ItemNormalizedName = itemNormalizedName,
                RequiredTotal = requiredTotal,
                RequiredFir = requiredFir,
                OwnedFir = inventory.FirQuantity,
                OwnedNonFir = inventory.NonFirQuantity
            };
        }

        /// <summary>
        /// Get all items in inventory
        /// </summary>
        public IReadOnlyDictionary<string, ItemInventory> GetAllInventory()
        {
            lock (_lock)
            {
                return new Dictionary<string, ItemInventory>(_inventoryData.Items, StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Get inventory statistics
        /// </summary>
        public (int TotalItems, int TotalFirCount, int TotalNonFirCount) GetStatistics()
        {
            lock (_lock)
            {
                var totalFir = _inventoryData.Items.Values.Sum(i => i.FirQuantity);
                var totalNonFir = _inventoryData.Items.Values.Sum(i => i.NonFirQuantity);
                return (_inventoryData.Items.Count, totalFir, totalNonFir);
            }
        }

        /// <summary>
        /// Reset all inventory data
        /// </summary>
        public void ResetAllInventory()
        {
            lock (_lock)
            {
                _inventoryData = new ItemInventoryData();
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await _userDataDb.ClearAllItemInventoryAsync(_loadedProfile);
                }
                catch (Exception ex)
                {
                    _log.Error($"Reset failed: {ex.Message}");
                }
            });

            InventoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 강제로 인벤토리를 다시 로드 (프로필 전환 시 사용)
        /// </summary>
        public async Task ReloadInventoryAsync()
        {
            await LoadInventoryFromDbAsync();
            InventoryChanged?.Invoke(this, EventArgs.Empty);
        }

        #region Persistence

        private void ScheduleSave(string itemNormalizedName)
        {
            lock (_lock)
            {
                _pendingSaves.Add(itemNormalizedName);
            }
            _inventoryData.LastUpdated = DateTime.UtcNow;
            _saveTimer?.Stop();
            _saveTimer?.Start();
        }

        public async Task InitializeAsync()
        {
            _loadedProfile = ProfileService.Instance.CurrentProfile;
            await LoadInventoryFromDbAsync();
        }

        public async Task LoadInventoryAsync()
        {
            await LoadInventoryFromDbAsync();
        }

        private void LoadInventory()
        {
            _ = LoadInventoryFromDbAsync();
        }

        private async Task LoadInventoryFromDbAsync()
        {
            try
            {
                var items = await _userDataDb.LoadItemInventoryAsync(_loadedProfile);
                var newData = new ItemInventoryData
                {
                    LastUpdated = DateTime.UtcNow,
                    Items = new Dictionary<string, ItemInventory>(StringComparer.OrdinalIgnoreCase)
                };

                foreach (var kvp in items)
                {
                    newData.Items[kvp.Key] = new ItemInventory
                    {
                        ItemNormalizedName = kvp.Key,
                        FirQuantity = kvp.Value.FirQuantity,
                        NonFirQuantity = kvp.Value.NonFirQuantity
                    };
                }

                lock (_lock)
                {
                    _inventoryData = newData;
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Load failed: {ex.Message}");
                _inventoryData = new ItemInventoryData();
            }
        }

        #endregion
    }
}
