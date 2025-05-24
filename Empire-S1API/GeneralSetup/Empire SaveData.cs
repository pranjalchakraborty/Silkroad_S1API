using System;
using System.Collections.Generic;
using System.Linq;
using S1API.Internal.Utils;
using S1API.PhoneApp;
using S1API.Entities;
using UnityEngine;
using Empire;
using S1API.Saveables;
using MelonLoader;
using MelonLoader.Utils;
using System.IO;

namespace Empire
{
    public class EmpireSaveData : NPC
    {


        [SaveableField("empire_save_data")]
        public GlobalSaveData SaveData;
        //public bool UncNelsonCartelIntroDone;

        public EmpireSaveData() : base("EmpireSaveData", "Empire", "SaveData")
        {
            // Initialize the save data here if needed
            SaveData = new GlobalSaveData
            {
                // Initialize your save data properties here

            };
        }

    }
}