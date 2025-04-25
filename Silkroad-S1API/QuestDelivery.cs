using System;
using System.Linq;
using UnityEngine;
using MelonLoader;
using S1API.Items;
using S1API.Storages;
using S1API.DeadDrops;
using S1API.Quests;
using S1API.Saveables;
using S1API.NPCs;
using System.Collections.Generic;
using S1API.GameTime;
using S1API.Internal.Utils;
using S1API.Products;
using S1API.Utils;
using Newtonsoft.Json;
using MelonLoader.Utils;
using System.IO;

namespace Silkroad
{
    public class QuestDelivery : Quest
    {
        [SaveableField("DeliveryData")]
        public DeliverySaveData Data = new DeliverySaveData();

        private DeadDropInstance deliveryDrop;
        public static HashSet<string> CompletedQuestKeys = new HashSet<string>();
        private QuestEntry deliveryEntry;
        private QuestEntry rewardEntry;
        public static bool QuestActive = false;
        public static event Action OnQuestCompleted;
        private Sprite? _questIcon; // Backing field for QuestIcon


        // Add a static instance to access the current quest from UI / Force Complete/Fail Quests
        public static QuestDelivery Instance { get; private set; }

        protected override Sprite? QuestIcon
        {
            get
            {
                MelonLogger.Msg($"Loading quest icon for {Data.DealerName}={Data.QuestImage}");

                // Dynamically load the image based on the DealerImage of the current instance
                return ImageUtils.LoadImage(MyApp.QuestImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", "SilkRoadIcon_quest.png"));
;
            }
        }

        protected override void OnCreated()
        {
            MelonLogger.Msg($"Setting ON CREATED quest icon for {Data.DealerName}={Data.QuestImage}");
            
            base.OnCreated();
            Instance = this;
            QuestActive = true;

            // Set the QuestIcon backing field after OnCreated() is called


            if (!Data.Initialized)
            {
                var drops = DeadDropManager.All?.ToList();
                if (drops == null || drops.Count < 1)
                {
                    MelonLogger.Error("❌ Not enough dead drops to assign delivery/reward.");
                    return;
                }

                deliveryDrop = drops[RandomUtils.RangeInt(0, drops.Count)];
                Data.DeliveryDropGUID = deliveryDrop.GUID;
                Data.Initialized = true;
            }
            else
            {
                deliveryDrop = DeadDropManager.All.FirstOrDefault(d => d.GUID == Data.DeliveryDropGUID);
            }

            deliveryEntry = AddEntry($"Deliver {Data.RequiredAmount}x bricks of {Data.ProductID} at the dead drop.");
            deliveryEntry.POIPosition = deliveryDrop.Position;
            deliveryEntry.Begin();

            rewardEntry = AddEntry($"Wait for the payment to arrive.");
            rewardEntry.SetState(QuestState.Inactive);

            deliveryDrop.Storage.OnClosed += CheckDelivery;

            MelonLogger.Msg("📦 QuestDelivery started with drop locations assigned.");
        }

        private void CheckDelivery()
        {
            var total = deliveryDrop.Storage.Slots
                .Where(slot => slot.ItemInstance is ProductInstance product &&
                               product.Definition.Name == Data.ProductID)
                .Sum(slot => slot.Quantity);

            if (total < Data.RequiredAmount)
            {
                MelonLogger.Msg($"❌ Not enough bricks: {total}/{Data.RequiredAmount}");
                return;
            }

            uint toRemove = Data.RequiredAmount;
            foreach (var slot in deliveryDrop.Storage.Slots)
            {
                if (slot.ItemInstance is ProductInstance product &&
                    product.Definition.Name == Data.ProductID)
                {
                    int remove = (int)Mathf.Min(slot.Quantity, toRemove);
                    slot.AddQuantity(-remove);
                    toRemove -= (uint)remove;
                    if (toRemove == 0) break;
                }
            }

            deliveryEntry.Complete();
            rewardEntry.SetState(QuestState.Active);
            MelonCoroutines.Start(DelayedReward());
        }

        private System.Collections.IEnumerator DelayedReward()
        {
            yield return new WaitForSeconds(RandomUtils.RangeInt(120, 200));
            GiveReward();
        }

        private void GiveReward()
        {
            var rewardAmount = Data.Reward;
            ConsoleHelper.RunCashCommand(rewardAmount);

            if (BlackmarketBuyer.Buyers.TryGetValue(Data.DealerName, out var dealerData))
            {
                dealerData.Reputation += 10;
                MelonLogger.Msg($"   Updated Reputation: {dealerData.Reputation}");
                if (Contacts.GetBuyer(Data.DealerName) is BlackmarketBuyer buyer)
                {
                    buyer.SendDeliverySuccess(Data.DealerName, Data.ProductID);
                    buyer.SendRewardDropped(Data.DealerName);
                }
                CheckUnlocks(dealerData);
            }

            QuestActive = false;
            CompletedQuestKeys.Add($"{Data.ProductID}_{Data.RequiredAmount}");
            rewardEntry.Complete();
            Complete();
            OnQuestCompleted?.Invoke();
        }

        private void CheckUnlocks(DealerSaveData dealerData)
        {
            // Get the original dealer data from the JSON to check unlocks
            var jsonPath = Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad/empire.json");
            if (!File.Exists(jsonPath)) return;

            var jsonText = File.ReadAllText(jsonPath);
            var dealerConfig = JsonConvert.DeserializeObject<DealerData>(jsonText);

            if (dealerConfig?.Dealers == null) return;

            var dealer = dealerConfig.Dealers.FirstOrDefault(d => d.Name == Data.DealerName);
            if (dealer == null) return;

            // Check for new drug unlocks
            foreach (var drug in dealerConfig.Dealers.FirstOrDefault(d => d.Name == Data.DealerName)?.Drugs ?? Enumerable.Empty<Drug>())
            {
                if (drug.UnlockRep <= dealerData.Reputation && !dealerData.UnlockedDrugs.Contains(drug.Type))
                {
                    if (Contacts.GetBuyer(Data.DealerName) is BlackmarketBuyer buyer)
                    {
                        buyer.UnlockDrug(Data.DealerName, drug.Type);
                    }
                }
            }

            // Check for new quality unlocks
            foreach (var drug in dealerConfig.Dealers.FirstOrDefault(d => d.Name == Data.DealerName)?.Drugs ?? Enumerable.Empty<Drug>())
            {
                foreach (var quality in drug.Qualities)
                {
                    if (quality.UnlockRep <= dealerData.Reputation &&
                        (!dealerData.UnlockedQuality.ContainsKey(drug.Type) ||
                         dealerData.UnlockedQuality[drug.Type] != quality.Type))
                    {
                        if (Contacts.GetBuyer(Data.DealerName) is BlackmarketBuyer buyer)
                        {
                            buyer.UnlockQuality(Data.DealerName, drug.Type, quality.Type);
                        }
                    }
                }
            }

            // Check for new effect unlocks
            foreach (var drug in dealerConfig.Dealers.FirstOrDefault(d => d.Name == Data.DealerName)?.Drugs ?? Enumerable.Empty<Drug>())
            {
                foreach (var effect in drug.Effects)
                {
                    if (effect.UnlockRep <= dealerData.Reputation)
                    {
                        if (effect.Probability >= 1.0f)
                        {
                            if (!dealerData.NecessaryEffects.Any(e => e.Name == effect.Name))
                            {
                                if (Contacts.GetBuyer(Data.DealerName) is BlackmarketBuyer buyer)
                                {
                                    buyer.UnlockNecessaryEffect(Data.DealerName, effect.Name);
                                }
                            }
                        }
                        else
                        {
                            if (!dealerData.OptionalEffects.Any(e => e.Name == effect.Name))
                            {
                                if (Contacts.GetBuyer(Data.DealerName) is BlackmarketBuyer buyer)
                                {
                                    buyer.UnlockOptionalEffect(Data.DealerName, effect.Name);
                                }
                            }
                        }
                    }
                }
            }

            // Check for new dealer unlocks
            foreach (var _dealer in dealerConfig.Dealers)
            {
                if (_dealer.UnlockRequirements == null || BlackmarketBuyer.Buyers.ContainsKey(_dealer.Name))
                    continue;

                bool allRequirementsMet = _dealer.UnlockRequirements.All(req =>
                    BlackmarketBuyer.Buyers.TryGetValue(req.Name, out var requiredDealer) &&
                    requiredDealer.Reputation >= req.MinRep);

                if (allRequirementsMet)
                {
                    // Unlock the dealer
                    var newBuyer = new BlackmarketBuyer(_dealer);
                    MelonLogger.Msg($"✅ New dealer unlocked: {_dealer.Name}");
                }
            }

            // Check for new shipping unlocks

            if (dealerConfig.Dealers.FirstOrDefault(d => d.Name == Data.DealerName) != null)
            {
                // Directly update shipping amounts based on the dealer’s shipping list and current reputation.
                dealerData.UpdateDeliveryAmounts(dealer.Shippings ?? new List<Shipping>(), dealerData.Reputation);
                MelonLogger.Msg($"   Updated delivery amounts: {dealerData.MinDeliveryAmount}-{dealerData.MaxDeliveryAmount}");
            }

            // Update delivery amounts based on unlocked shipping

            MelonLogger.Msg($"   Updated delivery amounts: {dealerData.MinDeliveryAmount}-{dealerData.MaxDeliveryAmount}");

            MelonLogger.Msg($"   Unlocked Drugs: {string.Join(", ", dealerData.UnlockedDrugs)}");
            MelonLogger.Msg($"   Unlocked Qualities: {string.Join(", ", dealerData.UnlockedQuality.Keys)}");
        }

        // NEW: Force-complete the active quest (i.e. give the reward immediately)
        public static void ForceCompleteQuest()
        {
            if (QuestActive && Instance != null)
            {
                Instance.GiveReward();
                MelonLogger.Msg("Quest force-completed.");
            }
            else
            {
                MelonLogger.Msg("No active quest to complete.");
            }
        }

        // NEW: Force-fail the active quest (i.e. mark as complete without reward)
        public static void ForceFailQuest()
        {
            if (QuestActive && Instance != null)
            {
                QuestActive = false;
                MelonLogger.Msg("Quest force-failed.");
                // Set the reward entry inactive and complete the quest, marking it as failed.
                Instance.rewardEntry.SetState(QuestState.Inactive);
                Instance.Complete();
            }
            else
            {
                MelonLogger.Msg("No active quest to fail.");
            }
        }

        protected override string Title =>
            !string.IsNullOrEmpty(Data?.ProductID)
                ? $"Deliver {Data.RequiredAmount}x {Data.ProductID} bricks"
                : "Silkroad Delivery";

        protected override string Description =>
            !string.IsNullOrEmpty(Data?.ProductID) && Data.RequiredAmount > 0
                ? $"Deliver {Data.RequiredAmount}x bricks of {Data.ProductID} to the drop point."
                : "Deliver the assigned product to the stash location.";
    }
}
