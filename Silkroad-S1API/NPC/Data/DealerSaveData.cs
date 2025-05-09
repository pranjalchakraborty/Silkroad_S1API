using Newtonsoft.Json;
using Silkroad;
using System;
using System.Collections.Generic;

public class DealerSaveData
{
    public string DealerName; // The name of the dealer
    public int Reputation; // The dealer's reputation
    public int ShippingTier; // The dealer's shipping tier
    public int DealsCompleted; // Number of deals completed with this dealer

    public int MinDeliveryAmount; // Minimum delivery amount for this dealer
    public int StepDeliveryAmount; // Step delivery amount for this dealer
    public int MaxDeliveryAmount; // Maximum delivery amount for this dealer
    public List<Drug>? UnlockedDrugs; // List of unlocked drug types with unlocked qualities and effects only

    public DealerSaveData()
    {
        DealerName = string.Empty;
        Reputation = 0;
        ShippingTier = 0;
        MinDeliveryAmount = 0;
        StepDeliveryAmount = 0;
        MaxDeliveryAmount = 0;
        DealsCompleted = 0;
        UnlockedDrugs = new List<Drug>();
    }
}