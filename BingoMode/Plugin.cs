using System;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using UnityEngine;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace BingoMode
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using BingoChallenges;
    using BingoHUD;
    using BingoSteamworks;

    [BepInPlugin(ID, "Bingo", VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string VERSION = "2.08";
        public const string ID = "nacu_shrub.bingomode";
        public static bool AppliedAlreadyDontDoItAgainPlease;
        public static bool AppliedAlreadyDontDoItAgainPleasePartTwo;
        internal static ManualLogSource logger;
        private BingoModOptions _bingoConfig;
        public BingoModOptions BingoConfig => _bingoConfig;
        public static Plugin PluginInstance;
        public static bool AutoRestarter;

        public void OnEnable()
        {
            PluginInstance = this;
            _bingoConfig = new BingoModOptions(this);
            //new Hook(typeof(LogEventArgs).GetMethod("ToString", BindingFlags.Default | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.InvokeMethod), AddTimeToLog);
            logger = Logger;
            On.RainWorld.OnModsInit += OnModsInit;
            On.RainWorld.PostModsInit += RainWorld_PostModsInit;
            // Always restart
            On.Menu.ModdingMenu.Singal += ModdingMenu_Singal;
            // Auto restart
            On.ModManager.ModApplyer.RequiresRestart += ModApplyer_RequiresRestart;

            BingoHooks.EarlyApply();
            BingoSaveFile.Apply();
        }

        //public static string AddTimeToLog(Func<LogEventArgs, string> orig, LogEventArgs self)
        //{
        //    return "[" + DateTime.Now.Hour + ":" + (DateTime.Now.Minute < 10 ? "0" : "") + DateTime.Now.Minute + ":" + (DateTime.Now.Second < 10 ? "0" : "") + DateTime.Now.Second + "]" + orig.Invoke(self);
        //}

        public void OnDisable()
        {
            logger = null;
        }

        public void Update()
        {
            if (_bingoConfig == null || _bingoConfig.UseMapInput.Value) return;
            if (Input.anyKeyDown && (Input.GetKeyDown(_bingoConfig.HUDKeybindKeyboard.Value) || Input.GetKeyDown(_bingoConfig.HUDKeybindC1.Value)))
            {
                BingoHUDMain.Toggled = !BingoHUDMain.Toggled;
            }
        }

        public static void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld raingame)
        { 
            orig(raingame);
            if (!AppliedAlreadyDontDoItAgainPlease)
            {
                AppliedAlreadyDontDoItAgainPlease = true;

                if (!File.Exists(AssetManager.ResolveFilePath("decals" + Path.DirectorySeparatorChar + "the_original.png")))
                {
                    logger.LogFatal("These modders are PISSING me off...");
                    return;
                }

                SteamTest.Apply();

                Futile.atlasManager.LoadAtlas("Atlases/bingomode");
                Futile.atlasManager.LoadAtlas("Atlases/bingoicons");
                BingoEnums.Register();
                // Passage screens
                BingoEnums.LandscapeType.RegisterValues();

                BingoHooks.Apply();
                ChallengeHooks.Apply();
                ChallengeUtils.Apply();
                DiscordSDK.DiscordInit.Init();

                // Timeline fix
                IL.MainLoopProcess.RawUpdate += MainLoopProcess_RawUpdate;
                MachineConnector.SetRegisteredOI(ID, PluginInstance.BingoConfig);
            }
        }

        private static void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
        {
            orig.Invoke(self);

            if (!AppliedAlreadyDontDoItAgainPleasePartTwo)
            {
                AppliedAlreadyDontDoItAgainPleasePartTwo = true;
                foreach (SlugcatStats.Name slug in Expedition.ExpeditionData.GetPlayableCharacters())
                {
                    BingoData.LoadAllBannedChallengeLists(slug);
                }
            }

            AutoRestarter = ModManager.ActiveMods.Any(x => x.id == "Gamer025.RemixAutoRestart" || x.id == "MenuFixes");
            ChallengeUtilsFiltering.ClearCache();
        }

        public static void MainLoopProcess_RawUpdate(ILContext il)
        {
            ILCursor c = new(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt<MainLoopProcess>("Update")
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<MainLoopProcess>>((process) =>
                {
                    if (process is RainWorldGame or Menu.Menu)
                    {
                        SteamFinal.ReceiveMessagesUpdate();
                    }
                });
            }
            else logger.LogError("MainLoopProcess_RawUpdate IL fail " + il);
        }

        public static void ModdingMenu_Singal(On.Menu.ModdingMenu.orig_Singal orig, Menu.ModdingMenu self, Menu.MenuObject sender, string message)
        {
            if (AutoRestarter)
            {
                orig.Invoke(self, sender, message);
                return;
            }
            if (message == "RESTART")
            {
                Process currentProcess = Process.GetCurrentProcess();
                string fileName = "\"" + currentProcess.MainModule.FileName + "\"";
                IDictionary environmentVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);
                List<string> list = new List<string>();
                foreach (object obj in environmentVariables)
                {
                    DictionaryEntry dictionaryEntry = (DictionaryEntry)obj;
                    if (dictionaryEntry.Key.ToString().StartsWith("DOORSTOP"))
                    {
                        list.Add(dictionaryEntry.Key.ToString());
                    }
                }
                foreach (string key in list)
                {
                    environmentVariables.Remove(key);
                }
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.EnvironmentVariables.Clear();
                foreach (object obj2 in environmentVariables)
                {
                    DictionaryEntry dictionaryEntry2 = (DictionaryEntry)obj2;
                    processStartInfo.EnvironmentVariables.Add((string)dictionaryEntry2.Key, (string)dictionaryEntry2.Value);
                }
                processStartInfo.UseShellExecute = false;
                processStartInfo.FileName = fileName;
                List<string> list2 = new List<string>();
                string[] commandLineArgs = Environment.GetCommandLineArgs();
                for (int i = 0; i < commandLineArgs.Length; i++)
                {
                    if (i != 0)
                    {
                        if (commandLineArgs[i] == "-logFile")
                        {
                            i++;
                        }
                        else
                        {
                            list2.Add(commandLineArgs[i]);
                        }
                    }
                }
                processStartInfo.Arguments = string.Join(" ", list2.ToArray());
                Process.Start(processStartInfo);
                Application.Quit();
            }
            orig.Invoke(self, sender, message);
        }

        // Always restart even on DLC changes
        private bool ModApplyer_RequiresRestart(On.ModManager.ModApplyer.orig_RequiresRestart orig, ModManager.ModApplyer self)
        {
            //return orig.Invoke(self);
            return true;
        }
    }
}
