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

        private ConfigEntry<bool> configDebug;
        private ConfigEntry<int> configCarryAmount;
        private ConfigEntry<int> configDuckPrice;
        private ConfigEntry<float> configSpeed;
        private ConfigEntry<bool> configHittable;
        private ConfigEntry<int> configHp;


        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }

            // load settings
            configDebug = Config.Bind("General",
                                    "Debugging",
                                    false,
                                    "Enable logging in Console?"
                                    );

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

            configHittable = Config.Bind("Duck",
                                         "Hittable",
                                         false,
                                         "Is the duck hittable");

            configHp = Config.Bind("Duck",
                                   "Hp",
                                   10,
                                   "Health points of the duck");

            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            PetAI.mls = configDebug.Value ? mls : null;

            PetAI.InitializeRPCS_PetAI();

            // load assets
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string assetDir = Path.Combine(path, "duckmod");
            AssetBundle bundle = AssetBundle.LoadFromFile(assetDir);

            Item petDuck = bundle.LoadAsset<Item>("Assets/Items/PetDuck/PetDuckItem.asset");
            PetDuckAI petDuckAI = petDuck.spawnPrefab.AddComponent<PetDuckAI>();
            petDuckAI.itemCapacity = configCarryAmount.Value;
            petDuckAI.speedFactor = configSpeed.Value;
            petDuckAI.hittable = configHittable.Value;
            petDuckAI.maxHp = configHp.Value;

            Item petDuckHat = bundle.LoadAsset<Item>("Assets/Items/PetDuck/PetDuckHatItem.asset");
            PetDuckAI petDuckHatAI = petDuckHat.spawnPrefab.AddComponent <PetDuckAI>();
            petDuckHatAI.itemCapacity = configCarryAmount.Value;
            petDuckHatAI.speedFactor = configSpeed.Value;
            petDuckHatAI.hittable = configHittable.Value;
            petDuckHatAI.maxHp = configHp.Value;

            int duckPrice = configDuckPrice.Value;

            // Register pet duck

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(petDuck.spawnPrefab);
            Utilities.FixMixerGroups(petDuck.spawnPrefab);

            TerminalNode duckNode = ScriptableObject.CreateInstance<TerminalNode>();
            duckNode.clearPreviousText = true;
            duckNode.displayText = "You have requested to order mighty pet ducks. Amount: [variableAmount]." +
                "\nTotal cost of items: [totalCost]" + 
                "\n\nPlease CONFIRM or DENY.\n\n";

            Items.RegisterShopItem(petDuck, duckNode, null, null, duckPrice);

            // Register pet duck with hat

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(petDuckHat.spawnPrefab);
            Utilities.FixMixerGroups(petDuckHat.spawnPrefab);

            TerminalNode duckHatNode = ScriptableObject.CreateInstance<TerminalNode>();
            duckHatNode.clearPreviousText = true;
            duckHatNode.displayText = "You have requested to order gentle ducks with hats. Amount: [variableAmount]." +
                "\nTotal cost of items: [totalCost]" +
                "\n\nPlease CONFIRM or DENY.\n\n";

            Items.RegisterShopItem(petDuckHat, duckHatNode, null, null, duckPrice);

            mls.LogInfo("The duck mod has awaken :)");
            //foreach(Items.PlainItem each in Items.plainItems)
            //{
            //    mls.LogInfo(each.item.itemName);
            //}

            harmony.PatchAll(typeof(DuckMod));
        }
    }
}
