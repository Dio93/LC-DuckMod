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
        private const string modVersion = "1.4.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static DuckMod instance;

        internal ManualLogSource mls;

        private ConfigEntry<bool> configDebug;
        private ConfigEntry<int> configMaxDucks;
        private ConfigEntry<int> configCarryAmount;
        private ConfigEntry<bool> configCanGrabTwoHanded;
        private ConfigEntry<bool> configCanGrabHive;
        private ConfigEntry<bool> configCanUseItem;
        private ConfigEntry<int> configDuckPrice;
        private ConfigEntry<float> configSpeed;
        private ConfigEntry<bool> configHittable;
        private ConfigEntry<int> configHp;
        private ConfigEntry<float> configTextureWhite;
        private ConfigEntry<float> configTextureGreen;
        private ConfigEntry<float> configTextureGold;

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

            configMaxDucks = Config.Bind("Duck",
                                         "Max Duck Count",
                                         -1,
                                         "Maximal number of ducks. -1 = infinite");

            configDuckPrice = Config.Bind("Duck",
                                          "Duck Price",
                                          25,
                                          "Price of a duck");

            configCarryAmount = Config.Bind("Duck.Items",
                                            "Carry Amount",
                                            1,
                                            "The amount of items the duck can carry");

            configCanGrabTwoHanded = Config.Bind("Duck.Items",
                                                 "Can grab two handed",
                                                 true,
                                                 "Cant he duck grab two handed items?");

            configCanGrabHive = Config.Bind("Duck.Items",
                                            "Can grab hive",
                                            false,
                                            "Can the duck grab hives?");

            configCanUseItem = Config.Bind("Duck.Items",
                                           "Can use item",
                                           false,
                                           "Can the duck use items?");

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

            configTextureWhite = Config.Bind("Duck.Textures",
                                             "White Texture",
                                             0.5f,
                                             "Probability of white texture");

            configTextureGreen = Config.Bind("Duck.Textures",
                                             "Green Texture",
                                             0.5f,
                                             "Probability of green texture");

            configTextureGold = Config.Bind("Duck.Textures",
                                             "Gold Texture",
                                             0.01f,
                                             "Probability of gold texture");


            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            PetAI.mls = configDebug.Value ? mls : null;

            PetDuckAI.InitializeRPCS_PetDuckAI();
            PetAI.InitializeRPCS_PetAI();
            PetAI.maxPets = configMaxDucks.Value;
            int duckPrice = configDuckPrice.Value;

            // load assets
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string assetDir = Path.Combine(path, "duckmod");
            AssetBundle bundle = AssetBundle.LoadFromFile(assetDir);

            Material duckShaderWhite = bundle.LoadAsset<Material>("Assets/Items/PetDuck/DuckShader White.mat");
            Material duckShaderGreen = bundle.LoadAsset<Material>("Assets/Items/PetDuck/DuckShader Green.mat");
            Material duckShaderGold = bundle.LoadAsset<Material>("Assets/Items/PetDuck/DuckShader Gold.mat");

            PetDuckAI.materials.Add((configTextureGold.Value, duckShaderGold));
            PetDuckAI.materials.Add((configTextureGreen.Value, duckShaderGreen));
            PetDuckAI.materials.Add((configTextureWhite.Value, duckShaderWhite));

            Item petDuck = bundle.LoadAsset<Item>("Assets/Items/PetDuck/PetDuckItem.asset");
            PetDuckAI petDuckAI = petDuck.spawnPrefab.AddComponent<PetDuckAI>();
            petDuckAI.itemCapacity = configCarryAmount.Value;
            petDuckAI.canGrabTwoHanded = configCanGrabTwoHanded.Value;
            petDuckAI.canGrabHive = configCanGrabHive.Value;
            petDuckAI.canUseItem = configCanUseItem.Value;
            petDuckAI.speedFactor = configSpeed.Value;
            petDuckAI.hittable = configHittable.Value;
            petDuckAI.maxHp = configHp.Value;
            petDuckAI.SetScrapValue(duckPrice / 2f);

            Item petDuckHat = bundle.LoadAsset<Item>("Assets/Items/PetDuck/PetDuckHatItem.asset");
            PetDuckAI petDuckHatAI = petDuckHat.spawnPrefab.AddComponent <PetDuckAI>();
            petDuckHatAI.itemCapacity = configCarryAmount.Value;
            petDuckHatAI.canGrabTwoHanded = configCanGrabTwoHanded.Value;
            petDuckHatAI.canGrabHive = configCanGrabHive.Value;
            petDuckHatAI.canUseItem= configCanUseItem.Value;
            petDuckHatAI.speedFactor = configSpeed.Value;
            petDuckHatAI.hittable = configHittable.Value;
            petDuckHatAI.maxHp = configHp.Value;
            petDuckHatAI.SetScrapValue(duckPrice / 2f);

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
