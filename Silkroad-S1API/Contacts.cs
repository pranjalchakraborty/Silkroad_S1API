using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;

namespace Silkroad
{
    public static class Contacts
    {
        //Bypass to set NPC image as override is called before constructor
        public static string CurrentBuyerImage { get; private set; }
        
        public static Dictionary<string, BlackmarketBuyer> Buyers { get; private set; } = new Dictionary<string, BlackmarketBuyer>();

        public static void Initialize()
        {
            Buyers[BlackmarketBuyer.SavedNPCName]= new BlackmarketBuyer();
            // Load dealer data
            string jsonPath = Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", "empire.json");
            if (!File.Exists(jsonPath))
            {
                MelonLogger.Error("❌ empire.json file not found.");
                return;
            }

            try
            {
                string jsonContent = File.ReadAllText(jsonPath);
                var dealerData = JsonConvert.DeserializeObject<DealerData>(jsonContent);

                if (dealerData?.Dealers == null || dealerData.Dealers.Count == 0)
                {
                    MelonLogger.Error("❌ No dealers found in empire.json.");
                    return;
                }

                foreach (var dealer in dealerData.Dealers)
                {
                    
                    CurrentBuyerImage = Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", dealer.Image ?? string.Empty);
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