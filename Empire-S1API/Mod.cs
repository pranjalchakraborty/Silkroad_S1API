using MelonLoader;
using Empire;
using S1API.Saveables;

[assembly: MelonInfo(typeof(MyMod), "Empire", "0.75", "Aracor")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace Empire
{
    public class MyMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            // Register the EmpireSaveData with the ModSaveableRegistry
            var empireSaveData = new EmpireSaveData();
            ModSaveableRegistry.Register(empireSaveData, "Empire");
        }
    }
}