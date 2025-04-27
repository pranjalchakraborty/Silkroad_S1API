using Newtonsoft.Json;
using Silkroad;
using System;
using System.Collections.Generic;

[Serializable]
public class DrugTest
{

    public string Name { get; set; }
    public int Quantity { get; set; }
    public float Price { get; set; }


    [JsonConstructor]
    public DrugTest()
    {
        Name = string.Empty;
        Quantity = 0;
        Price = 0.0f;
    }
}