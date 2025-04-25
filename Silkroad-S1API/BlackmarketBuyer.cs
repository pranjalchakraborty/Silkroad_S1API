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
        private DealerSaveData _DealerData;

        [SaveableField("Buyers")]
        public static Dictionary<string, DealerSaveData> Buyers = new Dictionary<string, DealerSaveData>();

        public string DealerName { get; private set; }
        public string? DealerImage { get; private set; }
        public BlackmarketBuyer() : base("blackmarket_buyer", "Blackmarket", "Buyer")
        {
            DealerName = "Blackmarket Buyer";
            DealerImage = Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", "SilkRoadIcon_quest.png");

            // Create default DealerSaveData with valid non-null collections.
            _DealerData = new DealerSaveData
            {
                DeliveryAcceptedTexts = new List<string> { "Default accepted delivery text" },
                DeliverySuccessTexts = new List<string> { "Default success text" },
                RewardDroppedTexts = new List<string> { "Default reward dropped text" },
                DealerName = DealerName,
                Icon = DealerImage,
                Reputation = 0,
                UnlockedDrugs = new List<string> { "default drug" },
                UnlockedQuality = new Dictionary<string, string>
        {
            { "default drug", "default quality" }
        },
                NecessaryEffects = new List<Effect>
        {
            new Effect { Name = "DefaultNecessaryEffect", Probability = 1.0f, UnlockRep = 0 }
        },
                OptionalEffects = new List<Effect>(),
                MinDeliveryAmount = 1,
                MaxDeliveryAmount = 5
            };

            

            // Register the default dealer save data so that later lookups won't return null.
            Buyers[DealerName] = _DealerData;
        }
        public BlackmarketBuyer(Dealer dealer) : base(
            dealer.Name.ToLower().Replace(" ", "_"),
            dealer.Name.Split(' ')[0],
            dealer.Name.Contains(' ') ? dealer.Name.Substring(dealer.Name.IndexOf(' ') + 1) : "Dealer")
        {
            if (dealer == null)
                throw new ArgumentNullException(nameof(dealer));

            DealerName = dealer.Name;
            DealerImage = Path.Combine(MelonEnvironment.ModsDirectory, dealer.Image);

            if (Buyers.ContainsKey(dealer.Name))
            {
                _DealerData = Buyers[dealer.Name];
                MelonLogger.Msg($"⚠️ Dealer {dealer.Name} already exists in Buyers dictionary.");
                return;
            }

            // Create DealerSaveData with safe enumeration for dialogue, drugs, and effects.
            _DealerData = new DealerSaveData
            {
                DeliveryAcceptedTexts = dealer.Dialogue?.DealStart ?? new List<string>(),
                DeliverySuccessTexts = dealer.Dialogue?.Responses?.Accept ?? new List<string>(),
                RewardDroppedTexts = dealer.Dialogue?.Responses?.Success ?? new List<string>(),
                DealerName = dealer.Name,
                Icon = dealer.Image,
                Reputation = 0,
                UnlockedDrugs = (dealer.Drugs ?? Enumerable.Empty<Drug>())
                    .Where(d => d.UnlockRep == 0)
                    .Select(d => d.Type)
                    .ToList(),
                UnlockedQuality = (dealer.Drugs ?? Enumerable.Empty<Drug>())
                    .Where(d => d.UnlockRep == 0)
                    .ToDictionary(
                        d => d.Type,
                        d => d.Qualities.First(q => q.UnlockRep == 0).Type
                    ),
                NecessaryEffects = (dealer.Drugs ?? Enumerable.Empty<Drug>())
                    .SelectMany(d => (d.Effects ?? Enumerable.Empty<Effect>())
                        .Where(e => e.UnlockRep == 0 && e.Probability >= 1.0f)
                        .Select(e => new Effect { Name = e.Name, Probability = e.Probability, UnlockRep = e.UnlockRep }))
                    .ToList(),
                OptionalEffects = (dealer.Drugs ?? Enumerable.Empty<Drug>())
                    .SelectMany(d => (d.Effects ?? Enumerable.Empty<Effect>())
                        .Where(e => e.UnlockRep == 0 && e.Probability < 1.0f)
                        .Select(e => new Effect { Name = e.Name, Probability = e.Probability, UnlockRep = e.UnlockRep }))
                    .ToList()
            };

            var shippingList = dealer.Shippings ?? Enumerable.Empty<Shipping>();
var validShippings = shippingList
    .Where(s => s.UnlockRep <= _DealerData.Reputation && s.MinAmount > 0 && s.MaxAmount > 0)
    .ToList();
MelonLogger.Msg($"   Found {validShippings.Count} shipping option(s) unlocked for dealer '{dealer.Name}' at rep {_DealerData.Reputation}.");

foreach (var s in validShippings)
{
    MelonLogger.Msg($"      Shipping Option: {s.Name} | UnlockRep: {s.UnlockRep} | MinAmount: {s.MinAmount} | MaxAmount: {s.MaxAmount}");
}

var shippingMethod = validShippings
    .OrderByDescending(s => s.MaxAmount)
    .FirstOrDefault();

if (shippingMethod != null)
{
    _DealerData.MinDeliveryAmount = shippingMethod.MinAmount;
    _DealerData.MaxDeliveryAmount = shippingMethod.MaxAmount;
    MelonLogger.Msg($"   Using shipping method: {shippingMethod.Name} ({shippingMethod.MinAmount}-{shippingMethod.MaxAmount})");
}
else
{
    _DealerData.MinDeliveryAmount = 1; // Default fallback
    _DealerData.MaxDeliveryAmount = 5; // Default fallback
    MelonLogger.Msg("   No shipping methods unlocked, using defaults (1-5)");
}

            Buyers[dealer.Name] = _DealerData;

            // Log initialization details
            MelonLogger.Msg($"✅ Dealer initialized: {dealer.Name}");
            MelonLogger.Msg($"   Unlocked Drugs: {string.Join(", ", _DealerData.UnlockedDrugs)}");
            MelonLogger.Msg($"   MinDeliveryAmount: {_DealerData.MinDeliveryAmount}, MaxDeliveryAmount: {_DealerData.MaxDeliveryAmount}");
        }

        protected override Sprite? NPCIcon => ImageUtils.LoadImage(DealerImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", "SilkRoadIcon_quest.png"));

        protected override void OnLoaded()
        {
            base.OnLoaded();
            MelonCoroutines.Start(WaitForDealerAndSendStatus());
            IsInitialized = true;
        }

        protected override void OnCreated()
        {
            base.OnCreated();
            Debug.Log($"BlackmarketBuyer {DealerName} created.");
        }

        public void UnlockDrug(string dealerName, string drugType)
        {
            if (!BlackmarketBuyer.Buyers.TryGetValue(dealerName, out var dealerData))
                return;

            if (!dealerData.UnlockedDrugs.Contains(drugType))
            {
                dealerData.UnlockedDrugs.Add(drugType);
            }
        }

        public void UnlockQuality(string dealerName, string drugType, string qualityType)
        {
            if (!BlackmarketBuyer.Buyers.TryGetValue(dealerName, out var dealerData))
                return;

            dealerData.UnlockedQuality[drugType] = qualityType;
        }

        public void UnlockNecessaryEffect(string dealerName, string effectName)
        {
            if (!BlackmarketBuyer.Buyers.TryGetValue(dealerName, out var dealerData))
                return;

            if (!dealerData.NecessaryEffects.Any(e => e.Name == effectName))
            {
                dealerData.NecessaryEffects.Add(new Effect { Name = effectName, Probability = 1.0f });
            }
        }
        public void UnlockOptionalEffect(string dealerName, string effectName, float probability = 0.5f)
        {
            if (!BlackmarketBuyer.Buyers.TryGetValue(dealerName, out var dealerData))
                return;

            if (!dealerData.OptionalEffects.Any(e => e.Name == effectName))
            {
                dealerData.OptionalEffects.Add(new Effect { Name = effectName, Probability = probability });
            }
        }

        public void SendDeliveryAccepted(string dealerName, string product, int amount)
        {
            if (!BlackmarketBuyer.Buyers.TryGetValue(dealerName, out var dealerData))
                return;

            DealerName = dealerData.DealerName;
            DealerImage = dealerData.Icon;

            string line = dealerData.DeliveryAcceptedTexts[RandomUtils.RangeInt(0, dealerData.DeliveryAcceptedTexts.Count)];
            string formatted = line
                .Replace("{product}", $"<color=#34AD33>{product}</color>")
                .Replace("{amount}", $"<color=#FF0004>{amount}x</color>");

            SendTextMessage(formatted);
        }

        public void SendDeliverySuccess(string dealerName, string product)
        {
            if (!BlackmarketBuyer.Buyers.TryGetValue(dealerName, out var dealerData))
                return;

            DealerName = dealerData.DealerName;
            DealerImage = dealerData.Icon;

            string line = dealerData.DeliverySuccessTexts[RandomUtils.RangeInt(0, dealerData.DeliverySuccessTexts.Count)];
            SendTextMessage(line);
        }

        public void SendRewardDropped(string dealerName)
        {
            if (!BlackmarketBuyer.Buyers.TryGetValue(dealerName, out var dealerData))
                return;

            DealerName = dealerData.DealerName;
            DealerImage = dealerData.Icon;

            string line = dealerData.RewardDroppedTexts[RandomUtils.RangeInt(0, dealerData.RewardDroppedTexts.Count)];
            SendTextMessage(line);
        }
        private System.Collections.IEnumerator WaitForDealerAndSendStatus()
        {
            float timeout = 5f;
            float waited = 0f;

            // Wait for this specific dealer's data to be initialized
            while (!IsInitialized && waited < timeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (!IsInitialized)
            {
                MelonLogger.Warning($"⚠️ Dealer {DealerName} not initialized after timeout");
                yield break;
            }

            // Check if dealer data exists in the Buyers dictionary
            if (!Buyers.TryGetValue(DealerName, out var dealerData))
            {
                MelonLogger.Warning($"⚠️ No save data found for dealer {DealerName}");
                yield break;
            }

            MelonLogger.Msg($"✅ Dealer {DealerName} initialized with save data");

            // Additional initialization logic can go here
            // For example, syncing reputation, unlocks, etc.
        }
    }
}