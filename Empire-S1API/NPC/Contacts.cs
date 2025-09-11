using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using S1API.Logging;
using S1API.Entities.NPCs;
using UnityEngine;
using S1API.GameTime;

namespace Empire
{
    public static class Contacts
    {
        public static Dictionary<string, BlackmarketBuyer> Buyers { get; set; } = new Dictionary<string, BlackmarketBuyer>();
        public static bool IsInitialized { get; set; } = false;
        public static bool IsUnlocked { get; set; } = false;
        //public static BlackmarketBuyer saveBuyer { get; set; }
        public static Dealer standardDealer { get; set; } = new Dealer { Name = "Blackmarket Buyer", Image = "EmpireIcon_quest.png" };
        private static bool _isUpdateCoroutineRunning = false;

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
            var dealer = JSONDeserializer.dealerData.Dealers.FirstOrDefault(d => d.Name == dealerName);
            if (dealer == null)
            {
                MelonLogger.Error($"❌ Dealer not found: {dealerName}");
            }
            return dealer;
        }
        //GetDealerDataByIndex
        public static Dealer GetDealerDataByIndex(int index)
        {
            // Ensure dealer data is loaded
            if (JSONDeserializer.dealerData.Dealers == null || JSONDeserializer.dealerData.Dealers.Count == 0)
            {
                MelonLogger.Warning("⚠️ Dealer data not loaded yet, returning standard dealer");
                return standardDealer;
            }

            // If index out of range, return standard dealer
            if (index < 0 || index >= JSONDeserializer.dealerData.Dealers.Count)
            {
                //MelonLogger.Msg($"❌ Index {index} is out of range for dealers (count: {JSONDeserializer.dealerData.Dealers.Count}).");
                return standardDealer;
            }
            return JSONDeserializer.dealerData.Dealers.ElementAtOrDefault(index);
        }

        /// <summary>
        /// Reset Contacts static state between scene loads to avoid leaking over the previous session.
        /// </summary>
        public static void Reset()
        {
            Buyers.Clear();
            IsInitialized = false;
            IsUnlocked = false;
            BlackmarketBuyer.dealerDataIndex = 0;
            // Reset the dealer field to force re-initialization
            BlackmarketBuyer.dealer = null;
            _isUpdateCoroutineRunning = false; // Allow coroutine to be restarted
            MelonLogger.Msg("🧹 Empire Contacts state reset complete");
        }

        public static void Update()
        {
            // Prevent multiple coroutines from running
            if (_isUpdateCoroutineRunning)
            {
                MelonLogger.Msg("⚠️ Contacts Update coroutine already running, skipping...");
                return;
            }

            

            MelonLogger.Msg("Testing 100");
            _isUpdateCoroutineRunning = true;
            MelonLoader.MelonCoroutines.Start(UpdateCoroutine());
        }
        

        private static System.Collections.IEnumerator UpdateCoroutine()
        {
            while (!IsInitialized)
            {
                yield return null;
            }
            try
            {
                // Melonlogger Test
                //MelonLogger.Msg("Testing 101}");
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
                            if (buyer._DealerData.IntroDone == false) // First time Intro
                            {
                                buyer.SendCustomMessage("Intro");
                                MelonLogger.Msg($"✅ Dealer {buyer.DealerName} intro sent.");
                                buyer._DealerData.IntroDone = true; // Set IntroDone to true
                            }
                            
                            
                                if (buyer.Debt != null && buyer.Debt.TotalDebt > 0 && buyer._DealerData.DebtRemaining > 0)
                                {
                                    buyer.DebtManager = new DebtManager(buyer);
                                    MelonLogger.Msg($"❌ Dealer {buyer.DealerName} is locked due to debt: {buyer.Debt.TotalDebt}");
                                }

                            
                            MelonLogger.Msg($"✅ Initialized dealer: {buyer.DealerName}");
                        }
                        buyer.UnlockDrug();
                    }
                    else
                    {
                        MelonLogger.Msg($"⚠️ Dealer {buyer.DealerName} is locked (unlock requirements not met)");
                    }

                    //MelonLogger.Msg($"✅ Contacts.Buyers now contains {Buyers.Count} buyers.");
                    IsUnlocked = true;
                }

            }
            catch (Exception ex)
            {
                MelonLogger.Error($"❌ Unexpected error during Update: {ex}");
            }
            finally
            {
                // Reset the flag when coroutine completes
                _isUpdateCoroutineRunning = false;
            }
        }
    }
    //TODO - Move to JSON
    //create a public static class QualityColors that contains an array caller Color
public static class QualityColors
    {
        public static string[] Colors = new string[]
        {
            "#a84545", "#5bad38", "#358ecd", "#e93be9", "#ecb522"
        };
    }

    public static class JSONDeserializer
    {
        public static DealerData dealerData { get; set; } = new DealerData();
        //two public dictionary to store the EffectsName and EffectsDollarMult; and QualitiesName and QualitiesDollarMult Lists
        public static Dictionary<string, float> EffectsDollarMult { get; set; } = new Dictionary<string, float>();
        public static Dictionary<string, float> QualitiesDollarMult { get; set; } = new Dictionary<string, float>();
        public static List<float> RandomNumberRanges { get; set; } = new List<float>();
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
                    MelonLogger.Msg("JSON Content read. Deserializing");
                    try
                    {
                        // Deserialize the JSON content into DealerData object
                        dealerData = JsonConvert.DeserializeObject<DealerData>(jsonContent);
                    }
                    catch (JsonReaderException ex)
                    {
                        MelonLogger.Error($"❌ Failed to parse empire.json: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"❌ Unexpected error during initialization: {ex}");
                    }
                    MelonLogger.Msg("JSON Content deserialized");
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
            
            // Load additional JSON files in the Empire folder
            string empireFolder = Path.Combine(MelonEnvironment.ModsDirectory, "Empire");
            try
            {
                var additionalFiles = Directory.GetFiles(empireFolder, "*.json")
                    .Where(f => !f.Equals(jsonPath, StringComparison.OrdinalIgnoreCase));
                foreach (var file in additionalFiles)
                {
                    try
                    {
                        string additionalJson = File.ReadAllText(file);
                        var additionalData = JsonConvert.DeserializeObject<DealerData>(additionalJson);
                        if (additionalData?.Dealers != null && additionalData.Dealers.Count > 0)
                        {
                            // Only add dealers whose Name is not already present
                            var existingNames = new HashSet<string>(dealerData.Dealers.Select(d => d.Name), StringComparer.OrdinalIgnoreCase);
                            var newDealers = additionalData.Dealers
                                .Where(d => !string.IsNullOrWhiteSpace(d.Name) && !existingNames.Contains(d.Name))
                                .ToList();

                            dealerData.Dealers.AddRange(newDealers);
                            MelonLogger.Msg($"Loaded additional {newDealers.Count} dealers from {Path.GetFileName(file)}");
                        }
                    }
                    catch (Exception exFile)
                    {
                        MelonLogger.Error($"❌ Error reading/de-serializing additional file {Path.GetFileName(file)}: {exFile.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"❌ Unexpected error while scanning additional JSON files: {ex}");
            }

            // Create two dictionaries from EffectsName and EffectsDollarMult; and QualityTypes and QualitiesDollarMult Lists
            EffectsDollarMult = dealerData?.EffectsName?.Select((name, index) => new { name = name.Trim().ToLowerInvariant(), index })
                .ToDictionary(x => x.name, x => dealerData?.EffectsDollarMult?[x.index] ?? 0f);
            QualitiesDollarMult = (dealerData?.QualityTypes ?? new List<string>())
                .Select((name, index) => new { name = name.Trim().ToLowerInvariant(), index })
                .ToDictionary(x => x.name, x => dealerData?.QualitiesDollarMult?[x.index] ?? 0f);
            // Log both in MelonLogger
            MelonLogger.Msg($"Effects Dollar Mult: {string.Join(", ", EffectsDollarMult.Select(x => $"{x.Key}: {x.Value}"))}");
            MelonLogger.Msg($"Qualities Dollar Mult: {string.Join(", ", QualitiesDollarMult.Select(x => $"{x.Key}: {x.Value}"))}");
            RandomNumberRanges = dealerData?.RandomNumberRanges ?? new List<float>();
        }
    }
}