using Newtonsoft.Json;
using Silkroad;
using System;
using System.Collections.Generic;


public class BuyerSaveData 
{
    public Dictionary<string, DealerSaveData> Dealers;
    
    public BuyerSaveData() {
        Dealers = new Dictionary<string, DealerSaveData>();
    }
}