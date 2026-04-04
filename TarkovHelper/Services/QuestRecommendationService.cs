using System;
using System.Collections.Generic;
using System.Linq;
using TarkovHelper.Models;

namespace TarkovHelper.Services
{
    /// <summary>
    /// Recommendation type for quest suggestions
    /// </summary>
    public enum RecommendationType
    {
        /// <summary>
        /// Quest can be completed right now (items ready, objectives doable)
        /// </summary>
        ReadyToComplete,

        /// <summary>
        /// Quest only needs item hand-in (no raid objectives)
        /// </summary>
        ItemHandInOnly,

        /// <summary>
        /// High priority for Kappa progression
        /// </summary>
        KappaPriority,

        /// <summary>
        /// Unlocks multiple follow-up quests
        /// </summary>
        UnlocksMany,

        /// <summary>
        /// Easy to complete (no items, simple objectives)
        /// </summary>
        EasyQuest
    }

    /// <summary>
    /// A quest recommendation with reason
    /// </summary>
    public class QuestRecommendation
    {
        public TarkovTask Quest { get; set; } = null!;
        public RecommendationType Type { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int Priority { get; set; }

        /// <summary>
        /// Items the player already has for this quest
        /// </summary>
        public List<(QuestItem Item, int Owned)> ReadyItems { get; set; } = new();

        /// <summary>
        /// Items still needed
        /// </summary>
        public List<(QuestItem Item, int Needed)> MissingItems { get; set; } = new();

        /// <summary>
        /// Number of quests this will unlock
        /// </summary>
        public int UnlocksCount { get; set; }
    }

    /// <summary>
    /// Service for generating smart quest recommendations based on player state
    /// </summary>
    public class QuestRecommendationService
    {
        private static QuestRecommendationService? _instance;
        public static QuestRecommendationService Instance => _instance ??= new QuestRecommendationService();

        /// <summary>
        /// Get recommended quests based on current player state
        /// </summary>
        /// <param name="maxResults">Maximum number of recommendations to return</param>
        /// <returns>List of quest recommendations sorted by priority</returns>
        public List<QuestRecommendation> GetRecommendations(int maxResults = 5)
        {
            var recommendations = new List<QuestRecommendation>();
            var progressService = QuestProgressService.Instance;
            var inventoryService = ItemInventoryService.Instance;
            var graphService = QuestGraphService.Instance;

            // Get all active quests (not locked, not done, not failed)
            var activeQuests = progressService.AllTasks
                .Where(t => progressService.GetStatus(t) == QuestStatus.Active)
                .ToList();

            foreach (var quest in activeQuests)
            {
                var recommendation = AnalyzeQuest(quest, progressService, inventoryService, graphService);
                if (recommendation != null)
                {
                    recommendations.Add(recommendation);
                }
            }

            // Sort by priority (higher first) and take top results
            return recommendations
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(r => r.Type == RecommendationType.ReadyToComplete)
                .ThenByDescending(r => r.Type == RecommendationType.ItemHandInOnly)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Get quests that can be completed immediately (all items ready)
        /// </summary>
        public List<QuestRecommendation> GetReadyToCompleteQuests()
        {
            var recommendations = new List<QuestRecommendation>();
            var progressService = QuestProgressService.Instance;
            var inventoryService = ItemInventoryService.Instance;

            var activeQuests = progressService.AllTasks
                .Where(t => progressService.GetStatus(t) == QuestStatus.Active)
                .ToList();

            foreach (var quest in activeQuests)
            {
                if (IsQuestReadyToComplete(quest, inventoryService, out var readyItems))
                {
                    recommendations.Add(new QuestRecommendation
                    {
                        Quest = quest,
                        Type = RecommendationType.ReadyToComplete,
                        Reason = GetReadyToCompleteReason(quest, readyItems),
                        Priority = 100,
                        ReadyItems = readyItems
                    });
                }
            }

            return recommendations.OrderByDescending(r => r.Quest.ReqKappa).ToList();
        }

        /// <summary>
        /// Get quests that only require item hand-in (no raid objectives)
        /// </summary>
        public List<QuestRecommendation> GetItemHandInOnlyQuests()
        {
            var recommendations = new List<QuestRecommendation>();
            var progressService = QuestProgressService.Instance;
            var inventoryService = ItemInventoryService.Instance;

            var activeQuests = progressService.AllTasks
                .Where(t => progressService.GetStatus(t) == QuestStatus.Active)
                .ToList();

            foreach (var quest in activeQuests)
            {
                if (IsItemHandInOnly(quest))
                {
                    var (readyItems, missingItems) = AnalyzeItemRequirements(quest, inventoryService);
                    var fulfillmentRatio = CalculateFulfillmentRatio(readyItems, missingItems);

                    recommendations.Add(new QuestRecommendation
                    {
                        Quest = quest,
                        Type = RecommendationType.ItemHandInOnly,
                        Reason = GetItemHandInReason(quest, readyItems, missingItems),
                        Priority = 50 + (int)(fulfillmentRatio * 50), // 50-100 based on fulfillment
                        ReadyItems = readyItems,
                        MissingItems = missingItems
                    });
                }
            }

            return recommendations
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(r => r.Quest.ReqKappa)
                .ToList();
        }

        /// <summary>
        /// Get Kappa-required quests that are currently active
        /// </summary>
        public List<QuestRecommendation> GetKappaPriorityQuests()
        {
            var recommendations = new List<QuestRecommendation>();
            var progressService = QuestProgressService.Instance;
            var inventoryService = ItemInventoryService.Instance;
            var graphService = QuestGraphService.Instance;

            var kappaQuests = progressService.AllTasks
                .Where(t => t.ReqKappa && progressService.GetStatus(t) == QuestStatus.Active)
                .ToList();

            foreach (var quest in kappaQuests)
            {
                var (readyItems, missingItems) = AnalyzeItemRequirements(quest, inventoryService);
                var unlocksCount = quest.LeadsTo?.Count ?? 0;

                recommendations.Add(new QuestRecommendation
                {
                    Quest = quest,
                    Type = RecommendationType.KappaPriority,
                    Reason = GetKappaReason(quest, unlocksCount),
                    Priority = 80 + unlocksCount * 5,
                    ReadyItems = readyItems,
                    MissingItems = missingItems,
                    UnlocksCount = unlocksCount
                });
            }

            return recommendations
                .OrderByDescending(r => r.Priority)
                .ToList();
        }

        /// <summary>
        /// Get quests that unlock many follow-up quests
        /// </summary>
        public List<QuestRecommendation> GetHighImpactQuests()
        {
            var recommendations = new List<QuestRecommendation>();
            var progressService = QuestProgressService.Instance;
            var inventoryService = ItemInventoryService.Instance;

            var activeQuests = progressService.AllTasks
                .Where(t => progressService.GetStatus(t) == QuestStatus.Active)
                .ToList();

            foreach (var quest in activeQuests)
            {
                var unlocksCount = quest.LeadsTo?.Count ?? 0;
                if (unlocksCount >= 2) // Only include quests that unlock 2+ quests
                {
                    var (readyItems, missingItems) = AnalyzeItemRequirements(quest, inventoryService);

                    recommendations.Add(new QuestRecommendation
                    {
                        Quest = quest,
                        Type = RecommendationType.UnlocksMany,
                        Reason = GetUnlocksReason(quest, unlocksCount),
                        Priority = 60 + unlocksCount * 10,
                        ReadyItems = readyItems,
                        MissingItems = missingItems,
                        UnlocksCount = unlocksCount
                    });
                }
            }

            return recommendations
                .OrderByDescending(r => r.UnlocksCount)
                .ThenByDescending(r => r.Quest.ReqKappa)
                .ToList();
        }

        private QuestRecommendation? AnalyzeQuest(
            TarkovTask quest,
            QuestProgressService progressService,
            ItemInventoryService inventoryService,
            QuestGraphService graphService)
        {
            var (readyItems, missingItems) = AnalyzeItemRequirements(quest, inventoryService);
            var unlocksCount = quest.LeadsTo?.Count ?? 0;

            // Check if ready to complete
            if (IsQuestReadyToComplete(quest, inventoryService, out _))
            {
                return new QuestRecommendation
                {
                    Quest = quest,
                    Type = RecommendationType.ReadyToComplete,
                    Reason = GetReadyToCompleteReason(quest, readyItems),
                    Priority = 100 + (quest.ReqKappa ? 20 : 0) + unlocksCount * 5,
                    ReadyItems = readyItems,
                    UnlocksCount = unlocksCount
                };
            }

            // Check if item hand-in only with high fulfillment
            if (IsItemHandInOnly(quest))
            {
                var fulfillmentRatio = CalculateFulfillmentRatio(readyItems, missingItems);
                if (fulfillmentRatio >= 0.5) // At least 50% items ready
                {
                    return new QuestRecommendation
                    {
                        Quest = quest,
                        Type = RecommendationType.ItemHandInOnly,
                        Reason = GetItemHandInReason(quest, readyItems, missingItems),
                        Priority = 50 + (int)(fulfillmentRatio * 40) + (quest.ReqKappa ? 10 : 0),
                        ReadyItems = readyItems,
                        MissingItems = missingItems
                    };
                }
            }

            // Check if Kappa priority
            if (quest.ReqKappa)
            {
                return new QuestRecommendation
                {
                    Quest = quest,
                    Type = RecommendationType.KappaPriority,
                    Reason = GetKappaReason(quest, unlocksCount),
                    Priority = 70 + unlocksCount * 5,
                    ReadyItems = readyItems,
                    MissingItems = missingItems,
                    UnlocksCount = unlocksCount
                };
            }

            // Check if unlocks many quests
            if (unlocksCount >= 2)
            {
                return new QuestRecommendation
                {
                    Quest = quest,
                    Type = RecommendationType.UnlocksMany,
                    Reason = GetUnlocksReason(quest, unlocksCount),
                    Priority = 60 + unlocksCount * 10,
                    ReadyItems = readyItems,
                    MissingItems = missingItems,
                    UnlocksCount = unlocksCount
                };
            }

            // Check if easy quest (no items required)
            if ((quest.RequiredItems == null || quest.RequiredItems.Count == 0))
            {
                return new QuestRecommendation
                {
                    Quest = quest,
                    Type = RecommendationType.EasyQuest,
                    Reason = GetEasyQuestReason(quest),
                    Priority = 40,
                    UnlocksCount = unlocksCount
                };
            }

            return null;
        }

        private bool IsQuestReadyToComplete(TarkovTask quest, ItemInventoryService inventoryService, out List<(QuestItem, int)> readyItems)
        {
            readyItems = new List<(QuestItem, int)>();

            // If no items required, just check if it's an item-only quest
            if (quest.RequiredItems == null || quest.RequiredItems.Count == 0)
            {
                return IsItemHandInOnly(quest);
            }

            // Check if all required items are available
            foreach (var item in quest.RequiredItems)
            {
                var inventory = inventoryService.GetInventory(item.ItemNormalizedName);
                var available = item.FoundInRaid ? inventory.FirQuantity : inventory.TotalQuantity;

                if (available >= item.Amount)
                {
                    readyItems.Add((item, available));
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsItemHandInOnly(TarkovTask quest)
        {
            // Quest is item hand-in only if:
            // 1. Has required items
            // 2. No complex objectives (just "Obtain" or "Hand over" type)
            if (quest.RequiredItems == null || quest.RequiredItems.Count == 0)
                return false;

            // Check objectives - if all are simple or missing
            if (quest.Objectives == null || quest.Objectives.Count == 0)
                return true;

            // Check if objectives are only about items (hand over, obtain, etc.)
            var itemKeywords = new[] { "hand over", "obtain", "find", "turn in", "give", "bring", "provide" };
            var complexKeywords = new[] { "kill", "eliminate", "survive", "visit", "plant", "mark", "extract", "reach" };

            foreach (var objective in quest.Objectives)
            {
                var lowerObj = objective.ToLowerInvariant();
                if (complexKeywords.Any(k => lowerObj.Contains(k)))
                {
                    return false;
                }
            }

            return true;
        }

        private (List<(QuestItem, int)> ready, List<(QuestItem, int)> missing) AnalyzeItemRequirements(
            TarkovTask quest,
            ItemInventoryService inventoryService)
        {
            var ready = new List<(QuestItem, int)>();
            var missing = new List<(QuestItem, int)>();

            if (quest.RequiredItems == null)
                return (ready, missing);

            foreach (var item in quest.RequiredItems)
            {
                var inventory = inventoryService.GetInventory(item.ItemNormalizedName);
                var available = item.FoundInRaid ? inventory.FirQuantity : inventory.TotalQuantity;

                if (available >= item.Amount)
                {
                    ready.Add((item, available));
                }
                else
                {
                    missing.Add((item, item.Amount - available));
                }
            }

            return (ready, missing);
        }

        private double CalculateFulfillmentRatio(List<(QuestItem, int)> ready, List<(QuestItem, int)> missing)
        {
            var totalItems = ready.Count + missing.Count;
            if (totalItems == 0) return 1.0;
            return (double)ready.Count / totalItems;
        }

        private string GetReadyToCompleteReason(TarkovTask quest, List<(QuestItem, int)> readyItems)
        {
            if (readyItems.Count > 0)
            {
                return $"필요 아이템 {readyItems.Count}개 보유 중";
            }

            return "지금 바로 완료 가능";
        }

        private string GetItemHandInReason(TarkovTask quest, List<(QuestItem, int)> ready, List<(QuestItem, int)> missing)
        {
            var total = ready.Count + missing.Count;

            return $"아이템 제출만 필요 ({ready.Count}/{total}개 보유)";
        }

        private string GetKappaReason(TarkovTask quest, int unlocksCount)
        {
            if (unlocksCount > 0)
            {
                return $"카파 필수 + {unlocksCount}개 퀘스트 해금";
            }

            return "카파 컨테이너 필수 퀘스트";
        }

        private string GetUnlocksReason(TarkovTask quest, int unlocksCount)
        {
            return $"{unlocksCount}개 퀘스트 해금";
        }

        private string GetEasyQuestReason(TarkovTask quest)
        {
            return "아이템 필요 없음";
        }
    }
}
