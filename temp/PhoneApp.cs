using System;
using System.Collections;
using Empire;
using MelonLoader;
using MelonLoader.Utils;
using S1API.GameTime;
using S1API.Internal.Utils;
using S1API.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Empire
{
    public partial class MyApp : S1API.PhoneApp.PhoneApp
    {
        public static MyApp Instance { get; private set; }

        protected override string AppName => "Empire";
        protected override string AppTitle => "Empire";
        protected override string IconLabel => "Empire";
        protected override string IconFileName => System.IO.Path.Combine(MelonEnvironment.ModsDirectory, "Empire", "EmpireIcon.png");

        protected override void OnCreated()
        {
            base.OnCreated();
            Instance = this;
            TimeManager.OnDayPass += LoadQuests;
        }

        protected override void OnDestroyed()
        {
            base.OnDestroyed();
            TimeManager.OnDayPass -= LoadQuests;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void InitializeDealers()
        {
            try
            {
                JSONDeserializer.Initialize();
                Contacts.Update();
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Failed to initialize dealers: {ex}");
            }
        }

        private IEnumerator WaitForBuyerAndInitialize()
        {
            float timeout = 5f;
            float waited = 0f;

            while (!Contacts.IsUnlocked && waited < timeout)
            {
                waited += UnityEngine.Time.deltaTime;
                yield return null;
            }

            if (!Contacts.IsUnlocked)
            {
                MelonLoader.MelonLogger.Warning("PhoneApp-Timeout reached. Contacts are still not unlocked.");
                yield break;
            }

            LoadQuests();
        }
    }
}