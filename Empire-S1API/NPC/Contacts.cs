using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using S1API.Logging;

namespace Empire
{
    public static class Contacts
    {
        public static Dictionary<string, BlackmarketBuyer> Buyers { get; set; } = new Dictionary<string, BlackmarketBuyer>();
        public static bool IsInitialized { get; set; } = false;
        public static bool IsUnlocked { get; set; } = false;
        public static DealerData dealerData { get; set; } = JSONDeserializer.dealerData;
        //public static BlackmarketBuyer saveBuyer { get; set; }
        public static Dealer standardDealer { get; set; } = new Dealer { Name = "Blackmarket Buyer", Image = "EmpireIcon_quest.png" };

        public static BlackmarketBuyer GetBuyer(string dealerName)
        {
            return Buyers.TryGetValue(dealerName, out var buyer) ? buyer : null;
        }
        //GetDealerDataByName
        public static Dealer GetDealerDataByName(string dealerName)
        {
            //If dealerName is null or empty or not found, return null
            if (string.IsNullOrEmpty(dealerName))
            {
                MelonLogger.Error("❌ dealerName is null or empty.");
                return null;
            }
            var dealer = dealerData.Dealers.FirstOrDefault(d => d.Name == dealerName);
            if (dealer == null)
            {
                MelonLogger.Error($"❌ Dealer not found: {dealerName}");
            }
            return dealer;
        }
        //GetDealerDataByIndex
        public static Dealer GetDealerDataByIndex(int index)
        {
            // If index out of range, return standard dealer
            if (index < 0 || index >= dealerData.Dealers.Count)
            {
                MelonLogger.Error($"❌ Index {index} is out of range for dealers.");
                return standardDealer;
            }
            return dealerData.Dealers.ElementAtOrDefault(index);
        }
        public static void Update()
        {
            BlackmarketBuyer testBuyer = new BlackmarketBuyer();
            MelonLoader.MelonCoroutines.Start(UpdateCoroutine());
        }

        private static System.Collections.IEnumerator UpdateCoroutine()
        {
            // Wait until IsInitialized is true - Change to Wait Logic as others - TODO
            while (!IsInitialized)
            {
                yield return null;
            }
            try
            {
                foreach (var buyer in Buyers.Values)
                {
                    
                    bool canUnlock = buyer.UnlockRequirements == null ||
                                     !buyer.UnlockRequirements.Any() ||
                                     buyer.UnlockRequirements.All(req =>
                                         GetBuyer(req.Name)?._DealerData.Reputation >= req.MinRep);

                    ////Log the buyer name and unlock status
                     MelonLogger.Msg($"Buyer: {buyer.DealerName}, Unlock Status: {canUnlock}");
                     //If cannot unlock, log the requirements and the current reputation
                        if (!canUnlock)
                        {
                            foreach (var req in buyer.UnlockRequirements)
                            {
                                var unlockBuyer = GetBuyer(req.Name);
                                if (unlockBuyer != null)
                                {
                                    MelonLogger.Msg($"Unlock Requirement: {req.Name}, Current Reputation: {unlockBuyer._DealerData.Reputation}, Required Reputation: {req.MinRep}");
                                }
                                else
                                {
                                    MelonLogger.Msg($"Unlock Requirement: {req.Name} not found.");
                                }
                            }
                        }
                       
                    if (canUnlock)
                    {
                        if (!buyer.IsInitialized)
                        {
                            buyer.IsInitialized = true;
                            if (buyer._DealerData.Reputation==0){
                                buyer.SendCustomMessage("Intro");
                            }
                        }
                        else
                        {
                            buyer.UnlockDrug();
                        }
                        
                        MelonLogger.Msg($"✅ Initialized dealer: {buyer.DealerName}");
                    }
                    else
                    {
                        MelonLogger.Msg($"⚠️ Dealer {buyer.DealerName} is locked (unlock requirements not met)");
                    }
                }
                IsUnlocked = true;
                MelonLogger.Msg($"✅ Contacts.Buyers now contains {Buyers.Count} buyers.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"❌ Unexpected error during Update: {ex}");
            }
        }
    }

    public static class JSONDeserializer
    {
        public static DealerData dealerData { get; set; } = new DealerData();
        public static void Initialize()
        {
            // Load dealer data
            string jsonPath = Path.Combine(MelonEnvironment.ModsDirectory, "Empire", "empire.json");
            if (!File.Exists(jsonPath))
            {
                MelonLogger.Error("❌ empire.json file not found.");
                return;
            }

            try
            {
                // Load dealer data if not already loaded (null or empty)
                if (dealerData.Dealers == null || dealerData.Dealers.Count == 0)
                {
                    MelonLogger.Msg("Loading dealer data from empire.json...");
                    string jsonContent = File.ReadAllText(jsonPath);
                    dealerData = JsonConvert.DeserializeObject<DealerData>(jsonContent);
                }

                if (dealerData?.Dealers == null || dealerData.Dealers.Count == 0)
                {
                    MelonLogger.Error("❌ No dealers found in empire.json.");
                    return;
                }
            }
            catch (JsonReaderException ex)
            {
                MelonLogger.Error($"❌ Failed to parse empire.json: {ex.Message}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"❌ Unexpected error during initialization: {ex}");
            }
            
        }
    }
}