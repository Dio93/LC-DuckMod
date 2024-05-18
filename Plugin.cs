using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using LethalLib.Modules;
using DuckMod.Behaviors;
using System.IO;
using BepInEx.Configuration;

namespace DuckMod
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class DuckMod : BaseUnityPlugin
    {
        private const string modGUID = "Dio93.DuckMod";
        private const string modName = "DuckMod";
        private const string modVersion = "1.2.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static DuckMod instance;

        internal ManualLogSource mls;

        private ConfigEntry<int> configCarryAmount;
        private ConfigEntry<int> configDuckPrice;
        private ConfigEntry<float> configSpeed;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }

            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);

            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            PetAI.InitializeRPCS_PetAI();


            string assetDir = Path.Combine(path, "duckmod");
            AssetBundle bundle = AssetBundle.LoadFromFile(assetDir);

            Item petDuck = bundle.LoadAsset<Item>("Assets/Items/PetDuck/PetDuckItem.asset");

            PetDuckAI.mls = mls;

            PetDuckAI petDuckAI = petDuck.spawnPrefab.AddComponent<PetDuckAI>();
            int duckPrice = 0;

            // load settings
            configDuckPrice = Config.Bind("Duck",
                                          "Duck Price",
                                          0,
                                          "Price of a duck");

            configCarryAmount = Config.Bind("Duck",
                                            "Carry Amount",
                                            1,
                                            "The amount of items the duck can carry");

            configSpeed = Config.Bind("Duck",
                                      "Speed",
                                      0.8f,
                                      "The speed of the duck proportional to the player");

            petDuckAI.itemCapacity = configCarryAmount.Value;
            petDuckAI.speedFactor = configSpeed.Value;
            duckPrice = configDuckPrice.Value;

            // Register petduck

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(petDuck.spawnPrefab);
            Utilities.FixMixerGroups(petDuck.spawnPrefab);

            TerminalNode duckNode = ScriptableObject.CreateInstance<TerminalNode>();
            duckNode.clearPreviousText = true;
            duckNode.displayText = "You have requested to order mighty pet ducks. Amount: [variableAmount]." +
                "\nTotal cost of items: [totalCost]" + 
                "\n\nPlease CONFIRM or DENY.\n\n";

            Items.RegisterShopItem(petDuck, duckNode, null, null, duckPrice);

            mls.LogInfo("The duck shovel mod has awaken :)");
            foreach(Items.PlainItem each in Items.plainItems)
            {
                mls.LogInfo(each.item.itemName);
            }

            harmony.PatchAll(typeof(DuckMod));
        }
    }
}
