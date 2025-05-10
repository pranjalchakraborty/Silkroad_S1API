using Newtonsoft.Json;
using Empire;
using System;
using System.Collections.Generic;

public class DealerSaveData
{
    public int Reputation; // The dealer's reputation
    public int ShippingTier; // The dealer's shipping tier
    public int DealsCompleted; // Number of deals completed with this dealer
    public List<Drug>? UnlockedDrugs; // List of unlocked drug types with unlocked qualities and effects only

    public DealerSaveData()
    {
        Reputation = 1;
        ShippingTier = 0;
        DealsCompleted = 0;
        UnlockedDrugs = new List<Drug>();
    }
}