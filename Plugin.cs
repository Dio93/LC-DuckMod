using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using LethalLib.Modules;
using DuckMod.Behaviors;
using System.IO;

namespace DuckMod
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class DuckMod : BaseUnityPlugin
    {
        private const string modGUID = "SchanniBunni.DuckMod";
        private const string modName = "Duck Mod";
        private const string modVersion = "1.0.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static DuckMod instance;

        internal ManualLogSource mls;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }

            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            PetAI.InitializeRPCS_PetAI();

            string assetDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "duckmod");
            AssetBundle bundle = AssetBundle.LoadFromFile(assetDir);

            Item duckShovel = bundle.LoadAsset<Item>("Assets/Items/DickShovel/DickShovelItem.asset");
            Item petDuck = bundle.LoadAsset<Item>("Assets/Items/PetDuck/PetDuckItem.asset");

            PetDuckAI.mls = mls;

            PetDuckAI petDuckAI = petDuck.spawnPrefab.AddComponent<PetDuckAI>();

            // Register shovel

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(duckShovel.spawnPrefab);
            Utilities.FixMixerGroups(duckShovel.spawnPrefab);

            TerminalNode shovelNode = ScriptableObject.CreateInstance<TerminalNode>();
            shovelNode.clearPreviousText = true;
            shovelNode.displayText = "A mighty shovel for ducks.\n\nPlease CONFIRM or DENY.\n\n";

            Items.RegisterShopItem(duckShovel, shovelNode, null, null, 25);

            // Register petduck

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(petDuck.spawnPrefab);
            Utilities.FixMixerGroups(petDuck.spawnPrefab);

            TerminalNode duckNode = ScriptableObject.CreateInstance<TerminalNode>();
            duckNode.clearPreviousText = true;
            duckNode.displayText = "A mighty pet duck.\n\nPlease CONFIRM or DENY.\n\n";

            Items.RegisterShopItem(petDuck, duckNode, null, null, 0);

            mls.LogInfo("The duck shovel mod has awaken :)");
            foreach(Items.PlainItem each in Items.plainItems)
            {
                mls.LogInfo(each.item.itemName);
            }

            harmony.PatchAll(typeof(DuckMod));
        }
    }
}
