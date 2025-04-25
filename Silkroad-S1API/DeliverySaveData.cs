using Newtonsoft.Json;
using System;
using System.Collections.Generic;

[Serializable]
public class DeliverySaveData
{
    public string ProductID;
    public uint RequiredAmount;
    public int Reward;
    public string DeliveryDropGUID;
    public string RewardDropGUID;
    public bool Initialized;
    public string DealerName;
    public List<string> NecessaryEffects;
    public List<string> OptionalEffects;

    [JsonConstructor]
    public DeliverySaveData() { }
}
