﻿using BepInEx;
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
        private const string modGUID = "Dio93.DuckMod";
        private const string modName = "DuckMod";
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

            Item petDuck = bundle.LoadAsset<Item>("Assets/Items/PetDuck/PetDuckItem.asset");

            PetDuckAI.mls = mls;

            PetDuckAI petDuckAI = petDuck.spawnPrefab.AddComponent<PetDuckAI>();

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
