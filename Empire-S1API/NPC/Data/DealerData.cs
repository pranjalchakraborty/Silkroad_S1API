using System.Collections.Generic;
using Newtonsoft.Json;

namespace Empire
{
    public class Dealer
    {
        public string Name { get; set; }
        public string? Image { get; set; }
        public int Tier { get; set; }
        public List<UnlockRequirement> UnlockRequirements { get; set; }
        public List<string> DealDays { get; set; }
        public bool CurfewDeal { get; set; }
        public List<List<float>> Deals { get; set; }
        public int RefreshCost { get; set; }
        [JsonProperty("reward")]
        public DealerReward Reward { get; set; }
        public float RepLogBase { get; set; }
        public List<Drug> Drugs { get; set; }

        [JsonProperty("shipping")]
        public List<Shipping> Shippings { get; set; }
        public Dialogue Dialogue { get; set; }
        [JsonProperty("debt")]
        public Debt Debt { get; set; } = new Debt();
        [JsonProperty("gift")]
        public Gift Gift { get; set; }
    }

    public class DealerReward
    {
        [JsonProperty("unlockRep")]
        public int unlockRep { get; set; }
        [JsonProperty("rep_cost")]
        public int RepCost { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("args")]
        public List<string> Args { get; set; }
    }

    public class UnlockRequirement
    {
        public string Name { get; set; }
        public int MinRep { get; set; }
    }

    public class Drug
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("unlockRep")]
        public int UnlockRep { get; set; }
        [JsonProperty("base_dollar")]
        public int BaseDollar { get; set; } // Changed from BonusDollar: now reflects JSON "base_dollar"
        [JsonProperty("base_rep")]
        public int BaseRep { get; set; } // Changed from BonusRep
        [JsonProperty("base_xp")]
        public int BaseXp { get; set; }
        [JsonProperty("rep_mult")]
        public float RepMult { get; set; } // Changed to use JSON "rep_mult"
        [JsonProperty("xp_mult")]
        public float XpMult { get; set; } // Changed to use JSON "xp_mult"
        [JsonProperty("qualities")]
        public List<Quality> Qualities { get; set; }
        [JsonProperty("effects")]
        public List<Effect> Effects { get; set; }
    }

    public class Quality
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        public int UnlockRep { get; set; }
        public float DollarMult { get; set; }
        //public bool TakeFromList { get; set; } // Change to use JSON "take_from_list"
    }

    public class Effect
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("unlockRep")]
        public int UnlockRep { get; set; }
        [JsonProperty("probability")]
        public float Probability { get; set; }
        [JsonProperty("dollar_mult")]
        public float DollarMult { get; set; }
        //public bool TakeFromList { get; set; } // Change to use JSON "take_from_list"
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
        [JsonProperty("dealModifier")]
        public List<float> DealModifier { get; set; }
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
        [JsonProperty("effectsName")]
        public List<string> EffectsName { get; set; }
        [JsonProperty("effectsDollarMult")]
        public List<float> EffectsDollarMult { get; set; }
        [JsonProperty("qualityTypes")]
        public List<string> QualityTypes { get; set; }
        [JsonProperty("qualitiesDollarMult")]
        public List<float> QualitiesDollarMult { get; set; }
        [JsonProperty("randomNumberRanges")]
        public List<float> RandomNumberRanges { get; set; }
        [JsonProperty("dealers")]
        public List<Dealer> Dealers { get; set; }
    }

    public class Debt
    {
        [JsonProperty("total_debt")]
        public float TotalDebt { get; set; } = 0f;
        [JsonProperty("interest_rate")]
        public float InterestRate { get; set; } = 0f;
        [JsonProperty("day_multiple")]
        public float DayMultiple { get; set; } = 0f;
        [JsonProperty("day_exponent")]
        public float DayExponent { get; set; } = 0f;
        [JsonProperty("product_bonus")]
        public float ProductBonus { get; set; } = 0f;
    }

    public class Gift
    {
        [JsonProperty("cost")]
        public int Cost { get; set; }
        [JsonProperty("rep")]
        public int Rep { get; set; }
    }
}