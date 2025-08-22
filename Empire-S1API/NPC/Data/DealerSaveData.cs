using Newtonsoft.Json;
using Empire;
using System;
using System.Collections.Generic;

namespace Empire
{

    public class DealerSaveData
    {
        public int Reputation; // The dealer's reputation
        public int ShippingTier; // The dealer's shipping tier
        public int DealsCompleted; // Number of deals completed with this dealer
        public List<Drug>? UnlockedDrugs; // List of unlocked drug types with unlocked qualities and effects only
        public float DebtRemaining; // Remaining debt to the dealer
        public float DebtPaidThisWeek; // Amount paid to the dealer this week
        public bool IntroDone; // Whether the dealer's intro has been completed
        public DealerSaveData()
        {
            Reputation = 0;
            ShippingTier = 0;
            DealsCompleted = 0;
            UnlockedDrugs = new List<Drug>();
            DebtRemaining = 0f;
            DebtPaidThisWeek = 0f;
            IntroDone = false;
        }
    }

}