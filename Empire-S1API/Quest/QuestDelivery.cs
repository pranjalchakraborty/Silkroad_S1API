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
using S1API.Quests.Constants;
using Random = UnityEngine.Random;
using System.IO;
using MelonLoader.Utils;



namespace Empire
{
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
            // Check if the quest time has expired and rewardEntry is not active
            if (Data.DealTime < 0 && rewardEntry.State!=QuestState.Active)
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
            ConsoleHelper.SetLawIntensity((float)2*buyer.Tier);// TODO - Expose Deal Heat thru JSON
        }


        protected override void OnCreated()
        {
            MelonLogger.Msg("Quest OnCreated called.");
            base.OnCreated();
            MelonLogger.Msg($"QuestOnCreated() done.");
            
            buyer = Contacts.GetBuyer(Data.DealerName);
            ConsoleHelper.SetLawIntensity((float)2*buyer.Tier);// TODO - Expose Deal Heat thru JSON
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
            // If buyer.CurfewDeal is true and TimeManager.IsNight is false, return and Log
            if (buyer.CurfewDeal && !TimeManager.IsNight)
            {
                MelonLogger.Msg("❌ Curfew deal is true but it is not night. Cannot deliver.");
                buyer.SendCustomMessage("Deliveries only after Curfew.", Data.ProductID, (int)Data.RequiredAmount, Data.Quality, Data.NecessaryEffects, Data.OptionalEffects);
                return;
            }

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
                    buyer.SendCustomMessage("This is not even a product...");
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
                S1API.Products.ProductDefinition productDef = ProductManager.DiscoveredProducts.FirstOrDefault(p => p.ID == item?.Definition.ID);
                var productType = GetProductType(productDef);


                if (productType != Data.ProductID)
                {
                    MelonLogger.Error($"❌ Product type mismatch: {productType} != {Data.ProductID}");
                    buyer.SendCustomMessage("This is not the drug type I ordered.");
                    continue;
                }

                var props = productDef.Properties; 
                if (productDef is WeedDefinition weed)
                    props = weed.GetProperties();
                else if (productDef is MethDefinition meth)
                    props = meth.GetProperties();
                else if (productDef is CocaineDefinition coke)
                    props = coke.GetProperties();

                MelonLogger.Msg($"count : {props.Count}");
                var properties = new List<string>();
                if (props.Count > 0)
                {
                    for (int i = 0; i < props.Count; i++)
                    {
                        var prop = props[i];
                        properties.Add(prop.name.Trim().ToLower());
                    }
                }
                MelonLogger.Msg($"Item Properties: {string.Join(", ", properties)}");
                // Melonlogger the Data.NecessaryEffects and OptionalEffects
                MelonLogger.Msg($"NecessaryEffects: {string.Join(", ", Data.NecessaryEffects)}");
                MelonLogger.Msg($"OptionalEffects: {string.Join(", ", Data.OptionalEffects)}");

                if (!Data.NecessaryEffects.All(effect => properties.Contains(effect.Trim().ToLower())))
                {
                    MelonLogger.Error($"❌ Effect type mismatch");
                    buyer.SendCustomMessage("All the required necessary effects are not present.");
                    continue;
                }
                var quality = item?.Quality ?? 0;
                MelonLogger.Msg($"Quality: {quality}");
                // convert the quality enum to a lower trim quality string
                string qualityString = quality.ToString().ToLower().Trim();
                int qualityNumber = GetQualityNumber(qualityString);
                // Check if the quality is within the required range after converting quality enum to string.trim.lower
                if (qualityNumber < GetQualityNumber(Data.Quality))
                {
                    MelonLogger.Error($"❌ Quality mismatch: {quality} < {GetQualityNumber(Data.Quality)} or {quality} > {GetQualityNumber(Data.Quality)}");
                    buyer.SendCustomMessage("The quality of the product is worse than what I ordered.");
                    continue;
                }
                if (isProductInstance)
                {
                    uint total = (uint)(quantity * PackageAmount(packaging));
                    if (total <= Data.RequiredAmount)
                    {
                        slot.AddQuantity(-quantity);
                        UpdateReward(total, productDef, properties);
                        Data.RequiredAmount -= total;
                        MelonLogger.Msg($"✅ Delivered {total}x {slotProductID} to the stash. Remaining: {Data.RequiredAmount}. Reward now: {Data.Reward}");
                    }
                    else
                    {
                        //FLOOR of the negative of the division to get the number of packages to remove
                        int toRemove = (int)Math.Ceiling((float)Data.RequiredAmount / PackageAmount(packaging));
                        toRemove = Math.Min(toRemove, slot.Quantity);
                        slot.AddQuantity(-toRemove);
                        UpdateReward(Data.RequiredAmount, productDef, properties);
                        Data.RequiredAmount = 0;
                        MelonLogger.Msg($"✅ Delivered {total}x {slotProductID} to the stash. Remaining: {Data.RequiredAmount}. Reward now: {Data.Reward}");
                        break;
                    }
                }
            }
            if (Data.RequiredAmount <= 0 && deliveryEntry.State!=QuestState.Completed)
            {
                //MelonLogger.Msg("Test2");
                buyer.SendCustomMessage("Success", Data.ProductID, (int)Data.RequiredAmount, Data.Quality, Data.NecessaryEffects, Data.OptionalEffects,Data.Reward);
                MelonLogger.Msg("❌ No required amount to deliver. Quest done.");
                deliveryDrop.Storage.OnClosed -= CheckDelivery;
                deliveryEntry.Complete();
                rewardEntry.SetState(QuestState.Active);
                MelonCoroutines.Start(DelayedReward("Completed"));

            }
            else if (Data.RequiredAmount > 0)
            {
                buyer.SendCustomMessage("Incomplete", Data.ProductID, (int)Data.RequiredAmount, Data.Quality, Data.NecessaryEffects, Data.OptionalEffects);
                MelonLogger.Msg($"Continue delivery. Remaining amount: {Data.RequiredAmount}");
            }
        }

        //A method that checks class type of a product definition. If it WeedDefinition, return weed string, MethDefinition return meth string, CocaineDefinition return cocaine string, else return null.
        //TODO - UPDATABLE
        private string? GetProductType(ProductDefinition? productDef)
        {
            if (productDef is WeedDefinition)
            {
                return "weed";
            }
            else if (productDef is MethDefinition)
            {
                return "meth";
            }
            else if (productDef is CocaineDefinition)
            {
                return "cocaine";
            }
            else
            {
                return null;
            }
        }
        //A method that checks type of a product quality. return quality number. Takes arg as Data.quality string and returns Contacts.QualitiesDollarMult index where the key is the quality string.
        //TODO - UPDATABLE
        private int GetQualityNumber(string quality)
        {
            // Check if the quality is null or empty
            if (string.IsNullOrEmpty(quality))
            {
                MelonLogger.Error("❌ Quality is null or empty.");
                return -1;
            }
            // Check if the quality exists in the dictionary
            if (!JSONDeserializer.QualitiesDollarMult.ContainsKey(quality.ToLower().Trim()))
            {
                MelonLogger.Error($"❌ Quality not found: {quality}");
                return -1;
            }
            //Iterate the dictionary and return index where the key is the quality string
            for (int i = 0; i < JSONDeserializer.QualitiesDollarMult.Count; i++)
            {
                if (JSONDeserializer.QualitiesDollarMult.ElementAt(i).Key == quality.ToLower().Trim())
                {
                    return i;
                }
            }
            return -1;
        }

        private void UpdateReward(uint total, ProductDefinition? productDef, List<string> properties)
        {
            // Check if productDef is null or not a ProductDefinition
            if (productDef == null)
            {
                MelonLogger.Error("❌ Product definition is null or not a ProductDefinition. Reward calculation skipped.");
                return;
            }
            var qualityMult = Data.QualityMult;
            var requiredQuality = GetQualityNumber(Data.Quality);
            // Sum of all in Data.NecessaryEffectMult
            float EffectsSum = Data.NecessaryEffectMult.Sum();
            //  Add Data.OptionalEffectMult[index] to EffectsSum if key is present in properties for Data.OptionalEffects[index]
            for (int i = 0; i < Data.OptionalEffects.Count; i++)
            {
                if (properties.Contains(Data.OptionalEffects[i]))
                {
                    EffectsSum += Data.OptionalEffectMult[i];
                }
            }
            Data.Reward += (int)(total * productDef.Price * (1 + qualityMult) * Data.DealTimeMult * (1 + EffectsSum));
            MelonLogger.Msg($"   Reward updated: {Data.Reward} with Price: {productDef.Price}, Quality: {qualityMult} and EffectsSum: {EffectsSum} and DealTimeMult: {Data.DealTimeMult}.");
        }



        //Call with QuestState to be set as string - UPDATABLE
        private System.Collections.IEnumerator DelayedReward(string source)
        {
            yield return new WaitForSeconds(RandomUtils.RangeInt(15, 30));
            Data.RepReward += (int)(Data.Reward * Data.RepMult);
            Data.XpReward += (int)(Data.Reward * Data.XpMult);
            GiveReward(source);
        }

        private void GiveReward(string source)
        {
            TimeManager.OnDayPass -= ExpireCountdown;
            ConsoleHelper.GiveXp(Data.XpReward);
            buyer.GiveReputation((int)Data.RepReward);
            // Pay the reward or debt
            if (buyer._DealerData.DebtRemaining > 0)
            {
                // If debt remaining < reward, set it to 0 and pay the rest
                if (buyer._DealerData.DebtRemaining <= buyer.Debt.ProductBonus * Data.Reward)
                {
                    Data.Reward -= (int)(buyer._DealerData.DebtRemaining / buyer.Debt.ProductBonus);
                    buyer._DealerData.DebtRemaining = 0;
                    //buyer.SendCustomMessage("Congrats! You Paid off the debt.");
                    MelonLogger.Msg($"   Paid off debt to {buyer.DealerName}");
                    Money.ChangeCashBalance(Data.Reward);
                }
                else
                {
                    MelonLogger.Msg($"   Paid off debt: ${Data.Reward} to {buyer.DealerName}");
                    buyer._DealerData.DebtRemaining -= buyer.Debt.ProductBonus * Data.Reward;
                    buyer._DealerData.DebtPaidThisWeek += buyer.Debt.ProductBonus * Data.Reward;
                }
                buyer.DebtManager.SendDealDebtMessage();    
            }
            else
            {
                Money.ChangeCashBalance(Data.Reward);
            }

            MelonLogger.Msg($"   Rewarded : ${Data.Reward} and Rep {Data.RepReward} and Xp {Data.XpReward} from {Data.DealerName}");

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
                buyer.SendCustomMessage("Reward", Data.ProductID, (int)Data.RequiredAmount, Data.Quality, Data.NecessaryEffects, Data.OptionalEffects,Data.Reward);
                buyer.IncreaseCompletedDeals(1);
                buyer.UnlockDrug();
                Contacts.Update();
                Complete();
                QuestActive = false;
                Active = null;

            }
            else
            {
                MelonLogger.Error($"❌ Unknown source: {source}.");
                return;
            }
            MyApp.Instance.OnQuestComplete();
            //ConsoleHelper.SetLawIntensity(1f);
            rewardEntry?.Complete();
            // Trigger the event with no payload
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
