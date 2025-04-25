using Newtonsoft.Json;
using Silkroad;
using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class DealerSaveData
{
    public string DealerName; // The name of the dealer
    public string Icon; // The icon of the dealer
    public int Reputation; // The dealer's reputation
    public int MinDeliveryAmount; // Minimum delivery amount for this dealer
    public int MaxDeliveryAmount; // Maximum delivery amount for this dealer
    public List<string> DeliveryAcceptedTexts { get; set; }
    public List<string> DeliverySuccessTexts { get; set; }
    public List<string> RewardDroppedTexts { get; set; }
    public List<string> UnlockedDrugs; // List of unlocked drug types
    public Dictionary<string, string> UnlockedQuality; // Unlocked highest quality for each drug type
    // Changed the types for effects: now a list of Effect objects instead of a dictionary.
        public List<Effect> NecessaryEffects; // Unlocked necessary effects (always active)
        public List<Effect> OptionalEffects;  // Unlocked optional effects (rolled by probability)
           
    [JsonConstructor]
    public DealerSaveData()
    {
        UnlockedDrugs = new List<string>();
        UnlockedQuality = new Dictionary<string, string>();
        NecessaryEffects = new List<Effect>();
        OptionalEffects = new List<Effect>();
        DeliveryAcceptedTexts = new List<string>();
        DeliverySuccessTexts = new List<string>();
        RewardDroppedTexts = new List<string>();
       
    }

    public void UpdateDeliveryAmounts(List<Shipping> allShippingOptions, int reputation)
    {
        var unlockedShipping = allShippingOptions
            .Where(s => s.UnlockRep <= reputation && s.MinAmount > 0 && s.MaxAmount > 0)
            .ToList();

        if (unlockedShipping.Any())
        {
            MinDeliveryAmount = unlockedShipping.Min(s => s.MinAmount);
            MaxDeliveryAmount = unlockedShipping.Max(s => s.MaxAmount);
        }
    }
}
