using System;
using System.Collections.Generic;
using System.Linq;
using S1API.Internal.Utils;
using S1API.PhoneApp;
using S1API.Entities;
using UnityEngine;
using Empire;
using S1API.Saveables;
using MelonLoader;
using MelonLoader.Utils;
using System.IO;

namespace Empire
{
    public class BlackmarketBuyer : NPC
    {
        public bool IsInitialized { get; private set; } = false;
        public DealerSaveData _DealerData;
        public List<UnlockRequirement> UnlockRequirements { get; set; } = new List<UnlockRequirement>();
        private List<Drug> Drugs = new List<Drug>(); // Initialize Drugs list
        public List<Shipping> Shippings { get; set; } = new List<Shipping>(); // Initialize Shippings list
        private Dialogue Dialogues = new Dialogue();
        public float RepLogBase { get; set; } = 0.1f; // Base value for reputation log
        public List<List<float>> Deals { get; set; } = new List<List<float>>(); // List of Deals
        public static string SavedNPCName { get; private set; } = "Blackmarket Buyer"; // Static string to set save/load directory
        public string DealerName { get; private set; } = SavedNPCName;
        public string? DealerImage { get; private set; } = Path.Combine(MelonEnvironment.ModsDirectory, "Empire", "EmpireIcon_quest.png");
        [SaveableField("BuyerSaveData")]
        private BuyerSaveData BuyerSaveData = new BuyerSaveData();
        static Sprite? npcSprite => ImageUtils.LoadImage(Contacts.CurrentBuyerImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Empire", "EmpireIcon_quest.png"));

        //Parameterless Constructor for the S1API call
        public BlackmarketBuyer() : base(SavedNPCName.ToLower().Replace(" ", "_"),
            SavedNPCName.Split(' ')[0],
            SavedNPCName.Contains(' ') ? SavedNPCName.Substring(SavedNPCName.IndexOf(' ') + 1) : "", npcSprite)
        {
            MelonLogger.Msg($"BlackmarketBuyer () Constructor {SavedNPCName} created.");
             Contacts.Buyers[SavedNPCName] = this;
             //Melonlogger - number of elements in contacts.Buyers
            //MelonLogger.Msg($"2Contacts.Buyers Count: {Contacts.Buyers.Count}");
            // Ensure BuyerSaveData is initialized
            if (BuyerSaveData == null)
            {
                MelonLogger.Warning($"⚠️ BuyerSaveData was null. .");

            }
            else if (BuyerSaveData.Dealers == null)
            {
                BuyerSaveData.Dealers = new Dictionary<string, DealerSaveData>();
                MelonLogger.Warning($"⚠️ Dealers dictionary was null. Initialized a new dictionary.");
            }
            // Ensure Dealers dictionary is initialized

            //try to load the dealer data from the save file with error catch - overwrite save
            try
            {
                LoadDealerData();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"❌ Error loading dealer data: {ex.Message}");

            }

            if (_DealerData == null)
            {
                _DealerData = new DealerSaveData
                {
                    
                };
                SaveDealerData();
            }
            else
            {
                MelonLogger.Msg($"⚠️ Dealer {DealerName} already exists in BuyerSaveData dictionary.");
            }

        }
        public BlackmarketBuyer(Dealer dealer) : base(
            dealer.Name.Trim().ToLower().Replace(" ", "_"),
            dealer.Name.Trim().Split(' ')[0],
            dealer.Name.Trim().Contains(' ') ? dealer.Name.Trim().Substring(dealer.Name.Trim().IndexOf(' ') + 1) : "", npcSprite)
        {
            if (dealer == null)
                throw new ArgumentNullException(nameof(dealer));
            DealerName = dealer.Name.Trim();
            DealerImage = Path.Combine(MelonEnvironment.ModsDirectory, "Empire", dealer.Image);
            Dialogues = dealer.Dialogue;
            UnlockRequirements = dealer.UnlockRequirements ?? new List<UnlockRequirement>();
            Drugs = dealer.Drugs ?? new List<Drug>();
            Shippings = dealer.Shippings ?? new List<Shipping>();
            Deals = dealer.Deals ?? new List<List<float>>();
            RepLogBase = dealer.RepLogBase;
            try
            {
                LoadDealerData();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"❌ Error loading dealer data: {ex.Message}");

            }
            if (_DealerData != null)
            {
                MelonLogger.Msg($"⚠️ Dealer {DealerName} already exists in BuyerSaveData dictionary.");
            }
            else
            {
                // Create DealerSaveData with safe enumeration for dialogue, drugs, and effects.
                _DealerData = new DealerSaveData
                {
                    
                };
                SendCustomMessage("Intro");
            }
            UnlockDrug(); // Check if the dealer has any unlocked drugs based on reputation
            SaveDealerData();  // Save the dealer data to the BuyerSaveData dictionary
            IsInitialized = true;
            MelonLogger.Msg($"✅ Dealer initialized: {DealerName}");
        }
        public void LoadDealerData()
        {
            var buyer = Contacts.GetBuyer(SavedNPCName);
            if (buyer == null)
            {
                MelonLogger.Error($"❌ Buyer with name '{SavedNPCName}' not found.");
                return;
            }

            if (buyer.BuyerSaveData == null)
            {
                MelonLogger.Error($"❌ BuyerSaveData is null for buyer '{SavedNPCName}'. Initializing a new BuyerSaveData.");
                //buyer.BuyerSaveData = new BuyerSaveData(); // Ensure BuyerSaveData is initialized
                return;
            }

            if (buyer.BuyerSaveData.Dealers == null)
            {
                MelonLogger.Error($"❌ Dealers dictionary is null for buyer '{SavedNPCName}'. Initializing a new dictionary.");
                //buyer.BuyerSaveData.Dealers = new Dictionary<string, DealerSaveData>(); // Ensure Dealers is initialized
                return;
            }

            if (buyer.BuyerSaveData.Dealers.TryGetValue(DealerName, out var dealerData))
            {
                //DebugUtils.LogObjectJson(dealerData, $"{DealerName} DealerSaveData");
                _DealerData = dealerData;
                MelonLogger.Msg($"✅ Dealer data loaded for {DealerName}.");
                //DebugUtils.LogObjectJson(_DealerData, $"{DealerName} DealerSaveData");
            }
            else
            {
                MelonLogger.Error($"❌ Dealer data not found for {DealerName}.");
            }
        }

        public void SaveDealerData()
        {
            var buyer = Contacts.GetBuyer(SavedNPCName);
            if (buyer == null)
            {
                MelonLogger.Error($"❌ Buyer with name '{SavedNPCName}' not found. Cannot save dealer data.");
                return;
            }

            if (buyer.BuyerSaveData == null)
            {
                MelonLogger.Warning($"⚠️ BuyerSaveData was null for buyer '{SavedNPCName}'. Initialized a new dictionary.");
                return;
            }

            if (buyer.BuyerSaveData.Dealers == null)
            {
                MelonLogger.Warning($"⚠️ Dealers dictionary was null for buyer '{SavedNPCName}'. Initialized a new dictionary.");
                //buyer.BuyerSaveData.Dealers = new Dictionary<string, DealerSaveData>();
                return;
            }

            buyer.BuyerSaveData.Dealers[DealerName] = _DealerData;
            MelonLogger.Msg($"✅ Dealer data saved for {DealerName}.");
        }

        protected override void OnLoaded()
        {
            MelonLogger.Msg($"BlackmarketBuyer {DealerName} ONloaded.");
            base.OnLoaded();
            IsInitialized = true;
            MelonCoroutines.Start(WaitForDealerSaveDataAndSendStatus());
            //Melonlogger - number of elements in contacts.Buyers
            //MelonLogger.Msg($"3Contacts.Buyers Count: {Contacts.Buyers.Count}");
            //DebugUtils.LogObjectJson(Contacts.GetBuyer(SavedNPCName).BuyerSaveData, "BlackmarketBuyer DealerSaveData");
        }

        protected override void OnCreated()
        {
            base.OnCreated();
            Debug.Log($"BlackmarketBuyer {DealerName} ONcreated.");
            IsInitialized = true;
        }

        public static DealerSaveData GetDealerSaveData(string dealerName)
        {
            return Contacts.GetBuyer(dealerName)._DealerData;
        }
        //Saves the current dealer data to the BuyerSaveData Saveable Field dictionary

        public void IncreaseCompletedDeals(int amount)
        {
            _DealerData.DealsCompleted += amount;
            MelonLogger.Msg($"✅ {DealerName}'s completed deals increased by {amount}. Total Completed Deals: {_DealerData.DealsCompleted}");
        }
        public void GiveReputation(int amount)
        {
            _DealerData.Reputation += amount;
            //if reputation <1 make it 1
            if (_DealerData.Reputation < 1)
            {
                _DealerData.Reputation = 1;
            }
            //if reputation > 100 make it 100 - TODO - Once Global Reputation is implemented, make this NPC specific relationship
            /*if (_DealerData.Reputation > 100)
            {
                _DealerData.Reputation = 100;
            }*/
            MelonLogger.Msg($"✅ {DealerName}'s reputation increased by {amount}. New Reputation: {_DealerData.Reputation}");
        }
        //A method to check if the new reputation unlocks any new drug, quality or effects for the dealer 
        public void UnlockDrug()
        {
            // Initialize the drug list
            var drugList = Drugs ?? new List<Drug>();

            // Filter drugs based on the current reputation
            var validDrugs = drugList
                .Where(d => d.UnlockRep <= _DealerData.Reputation) // Unlock drugs with UnlockRep <= current reputation
                .Select(d => new Drug
                {
                    Type = d.Type,
                    UnlockRep = d.UnlockRep,
                    BaseDollar = d.BaseDollar,
                    BaseRep = d.BaseRep,
                    BaseXp = d.BaseXp,
                    RepMult = d.RepMult,
                    XpMult = d.XpMult,
                    // Unlock qualities where UnlockRep <= current reputation
                    Qualities = d.Qualities
                        .Where(q => q.UnlockRep <= _DealerData.Reputation)
                        .ToList(),
                    // Unlock effects where UnlockRep <= current reputation
                    Effects = d.Effects
                        .Where(e => e.UnlockRep <= _DealerData.Reputation)
                        .ToList()
                })
                .ToList();

            // Update the DealerSaveData with the unlocked drugs
            _DealerData.UnlockedDrugs = validDrugs;

            // Log the unlocked drugs for debugging
            MelonLogger.Msg($"   Found {validDrugs.Count} drug(s) unlocked for dealer '{DealerName}' at rep {_DealerData.Reputation}.");
            foreach (var drug in validDrugs)
            {
                MelonLogger.Msg($"      Drug: {drug.Type} | UnlockRep: {drug.UnlockRep}");
                MelonLogger.Msg($"         Unlocked Qualities: {string.Join(", ", drug.Qualities.Select(q => q.Type))}");
                MelonLogger.Msg($"         Unlocked Effects: {string.Join(", ", drug.Effects.Select(e => e.Name))}");
            }
        }


        //A method that upgrades ShippingTier to the next available shipping option
        public bool UpgradeShipping()
        {
            if (_DealerData.ShippingTier < Shippings.Count - 1)
            {
                _DealerData.ShippingTier++;
                MelonLogger.Msg($"✅ Shipping upgraded to tier {_DealerData.ShippingTier}.");
                return true;
            }
            else
            {
                MelonLogger.Msg($"⚠️ Shipping already at max tier {_DealerData.ShippingTier}.");
                return false;
            }
        }

        //Send the message to the player using the phone app or return the message string only if returnMessage is true
        public string SendCustomMessage(string messageType, string product = "", int amount = 0, string quality = "", List<string>? necessaryEffects = null, List<string> optionalEffects = null, bool returnMessage = false)
        {

            List<string> messages = Dialogues.GetType().GetProperty(messageType)?.GetValue(Dialogues) as List<string>;

            //throw melonloader error if message is null
            if (messages == null)
            {
                MelonLogger.Error($"❌ Message type '{messageType}' not found in Dialogue.");
                if (returnMessage)
                {
                    return messageType;
                }
                else
                {
                    SendTextMessage(messageType);
                    return null;
                }
            }
            string line = messages[RandomUtils.RangeInt(0, messages.Count)];

            string formatted = line
                .Replace("{product}", $"<color=#34AD33>{product}</color>")
                .Replace("{amount}", $"<color=#FF0004>{amount}x</color>")
                .Replace("{quality}", $"<color=#FF0004>{quality}</color>");
            if (necessaryEffects != null && necessaryEffects.Count > 0)
            {
                string effects = string.Join(", ", necessaryEffects.Select(e => $"<color=#FF0004>{e}</color>"));
                formatted = formatted.Replace("{effects}", effects);
            }
            else
            {
                formatted = formatted.Replace("{effects}", "none");
            }
            if (optionalEffects != null && optionalEffects.Count > 0)
            {
                string effects = string.Join(", ", optionalEffects.Select(e => $"<color=#FF0004>{e}</color>"));
                formatted = formatted.Replace("{optionalEffects}", effects);
            }
            else
            {
                formatted = formatted.Replace("{optionalEffects}", "none");
            }
            if (returnMessage)
            {
                return formatted;
            }
            else
            {
                SendTextMessage(formatted);
                return null;
            }
        }



        //Possible Check to see if all dealers save data are initialized and send a message to the player
        private System.Collections.IEnumerator WaitForDealerSaveDataAndSendStatus()
        {
            float timeout = 5f;
            float waited = 0f;

            MelonLogger.Msg($"⏳ WaitForDealerSaveDataAndSendStatus- Waiting for dealer {DealerName} to be initialized...");
            // Check if all buyer.IsInitialised is false in Contacts.Buyers 
            // If it is, wait until it is true or the timeout is reached
            foreach (var buyer in Contacts.Buyers.Values)
            {
                if (!buyer.IsInitialized && waited < timeout)
                {
                    MelonLogger.Msg($"⏳ Waiting for dealer {buyer.DealerName} to be initialized...");
                    waited += Time.deltaTime;
                    yield return null;
                }
            }

            if (!IsInitialized)
            {
                // If the dealer data is still not initialized after the timeout, log a warning
                MelonLogger.Warning($"⚠️ Dealer {DealerName} not initialized after timeout");
                yield break;
            }


            MelonLogger.Msg($"✅ Dealer {DealerName} initialized with save data");
           


            // Additional initialization logic can go here
            // For example, syncing reputation, unlocks, etc.
        }
    }


}