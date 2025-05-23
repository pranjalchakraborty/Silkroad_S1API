﻿using System;
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
        public static int dealerDataIndex = 0;
        //public int Index { get; set; } = dealerDataIndex;
        public static Dealer dealer = Contacts.GetDealerDataByIndex(dealerDataIndex);
        public bool IsInitialized { get; set; } = false;
        [SaveableField("DealerSaveData")]
        public DealerSaveData _DealerData;
        public List<UnlockRequirement> UnlockRequirements { get; set; } = new List<UnlockRequirement>();
        private List<Drug> Drugs = new List<Drug>(); // Initialize Drugs list
        public List<Shipping> Shippings { get; set; } = new List<Shipping>(); // Initialize Shippings list
        private Dialogue Dialogues = new Dialogue();
        public float RepLogBase { get; set; } = 0.1f; // Base value for reputation log
        public List<List<float>> Deals { get; set; } = new List<List<float>>(); // List of Deals
        public string DealerName { get; private set; }
        public string? DealerImage { get; private set; }
        public int Tier { get; set; }
        public List<string> DealDays { get; set; }
        public DebtManager DebtManager { get; set; } // Initialize DebtManager object
        public bool CurfewDeal { get; set; } = false;
        public int RefreshCost { get; set; }
        public Debt Debt { get; set; } = new Debt(); // Initialize Debt object
        public Gift Gift { get;  set; }
        public DealerReward Reward { get; set; }

        public RewardManager RewardManager { get; set; } // Initialize RewardManager object
        static Sprite? npcSprite => ImageUtils.LoadImage(
            string.IsNullOrEmpty(dealer?.Image)
                ? Path.Combine(MelonEnvironment.ModsDirectory, "Empire", "EmpireIcon_quest.png")
                : Path.Combine(MelonEnvironment.ModsDirectory, "Empire", dealer.Image)
        );

        //Parameterless Constructor for the S1API call
        public BlackmarketBuyer() : base(
            dealer.Name.Trim().ToLower().Replace(" ", "_"),
            dealer.Name.Trim().Split(' ')[0],
            dealer.Name.Trim().Contains(' ') ? dealer.Name.Substring(dealer.Name.IndexOf(' ') + 1) : "", npcSprite)
        {
            DealerName = dealer.Name.Trim();
            DealerImage = Path.Combine(MelonEnvironment.ModsDirectory, "Empire", dealer.Image);
            MelonLogger.Msg($"BlackmarketBuyer () Constructor created with Name {DealerName} and Index {dealerDataIndex}.");
            // If dealerDataIndex is more than count in Contacts.DealerData, return
            if (dealerDataIndex >= JSONDeserializer.dealerData.Dealers.Count)
            {
                //Needs more NPC than JSON file to progress - poor design - TODO - fix this
                Contacts.IsInitialized = true;
                MelonLogger.Msg($"⚠️ Out of range for dealerDataIndex: {dealerDataIndex}.");
                return;

            }
            //DebugUtils.LogObjectJson(dealer,$"{DealerName}");

            if (dealer == null)
                throw new ArgumentNullException(nameof(dealer));

            Contacts.Buyers[DealerName] = this;

            // Initialize dealer data
            Dialogues = dealer.Dialogue;
            UnlockRequirements = dealer.UnlockRequirements ?? new List<UnlockRequirement>();
            Drugs = dealer.Drugs ?? new List<Drug>();
            Shippings = dealer.Shippings ?? new List<Shipping>();
            Deals = dealer.Deals ?? new List<List<float>>();
            DealDays = dealer.DealDays ?? new List<string>();
            RepLogBase = dealer.RepLogBase;
            CurfewDeal = dealer.CurfewDeal;
            Tier = dealer.Tier;
            Debt = dealer.Debt ?? new Debt();
            RefreshCost = dealer.RefreshCost;
            Gift = dealer.Gift;
            Reward = dealer.Reward ?? new DealerReward(); // Initialize Reward with a new DealerReward if null
            // NEW: Initialize RewardManager using the updated dealer reward field.
            RewardManager = new RewardManager(this);
            if (_DealerData != null)
            {
                MelonLogger.Msg($"⚠️ Dealer {DealerName} already exists in BuyerSaveData dictionary.");
            }
            else
            {
                _DealerData = new DealerSaveData { };
            }
            //MelonLogger.Msg($"BlackmarketBuyer {DealerName} unlocking drugs");
            MelonLogger.Msg($"✅ Dealer updated to Buyers: {DealerName}");

        }

        protected override void OnLoaded()
        {
            MelonLogger.Msg($"BlackmarketBuyer {DealerName} ONloaded.");
            base.OnLoaded();
            if (dealerDataIndex < JSONDeserializer.dealerData.Dealers.Count)
            {
                UnlockDrug(); // Check if the dealer has any unlocked drugs based on reputation
                MelonLogger.Msg($"✅ Dealer {DealerName} unlocked drugs.");
                if (_DealerData.DebtRemaining == 0)
                {
                    _DealerData.DebtRemaining = dealer.Debt.TotalDebt; // Set the initial debt amount 
                }
            }
            else
            {
                MelonLogger.Msg($"⚠️ No more drugs to unlock for index {dealerDataIndex}.");
            }
            dealer = Contacts.GetDealerDataByIndex(++dealerDataIndex);
        }

        protected override void OnCreated()
        {
            base.OnCreated();
            Debug.Log($"BlackmarketBuyer {DealerName} ONcreated.");
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
            MelonLogger.Msg($"✅ {DealerName}'s reputation increased by {amount}. New Reputation: {_DealerData.Reputation}");
        }
        //A method to check if the new reputation unlocks any new drug, quality or effects for the dealer 
        public void UnlockDrug()
        {
            // Initialize the drug list
            var drugList = Drugs ?? new List<Drug>();

            // Filter drugs based on the current reputation and use null-coalescing for qualities and effects
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
                    // Unlock qualities where UnlockRep <= current reputation safely
                    Qualities = (d.Qualities?.Where(q => q.UnlockRep <= _DealerData.Reputation).ToList()) ?? new List<Quality>(),
                    // Unlock effects where UnlockRep <= current reputation safely
                    Effects = (d.Effects?.Where(e => e.UnlockRep <= _DealerData.Reputation).ToList()) ?? new List<Effect>()
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

        // New method to show unlocked and upcoming drug unlocks in a detailed string
        public string GetDrugUnlockInfo()
        {

            System.Text.StringBuilder info = new System.Text.StringBuilder();
            info.AppendLine($"<b>Dealer: {DealerName}</b> (Reputation: <color=#FFFFFF>{_DealerData.Reputation}</color>)");
            //info.AppendLine("<u>Drug Unlocks</u>:");
            foreach (var drug in Drugs)
            {
                string drugStatus = drug.UnlockRep <= _DealerData.Reputation
                    ? "<color=#00FF00>Unlocked</color>"
                    : $"<color=#FF4500>Locked (Unlock at: {drug.UnlockRep})</color>";
                info.AppendLine($"<b>• {drug.Type}</b> - {drugStatus}");
                // Qualities
                if (drug.Qualities != null && drug.Qualities.Count > 0)
                {
                    info.AppendLine("  Qualities:");
                    foreach (var quality in drug.Qualities)
                    {
                        float effectiveQuality = quality.DollarMult;
                        if (JSONDeserializer.QualitiesDollarMult.ContainsKey(quality.Type))
                        {
                            effectiveQuality += JSONDeserializer.QualitiesDollarMult[quality.Type];
                        }
                        string qualityStatus = quality.UnlockRep <= _DealerData.Reputation
                            ? "<color=#00FF00>Unlocked</color>"
                            : $"<color=#FF4500>Locked (Unlock at: {quality.UnlockRep})</color>";
                        info.AppendLine($"    - {quality.Type} (x<color=#00FFFF>{effectiveQuality:F2}</color>) : {qualityStatus}");
                    }
                }
                // Effects
                if (drug.Effects != null && drug.Effects.Count > 0)
                {
                    info.AppendLine("  Effects:");
                    foreach (var effect in drug.Effects)
                    {
                        float effectiveEffect = effect.DollarMult;
                        if (JSONDeserializer.EffectsDollarMult.ContainsKey((effect.Name).ToLower().Trim()))
                        {
                            effectiveEffect += JSONDeserializer.EffectsDollarMult[(effect.Name).ToLower().Trim()];
                        }
                        string effectStatus = effect.UnlockRep <= _DealerData.Reputation
                            ? "<color=#00FF00>Unlocked</color>"
                            : $"<color=#FF4500>Locked (Unlock at: {effect.UnlockRep})</color>";
                        info.AppendLine($"    - {effect.Name} (Prob <color=#FFA500>{effect.Probability:F2}</color>, x<color=#00FFFF>{effectiveEffect:F2}</color>) : {effectStatus}");
                    }
                }
                info.AppendLine(""); // spacer
            }
            return info.ToString();
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
            //UPDATABELE - May change
            // If messageType==accept, add another line to formatted = "Remember that we only accept packages after curfew"
            if (messageType == "accept")
            {
                formatted += "\nRemember that we only accept packages under cover of night.";
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
                    //MelonLogger.Msg($"⏳ Waiting for dealer {buyer.DealerName} to be initialized...");
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

        }
    }


}