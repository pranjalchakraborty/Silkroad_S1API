using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using S1API.Logging;
using S1API.Entities.NPCs;
using S1API.PhoneCalls;
using Empire.PhoneCalls;

namespace Empire
{
    public static class GeneralSetup
    {
        public static EmpireSaveData EmpireSaveData { get; set; } 
        // Basic intro to mod and debt mechanics
        public static void UncCalls()
        {
            MelonLogger.Msg("Unc Calls Method Triggered.");
            //Log if intro has been done
            MelonLogger.Msg($"Unc Calls: {EmpireSaveData.SaveData.UncNelsonCartelIntroDone}");
            if (!EmpireSaveData.SaveData.UncNelsonCartelIntroDone)
            {
                MelonLogger.Msg($"Unc Calls: {EmpireSaveData.SaveData.UncNelsonCartelIntroDone}");
                EmpireSaveData.SaveData.UncNelsonCartelIntroDone = true;
                // Queue the intro as a phone call with staged dialogue
                var caller = NPC1.Get<UncleNelson>() as UncleNelson;

                if (caller != null)
                {
                    var call = new UncleNelsonIntroCall(caller);
                    CallManager.QueueCall(call);
                }
            }
        }
        public static void ResetPlayerStats()
        {
            MelonLogger.Msg("Resetting Player Stats.");
            S1API.Console.ConsoleHelper.SetPlayerJumpMultiplier(1f);
            S1API.Console.ConsoleHelper.SetPlayerMoveSpeedMultiplier(1f);
            S1API.Console.ConsoleHelper.SetPlayerHealth(100f);
            S1API.Console.ConsoleHelper.SetPlayerEnergyLevel(100f);
            //S1API.Console.ConsoleHelper.SetLawIntensity(1f);
            MelonLogger.Msg("Player Stats Reset.");
        }
    }

}
