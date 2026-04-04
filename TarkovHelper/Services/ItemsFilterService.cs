using TarkovHelper.Pages; // For AggregatedItemViewModel and CollectorItemViewModel
using TarkovHelper.Models;

namespace TarkovHelper.Services
{
    /// <summary>
    /// Service for filtering and sorting item requirement view models.
    /// Extracted from ItemsPage.xaml.cs and CollectorPage.xaml.cs to improve maintainability.
    /// </summary>
    public static class ItemsFilterService
    {
        public static IEnumerable<AggregatedItemViewModel> FilterAndSort(
            IEnumerable<AggregatedItemViewModel> items,
            string searchText,
            string sourceFilter,
            string categoryFilter,
            string fulfillmentFilter,
            bool firOnly,
            bool hideFulfilled,
            string sortBy)
        {
            var filtered = items.Where(vm =>
            {
                // Search filter
                if (!string.IsNullOrEmpty(searchText))
                {
                    if (!vm.DisplayName.ToLowerInvariant().Contains(searchText) &&
                        !vm.SubtitleName.ToLowerInvariant().Contains(searchText))
                        return false;
                }

                // Source filter
                if (sourceFilter == "Quest" && vm.QuestCount == 0)
                    return false;
                if (sourceFilter == "Hideout" && vm.HideoutCount == 0)
                    return false;

                // Category filter (uses parent/grouped category)
                if (categoryFilter != "All")
                {
                    if (!string.Equals(vm.ParentCategory, categoryFilter, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                // FIR filter
                if (firOnly && !vm.FoundInRaid)
                    return false;

                // Fulfillment filter
                if (fulfillmentFilter != "All")
                {
                    var status = vm.FulfillmentStatus;
                    if (fulfillmentFilter == "NotStarted" && status != ItemFulfillmentStatus.NotStarted)
                        return false;
                    if (fulfillmentFilter == "InProgress" && status != ItemFulfillmentStatus.PartiallyFulfilled)
                        return false;
                    if (fulfillmentFilter == "Fulfilled" && status != ItemFulfillmentStatus.Fulfilled)
                        return false;
                }

                // Hide fulfilled filter
                if (hideFulfilled && vm.IsFulfilled)
                    return false;

                return true;
            });

            // Apply sorting
            return sortBy switch
            {
                "Total" => filtered.OrderByDescending(vm => vm.TotalCount).ThenBy(vm => vm.DisplayName),
                "Quest" => filtered.OrderByDescending(vm => vm.QuestCount).ThenBy(vm => vm.DisplayName),
                "Hideout" => filtered.OrderByDescending(vm => vm.HideoutCount).ThenBy(vm => vm.DisplayName),
                "Progress" => filtered.OrderByDescending(vm => vm.ProgressPercent).ThenBy(vm => vm.DisplayName),
                _ => filtered.OrderBy(vm => vm.DisplayName)
            };
        }

        public static IEnumerable<CollectorItemViewModel> FilterAndSortCollector(
            IEnumerable<CollectorItemViewModel> items,
            string searchText,
            string fulfillmentFilter,
            bool firOnly,
            bool hideFulfilled,
            string sortBy)
        {
            var filtered = items.Where(vm =>
            {
                // Search filter
                if (!string.IsNullOrEmpty(searchText))
                {
                    if (!vm.DisplayName.ToLowerInvariant().Contains(searchText) &&
                        !vm.SubtitleName.ToLowerInvariant().Contains(searchText))
                        return false;
                }

                // FIR filter
                if (firOnly && !vm.FoundInRaid)
                    return false;

                // Fulfillment filter
                if (fulfillmentFilter != "All")
                {
                    var status = vm.FulfillmentStatus;
                    if (fulfillmentFilter == "NotStarted" && status != ItemFulfillmentStatus.NotStarted)
                        return false;
                    if (fulfillmentFilter == "InProgress" && status != ItemFulfillmentStatus.PartiallyFulfilled)
                        return false;
                    if (fulfillmentFilter == "Fulfilled" && status != ItemFulfillmentStatus.Fulfilled)
                        return false;
                }

                // Hide fulfilled filter
                if (hideFulfilled && vm.IsFulfilled)
                    return false;

                return true;
            });

            // Apply sorting
            return sortBy switch
            {
                "Total" => filtered.OrderByDescending(vm => vm.TotalCount).ThenBy(vm => vm.DisplayName),
                "Quest" => filtered.OrderByDescending(vm => vm.QuestCount).ThenBy(vm => vm.DisplayName),
                "Progress" => filtered.OrderByDescending(vm => vm.ProgressPercent).ThenBy(vm => vm.DisplayName),
                _ => filtered.OrderBy(vm => vm.DisplayName)
            };
        }
    }
}
