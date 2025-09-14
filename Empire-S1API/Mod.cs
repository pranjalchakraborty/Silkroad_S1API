using MelonLoader;
using Empire;
using S1API.Saveables;

[assembly: MelonInfo(typeof(MyMod), "Empire", "1.11", "Aracor")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace Empire
{
    public class MyMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            // Initialize JSON data first
            JSONDeserializer.Initialize();
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Main")
            {
                MelonLogger.Msg("🧹 Resetting Empire static state after Main scene unload");
                Contacts.Reset();
                MyApp.Reset();
            }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (sceneName == "Main")
            {
                // Also reset on initialization to be safe
                MelonLogger.Msg("🧹 Resetting Empire static state after Main scene initialization");
                Contacts.Reset();
                MyApp.Reset();
            }
        }
    }
}