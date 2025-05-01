using Newtonsoft.Json;
using Silkroad;
using System;
using System.Collections.Generic;



[Serializable]
public class BuyerSaveData 
{
    public Dictionary<string, DealerSaveData> Dealers;
    
    [JsonConstructor]
    public BuyerSaveData() {
        Dealers = new Dictionary<string, DealerSaveData>();
    }
}