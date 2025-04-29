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
        public BlackmarketBuyer buyer = new BlackmarketBuyer();
        private DeadDropInstance deliveryDrop;
        public static HashSet<string> CompletedQuestKeys = new HashSet<string>();
        private QuestEntry deliveryEntry;
        private QuestEntry rewardEntry;
        public static bool QuestActive = false;
        public static event Action OnQuestCompleted;

//move to time manager 
//TODO
private void TimeManagerOnDayPass()
        {
            //use syntax like += to add a new event handler to the DayPass event
            // Reduce the quest time by 1 day
            Data.DealTime -= 1;
            // Check if the quest time has expired
            if (Data.DealTime < 0)
            {
                //var buyer = Contacts.GetBuyer(Data.DealerName);
                // If the quest time has expired, fail the quest
                buyer.SendCustomMessage("Expire", Data.ProductID);
                Data.Reward = -Data.Penalties[0];
                Data.RepReward = -Data.Penalties[1];
                MelonCoroutines.Start(DelayedReward());
            }
    }
        protected override Sprite? QuestIcon
        {
            get
            {
                MelonLogger.Msg($"QuestIcon: {Data.QuestImage??"null"}");
                // Dynamically load the image based on the DealerImage of the current instance - Doesn't work
                //TODO
                return ImageUtils.LoadImage(Data.QuestImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", "SilkRoadIcon_quest.png"));
            }
        }
        protected override void OnLoaded()
        {
            base.OnLoaded();
            buyer = Contacts.GetBuyer(Data.DealerName);
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
        
        
        protected override void OnCreated()
        {

            base.OnCreated();
            buyer = Contacts.GetBuyer(Data.DealerName);
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
            MelonLogger.Msg($"Expecting ProductID: {Data.ProductID}, RequiredAmount: {Data.RequiredAmount}");
            if (Data.RequiredAmount <= 0)
            {
                
                buyer.SendCustomMessage("Incomplete", Data.ProductID);
                MelonLogger.Msg("❌ No required amount to deliver. Quest done.");
                deliveryEntry.Complete();
            rewardEntry.SetState(QuestState.Active);
            MelonCoroutines.Start(DelayedReward());
                return;
            }
            //necessary and optional effects are based on Data.RequiredDrug.Effects => Effect.Probability ==1 means necessary, else optional
            List<string> necessaryEffects = Data.RequiredDrug.Effects.Where(e => e.Probability == 1).Select(e => e.Name).ToList();
            List<string> optionalEffects = Data.RequiredDrug.Effects.Where(e => e.Probability < 1).Select(e => e.Name).ToList();

            foreach (var slot in deliveryDrop.Storage.Slots)
            {
                bool isProductInstance = slot.ItemInstance is ProductInstance;
                var item = ((ProductInstance)slot.ItemInstance);
                MelonLogger.Msg($"Slot: {item.Definition.Category} - {slot.Quantity} - {item.Definition.Name} ");
                string slotProductID = isProductInstance ? item.Definition.Name : "null";
                string packaging = isProductInstance ? item.AppliedPackaging.Name : "null";
                int quantity = slot.Quantity;
                //Temporary list to hold the effects and test with dummy values
                List<string> productEffects = RandomEffectSelector.GetRandomEffects(necessaryEffects, 8);
                //Check isProductInstance AND if productEffects contains ALL of the necessary effects
                //ADD non-dummy check for quality and effects
                if (isProductInstance && necessaryEffects.All(effect => productEffects.Contains(effect)))
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


            if (Data.RequiredAmount > 0)
            {
                MelonLogger.Msg($"❌ Not enough amount delivered. {Data.RequiredAmount} remaining.");
                return;
            }


            
        }

        //Update Dummy with real effect and quality calculation
        private void UpdateReward(uint total, ProductInstance? item)
        {
            var itemDef=ItemManager.GetItemDefinition(item?.Definition.ID);
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

        private System.Collections.IEnumerator DelayedReward()
        {
            yield return new WaitForSeconds(RandomUtils.RangeInt(120, 200));
            GiveReward();
        }

        private void GiveReward()
        {
            var rewardAmount = Data.Reward;
            ConsoleHelper.RunCashCommand(rewardAmount);
            buyer.SendCustomMessage("Reward", Data.ProductID);
            buyer.GiveReputation((int)Data.RepReward + 10);//todo
            MelonLogger.Msg($"   Rewarded : ${rewardAmount} and Rep {Data.RepReward + 10} to {Data.DealerName}");
            buyer.UnlockDrug();
            buyer.IncreaseCompletedDeals(1);
            buyer.SaveDealerData();


            QuestActive = false;
            CompletedQuestKeys.Add($"{Data.ProductID}_{Data.RequiredAmount}");
            rewardEntry?.Complete();
            Complete();
            OnQuestCompleted?.Invoke();
            // Based on New Reputations, Check JSON to see if new NPC unlocked - Remove and Replace with button if slow/crash
            Contacts.Initialize();
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
