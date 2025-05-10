using System;
using System.Linq;
using UnityEngine;
using MelonLoader;
using S1API.Items;
using S1API.Money;
using S1API.Storages;
using S1API.DeadDrops;
using S1API.Quests;
using S1API.Products;
using S1API.Saveables;
using System.Collections.Generic;
using S1API.Console;
using S1API.GameTime;
using S1API.Internal.Utils;
using S1API.PhoneApp;
//using S1API.Quests.Constants;
using Random = UnityEngine.Random;
using System.IO;
using MelonLoader.Utils;

namespace Empire
{

    // This is temporary mock method to test the logic. Will be replaced with the actual method to get the complete list of strings.
    // A method that returns a list of strings randomly selected from the complete list of strings 
    //TODO
    public static class RandomEffectSelector
    {
        //Create default values for completeList and count
        public static List<string> GetRandomEffects(List<string> completeList = null, int count = 100)
        {
            // If completeList is null, initialize it with a default list of strings
            if (completeList == null)
            {
                completeList = new List<string>
                {
"Euphoric",
"Foggy",
"Paranoia",
"Munchies",
"Calming",
"Refreshing",
"Disorienting",
"Energizing",
"Focused",
"Glowing",
"Lethal",
"Toxic",
"Thought-Provoking",
"Spicy",
"Smelly",
"Explosive",
"Seizure-Inducing",
"Sneaky",
"Sedating",
"Jennerising"
                };
            }


            count = Math.Min(count, completeList.Count);

            //return a random selection of strings from completeList with count elements
            return completeList.OrderBy(x => Guid.NewGuid()).Take(count).ToList();

        }
    }


    public class QuestDelivery : Quest
    {
        [SaveableField("DeliveryData")]
        public DeliverySaveData Data = new DeliverySaveData();
        public BlackmarketBuyer buyer;
        private DeadDropInstance deliveryDrop;
        public static HashSet<string> CompletedQuestKeys = new HashSet<string>();
        public QuestEntry deliveryEntry;
        public QuestEntry rewardEntry;
        public static bool QuestActive = false;
        public static event Action OnQuestCompleted;
        public static QuestDelivery? Active { get; internal set; }

        public QuestEntry GetDeliveryEntry() => deliveryEntry;
        public QuestEntry GetRewardEntry() => rewardEntry;
        public void ForceCancel()
        {
            MelonLogger.Msg("🚫 QuestDelivery.ForceCancel() called.");

            Data.Reward = -Data.Penalties[0];
            Data.RepReward = -Data.Penalties[1];
            //MelonCoroutines.Start(DelayedReward("Failed"));
            GiveReward("Failed");
            if (deliveryEntry != null && deliveryEntry.State != QuestState.Completed)
                rewardEntry.SetState(QuestState.Failed); // ✅
            if (rewardEntry != null && rewardEntry.State != QuestState.Completed)
                rewardEntry.SetState(QuestState.Failed);
            QuestActive = false;
            Active = null; // 👈 Reset after cancel
            Fail();
        }


        private void ExpireCountdown()
        {
            //use syntax like += to add a new event handler to the DayPass event
            // Reduce the quest time by 1 day
            Data.DealTime -= 1;
            // Check if the quest time has expired
            if (Data.DealTime < 0)
            {
                // If the quest time has expired, fail the quest
                Data.Reward = -Data.Penalties[0];
                Data.RepReward = -Data.Penalties[1];
                MelonCoroutines.Start(DelayedReward("Expired"));
                //GiveReward("Expired");
                if (deliveryEntry != null && deliveryEntry.State != QuestState.Completed)
                    rewardEntry.SetState(QuestState.Expired); // ✅
                if (rewardEntry != null && rewardEntry.State != QuestState.Completed)
                    rewardEntry.SetState(QuestState.Expired);
                QuestActive = false;
                Active = null; // 👈 Reset after cancel
                Expire();
            }
        }

        protected override Sprite? QuestIcon
        {
            get
            {
                //Use static image setting from PhoneApp Accept Quest
                //TODO - Do I even want dealer image for this or the standard image - Optional
                return ImageUtils.LoadImage(Data.QuestImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Empire", "EmpireIcon_quest.png"));
            }
        }
        protected override void OnLoaded()
        {
            MelonLogger.Msg("Quest OnLoaded called.");
            base.OnLoaded();
            MelonCoroutines.Start(WaitForBuyerAndLoad());


            TimeManager.OnDayPass += ExpireCountdown;
            MelonLogger.Msg($"Quest OnLoaded() done.");
        }

        private System.Collections.IEnumerator WaitForBuyerAndLoad()
        {
            float timeout = 5f;
            float waited = 0f;
            MelonLogger.Msg("Quest-WaitForBuyerAndLoad-Waiting for buyer to be initialized...");
            // while (Contacts.Buyers == null OR For all key value pairs in Contacts.Buyers, check if the value.IsInitialized is false for at least one of them OR waited < timeqout)
            while (!Contacts.IsInitialized && waited < timeout)
            {
                waited += Time.deltaTime;
                yield return null; // wait 1 frame
            }
            if (!Contacts.IsInitialized)
            {
                MelonLogger.Warning("⚠️ Buyer NPCs still not initialized after timeout. Skipping status sync.");
                yield break;
            }
            buyer = Contacts.GetBuyer(Data.DealerName);
        }


        protected override void OnCreated()
        {
            MelonLogger.Msg("OnCreated called.");
            base.OnCreated();
            MelonLogger.Msg($"OnCreated() done.");
            buyer = Contacts.GetBuyer(Data.DealerName);
            QuestActive = true;
            Active = this;
            TimeManager.OnDayPass += ExpireCountdown;
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
            //MelonLogger.Msg("📦 Testing 1.");
            deliveryEntry = AddEntry($"{Data.Task} at the dead drop.");
            deliveryEntry.POIPosition = deliveryDrop.Position;
            deliveryEntry.Begin();

            rewardEntry = AddEntry($"Wait for the payment to arrive.");
            MelonLogger.Msg("📦 Setting rewardEntry state to Inactive.");
            rewardEntry.SetState(QuestState.Inactive);

            deliveryDrop.Storage.OnClosed += CheckDelivery;

            MelonLogger.Msg("📦 QuestDelivery started with drop locations assigned.");
        }
        private uint PackageAmount(string packaging)
        {
            // Return the amount based on the packaging type - UPDATABLE
            return packaging switch
            {
                "Brick" => 20,
                "Jar" => 5,
                "Baggie" => 1,
                _ => 0,
            };
        }

        private void CheckDelivery()
        {
            MelonLogger.Msg("CheckDelivery called.");
            // Add null checks
            if (deliveryDrop?.Storage?.Slots == null)
            {
                MelonLogger.Error("❌ Storage or slots are null in CheckDelivery");
                return;
            }
            MelonLogger.Msg($"Expecting ProductID: {Data.ProductID}, RequiredAmount: {Data.RequiredAmount}");

            foreach (var slot in deliveryDrop.Storage.Slots)
            {
                // Add null check for slot
                if (slot?.ItemInstance == null)
                {
                    MelonLogger.Warning("⚠️ Encountered null slot or item instance, skipping...");
                    continue;
                }
                bool isProductInstance = slot.ItemInstance is ProductInstance;
                // Add null check and safe cast
                var item = slot.ItemInstance as ProductInstance;
                if (item == null)
                {
                    MelonLogger.Warning("⚠️ Item is not a ProductInstance, skipping...");
                    continue;
                }
                MelonLogger.Msg($"Slot: {item.Definition?.Category} - {slot.Quantity} package - {item.Definition?.Name} ");
                string slotProductID = isProductInstance ? item.Definition?.Name : "null";
                string packaging = isProductInstance ? item.AppliedPackaging?.Name : "null";
                int quantity = slot.Quantity;
                // Add null check for Data.NecessaryEffects
                if (Data?.NecessaryEffects == null)
                {
                    MelonLogger.Error("❌ NecessaryEffects is null");
                    return;
                }
                var productDef = ProductManager.DiscoveredProducts.FirstOrDefault(p => p.ID == item?.Definition.ID);
                //Temporary list to hold the effects and test with dummy values
                //TODO
                List<string> productEffects = RandomEffectSelector.GetRandomEffects(Data.NecessaryEffects);
                //Check isProductInstance AND if productEffects contains ALL of the necessary effects
                //ADD non-dummy check for quality and effects
                //TODO
                if (isProductInstance && Data.NecessaryEffects.All(effect => productEffects.Contains(effect)))
                {
                    uint total = (uint)(quantity * PackageAmount(packaging));
                    if (total <= Data.RequiredAmount)
                    {
                        slot.AddQuantity(-quantity);
                        UpdateReward(total, item, productDef);
                        Data.RequiredAmount -= total;
                        MelonLogger.Msg($"✅ Delivered {total}x {slotProductID} to the stash. Remaining: {Data.RequiredAmount}. Reward now: {Data.Reward}");
                    }
                    else
                    {
                        //FLOOR of the negative of the division to get the number of packages to remove
                        int toRemove = (int)Math.Ceiling((double)(Data.RequiredAmount / PackageAmount(packaging)));
                        toRemove = Math.Min(toRemove, slot.Quantity);
                        slot.AddQuantity(-toRemove);
                        UpdateReward(Data.RequiredAmount, item, productDef);
                        Data.RequiredAmount = 0;
                        MelonLogger.Msg($"✅ Delivered {total}x {slotProductID} to the stash. Remaining: {Data.RequiredAmount}. Reward now: {Data.Reward}");
                        break;
                    }
                }
            }
            if (Data.RequiredAmount <= 0)
            {
                //MelonLogger.Msg("Test2");
                buyer.SendCustomMessage("Success", Data.ProductID, (int)Data.RequiredAmount, Data.Quality, Data.NecessaryEffects, Data.OptionalEffects);
                MelonLogger.Msg("❌ No required amount to deliver. Quest done.");
                deliveryDrop.Storage.OnClosed -= CheckDelivery;
                deliveryEntry.Complete();
                rewardEntry.SetState(QuestState.Active);
                MelonCoroutines.Start(DelayedReward("Completed"));

            }
            else
            {
                buyer.SendCustomMessage("Incomplete", Data.ProductID, (int)Data.RequiredAmount, Data.Quality, Data.NecessaryEffects, Data.OptionalEffects);
                MelonLogger.Msg($"Continue delivery. Remaining amount: {Data.RequiredAmount}");
            }
        }

        //Update Dummy with real effect and quality calculation
        //TODO
        private void UpdateReward(uint total, ProductInstance? item, ProductDefinition? productDef)
        {
            // Check if productDef is null or not a ProductDefinition
            if (productDef == null)
            {
                MelonLogger.Error("❌ Product definition is null or not a ProductDefinition. Reward calculation skipped.");
                return;
            }
            // Sum of all in Data.NecessaryEffectMult
            float EffectsSum = Data.NecessaryEffectMult.Sum();
            //  optional effects - TODO
            Data.Reward += (int)(total * productDef.Price * (1 + Data.QualityMult) * Data.DealTimeMult * (1 + EffectsSum));
            MelonLogger.Msg($"   Reward updated: {Data.Reward} with Price: {productDef.Price}, Quality: {Data.QualityMult} and EffectsSum: {EffectsSum} and DealTimeMult: {Data.DealTimeMult}.");
        }



        //Call with QuestState to be set as string - UPDATABLE
        private System.Collections.IEnumerator DelayedReward(string source)
        {
            yield return new WaitForSeconds(RandomUtils.RangeInt(60, 120));
            GiveReward(source);
        }

        private void GiveReward(string source)
        {
            TimeManager.OnDayPass -= ExpireCountdown;

            ConsoleHelper.RunCashCommand(Data.Reward);
            MelonLogger.Msg($"   Rewarded : ${Data.Reward} to {Data.DealerName} and {Data.RepReward} with {Data.RepMult} in rep.");
            Data.RepReward += (int)(Data.Reward * Data.RepMult);
            buyer.GiveReputation((int)Data.RepReward);
            //Calculate and give XP - TODO
            MelonLogger.Msg($"   Rewarded : ${Data.Reward} and Rep {Data.RepReward} to {Data.DealerName}");

            if (source == "Expired")
            {
                buyer.SendCustomMessage("Expire", Data.ProductID, (int)Data.RequiredAmount, Data.Quality, Data.NecessaryEffects, Data.OptionalEffects);
            }
            else if (source == "Failed")
            {
                buyer.SendCustomMessage("Fail", Data.ProductID, (int)Data.RequiredAmount, Data.Quality, Data.NecessaryEffects, Data.OptionalEffects);
            }
            else if (source == "Completed")
            {
                buyer.SendCustomMessage("Reward", Data.ProductID, (int)Data.RequiredAmount, Data.Quality, Data.NecessaryEffects, Data.OptionalEffects);
                buyer.IncreaseCompletedDeals(1);
                buyer.UnlockDrug();
                Contacts.Initialize();
            }
            else
            {
                MelonLogger.Error($"❌ Unknown source: {source}.");
                return;
            }
            buyer.SaveDealerData();
            QuestActive = false;
            //CompletedQuestKeys.Add($"{Data.ProductID}_{Data.RequiredAmount}");
            rewardEntry?.Complete();
            Complete();
            OnQuestCompleted?.Invoke();

        }

        protected override string Title =>
            !string.IsNullOrEmpty(Data?.ProductID)
                ? $"Deliver {Data.ProductID} to {Data.DealerName}"
                : "Empire Delivery";

        protected override string Description =>
            !string.IsNullOrEmpty(Data?.ProductID) && Data.RequiredAmount > 0
                ? $"{Data.Task}"
                : "Deliver the assigned product to the stash location.";
    }
}
