using System.Collections.Generic;
using Newtonsoft.Json;

namespace Silkroad
{
    public class Dealer
    {
        public string Name { get; set; }
        public string? Image { get; set; }

        public List<int> DealTimes { get; set; } // List of Deal Times
        public List<float> DealTimesMult { get; set; } // List of Reward Multiplier for each Deal Time
        public List<int> Penalties { get; set; } // Money and Rep Penalties for failing Deal
        public List<UnlockRequirement> UnlockRequirements { get; set; } // Updated to match JSON structure
        public List<Drug> Drugs { get; set; }

        [JsonProperty("shipping")]
        public List<Shipping> Shippings { get; set; }
        public Dialogue Dialogue { get; set; }
    }

    public class UnlockRequirement
    {
        public string Name { get; set; } // Name of the dealer required to unlock
        public int MinRep { get; set; } // Minimum reputation required
    }

    public class Drug
    {
        public string Type { get; set; }
        public int UnlockRep { get; set; }
        public List<Quality> Qualities { get; set; }
        public List<Effect> Effects { get; set; }
        public int BonusDollar { get; set; } // Base dollar value
        public int BonusRep { get; set; } // Base reputation value
        public float BaseDollarMult { get; set; } // Base dollar multiplier
        public float BaseRepMult { get; set; } // Base reputation multiplier
    }

    public class Quality
    {
        public string Type { get; set; }
        public int UnlockRep { get; set; }
        public float DollarMult { get; set; } // Dollar multiplier for the quality
        public float RepMult { get; set; } // Reputation multiplier for the quality
    }

    public class Effect
    {
        [JsonProperty("type")]
        public string Name { get; set; }
        [JsonProperty("unlockRep")]
        public int UnlockRep { get; set; }
        [JsonProperty("probability")]
        public float Probability { get; set; }
        [JsonProperty("dollar_mult")]
        public float DollarMult { get; set; }
        [JsonProperty("rep_mult")]
        public float RepMult { get; set; }
    }

    public class Shipping
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("cost")]
        public int Cost { get; set; }
        [JsonProperty("unlockRep")]
        public int UnlockRep { get; set; }
        

        [JsonProperty("minAmount")]
        public int MinAmount { get; set; }
        [JsonProperty("stepAmount")]
        public int StepAmount { get; set; }
        [JsonProperty("maxAmount")]
        public int MaxAmount { get; set; }
    }

public class Dialogue
{
    public List<string> Intro { get; set; }
    public List<string> DealStart { get; set; }
    public List<string> Accept { get; set; }
    public List<string> Incomplete { get; set; }
    public List<string> Expire { get; set; }
    public List<string> Fail { get; set; }
    public List<string> Success { get; set; }
     public List<string> Reward { get; set; }
}


    public class DealerData
    {
        public List<Dealer> Dealers { get; set; }
    }
}