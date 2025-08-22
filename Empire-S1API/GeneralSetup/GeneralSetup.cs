using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using S1API.Logging;
using S1API.Entities.NPCs;

namespace Empire
{
    public static class GeneralSetup
    {
        public static EmpireSaveData EmpireSaveData { get; set; } 
        // Convert to call phone later - TODO
        // Basic intro to mod and debt mechanics
        public static void UncCalls()
        {
            if (!EmpireSaveData.SaveData.UncNelsonCartelIntroDone)
            {
                MelonLogger.Msg($"Unc Calls: {EmpireSaveData.SaveData.UncNelsonCartelIntroDone}");
                EmpireSaveData.SaveData.UncNelsonCartelIntroDone = true;
                //Send the mod intro and debt message to player
                NPC1.Get<UncleNelson>().SendTextMessage("Listen up, kid. I'm out of the business now, but I still got some connections. I'll put in a good word for you. How far you go is up to you. Make good relations and do good business with the dealers and you'll climb up the ladders. That'll get you access to some top tier deals.");
                NPC1.Get<UncleNelson>().SendTextMessage("But I owed some cash to the wrong people. Cartels and the wrong sort. They'll expect you to pay it off since you're the man of the family now. They not the negotiating type, if you know what I mean. That means you need to step up son. Don't let me down. I don't want to see you in a body bag. You got that?");
                NPC1.Get<UncleNelson>().SendTextMessage("I'll help you out however I can. Don't come looking for me though. Heh. I'll call from time to time. Take care son.");
            }
        }
    }

}