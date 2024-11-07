using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;

namespace AttackStamina
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency("MK_BetterUI", BepInDependency.DependencyFlags.SoftDependency)]
    public class AttackStaminaPlugin : BaseUnityPlugin
    {
        internal const string ModName = "AttackStamina";
        internal const string ModVersion = "1.0.3";
        internal const string Author = "Azumatt";
        private const string ModGUID = $"{Author}.{ModName}";
        private static string ConfigFileName = $"{ModGUID}.cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource AttackStaminaLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion, ModRequired = false};
        public static bool flag = true;
        public static bool flag1 = false;
        public static bool hasUsedRecently = false;
        public static bool bowDrawn = false;
        public static float attackStamina = 100f;
        public static int counter = 100;
        public static int displayCounter = 0;
        public static GameObject StaminaUI;
        public static bool betterUIInstalled = false;

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
            
            Chainloader.PluginInfos.TryGetValue("MK_BetterUI", out PluginInfo? pluginInfo);
            float defaultBind;
            if (pluginInfo != null)
            {
                defaultBind = 0.0f;
                betterUIInstalled = true;
            }
            else
            {
                defaultBind = -95.0f;
            }

            displayTime = config("2 - UI", "Time Until Full Bar disappears", 75, "Time until full bar disappears", false);
            uiAnchorMin = config("2 - UI", "UI Anchor Min", new Vector2(0.5f, 0.0f), "UI Anchor min", false);
            uiAnchorMax = config("2 - UI", "UI Anchor Max", new Vector2(0.5f, 0.0f), "UI Anchor max", false);
            uiDeltaOffset = config("2 - UI", "UI Delta Offset", new Vector2(12f, 0.0f), "UI Delta Offset. This is the offset amounts from the stamina panel. Change this if the bar is not in the right position", false);
            uiAnchoredPosition = config("2 - UI", "UI Anchored Position Offset", new Vector2(0.0f, defaultBind), "UI Anchored Position Offset. This is the offset amounts from the stamina panel. Change this if the bar is not in the right position", false);
            timeTillCharging = config("3 - Recharge", "Time Until the Bar Starts to Recharge", 75, "Time until bar starts to recharge");
            AttackStaminaRecharge = config("4 - Values", "Attack Stamina Recharge Rate", 1f, "Multiple of how fast the stamina bar recharges");
            MaxAttackStamina = config("4 - Values", "Maximum Attack Stamina", 100f, "Maximum Attack Stamina");
            noStaminaJumpForce = config("4 - Values", "Jump Height When Attack Stamina is Out", 0.6f, "Jump height when out of stamina");
            noStaminaSprintDrain = config("4 - Values", "Attack Stamina drain when normal is Out", 2f, "Attack Stamina drain when normal is out");
            useNormWhenDrained = config("5 - Game Rules", "Use Normal Stamina When Attack Stamina is Out", Toggle.On, "Use normal stamina when attack stamina is out");
            useAttackWhenDrained = config("5 - Game Rules", "Use Attack stamina when Normal Stamina is Out", Toggle.On, "Use Attack stamina when normal stamina is out");

            LoadAssets();


            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private static AssetBundle GetAssetBundleFromResources(string filename)
        {
            Assembly execAssembly = Assembly.GetExecutingAssembly();
            string resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(filename));

            using Stream? stream = execAssembly.GetManifestResourceStream(resourceName);
            return AssetBundle.LoadFromStream(stream);
        }

        public static void LoadAssets()
        {
            AssetBundle? assetBundle = GetAssetBundleFromResources("attackstaminabar");
            StaminaUI = assetBundle.LoadAsset<GameObject>("AttackStaminaBar");
            assetBundle?.Unload(false);
        }


        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                AttackStaminaLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                AttackStaminaLogger.LogError($"There was an issue loading your {ConfigFileName}");
                AttackStaminaLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        public static bool UseAttackInstead(Player currentPlayer, bool crouch = false)
        {
            if (useAttackWhenDrained.Value == Toggle.Off || currentPlayer.HaveStamina())
                return false;
            if (attackStamina > 0.0 && !currentPlayer.IsSneaking() | crouch)
            {
                currentPlayer.AddStamina(1f);
                HumanoidStartAttackPatch.UseAttackStaminaMod(noStaminaSprintDrain.Value);
                return true;
            }

            Hud.instance.StaminaBarEmptyFlash();
            return true;
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<int> displayTime;
        public static ConfigEntry<Vector2> uiAnchorMin;
        public static ConfigEntry<Vector2> uiAnchorMax;
        public static ConfigEntry<Vector2> uiDeltaOffset;
        public static ConfigEntry<Vector2> uiAnchoredPosition;
        public static ConfigEntry<int> timeTillCharging;
        public static ConfigEntry<float> AttackStaminaRecharge;
        public static ConfigEntry<float> MaxAttackStamina;
        public static ConfigEntry<float> noStaminaJumpForce;
        public static ConfigEntry<float> noStaminaSprintDrain;
        public static ConfigEntry<Toggle> useNormWhenDrained;
        public static ConfigEntry<Toggle> useAttackWhenDrained;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription = new(description.Description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"), description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order = null!;
            [UsedImplicitly] public bool? Browsable = null!;
            [UsedImplicitly] public string? Category = null!;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
        }

        #endregion
    }
}