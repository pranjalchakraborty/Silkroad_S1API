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

namespace Empire
{
    public class EmpireSaveData : NPC
    {


        [SaveableField("empire_save_data")]
        public GlobalSaveData SaveData;
        //public bool UncNelsonCartelIntroDone;

        public EmpireSaveData() : base("EmpireSaveData", "Empire", "SaveData")
        {
           SaveData= new GlobalSaveData();  
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            //GeneralSetup.EmpireSaveData = this;
            MelonLogger.Msg("Empire Save Data Loaded");
        }

        protected override void OnCreated()
        {
            base.OnCreated();
            GeneralSetup.EmpireSaveData = this;
            MelonLogger.Msg("Empire Save Data Created");
            GeneralSetup.UncCalls();//ToDO - shift to proper flow
        }


    }
}