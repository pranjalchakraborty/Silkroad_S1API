using Newtonsoft.Json;
using Silkroad;
using System;
using System.Collections.Generic;



[Serializable]
public class BuyerSaveData : Dictionary<string, DealerSaveData>
{
    [JsonConstructor]
    public BuyerSaveData() : base()
    {
        
    }
}