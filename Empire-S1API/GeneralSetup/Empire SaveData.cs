using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Empire;
using MelonLoader;
using MelonLoader.Utils;
using S1API.Entities;
using S1API.Internal.Utils;
using S1API.PhoneApp;
using S1API.Saveables;
using UnityEngine;
using S1API.GameTime;
using S1API.Logging;

namespace Empire
{
    public class EmpireSaveData : NPC
    {


        [SaveableField("empire_save_data")]
        public GlobalSaveData SaveData;

        public EmpireSaveData() : base("EmpireSaveData", "Empire", "SaveData")
        {
            MelonLogger.Msg("Empire Save Data Constructor");
            //if SaveData is null, create a new instance
            if (SaveData == null)
                SaveData = new GlobalSaveData();
            //if GeneralSetup.EmpireSaveData is null, set it to this instance
            GeneralSetup.EmpireSaveData = this;


        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            MelonLogger.Msg("Empire Save Data Loaded");
            //GeneralSetup.EmpireSaveData = this;
        }

        protected override void OnCreated()
        {
            base.OnCreated();
            MelonLogger.Msg("Empire Save Data Created");
            //GeneralSetup.EmpireSaveData = this;
            GeneralSetup.UncCalls();//ToDO - shift to proper flow
            TimeManager.OnDayPass += GeneralSetup.ResetPlayerStats;//ToDO - shift to proper flow
        }


    }
}