using Newtonsoft.Json;
using Empire;
using System;
using System.Collections.Generic;

public class GlobalSaveData
{
    public bool UncNelsonCartelIntroDone;
    public int TotalDealsCompleted;
    public GlobalSaveData()
    {
        UncNelsonCartelIntroDone = false;
        TotalDealsCompleted = 0;
    }
}