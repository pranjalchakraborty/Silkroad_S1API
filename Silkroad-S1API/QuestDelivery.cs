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

    // This is a temporary mock method to test the logic. Will be replaced with the actual method to get the complete list of strings.
    // A method that returns a list of strings randomly selected from the complete list of strings 
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

        private DeadDropInstance deliveryDrop;
        public static HashSet<string> CompletedQuestKeys = new HashSet<string>();
        private QuestEntry deliveryEntry;
        private QuestEntry rewardEntry;
        public static bool QuestActive = false;
        public static event Action OnQuestCompleted;

        protected override Sprite? QuestIcon
        {
            get
            {
                // Dynamically load the image based on the DealerImage of the current instance - Doesn't work
                return ImageUtils.LoadImage(Data.QuestImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", "SilkRoadIcon_quest.png"));
            }
        }
        protected override void OnLoaded()
        {
            base.OnLoaded();
            MelonCoroutines.Start(WaitForBuyerAndSendStatus());
        }

        private System.Collections.IEnumerator WaitForBuyerAndSendStatus()
        {
            float timeout = 5f;
            float waited = 0f;
            MelonLogger.Msg("Waiting for buyer to be initialized...");
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
        // Add a static instance to access the current quest from UI / Force Complete/Fail Quests
        public static QuestDelivery Instance { get; private set; }

        protected override void OnCreated()
        {

            base.OnCreated();
            Instance = this;
            QuestActive = true;


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

            deliveryEntry = AddEntry($"{Data.Task} at the dead drop.");
            deliveryEntry.POIPosition = deliveryDrop.Position;
            deliveryEntry.Begin();

            rewardEntry = AddEntry($"Wait for the payment to arrive.");
            rewardEntry.SetState(QuestState.Inactive);

            deliveryDrop.Storage.OnClosed += CheckDelivery;

            MelonLogger.Msg("📦 QuestDelivery started with drop locations assigned.");
        }
        private int PackageAmount(string packaging)
        {
            // Return the amount based on the packaging type
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
            MelonLogger.Msg($"Expecting ProductID: {Data.ProductID}, RequiredAmount: {Data.RequiredAmount}");
             //necessary and optional effects are based on Data.RequiredDrug.Effects => Effect.Probability ==1 means necessary, else optional
            List<string> necessaryEffects = Data.RequiredDrug.Effects.Where(e => e.Probability == 1).Select(e => e.Name).ToList();
            List<string> optionalEffects = Data.RequiredDrug.Effects.Where(e => e.Probability < 1).Select(e => e.Name).ToList();
               
            foreach (var slot in deliveryDrop.Storage.Slots)
            {
                bool isProductInstance = slot.ItemInstance is ProductInstance;
                var item =((ProductInstance)slot.ItemInstance);
                string slotProductID = isProductInstance ? item.Definition.Name : "null";
                string packaging = isProductInstance ? item.AppliedPackaging.Name : "null";
                int quantity = slot.Quantity;
                //Temporary list to hold the effects and test with dummy values
                List<string> productEffects = RandomEffectSelector.GetRandomEffects(necessaryEffects, 8);
                //Check isProductInstance AND if productEffects contains ALL of the necessary effects
                //ADD non-dummy check for quality and effects
                if (isProductInstance && necessaryEffects.All(effect => productEffects.Contains(effect)))
                {
                    int total = quantity * PackageAmount(packaging);
                    if (total < Data.RequiredAmount)
                    {
                        slot.AddQuantity(-quantity);    
                        Data.RequiredAmount -= (uint)total;
                        UpdateReward(total,item);
                    }
                    else{  
                        var toRemove = (int)(-Data.RequiredAmount / PackageAmount(packaging));
                        slot.AddQuantity(toRemove);
                        Data.RequiredAmount = 0;
                        UpdateReward(total,item);
                        break;
                    }
                }

                //MelonLogger.Msg($"Total Amount: {total}");
                //var definition = ((ProductInstance)slot.ItemInstance);
                //DebugUtils.LogObjectJson(definition, "Slot ItemInstance Definition");
                //MelonLogger.Msg($"Slot: isProductInstance={isProductInstance}, productID={slotProductID}, packaging={packaging}, quantity={quantity}");
            }


            if (Data.RequiredAmount > 0)
            {
                MelonLogger.Msg($"❌ Not enough amount delivered. {Data.RequiredAmount} remaining.");
                return;
            }


            deliveryEntry.Complete();
            rewardEntry.SetState(QuestState.Active);
            MelonCoroutines.Start(DelayedReward());
        }

        private void UpdateReward(int total, ProductInstance? item)
        {
            ProductDefinition itemDefinition = (ProductDefinition)(item?.Definition);
            //Dummy Reward calculation - to be replaced with effects and quality calculation
            MelonLogger.Msg($"Item Definition: {itemDefinition?.Name}");
            MelonLogger.Msg($"Item Quality: {itemDefinition?.Price}");
            Data.Reward = total * (int)itemDefinition.Price;
            return;
            //throw new NotImplementedException();
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
            var buyer= Contacts.GetBuyer(Data.DealerName);
            buyer.SendCustomMessage("Reward", Data.ProductID);
            buyer.GiveReputation((int)Data.RepReward);
            MelonLogger.Msg($"   Rewarded : ${rewardAmount} and Rep {Data.RepReward} to {Data.DealerName}");
            buyer.UnlockDrug();
            

            QuestActive = false;
            CompletedQuestKeys.Add($"{Data.ProductID}_{Data.RequiredAmount}");
            rewardEntry?.Complete();
            Complete();
            OnQuestCompleted?.Invoke();
            // Based on New Reputations, Check JSON to see if new NPC unlocked - Remove and Replace with button if slow/crash
            Contacts.Initialize();
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
                ? $"Deliver {Data.RequiredAmount}x {Data.ProductID} to {Data.DealerName}"
                : "Silkroad Delivery";

        protected override string Description =>
            !string.IsNullOrEmpty(Data?.ProductID) && Data.RequiredAmount > 0
                ? $"{Data.Task}"
                : "Deliver the assigned product to the stash location.";
    }
}
