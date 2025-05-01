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

namespace Silkroad
{

    // This is temporary mock method to test the logic. Will be replaced with the actual method to get the complete list of strings.
    // A method that returns a list of strings randomly selected from the complete list of strings 
    //TODO
    public static class RandomEffectSelector
    {
        //Create default values for completeList and count
        public static List<string> GetRandomEffects(List<string> completeList = null, int count = 8)
        {
            // If completeList is null, initialize it with a default list of strings
            if (completeList == null)
            {
                completeList = new List<string>
                {
                    "Munchies",
                    "Refreshing",
                    "Euphoric",
                    "Sneaky",
                    "Paranoia"
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

        //move to time manager 
        //TODO
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
                //MelonLogger.Msg($"QuestIcon: {Data.QuestImage ?? "null"}");
                // Dynamically load the image based on the DealerImage of the current instance - Doesn't work
                //Use static image setting from PhoneApp Accept Quest
                //TODO - Do I even want dealer image for this or the standard image - Optional
                return ImageUtils.LoadImage(Data.QuestImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", "SilkRoadIcon_quest.png"));
            }
        }
        protected override void OnLoaded()
        {
            MelonLogger.Msg("OnLoaded called.");
            base.OnLoaded();
            MelonLogger.Msg($"OnLoaded() done.");
            buyer = Contacts.GetBuyer(Data.DealerName);
            TimeManager.OnDayPass += ExpireCountdown;
            MelonCoroutines.Start(WaitForBuyerAndSendStatus());
        }

        private System.Collections.IEnumerator WaitForBuyerAndSendStatus()
        {
            float timeout = 5f;
            float waited = 0f;
            MelonLogger.Msg("WaitForBuyerAndSendStatus-Waiting for buyer to be initialized...");
            // while (Contacts.Buyers == null OR For all key value pairs in Contacts.Buyers, check if the value.IsInitialized is false for at least one of them OR waited < timeqout)
            while ((Contacts.Buyers == null || !Contacts.Buyers.Values.All(buyer => buyer.IsInitialized)) && waited < timeout)
            {
                waited += Time.deltaTime;
                yield return null; // wait 1 frame
            }

            if ((Contacts.Buyers == null || !Contacts.Buyers.Values.All(buyer => buyer.IsInitialized)))
            {
                MelonLogger.Warning("⚠️ Buyer NPC still not initialized after timeout. Skipping status sync.");
                //Log the buyer who is not initialized by logging the key from Contacts
                foreach (var buyer in Contacts.Buyers.Values.Where(b => !b.IsInitialized))
                {
                    MelonLogger.Warning($"Buyer is not initialized. Key: {Contacts.Buyers.FirstOrDefault(b => b.Value == buyer).Key}");
                }
                yield break;
            }

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
            MelonLogger.Msg("📦 Testing 1.");
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

                MelonLogger.Msg($"Slot: {item.Definition?.Category} - {slot.Quantity} - {item.Definition?.Name} ");
                string slotProductID = isProductInstance ? item.Definition?.Name : "null";
                string packaging = isProductInstance ? item.AppliedPackaging?.Name : "null";
                int quantity = slot.Quantity;

                // Add null check for Data.NecessaryEffects
                if (Data?.NecessaryEffects == null)
                {
                    MelonLogger.Error("❌ NecessaryEffects is null");
                    return;
                }

                //Temporary list to hold the effects and test with dummy values
                //TODO
                List<string> productEffects = RandomEffectSelector.GetRandomEffects(Data.NecessaryEffects, 8);
                //Check isProductInstance AND if productEffects contains ALL of the necessary effects
                //ADD non-dummy check for quality and effects
                //TODO
                if (isProductInstance && Data.NecessaryEffects.All(effect => Data.NecessaryEffects.Contains(effect)))
                {
                    uint total = (uint)(quantity * PackageAmount(packaging));
                    if (total <= Data.RequiredAmount)
                    {
                        slot.AddQuantity(-quantity);
                        UpdateReward(total, item);
                        Data.RequiredAmount -= total;

                        MelonLogger.Msg($"✅ Delivered {total}x {slotProductID} to the stash. Remaining: {Data.RequiredAmount}. Reward now: {Data.Reward}");
                    }
                    else
                    {
                        var toRemove = (int)(-Data.RequiredAmount / PackageAmount(packaging)) - 1;//Deal with it
                        slot.AddQuantity(toRemove);
                        UpdateReward(Data.RequiredAmount, item);
                        Data.RequiredAmount = 0;
                        MelonLogger.Msg($"✅ Delivered {total}x {slotProductID} to the stash. Remaining: {Data.RequiredAmount}. Reward now: {Data.Reward}");
                        break;
                    }
                }

                //MelonLogger.Msg($"Total Amount: {total}");
                //var definition = ((ProductInstance)slot.ItemInstance);
                //DebugUtils.LogObjectJson(definition, "Slot ItemInstance Definition");
                //MelonLogger.Msg($"Slot: isProductInstance={isProductInstance}, productID={slotProductID}, packaging={packaging}, quantity={quantity}");
            }
            if (Data.RequiredAmount <= 0)
            {
                buyer.SendCustomMessage("Success", Data.ProductID, (int)Data.RequiredAmount, Data.Quality, Data.NecessaryEffects, Data.OptionalEffects);
                MelonLogger.Msg("❌ No required amount to deliver. Quest done.");
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
        private void UpdateReward(uint total, ProductInstance? item)
        {
            var itemDef = ItemManager.GetItemDefinition(item?.Definition.ID);
            if (itemDef is ProductDefinition productDef)
            {
                // Dummy Reward calculation - to be replaced with effects and quality calculation
                MelonLogger.Msg($"Item Definition: {productDef.Name}");
                MelonLogger.Msg($"Item Quality: {productDef.Price}");
                Data.Reward += (int)(total * productDef.Price);
            }
            else
            {
                MelonLogger.Error("❌ Item definition is not a ProductDefinition. Reward calculation skipped.");
                Data.Reward += (int)(total * 200);
            }
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
            deliveryDrop.Storage.OnClosed -= CheckDelivery;
            ConsoleHelper.RunCashCommand(Data.Reward);
            buyer.GiveReputation((int)Data.RepReward + 10);//TODO - remove 10
            MelonLogger.Msg($"   Rewarded : ${Data.Reward} and Rep {Data.RepReward + 10} to {Data.DealerName}");

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
            }
            else
            {
                MelonLogger.Error($"❌ Unknown source: {source}.");
                return;
            }
            //buyer.IncreaseCompletedDeals(1);

            buyer.UnlockDrug();
            Contacts.Initialize();
            buyer.SaveDealerData();
            QuestActive = false;
            //CompletedQuestKeys.Add($"{Data.ProductID}_{Data.RequiredAmount}");
            rewardEntry?.Complete();
            Complete();
            OnQuestCompleted?.Invoke();
            // Based on New Reputations, Check JSON to see if new NPC unlocked - Remove and Replace with button if slow/crash
           
        }





        protected override string Title =>
            !string.IsNullOrEmpty(Data?.ProductID)
                ? $"Deliver {Data.ProductID} to {Data.DealerName}"
                : "Silkroad Delivery";

        protected override string Description =>
            !string.IsNullOrEmpty(Data?.ProductID) && Data.RequiredAmount > 0
                ? $"{Data.Task}"
                : "Deliver the assigned product to the stash location.";
    }
}
