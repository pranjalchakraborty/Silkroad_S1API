using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;

namespace Empire
{
    public static class Contacts
    {
        //Bypass to set NPC image as override is called before constructor
        public static string CurrentBuyerImage { get; private set; }
        public static Dictionary<string, BlackmarketBuyer> Buyers { get; set; } = new Dictionary<string, BlackmarketBuyer>();
        public static BlackmarketBuyer saveBuyer { get; set; }
        public static bool IsInitialized { get; private set; } = false;
        public static DealerData dealerData { get; private set; } = new DealerData();

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

                foreach (var dealer in dealerData.Dealers)
                {
                    // Continue if dealer is already initialized in Buyers
                    if (Buyers.ContainsKey(dealer.Name))
                    {
                        MelonLogger.Msg($"⚠️ Dealer {dealer.Name} is already initialized.");
                        continue;
                    }
                    CurrentBuyerImage = Path.Combine(MelonEnvironment.ModsDirectory, "Empire", dealer.Image ?? string.Empty);
                    // Check if dealer has no unlock requirements (initially available)
                    // or if all their unlock requirements are met
                    bool canUnlock = dealer.UnlockRequirements == null ||
                                   !dealer.UnlockRequirements.Any() ||
                                   dealer.UnlockRequirements.All(req =>
                                       Contacts.GetBuyer(req.Name)?._DealerData.Reputation >= req.MinRep);

                    if (canUnlock)
                    {
                        var buyer = new BlackmarketBuyer(dealer);
                        Buyers[dealer.Name] = buyer;

                        MelonLogger.Msg($"✅ Initialized dealer: {dealer.Name}");
                    }
                    else
                    {
                        MelonLogger.Msg($"⚠️ Dealer {dealer.Name} is locked (unlock requirements not met)");
                    }
                }
                IsInitialized = true;
                MelonLogger.Msg($"✅ Contacts.Buyers now contains {Buyers.Count} buyers.");
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

        public static BlackmarketBuyer GetBuyer(string dealerName)
        {
            return Buyers.TryGetValue(dealerName, out var buyer) ? buyer : null;
        }
    }
}