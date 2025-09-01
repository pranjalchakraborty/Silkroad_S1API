using System;
using MelonLoader;
using S1API.Internal.Abstraction;
using S1API.Saveables;
using S1API.GameTime;

namespace Empire
{
    public class EmpireSaveData : Saveable
    {
        [SaveableField("empire_save_data")]
        public GlobalSaveData SaveData = new GlobalSaveData();

        public EmpireSaveData()
        {
            MelonLogger.Msg("Empire Save Data Constructor");
            GeneralSetup.EmpireSaveData = this;
        }

        protected override void OnLoaded()
        {
            MelonLogger.Msg("Empire Save Data Loaded");
            GeneralSetup.EmpireSaveData = this;
        }

        protected override void OnCreated()
        {
            MelonLogger.Msg("Empire Save Data Created");
            GeneralSetup.EmpireSaveData = this;
            GeneralSetup.UncCalls(); // TODO - shift to proper flow
            TimeManager.OnDayPass += GeneralSetup.ResetPlayerStats; // TODO - shift to proper flow
        }


    }
}