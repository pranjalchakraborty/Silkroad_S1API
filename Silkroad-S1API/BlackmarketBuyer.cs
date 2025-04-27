using S1API.NPCs;
using System;
using System.Collections.Generic;
using System.Linq;
using S1API.Internal.Utils;
using S1API.PhoneApp;
using S1API.Utils;
using UnityEngine;
using Silkroad;
using S1API.Saveables;
using MelonLoader;
using MelonLoader.Utils;
using System.IO;

namespace Silkroad
{
    public class BlackmarketBuyer : NPC
    {
        public bool IsInitialized { get; private set; } = false;
        [SaveableField("DealerSaveData")]
        public DealerSaveData _DealerData;
        public List<UnlockRequirement> UnlockRequirements { get; set; } = new List<UnlockRequirement>(); // Updated to match JSON structure
        public List<Drug> Drugs { get; set; } = new List<Drug>(); // Initialize Drugs list
        public List<Shipping> Shippings { get; set; } = new List<Shipping>(); // Initialize Shippings list
        public Dialogue Dialogues { get; set; } = new Dialogue();

        public static string CurrentNPC = "Blackmarket Buyer";
        public string DealerName { get; private set; }= "Blackmarket Buyer";
        public string? DealerImage { get; private set; }=Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", "SilkRoadIcon_quest.png");
        //Parameterless Constructor for the S1API call
        public BlackmarketBuyer() : base(CurrentNPC.ToLower().Replace(" ", "_"),
            CurrentNPC.Split(' ')[0],
            CurrentNPC.Contains(' ') ? CurrentNPC.Substring(CurrentNPC.IndexOf(' ') + 1) : "")
        {   

            if (_DealerData!=null)
            {
                MelonLogger.Msg($"⚠️ Dealer {DealerName} already exists in Buyers dictionary.");
                return;
            }
            // Create default DealerSaveData with valid non-null collections.
            _DealerData = new DealerSaveData
            {
                DealerName = DealerName,
                Reputation = 0,
                ShippingTier = 0,
                MinDeliveryAmount = 1,
                StepDeliveryAmount = 1,
                MaxDeliveryAmount = 5
            };
IsInitialized= true; // Set the initialized flag to true
            // Register the default dealer save data so that later lookups won't return null.
            
        }
        public BlackmarketBuyer(Dealer dealer) : base(
            dealer.Name.ToLower().Replace(" ", "_"),
            dealer.Name.Split(' ')[0],
            dealer.Name.Contains(' ') ? dealer.Name.Substring(dealer.Name.IndexOf(' ') + 1) : "")
        {
            if (dealer == null)
                throw new ArgumentNullException(nameof(dealer));
            DealerName = dealer.Name;
            CurrentNPC = dealer.Name;// Use static string to set save/load directory
            new BlackmarketBuyer(); // Call the parameterless constructor to initialize the base class and save/load
            DealerImage = Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", dealer.Image);
            Dialogues = dealer.Dialogue;
            UnlockRequirements = dealer.UnlockRequirements ?? new List<UnlockRequirement>();
            Drugs = dealer.Drugs ?? new List<Drug>();
            Shippings = dealer.Shippings ?? new List<Shipping>();

            if (_DealerData != null && !dealer.resetSave)
            {
                MelonLogger.Msg($"⚠️ Dealer {dealer.Name} already exists in Buyers dictionary.");
                return;
            }
            dealer.resetSave = false; // Reset the save flag to false after using it
            // Create DealerSaveData with safe enumeration for dialogue, drugs, and effects.
            _DealerData = new DealerSaveData
            {
                DealerName = dealer.Name,
            };
            SendCustomMessage("Intro");
            UnlockDrug(); // Check if the dealer has any unlocked drugs based on reputation
            UpgradeShipping(); // Upgrade the shipping tier if possible
             // Save the dealer data to the Buyers dictionary
            IsInitialized = true;

            // Log initialization details
            MelonLogger.Msg($"✅ Dealer initialized: {DealerName}");
            //MelonLogger.Msg($"   Unlocked Drugs: {string.Join(", ", _DealerData.UnlockedDrugs)}");
            //MelonLogger.Msg($"   MinDeliveryAmount: {_DealerData.MinDeliveryAmount}, MaxDeliveryAmount: {_DealerData.MaxDeliveryAmount}");

        }

        protected override Sprite? NPCIcon
        {
            get
            {
                // Dynamically load the image based on the DealerImage of the current instance
                return ImageUtils.LoadImage(Contacts.CurrentBuyerImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", "SilkRoadIcon_quest.png"));
            }
        }
        protected override void OnLoaded()
        {
            base.OnLoaded();
            MelonCoroutines.Start(WaitForDealerSaveDataAndSendStatus());
        }

        protected override void OnCreated()
        {
            base.OnCreated();
            Debug.Log($"BlackmarketBuyer {DealerName} created.");
        }

        public static DealerSaveData GetDealerSaveData(string dealerName)
        {
            return Contacts.GetBuyer(dealerName)._DealerData;
        }
        //Saves the current dealer data to the Buyers Saveable Field dictionary

        public void GiveReputation(int amount)
        {
            _DealerData.Reputation += amount;
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
                    BonusDollar = d.BonusDollar,
                    BonusRep = d.BonusRep,
                    BaseDollarMult = d.BaseDollarMult,
                    BaseRepMult = d.BaseRepMult,
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
            var shippingList = Shippings ?? new List<Shipping>();
            if (_DealerData.ShippingTier < shippingList.Count - 1)
            {
                _DealerData.ShippingTier++;
                _DealerData.MinDeliveryAmount = shippingList[_DealerData.ShippingTier].MinAmount;
                _DealerData.StepDeliveryAmount = shippingList[_DealerData.ShippingTier].StepAmount;
                _DealerData.MaxDeliveryAmount = shippingList[_DealerData.ShippingTier].MaxAmount;
                MelonLogger.Msg($"✅ Shipping upgraded to tier {_DealerData.ShippingTier}.");
                return true;
            }
            else
            {
                MelonLogger.Msg($"⚠️ Shipping already at max tier {_DealerData.ShippingTier}.");
                return false;
            }
        } 

        public void SendCustomMessage(string messageType, string product="", int amount=0)
        {

            List<string> messages = Dialogues.GetType().GetProperty(messageType)?.GetValue(Dialogues) as List<string>;

            //throw melonloader error if message is null
            if (messages == null)
            {
                MelonLogger.Error($"❌ Message type '{messageType}' not found in Dialogue.");
                SendTextMessage(messageType);
                return;
            }
            string line = messages[RandomUtils.RangeInt(0, messages.Count)];

            string formatted = line
                .Replace("{product}", $"<color=#34AD33>{product}</color>")
                .Replace("{amount}", $"<color=#FF0004>{amount}x</color>");

            SendTextMessage(formatted);
        }



        //Possible Check to see if all dealers save data are initialized and send a message to the player
        private System.Collections.IEnumerator WaitForDealerSaveDataAndSendStatus()
        {
            float timeout = 5f;
            float waited = 0f;
            
            MelonLogger.Msg($"⏳ Waiting for dealer {DealerName} to be initialized...");
            // Wait for this specific dealer's data to be initialized
            while (!IsInitialized || _DealerData == null && waited < timeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (!IsInitialized || _DealerData == null)
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