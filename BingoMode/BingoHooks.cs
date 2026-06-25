using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Expedition;
using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MoreSlugcats;
using RWCustom;
using Steamworks;
using UnityEngine;
using Watcher;

namespace BingoMode
{
    using System.IO;
    using System.Net;
    using System.Text.RegularExpressions;
    using BingoChallenges;
    using BingoHUD;
    using BingoMenu;
    using BingoSteamworks;
    using IL.JollyCoop.JollyMenu;
    using Music;
    using static BingoMode.BingoSteamworks.LobbySettings;
    using static ExtraExtentions;

    public class BingoHooks
    {
        public static BingoBoard GlobalBoard;

        public static ConditionalWeakTable<ExpeditionMenu, BingoPage> bingoPage = new();
        public static ConditionalWeakTable<CharacterSelectPage, HoldButton> newBingoButton = new();

        public static float cantpresscounter;

        public static void EarlyApply()
        {
            // Ignoring bingo challenges in bingo (has to be done here)
            On.Expedition.ChallengeOrganizer.SetupChallengeTypes += ChallengeOrganizer_SetupChallengeTypes;
            // Remove all hooks while at it
            // Add the bingo challenges when loading challenges from string
            IL.Expedition.ExpeditionCoreFile.FromString += ExpeditionCoreFile_FromStringIL;
            On.Expedition.ExpeditionCoreFile.FromString += ExpeditionCoreFile_FromString;
        }

        public static void ExpeditionCoreFile_FromString(On.Expedition.ExpeditionCoreFile.orig_FromString orig, ExpeditionCoreFile self, string saveString)
        {
            if (GlobalBoard != null)
            {
                GlobalBoard.challengeGrid = new Challenge[GlobalBoard.size, GlobalBoard.size];
                GlobalBoard.recreateList = [];
            }
            orig.Invoke(self, saveString); // IL hook
            if (GlobalBoard != null) GlobalBoard.RecreateFromList();
        }

        public static void ExpeditionCoreFile_FromStringIL(ILContext il)
        {
            ILCursor c = new(il);
            ILCursor b = new(il);
            ILCursor a = new(il);
            ILCursor d = new(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld("Expedition.ChallengeOrganizer", "availableChallengeTypes")
                ))
            {
                c.EmitDelegate<Func<List<Challenge>, List<Challenge>>>((orig) =>
                {
                    List<Challenge> newList = [.. orig, .. BingoData.availableBingoChallenges];

                    return newList;
                });
            }
            else Plugin.logger.LogError("ExpeditionCoreFile_FromStringIL 1 threw!!! " + il);

            if (a.TryGotoNext(
                x => x.MatchLdcI4(6),
                x => x.MatchNewarr<System.String>(),
                x => x.MatchDup(),
                x => x.MatchLdcI4(0),
                x => x.MatchLdstr("["),
                x => x.MatchStelemRef(),
                x => x.MatchDup(),
                x => x.MatchLdcI4(1),
                x => x.MatchLdloc(27)
                ))
            {
                a.Emit(OpCodes.Ldloc, 30);
                a.Emit(OpCodes.Ldloc, 27);
                a.EmitDelegate<Action<Challenge, SlugcatStats.Name>>((c, slug) =>
                {
                    if (GlobalBoard != null && ExpeditionData.slugcatPlayer == slug)
                    {
                        //
                        GlobalBoard.recreateList.Add(ExpeditionData.allChallengeLists[slug].Last());
                    }
                });
            }
            else Plugin.logger.LogError("ExpeditionCoreFile_FromStringIL 3 threw!!! " + il);

            if (d.TryGotoNext(MoveType.Before,
                x => x.MatchLdstr("ERROR: Problem recreating challenge type with reflection: ")
                ))
            {
                d.Emit(OpCodes.Ldloc, 28);
                d.Emit(OpCodes.Ldloc, 27);
                d.EmitDelegate<Action<string[], SlugcatStats.Name>>((array11, name) =>
                {
                    try
                    {
                        string t = array11[0];
                        Challenge challenge = (Activator.CreateInstance(BingoData.availableBingoChallenges.Find((Challenge c) => c.GetType().Name == t).GetType()) as Challenge).Generate();
                        if (challenge != null)
                        {

                            if (!ExpeditionData.allChallengeLists.ContainsKey(name))
                            {
                                ExpeditionData.allChallengeLists.Add(name, new List<Challenge>());
                            }
                            ExpeditionData.allChallengeLists[name].Add(challenge);
                            if (GlobalBoard != null) GlobalBoard.recreateList.Add(ExpeditionData.allChallengeLists[name].Last());
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.logger.LogError("Error while regenerating broken challenge, call that shit inception fr how did this happen: " + ex);
                        string combined = "";
                        foreach (string s in array11)
                        {
                            combined += s;
                        }
                        Plugin.logger.LogInfo(combined);
                        Challenge challenge = (Activator.CreateInstance(BingoData.availableBingoChallenges.Find((Challenge c) => c.GetType().Name == "BingoEatChallenge").GetType()) as Challenge).Generate();
                        //challenge.FromString(array11[1]);
                        if (challenge != null)
                        {

                            if (!ExpeditionData.allChallengeLists.ContainsKey(name))
                            {
                                ExpeditionData.allChallengeLists.Add(name, []);
                            }
                            ExpeditionData.allChallengeLists[name].Add(challenge);
                            if (GlobalBoard != null) GlobalBoard.recreateList.Add(ExpeditionData.allChallengeLists[name].Last());
                        }
                    }
                });
            }
            else Plugin.logger.LogError("ExpeditionCoreFile_FromStringIL 4 threw!!! " + il);
        }

        public static void ChallengeOrganizer_SetupChallengeTypes(On.Expedition.ChallengeOrganizer.orig_SetupChallengeTypes orig)
        {
            BingoData.availableBingoChallenges ??= [];
            orig.Invoke();
            BingoData.availableBingoChallenges.AddRange(ChallengeOrganizer.availableChallengeTypes.Where(x => x is BingoChallenge).ToList());
            ChallengeOrganizer.availableChallengeTypes.RemoveAll(x => x is BingoChallenge);
        }

        public static void Apply()
        {
            if (ModManager.Watcher) WatcherBingoHooks.Apply();
            // Adding the bingo page to exp menu
            On.Menu.ExpeditionMenu.ctor += ExpeditionMenu_ctor;
            On.Menu.ExpeditionMenu.InitMenuPages += ExpeditionMenu_InitMenuPages;
            On.Menu.ExpeditionMenu.Singal += ExpeditionMenu_Singal;
            On.Menu.ExpeditionMenu.UpdatePage += ExpeditionMenu_UpdatePage;
            IL.Menu.ExpeditionMenu.Update += ExpeditionMenu_Update_Speed;

            // Add bingo to intro roll
            On.Menu.IntroRoll.ctor += IntroRoll_ctor;

            On.Menu.MenuScene.BuildScene += MenuScene_BuildScene;

            // HOLY EGO (replace expedition button with bingo) (and replace background)
            IL.Menu.MainMenu.ctor += MainMenu_ctor;
            // HOLY EGO 2 (replace expedition word with bingo word)
            On.Menu.CharacterSelectPage.ctor += CharacterSelectPage_ctor;

            // Adding new bingo button to the character select page
            //On.Menu.ChallengeSelectPage.Singal += ChallengeSelectPage_Singal;
            On.Menu.CharacterSelectPage.UpdateStats += CharacterSelectPage_UpdateStats;
            On.Menu.CharacterSelectPage.ClearStats += CharacterSelectPage_ClearStats;
            On.Menu.CharacterSelectPage.Update += CharacterSelectPage_Update;

            // Win and lose screens
            IL.WinState.CycleCompleted += WinState_CycleCompleted;

            // Add Bingo HUD and Stop the base Expedition HUD from appearing
            On.HUD.HUD.InitSinglePlayerHud += HUD_InitSinglePlayerHud;
            On.HUD.HUD.InitSleepHud += HUD_InitSleepHud;
            On.HUD.HUD.InitFastTravelHud += HUD_InitFastTravelHud;
            //IL.HUD.HUD.InitSinglePlayerHud += HUD_InitSinglePlayerHudIL;

            // Ficks
            On.Menu.ChallengeSelectPage.SetUpSelectables += ChallengeSelectPage_SetUpSelectables;

            // Unlocks butone
            On.Menu.UnlockDialog.Singal += UnlockDialog_Singal;
            On.Menu.UnlockDialog.Update += UnlockDialog_Update;
            // Needs to be done to grey out multiple groups of perks over different pages
            On.Menu.UnlockDialog.UpdateSelectables += UnlockDialog_UpdateSelectables;

            // Passage butone
            On.Menu.SleepAndDeathScreen.AddSubObjects += SleepAndDeathScreen_AddSubObjects;

            // Saving and loading shit
            On.Menu.CharacterSelectPage.AbandonButton_OnPressDone += CharacterSelectPage_AbandonButton_OnPressDone;

            // Preventing expedition antics
            IL.RainWorldGame.GoToDeathScreen += RainWorldGame_GoToDeathScreen;

            // Multiplayer lobbies slider
            On.Menu.ExpeditionMenu.SliderSetValue += ExpeditionMenu_SliderSetValue;
            On.Menu.ExpeditionMenu.ValueOfSlider += ExpeditionMenu_ValueOfSlider;

            // Remove challenge preview list
            On.Menu.CharacterSelectPage.UpdateChallengePreview += CharacterSelectPage_UpdateChallengePreview;

            // Make everyone quit if the host quits
            On.ProcessManager.RequestMainProcessSwitch_ProcessID_float += ProcessManager_RequestMainProcessSwitch_ProcessID_float;

            // Request host upkeep when going back to the game
            On.ShelterDoor.UpdatePathfindingCreatures += ShelterDoor_UpdatePathfindingCreatures; // Lil update thing
            On.ShelterDoor.Update += ShelterDoor_Update;

            // No red karma 1 + vanilla echo fix
            IL.Menu.KarmaLadder.KarmaSymbol.Update += KarmaSymbol_UpdateIL;

            // All cats unlocked because you're adults or smth
            On.Expedition.ExpeditionProgression.CheckUnlocked += ExpeditionData_CheckUnlocked;

            // Shift the position of the kills in menu
            On.Menu.SleepAndDeathScreen.Update += SleepAndDeathScreen_Update;

            // No force watch sleep screen ever
            new MonoMod.RuntimeDetour.Hook(
                typeof(SleepAndDeathScreen).GetProperty(nameof(SleepAndDeathScreen.ButtonsGreyedOut)).GetGetMethod(),
                typeof(BingoHooks).GetMethod(nameof(SleepAndDeathScreen_getButtonsGreyedOut)));

            // One passage per game
            On.Menu.SleepAndDeathScreen.AddExpeditionPassageButton += SleepAndDeathScreen_AddExpeditionPassageButton;
            IL.Menu.FastTravelScreen.Update += FastTravelScreen_Update;
            On.Menu.FastTravelScreen.Singal += FastTravelScreen_Singal;

            // Stop void win from happening
            On.Expedition.DepthsFinishScript.Update += DepthsFinishScript_Update;
            On.Player.ctor += Player_ctor;

            // Make Saint's echoes work like in their campaign
            IL.World.SpawnGhost += World_SpawnGhostIL;

            // Something something echo vanilla fix
            IL.Menu.GhostEncounterScreen.GetDataFromGame += GhostEncounterScreen_GetDataFromGameIL;

            // Same starting seed for everyone
            On.RainWorldGame.ctor += RainWorldGame_ctor;

            // Music shit (hell yeah)
            On.Menu.ExpeditionMenu.Update += ExpeditionMenu_Update;
            if (!ModManager.ActiveMods.Any(x => x.id == "crs"))
            {
                //Plugin.logger.LogMessage("No CRS. Applying async audio loading");
                IL.Music.MusicPiece.SubTrack.Update += SubTrack_Update;
            }

            // Fix crash when saved selected slug was removed
            On.Menu.CharacterSelectPage.UpdateSelectedSlugcat += CharacterSelectPage_UpdateSelectedSlugcat;

            // Fix duplicated starting perk objects
            IL.Room.Loaded += Room_LoadedIL;

            // Credits
            On.ProcessManager.PostSwitchMainProcess += ProcessManager_PostSwitchMainProcess;

            // Translate combo boxes because they don't for some reason
            On.Menu.Remix.MixedUI.OpComboBox._GetDisplayValue += OpComboBox__GetDisplayValue;

            // Flabberghasted this never got unloaded
            On.Menu.Menu.ShutDownProcess += Menu_ShutDownProcess;
        }

        private static void Menu_ShutDownProcess(On.Menu.Menu.orig_ShutDownProcess orig, Menu.Menu self)
        {
            orig.Invoke(self);

            if (self is not Menu.ExpeditionMenu) return;
            BingoData.globalMenu = null;
        }

        private static void ProcessManager_PostSwitchMainProcess(On.ProcessManager.orig_PostSwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
        {
            if (ID == BingoEnums.BingoCredits)
            {
                self.currentMainLoop = new BingoCredits(self);
            }

            orig.Invoke(self, ID);
        }

        private static void Room_LoadedIL(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(
                x => x.MatchLdsfld("ModManager", "Expedition")
                ) && c.TryGotoNext(
                x => x.MatchLdsfld("ModManager", "Expedition")
                ) && c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld("ModManager", "Expedition")
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<bool, Room, bool>>((orig, self) =>
                {
                    if (BingoData.BingoSaves.TryGetValue(ExpeditionData.slugcatPlayer, out var data) && data.firstCycleSaved)
                    {
                        orig = false;
                    }

                    return orig;
                });
            }
            else Plugin.logger.LogError("BingoHooks Room_LoadedIL FAIULRE " + il);
        }

        private static void CharacterSelectPage_UpdateSelectedSlugcat(On.Menu.CharacterSelectPage.orig_UpdateSelectedSlugcat orig, CharacterSelectPage self, int num)
        {
            if (num < 0 || num >= ExpeditionGame.playableCharacters.Count) num = 1;
            orig.Invoke(self, num);
        }

        // Credit to CRS for this code
        private static void SubTrack_Update(ILContext il)
        {
            var c = new ILCursor(il);
            while (c.TryGotoNext(MoveType.AfterLabel,
                x => x.MatchCall<AssetManager>(nameof(AssetManager.SafeWWWAudioClip))
                ))
            {
                c.Remove();
                c.EmitDelegate(AsyncLoad);
            }
        }
        public static AudioClip AsyncLoad(string path, bool threeD, bool stream, AudioType audioType)
        {
            WWW www = new WWW(path);
            return www.GetAudioClip(false, true, AudioType.OGGVORBIS);
        }
        //

        public static void RequestBingoSong(MusicPlayer self, string songName)
        {
            //if (self.song != null && self.song is BingoSong s)
            //{
            //    return;
            //}
            //if (self.nextSong != null && self.nextSong is BingoSong)
            //{
            //    return;
            //}
            if (!self.manager.rainWorld.setup.playMusic)
            {
                return;
            }
            Song song = new BingoSong(self, songName);
            if (self.song == null)
            {
                self.song = song;
                self.song.playWhenReady = true;
                return;
            }
            self.nextSong = song;
            self.nextSong.playWhenReady = false;
        }

        private static void ExpeditionMenu_Update(On.Menu.ExpeditionMenu.orig_Update orig, ExpeditionMenu self)
        {
            if (!self.muted && Plugin.PluginInstance.BingoConfig.PlayMenuSong.Value && self.manager?.musicPlayer != null && self.currentPage == 4 && (self.manager.musicPlayer.song == null || self.manager.musicPlayer.song.name == ExpeditionData.menuSong))
            {
                if (self.manager.musicPlayer.song != null)
                {
                    self.manager.musicPlayer.song.StopAndDestroy();
                    self.manager.musicPlayer.song = null;
                }
                if (ExpeditionData.slugcatPlayer == Watcher.WatcherEnums.SlugcatStatsName.Watcher)
                {
                    self.manager.musicPlayer.MenuRequestsSong("Bingo - Loops around the fast guy", 1f, 1f);
                    self.characterSelect.nowPlaying.label.text = self.Translate("Now Playing:") + "  " + "Loops around the fast guy";
                }
                else
                {
                    self.manager.musicPlayer.MenuRequestsSong("Bingo - Loops around the meattree", 1f, 1f);
                    self.characterSelect.nowPlaying.label.text = self.Translate("Now Playing:") + "  " + "Loops around the meattree";
                }
            }
            orig.Invoke(self);

        }

        private static void FastTravelScreen_Singal(On.Menu.FastTravelScreen.orig_Singal orig, FastTravelScreen self, MenuObject sender, string message)
        {
            orig.Invoke(self, sender, message);

            if (message == "HOLD TO START")
            {
                if (BingoData.BingoSaves.TryGetValue(ExpeditionData.slugcatPlayer, out var data) && !data.passageUsed)
                {
                    data.passageUsed = true;
                    BingoSaveFile.Save();
                }
            }
        }

        private static void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
        {
            orig.Invoke(self, manager);

            if (BingoData.RandomStartingSeed == -1 || !BingoData.BingoMode || !BingoData.MultiplayerGame) return;

            self.nextIssuedId = BingoData.RandomStartingSeed;
            BingoData.RandomStartingSeed = -1;
        }

        private static void GhostEncounterScreen_GetDataFromGameIL(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdarg(0),
                x => x.MatchLdarg(1),
                x => x.MatchCallOrCallvirt<KarmaLadderScreen>("GetDataFromGame")
                ))
            {
                c.MoveAfterLabels();
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<GhostEncounterScreen>>((self) =>
                {
                    if (ModManager.Expedition && self.manager.rainWorld.ExpeditionMode)
                    {
                        self.preGhostEncounterKarmaCap = 0;
                    }
                });
            }
            else Plugin.logger.LogError("GhostEncounterScreen_GetDataFromGameIL FAILURE " + il);
        }

        private static void World_SpawnGhostIL(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld<ModManager>("MSC"),
                x => x.MatchBrfalse(out _),
                x => x.MatchLdsfld<ModManager>("Expedition"),
                x => x.MatchBrfalse(out _)
                ))
            {
                c.Index -= 1;
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<bool, World, bool>>((orig, self) =>
                {
                    if (BingoData.BingoMode)
                    {
                        string ghost = GhostWorldPresence.GetGhostID(self.region.name).value;
                        if (ExpeditionData.challengeList.Any(x => x is BingoEchoChallenge))
                        {
                            return false;
                        }
                    }
                    return orig;
                });
            }
            else Plugin.logger.LogError("World_SpawnGhostIL FAILURE " + il);
        }

        private static void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            orig.Invoke(self, abstractCreature, world);

            if (!BingoData.CreateKarmaFlower || !self.room.abstractRoom.shelter) return;
            if (ExpeditionData.slugcatPlayer.value == "Saint")
            {

                AbstractSpear spear = new AbstractSpear(world, null, self.room.GetWorldCoordinate(self.mainBodyChunk.pos), self.room.game.GetNewID(), false, Mathf.Lerp(0.35f, 0.6f, Custom.ClampedRandomVariation(0.5f, 0.5f, 2f)));
                self.room.abstractRoom.entities.Add(spear);
                spear.RealizeInRoom();
                spear.realizedObject.firstChunk.HardSetPosition(self.firstChunk.pos);
                BingoData.CreateKarmaFlower = false;
                return;
            }

            AbstractConsumable karmaflow = new(world, AbstractPhysicalObject.AbstractObjectType.KarmaFlower, null, self.room.GetWorldCoordinate(self.mainBodyChunk.pos), self.room.game.GetNewID(), -1, -1, null);
            self.room.abstractRoom.entities.Add(karmaflow);
            karmaflow.RealizeInRoom();
            BingoData.CreateKarmaFlower = false;
        }

        private static void DepthsFinishScript_Update(On.Expedition.DepthsFinishScript.orig_Update orig, DepthsFinishScript self, bool eu)
        {
            if (BingoData.BingoMode)
            {
                self.evenUpdate = eu;

                if (self.room != null)
                {
                    if (self.room.shortCutsReady && self.room.abstractRoom.name == "SB_A14")
                    {
                        self.room.shortcuts[0].shortCutType = ShortcutData.Type.DeadEnd;
                    }
                    if (!self.triggered)
                    {
                        for (int i = 0; i < self.room.updateList.Count; i++)
                        {
                            if (self.room.updateList[i] is RoomSpecificScript.SB_A14KarmaIncrease)
                            {
                                (self.room.updateList[i] as RoomSpecificScript.SB_A14KarmaIncrease).Destroy();
                            }
                            if (ModManager.MSC && self.room.updateList[i] is MSCRoomSpecificScript.VS_E05WrapAround)
                            {
                                (self.room.updateList[i] as MSCRoomSpecificScript.VS_E05WrapAround).Destroy();
                            }
                            if (self.room.abstractRoom.name == "SB_A14" && self.room.updateList[i] is Player p && p.mainBodyChunk.pos.x < 550f)
                            {
                                p.Die();
                                BingoData.CreateKarmaFlower = true;
                                self.triggered = true;
                            }
                            if (self.room.abstractRoom.name == "SB_E05SAINT" && self.room.updateList[i] is Player p2 && p2.mainBodyChunk.pos.y < 0f)
                            {
                                p2.Die();
                                BingoData.CreateKarmaFlower = true;
                                self.triggered = true;
                            }
                        }
                    }
                }
                return;
            }
            orig.Invoke(self, eu);
        }

        private static void FastTravelScreen_Update(ILContext il)
        {
            //ILCursor c = new(il);
            //
            //ILLabel label = null;
            //if (c.TryGotoNext(MoveType.Before,
            //    x => x.MatchBr(out label),
            //    x => x.MatchLdarg(0),
            //    x => x.MatchLdfld<MainLoopProcess>("manager"),
            //    x => x.MatchLdsfld<ProcessManager.ProcessID>("MainMenu"),
            //    x => x.MatchCallOrCallvirt<ProcessManager>("RequestMainProcessSwitch")
            //    ))
            //{
            //    if (label == null) return;
            //    c.Index += 1;
            //    c.MoveAfterLabels();
            //    c.EmitDelegate<Func<bool>>(() =>
            //    {
            //        if (BingoData.BingoMode) return true;
            //        return false;
            //    });
            //    c.Emit(OpCodes.Brtrue, label);
            //}
            //else Plugin.logger.LogError("FastTravelScreen_Update FAILURE " + il);
        }

        private static void SleepAndDeathScreen_AddExpeditionPassageButton(On.Menu.SleepAndDeathScreen.orig_AddExpeditionPassageButton orig, SleepAndDeathScreen self)
        {
            if (self.RippleLadderMode) return;
            if (BingoData.BingoMode)
            {
                bool available = false;
                if (BingoData.BingoSaves.TryGetValue(ExpeditionData.slugcatPlayer, out var data) && !data.passageUsed)
                {
                    available = true;
                }

                self.expPassage = new SimpleButton(self, self.pages[0], self.Translate("PASSAGE"), "EXPPASSAGE", new Vector2(self.LeftHandButtonsPosXAdd + self.manager.rainWorld.options.SafeScreenOffset.x, Mathf.Max(self.manager.rainWorld.options.SafeScreenOffset.y, 15f)), new Vector2(110f, 30f));
                self.pages[0].subObjects.Add(self.expPassage);
                self.expPassage.lastPos = self.expPassage.pos;
                MenuLabel menuLabel = new MenuLabel(self, self.pages[0], self.Translate("AVAILABLE: ") + (available ? "1" : "0"), new Vector2(self.expPassage.pos.x + self.expPassage.size.x / 2f, self.expPassage.pos.y + 45f), default(Vector2), false, null);
                menuLabel.label.color = new Color(0.7f, 0.7f, 0.7f);
                self.pages[0].subObjects.Add(menuLabel);

                return;
            }
            orig.Invoke(self);
        }

        private static void ShelterDoor_Update(On.ShelterDoor.orig_Update orig, ShelterDoor self, bool eu)
        {
            orig.Invoke(self, eu);

            if (!BingoData.BingoMode) return;
            if (BingoData.BingoSaves.TryGetValue(ExpeditionData.slugcatPlayer, out var data) && !data.firstCycleSaved)
            {
                BingoData.BingoSaves[ExpeditionData.slugcatPlayer].firstCycleSaved = true;
                self.room.game.rainWorld.progression.TempDiscoverShelter(self.room.abstractRoom.name);

                if (data.hostID.GetSteamID64() == default)
                {

                    foreach (Challenge challenge in ExpeditionData.challengeList)
                    {
                        if (!challenge.completed && challenge is BingoChallenge b && !b.TeamsFailed[SteamTest.team] && b.ReverseChallenge())
                        {
                            b.OnChallengeCompleted(SteamTest.team);
                        }
                    }
                    Custom.rainWorld.progression.currentSaveState.BringUpToDate(self.room.game);
                    Custom.rainWorld.progression.currentSaveState.denPosition = self.room.abstractRoom.name;
                    Custom.rainWorld.progression.SaveWorldStateAndProgression(false);
                    Expedition.Expedition.coreFile.Save(false);
                    return;
                }


                if (data.hostID.GetSteamID64() == SteamTest.selfIdentity.GetSteamID64())
                {

                    foreach (Challenge challenge in ExpeditionData.challengeList)
                    {
                        if (!challenge.completed && challenge is BingoChallenge b && b.ReverseChallenge())
                        {
                            foreach (int team in BingoData.TeamsInBingo)
                            {
                                if (b.TeamsFailed[team]) continue;
                                b.OnChallengeCompleted(team);
                            }
                        }
                    }
                    SteamFinal.BroadcastCurrentBoardState();
                    Custom.rainWorld.progression.currentSaveState.BringUpToDate(self.room.game);
                    Custom.rainWorld.progression.currentSaveState.denPosition = self.room.abstractRoom.name;
                    Custom.rainWorld.progression.SaveWorldStateAndProgression(false);
                    Expedition.Expedition.coreFile.Save(false);
                    return;
                }
                else if (!SteamFinal.ReceivedHostUpKeep)
                {
                    // Would like to pause game here and make button unpause
                    self.room.game.manager.ShowDialog(new InfoDialog(self.room.game.manager, BingoData.globalMenu.Translate("Trying to reconnect to the host.")));
                }

                Custom.rainWorld.progression.currentSaveState.BringUpToDate(self.room.game);
                Custom.rainWorld.progression.currentSaveState.denPosition = self.room.abstractRoom.name;
                Custom.rainWorld.progression.SaveWorldStateAndProgression(false);
                Expedition.Expedition.coreFile.Save(false);
            }
        }

        public static bool SleepAndDeathScreen_getButtonsGreyedOut(Func<SleepAndDeathScreen, bool> orig, SleepAndDeathScreen self)
        {
            return (!self.UsesWarpMap && self.FreezeMenuFunctions) || (self.UsesWarpMap && (self.RevealMap || self.FreezeMenuFunctions));
        }

        private static void SleepAndDeathScreen_Update(On.Menu.SleepAndDeathScreen.orig_Update orig, SleepAndDeathScreen self)
        {
            orig.Invoke(self);

            //watchercondition
            if (self.RippleLadderMode) return;

            if (!BingoData.BingoMode) return;

            if (BingoData.BingoSaves.TryGetValue(ExpeditionData.slugcatPlayer, out var data))
            {
                self.expPassage.buttonBehav.greyedOut = data.passageUsed;
            }

            if (self.goalMalnourished)
            {
                self.expPassage.buttonBehav.greyedOut = true;
            }
            if (self.hud == null || self.hud.parts == null || self.killsDisplay == null) return;
            HUD.HudPart binguHUD = self.hud.parts.FirstOrDefault(x => x is BingoHUDMain);
            if (binguHUD is BingoHUDMain hud)
            {
                self.killsDisplay.pos.x = self.LeftHandButtonsPosXAdd + 420f * hud.alpha;
            }
        }

        private static void SleepAndDeathScreen_GrafUpdate(On.Menu.SleepAndDeathScreen.orig_GrafUpdate orig, SleepAndDeathScreen self, float timeStacker)
        {
            orig.Invoke(self, timeStacker);

            BingoHUDMain binguHUD = self.hud.parts.FirstOrDefault(x => x is BingoHUDMain) as BingoHUDMain;
            if (binguHUD != null)
            {
                //self.killsDisplay
            }
        }

        private static void KarmaSymbol_UpdateIL(ILContext il)
        {
            ILCursor c = new(il);
            //ILCursor b = new(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld<ModManager>("Expedition")
                ))
            {
                c.EmitDelegate<Func<bool, bool>>((orig) =>
                {
                    if (BingoData.BingoMode) orig = false;

                    return orig;
                });
            }
            else Plugin.logger.LogError("KarmaSymbol_UpdateIL 1 FAILURE " + il);

            //if (b.TryGotoNext(MoveType.After,
            //    x => x.MatchLdflda<KarmaLadder.KarmaSymbol>("displayKarma"),
            //    x => x.MatchLdfld<IntVector2>("x"),
            //    x => x.MatchLdarg(0),
            //    x => x.MatchLdflda<KarmaLadder.KarmaSymbol>("displayKarma"),
            //    x => x.MatchLdfld<IntVector2>("y"),
            //    x => x.MatchBge(out _)
            //    ))
            //{
            //    b.Index--;
            //    b.EmitDelegate<Func<int, int>>((orig) =>
            //    {
            //        
            //        if (ModManager.Expedition && Custom.rainWorld.ExpeditionMode) return orig - 1;
            //        return orig;
            //    });
            //} 
            //else Plugin.logger.LogError("KarmaSymbol_UpdateIL 2 FAILURE " + il);
        }

        public static bool ExpeditionData_CheckUnlocked(On.Expedition.ExpeditionProgression.orig_CheckUnlocked orig, ProcessManager manager, SlugcatStats.Name slugcat)
        {
            return true;
        }

        private static void ShelterDoor_UpdatePathfindingCreatures(On.ShelterDoor.orig_UpdatePathfindingCreatures orig, ShelterDoor self)
        {
            orig.Invoke(self);
            if (!BingoData.BingoMode) return;
            if (BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer))
            {
                if (BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID.GetSteamID64() != default &&
                    BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID.GetSteamID64() == SteamTest.selfIdentity.GetSteamID64())
                {
                    SteamFinal.BroadcastCurrentBoardState();
                }
            }
        }

        private static void ProcessManager_RequestMainProcessSwitch_ProcessID_float(On.ProcessManager.orig_RequestMainProcessSwitch_ProcessID_float orig, ProcessManager self, ProcessManager.ProcessID ID, float fadeOutSeconds)
        {
            orig.Invoke(self, ID, fadeOutSeconds);

            if (ID == ProcessManager.ProcessID.MainMenu)
            {
                if (ExpeditionData.challengeList != null && ExpeditionData.challengeList.Count > 0) BingoData.HookAll(ExpeditionData.challengeList, false);

                BingoData.BingoDen = "random";
                BingoData.BingoMode = false;
                BingoData.MultiplayerGame = false;
                SteamFinal.ConnectedPlayers.Clear();
                SteamFinal.ReceivedPlayerUpKeep.Clear();
                SteamFinal.SendUpKeepCounter = SteamFinal.PlayerUpkeepTime;
                SteamFinal.HostUpkeep = SteamFinal.MaxHostUpKeepTime;
                SteamFinal.ReceivedHostUpKeep = false;
                SteamFinal.TryToReconnect = false;
                SpectatorHooks.UnHook();
                SteamTest.LeaveLobby();
                ChallengeHooks.revealInMemory = [];
                BingoData.CreateKarmaFlower = false;
                BingoData.RandomStartingSeed = -1;
                BingoHUDMain.ForceTallyUp = false;
                BingoData.ResetMoonDeadOverride();
                if (BingoHUDMain.ReadyForLeave)
                {
                    if (BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer) && BingoData.BingoSaves[ExpeditionData.slugcatPlayer].isHost)
                    {
                        BingoHUDMain.EndBingoSessionHost();
                    }
                    else
                    {
                        if (BingoData.BingoSaves != null && BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer)) BingoData.BingoSaves.Remove(ExpeditionData.slugcatPlayer);
                        Custom.rainWorld.processManager.rainWorld.progression.WipeSaveState(ExpeditionData.slugcatPlayer);
                        BingoData.FinishBingo();
                    }
                    BingoHUDMain.ReadyForLeave = false;
                }
                else
                {
                    if (BingoData.BingoMode && SteamFinal.ConnectedPlayers.Count > 0 &&
                        BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID.GetSteamID64() == SteamTest.selfIdentity.GetSteamID64())
                    {
                        foreach (var player in SteamFinal.ConnectedPlayers)
                        {
                            InnerWorkings.SendMessage("e", player);
                        }
                    }
                }
            }
        }

        private static void CharacterSelectPage_AbandonButton_OnPressDone(On.Menu.CharacterSelectPage.orig_AbandonButton_OnPressDone orig, CharacterSelectPage self, Menu.Remix.MixedUI.UIfocusable trigger)
        {
            BingoData.BingoSaves.Remove(ExpeditionData.slugcatPlayer);
            BingoSaveFile.Save();
            orig.Invoke(self, trigger);
        }

        private static void CharacterSelectPage_Update(On.Menu.CharacterSelectPage.orig_Update orig, CharacterSelectPage self)
        {
            orig.Invoke(self);
            cantpresscounter = Mathf.Max(0, cantpresscounter - 1);
            if ((self.menu as ExpeditionMenu).currentPage == 1 && Input.anyKey && Plugin.PluginInstance != null && Input.GetKey(Plugin.PluginInstance.BingoConfig.ResetBind.Value) && cantpresscounter == 0)
            {
                if (self.abandonButton != null)
                {
                    cantpresscounter = 20;
                    self.AbandonButton_OnPressDone(self.abandonButton);
                }
            }
        }

        private static float ExpeditionMenu_ValueOfSlider(On.Menu.ExpeditionMenu.orig_ValueOfSlider orig, ExpeditionMenu self, Slider slider)
        {
            if ((slider.ID == BingoEnums.MultiplayerSlider ||
                    slider.ID == BingoEnums.RandomizerSlider) &&
                    bingoPage.TryGetValue(self, out var page))
            {
                return page.ValueOfSlider(slider);
            }

            return orig.Invoke(self, slider);
        }

        private static void ExpeditionMenu_SliderSetValue(On.Menu.ExpeditionMenu.orig_SliderSetValue orig, ExpeditionMenu self, Slider slider, float f)
        {
            orig.Invoke(self, slider, f);

            if ((slider.ID == BingoEnums.MultiplayerSlider ||
                    slider.ID == BingoEnums.RandomizerSlider) &&
                    bingoPage.TryGetValue(self, out var page))
            {
                page.SliderSetValue(slider, f);
            }
        }

        private static void RainWorldGame_GoToDeathScreen(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld("ModManager", "Expedition")
                ))
            {
                c.EmitDelegate<Func<bool, bool>>((orig) =>
                {
                    if (BingoData.BingoMode) orig = false;
                    return orig;
                });
            }
            else Plugin.logger.LogError("RainWorldGame_GoToDeathScreen IL failed!!!! " + il);
        }

        private static void SleepAndDeathScreen_AddSubObjects(On.Menu.SleepAndDeathScreen.orig_AddSubObjects orig, SleepAndDeathScreen self)
        {
            orig.Invoke(self);

            if (BingoData.BingoMode)
            {
                if (!ExpeditionGame.activeUnlocks.Contains("unl-passage"))
                {
                    ExpLog.Log("Add Expedition Passage but bingo");
                    self.AddExpeditionPassageButton();
                }
            }
        }

        private static void UnlockDialog_Update(On.Menu.UnlockDialog.orig_Update orig, UnlockDialog self)
        {
            orig.Invoke(self);

            if (bingoPage.TryGetValue(self.owner.menu as ExpeditionMenu, out var pag))
            {
                self.pageTitle.x = pag.pos.x + 685f;
                self.pageTitle.y = pag.pos.y + 680f;
            }
        }

        private static void UnlockDialog_Singal(On.Menu.UnlockDialog.orig_Singal orig, UnlockDialog self, MenuObject sender, string message)
        {
            orig.Invoke(self, sender, message);

            if (message == "CLOSE")
            {
                if (bingoPage.TryGetValue(self.owner.menu as ExpeditionMenu, out var pag))
                    pag.UnlocksDialogClose();

                if (!BingoData.MultiplayerGame || SteamMatchmaking.GetLobbyOwner(SteamTest.CurrentLobby) != SteamTest.selfIdentity.GetSteamID()) return;
                if (BingoData.globalSettings.perks == LobbySettings.AllowUnlocks.Inherited)
                {
                    SteamMatchmaking.SetLobbyData(SteamTest.CurrentLobby, "perkList", Expedition.Expedition.coreFile.ActiveUnlocksString(ExpeditionGame.activeUnlocks.Where(x => x.StartsWith("unl-")).ToList()));
                }
                if (BingoData.globalSettings.burdens == LobbySettings.AllowUnlocks.Inherited)
                {
                    SteamMatchmaking.SetLobbyData(SteamTest.CurrentLobby, "burdenList", Expedition.Expedition.coreFile.ActiveUnlocksString(ExpeditionGame.activeUnlocks.Where(x => x.StartsWith("bur-")).ToList()));
                }
            }
        }

        private static void UnlockDialog_UpdateSelectables(On.Menu.UnlockDialog.orig_UpdateSelectables orig, UnlockDialog self)
        {
            orig.Invoke(self);

            if (BingoData.MultiplayerGame)
            {
                bool isHost = SteamMatchmaking.GetLobbyOwner(SteamTest.CurrentLobby) == SteamTest.selfIdentity.GetSteamID();
                foreach (var perk in self.perkButtons)
                {
                    perk.buttonBehav.greyedOut = perk.buttonBehav.greyedOut || BingoData.globalSettings.perks == AllowUnlocks.None || (BingoData.globalSettings.perks == AllowUnlocks.Inherited && !isHost);
                }
                foreach (var burden in self.burdenButtons)
                {
                    burden.buttonBehav.greyedOut = burden.buttonBehav.greyedOut || BingoData.globalSettings.burdens == AllowUnlocks.None || (BingoData.globalSettings.burdens == AllowUnlocks.Inherited && !isHost);
                }
            }
            string[] bannedBurdens = ["bur-doomed"];
            string[] bannedPerks = ["unl-passage", "unl-karma"];
            foreach (var bur in self.burdenButtons)
            {
                if (bannedBurdens.Contains(bur.signalText))
                {
                    bur.buttonBehav.greyedOut = true;
                    if (ExpeditionGame.activeUnlocks.Contains(bur.signalText)) self.ToggleBurden(bur.signalText);
                }
            }
            foreach (var per in self.perkButtons)
            {
                if (bannedPerks.Contains(per.signalText))
                {
                    per.buttonBehav.greyedOut = true;
                    if (ExpeditionGame.activeUnlocks.Contains(per.signalText)) self.ToggleBurden(per.signalText);
                }
            }
        }

        public static void ChallengeSelectPage_SetUpSelectables(On.Menu.ChallengeSelectPage.orig_SetUpSelectables orig, ChallengeSelectPage self)
        {
            if (self?.menu?.currentPage != null && self.menu.currentPage == 4) return;
            orig.Invoke(self);
        }

        public static void WinState_CycleCompleted(ILContext il)
        {
            ILCursor b = new(il);

            if (b.TryGotoNext(MoveType.After,
                x => x.MatchLdstr("Cycle complete, saving run data"),
                x => x.MatchCallOrCallvirt("Expedition.ExpLog", "Log")
                ))
            {
                b.EmitDelegate(() =>
                {
                    if (BingoData.BingoMode)
                    {
                        ExpeditionGame.expeditionComplete = false;//GlobalBoard.CheckWin();
                    }
                });
            }
            else Plugin.logger.LogError(nameof(WinState_CycleCompleted) + " Threw 2 :(( " + il);
        }

        //public static void HUD_InitSinglePlayerHudIL(ILContext il)
        //{
        //    ILCursor c = new(il);
        //
        //    if (c.TryGotoNext(MoveType.After,
        //        x => x.MatchLdsfld("ModManager", "Expedition")
        //        ))
        //    {
        //        c.EmitDelegate<Func<bool, bool>>((orig) =>
        //        {
        //            orig &= !BingoData.BingoMode;
        //
        //            return orig;
        //        });
        //    }
        //    else Plugin.logger.LogError(nameof(HUD_InitSinglePlayerHudIL) + " Threw :(( " + il);
        //}

        public static void HUD_InitSinglePlayerHud(On.HUD.HUD.orig_InitSinglePlayerHud orig, HUD.HUD self, RoomCamera cam)
        {
            bool exp = ModManager.Expedition;
            if (BingoData.BingoMode && GlobalBoard != null && GlobalBoard.challengeGrid != null)
            {
                ModManager.Expedition = false;
                if (!BingoData.SpectatorMode) self.AddPart(new BingoHUDMain(self));
            }
            orig.Invoke(self, cam);
            ModManager.Expedition = exp;
        }

        private static void HUD_InitSleepHud(On.HUD.HUD.orig_InitSleepHud orig, HUD.HUD self, SleepAndDeathScreen sleepAndDeathScreen, HUD.Map.MapData mapData, SlugcatStats charStats)
        {
            orig.Invoke(self, sleepAndDeathScreen, mapData, charStats);
            if (BingoData.BingoMode && GlobalBoard != null && GlobalBoard.challengeGrid != null)
            {
                self.AddPart(new BingoHUDMain(self));
            }
        }

        private static void HUD_InitFastTravelHud(On.HUD.HUD.orig_InitFastTravelHud orig, HUD.HUD self, HUD.Map.MapData mapData)
        {
            orig.Invoke(self, mapData);
            if (BingoData.BingoMode && GlobalBoard != null && GlobalBoard.challengeGrid != null)
            {
                self.AddPart(new BingoHUDMain(self));
            }
        }

        public static void ExpeditionMenu_ctor(On.Menu.ExpeditionMenu.orig_ctor orig, ExpeditionMenu self, ProcessManager manager)
        {
            orig.Invoke(self, manager);
            if (ExpeditionData.challengeList != null && ExpeditionData.challengeList.Count > 0) BingoData.HookAll(ExpeditionData.challengeList, false);
            self.pages.Add(new Page(self, null, "BINGO", 4));
            BingoData.globalMenu = self;
            BingoData.MultiplayerGame = false;
            SteamTest.team = 0;
            BingoData.BingoDen = "random";
            BingoData.normalBingoBoard = "Red;BingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|3|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|4|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|6|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|4|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|3|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|4|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|7|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|3|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|7|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|CyanLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|6|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|CyanLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|9|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|CyanLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|CyanLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|3|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|3|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|3|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|4|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|CyanLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|CyanLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|CyanLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|4|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|CyanLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|3|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|4|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|10|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|5|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|6|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|3|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|5|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|3|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|3|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|4|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|4|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|4|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|3|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|5|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|8|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|4|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|4|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|8|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|3|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoKillChallenge~System.String|RedLizard|Creature Type|0|creatures><System.String|Any Weapon|Weapon Used|6|weaponsnojelly><System.Int32|2|Amount|1|NULL><0><System.String|Any Region|Region|5|regions><System.Boolean|false|In one Cycle|3|NULL><System.Boolean|false|Via a Death Pit|7|NULL><System.Boolean|false|While Starving|2|NULL><System.Boolean|false|While under mushroom effect|8|NULL><0><0bChGBingoDodgeLeviathanChallenge~0><0bChGBingoDodgeLeviathanChallenge~0><0";
            SteamFinal.ConnectedPlayers.Clear();
            SteamFinal.ReceivedPlayerUpKeep.Clear();
            SteamFinal.SendUpKeepCounter = SteamFinal.PlayerUpkeepTime;
            SteamFinal.HostUpkeep = SteamFinal.MaxHostUpKeepTime;
            SteamFinal.ReceivedHostUpKeep = false;
            SteamFinal.TryToReconnect = false;
            SpectatorHooks.UnHook();
        }

        public static void ExpeditionMenu_InitMenuPages(On.Menu.ExpeditionMenu.orig_InitMenuPages orig, ExpeditionMenu self)
        {
            orig.Invoke(self);

            GlobalBoard = new BingoBoard();

            if (!bingoPage.TryGetValue(self, out _))
            {
                bingoPage.Add(self, new BingoPage(self, self.pages[4], default));
            }
            bingoPage.TryGetValue(self, out var page);
            self.pages[4].subObjects.Add(page);
            self.pages[4].pos.x -= 1500f;
        }

        public static void ExpeditionMenu_Singal(On.Menu.ExpeditionMenu.orig_Singal orig, ExpeditionMenu self, MenuObject sender, string message)
        {
            orig.Invoke(self, sender, message);

            if (self.pagesMoving) return;
            if (message == "BINGOCREDITS")
            {
                self.manager.musicPlayer?.FadeOutAllSongs(1f);
                self.manager.RequestMainProcessSwitch(BingoEnums.BingoCredits);
                self.PlaySound(SoundID.MENU_Switch_Page_In);
                return;
            }
            if (message == "NEWBINGO")
            {
                SteamTest.team = 0;
                if (bingoPage.TryGetValue(self, out var page))
                {
                    self.UpdatePage(4);
                    GlobalBoard.GenerateBoard(GlobalBoard.size);
                    if (page.grid != null)
                    {
                        page.grid.RemoveSprites();
                        page.RemoveSubObject(page.grid);
                        page.grid = null;
                    }
                    page.grid = new BingoGrid(self, page, new(self.manager.rainWorld.screenSize.x / 2f, self.manager.rainWorld.screenSize.y / 2f), 500f);
                    page.subObjects.Add(page.grid);
                    self.MovePage(new Vector2(1500f, 0f));


                    string[] bannedBurdens = ["bur-doomed"];
                    string[] bannedPerks = ["unl-passage", "unl-karma"];
                    ExpeditionGame.activeUnlocks.RemoveAll(x => bannedBurdens.Contains(x) || bannedPerks.Contains(x));

                    if (Plugin.PluginInstance.BingoConfig.PlayMenuSong.Value && self.manager.musicPlayer != null && !self.muted)
                    {
                        self.manager.musicPlayer.FadeOutAllSongs(30f);
                    }

                    self.manualButton.signalText = "BINGOCREDITS";
                    self.manualButton.menuLabel.text = self.Translate("CREDITS");
                }
            }
            if (message == "LOADBINGO")
            {
                BingoData.InitializeBingo();
                LoadBingoNoStart();
                BingoData.RedoTokens();

                if (ModManager.CoopAvailable)
                {
                    for (int i = 1; i < self.manager.rainWorld.options.JollyPlayerCount; i++)
                    {
                        self.manager.rainWorld.RequestPlayerSignIn(i, null);
                    }
                    for (int j = self.manager.rainWorld.options.JollyPlayerCount; j < 4; j++)
                    {
                        self.manager.rainWorld.DeactivatePlayer(j);
                    }
                }

                self.manager.arenaSitting = null;
                self.manager.rainWorld.progression.currentSaveState = null;
                self.manager.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat = ExpeditionData.slugcatPlayer;

                if (self.manager.rainWorld.progression.IsThereASavedGame(ExpeditionData.slugcatPlayer))
                {
                    Expedition.Expedition.coreFile.Save(false);
                    self.manager.menuSetup.startGameCondition = ProcessManager.MenuSetup.StoryGameInitCondition.Load;
                    self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.Game);
                    self.PlaySound(SoundID.MENU_Continue_Game);
                }
            }
        }

        public static void LoadBingoNoStart()
        {
            if (BingoData.BingoSaves == null || !BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer)) return;
            if (BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID.GetSteamID64() != default && BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID.GetSteamID64() != SteamTest.selfIdentity.GetSteamID64())
            {
                SteamFinal.TryToReconnect = true;
                SteamFinal.HostUpkeep = 0;
            }

            int size = BingoData.BingoSaves[ExpeditionData.slugcatPlayer].size;
            GlobalBoard.size = size;
            GlobalBoard.challengeGrid = new Challenge[size, size];
            int chIndex = 0;
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    GlobalBoard.challengeGrid[i, j] = ExpeditionData.challengeList[chIndex++];
                    //ExpeditionData.challengeList.Add(GlobalBoard.challengeGrid[i, j]);
                }
            }
            SteamTest.team = BingoData.BingoSaves[ExpeditionData.slugcatPlayer].team;
            //if (BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID.GetSteamID64() == default) SteamTest.team = BingoPage.TeamNumber(Plugin.bingoConfig.SinglePlayerTeam.Value);
            //else 
            if (SteamTest.team == 8)
            {
                SpectatorHooks.Hook();
            }
        }

        public static void ExpeditionMenu_UpdatePage(On.Menu.ExpeditionMenu.orig_UpdatePage orig, ExpeditionMenu self, int pageIndex)
        {
            if (pageIndex == 4)
            {
                self.exitButton.RemoveSubObject(self.exitButton);
                self.exitButton = new SimpleButton(self, self.pages[self.currentPage], self.Translate("BACK"), "EXIT", new Vector2(self.leftAnchor + 50f, 695f), new Vector2(100f, 30f));

                if (bingoPage.TryGetValue(self, out var page))
                {
                    self.selectedObject = page.grid;
                }
                else self.selectedObject = self.characterSelect.slugcatButtons[0];
            }

            orig.Invoke(self, pageIndex);
        }

        public static void ExpeditionMenu_Update_Speed(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchCall(typeof(Mathf), nameof(Mathf.Lerp)
                )))
            {
                c.Emit(OpCodes.Ldc_R4, 2f);

                c.Emit(OpCodes.Mul);
            }
            else Plugin.logger.LogError("ExpeditionMenu_Update_Speed broked " + il);

        }

        private static void IntroRoll_ctor(On.Menu.IntroRoll.orig_ctor orig, IntroRoll self, ProcessManager manager)
        {
            orig.Invoke(self, manager);

            string folder = $"illustrations{Path.DirectorySeparatorChar}intro_roll";

            self.illustrations[2].RemoveSprites();
            self.pages[0].subObjects.Remove(self.illustrations[2]);

            self.illustrations[2] = new MenuIllustration(self, self.pages[0], folder, ModManager.Watcher ? "intro_roll_b_bingo_watcher" : "intro_roll_b_bingo", new Vector2(0f, 0f), true, false);

            self.pages[0].subObjects.Add(self.illustrations[2]);
        }

        private static void MenuScene_BuildScene(On.Menu.MenuScene.orig_BuildScene orig, Menu.MenuScene self)
        {
            orig.Invoke(self);
            if (self.sceneID == null || (self.sceneID != BingoEnums.MainMenu_Bingo && self.sceneID != BingoEnums.WatcherExpeditionBackground)) return;

            if (self.sceneID == BingoEnums.WatcherExpeditionBackground)
            {
                self.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + "outro prince 3";
                self.AddIllustration(new MenuIllustration(self.menu, self, self.sceneFolder, "outro prince 3-1 - flat", new Vector2(683f, 384f), false, true));
                return;
            }

            self.blurMin = -0.2f;
            self.blurMax = 0.4f;

            if (ModManager.Watcher)
            {
                string folder = $"scenes{Path.DirectorySeparatorChar}main menu - bingo watcher";

                self.sceneFolder = folder;
            
                if (self.flatMode)
                {
                    self.AddIllustration(new MenuIllustration(self.menu, self, folder, "bingo - flat", new Vector2(683f, 384f), false, true));
                }
                else
                {
                    self.AddIllustration(new MenuDepthIllustration(self.menu, self, folder, "bingo - 6", new Vector2(-137f, -89f), 9f, MenuDepthIllustration.MenuShader.Normal));
                    self.AddIllustration(new MenuDepthIllustration(self.menu, self, folder, "bingo - 5", new Vector2(187f, -78f), 6f, MenuDepthIllustration.MenuShader.Normal));
                    self.AddIllustration(new MenuDepthIllustration(self.menu, self, folder, "bingo - 4", new Vector2(161f, 36f), 3f, MenuDepthIllustration.MenuShader.Normal));
                    self.AddIllustration(new MenuDepthIllustration(self.menu, self, folder, "bingo - 3", new Vector2(313f, 29f), 4f, MenuDepthIllustration.MenuShader.Normal));
                    self.AddIllustration(new MenuDepthIllustration(self.menu, self, folder, "bingo - 2", new Vector2(364f, 42f), 2.5f, MenuDepthIllustration.MenuShader.Lighten));
                    self.AddIllustration(new MenuDepthIllustration(self.menu, self, folder, "bingo - 1", new Vector2(-137f, -89f), 2f, MenuDepthIllustration.MenuShader.Normal));
                }
            }
            else
            {
                string folder = $"Scenes{Path.DirectorySeparatorChar}main menu - bingo";

                self.sceneFolder = folder;

                if (self.flatMode)
                {
                    self.AddIllustration(new MenuIllustration(self.menu, self, folder, "bingo - flat", new Vector2(683f, 384f), false, true));
                }
                else
                {
                    self.AddIllustration(new MenuDepthIllustration(self.menu, self, folder, "bingo - 8", new Vector2(-117f, -112f), 8f, MenuDepthIllustration.MenuShader.Normal));
                    self.AddIllustration(new MenuDepthIllustration(self.menu, self, folder, "bingo - 7", new Vector2(305f, -24f), 6f, MenuDepthIllustration.MenuShader.Normal));
                    self.AddIllustration(new MenuDepthIllustration(self.menu, self, folder, "bingo - 6", new Vector2(119f, 256f), 4f, MenuDepthIllustration.MenuShader.Lighten));
                    self.AddIllustration(new MenuDepthIllustration(self.menu, self, folder, "bingo - 5", new Vector2(304f, 254f), 3f, MenuDepthIllustration.MenuShader.Lighten));
                    self.AddIllustration(new MenuDepthIllustration(self.menu, self, folder, "bingo - 4", new Vector2(334f, 225f), 2.5f, MenuDepthIllustration.MenuShader.Lighten));
                    self.AddIllustration(new MenuDepthIllustration(self.menu, self, folder, "bingo - 3", new Vector2(680f, 15f), 4f, MenuDepthIllustration.MenuShader.Normal));
                    self.AddIllustration(new MenuDepthIllustration(self.menu, self, folder, "bingo - 2", new Vector2(-126f, -138f), 4f, MenuDepthIllustration.MenuShader.Normal));
                }
            }
        }

        private static void MainMenu_ctor(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchNewobj(typeof(InteractiveMenuScene))))
            {
                c.Index--;
                c.Remove();
                var field = typeof(BingoEnums).GetField(nameof(BingoEnums.MainMenu_Bingo));
                c.Emit(OpCodes.Ldsfld, field);
            }
            else Plugin.logger.LogError("BingoMainMenuBackgroundReplacement broked " + il);

            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdstr("EXPEDITION"),
                x => x.MatchCallOrCallvirt("Menu.Menu", "Translate")))
            {
                c.Next.Operand = "BINGO";
            }
            else Plugin.logger.LogError("BingoExpeditionButtonReplacement broked " + il);

        }

        private static void CharacterSelectPage_ctor(On.Menu.CharacterSelectPage.orig_ctor orig, CharacterSelectPage self, Menu.Menu menu, MenuObject owner, Vector2 pos)
        {
            orig.Invoke(self, menu, owner, pos);

            FAtlasElement title = Futile.atlasManager.GetElementWithName("bingotitle");
            self.pageTitle.element = title;
        }

        // Creating butone
        public static void CharacterSelectPage_UpdateStats(On.Menu.CharacterSelectPage.orig_UpdateStats orig, CharacterSelectPage self)
        {
            SlugcatSelectMenu.SaveGameData saveGameData = SlugcatSelectMenu.MineForSaveData(self.menu.manager, ExpeditionData.slugcatPlayer);

            if (BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer))
            {
                bool isMultiplayer = SteamFinal.IsSaveMultiplayer(BingoData.BingoSaves[ExpeditionData.slugcatPlayer]);
                //bool isHost = BingoData.BingoSaves[ExpeditionData.slugcatPlayer].isHost; 
                bool isSpectator = BingoData.BingoSaves[ExpeditionData.slugcatPlayer].team == 8;
                if (saveGameData == null)
                {
                    BingoData.BingoSaves.Remove(ExpeditionData.slugcatPlayer);
                    goto invok;
                }
                self.slugcatDescription.text = "";
                if (!newBingoButton.TryGetValue(self, out _))
                {
                    newBingoButton.Add(self, new HoldButton(self.menu, self, isSpectator ? self.menu.Translate("CONTINUE<LINE>SPECTATING").Replace("<LINE>", "\r\n") : isMultiplayer ? self.menu.Translate("CONTINUE<LINE>MULTIPLAYER").Replace("<LINE>", "\r\n") : self.menu.Translate("CONTINUE<LINE>BINGO").Replace("<LINE>", "\r\n"), "LOADBINGO", new Vector2(680f, 210f), 30f));
                }
                newBingoButton.TryGetValue(self, out var bb);
                self.subObjects.Add(bb);
                self.abandonButton.Show();
                self.abandonButton.PosX = bb.pos.x - 55f;
                return;
            }
        invok:
            orig.Invoke(self);

            if (saveGameData != null) return;
            if (ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
            {
                if (self.confirmExpedition != null)
                {
                    self.confirmExpedition.RemoveSprites();
                    self.confirmExpedition.RemoveSubObject(self.confirmExpedition);
                }

                if (!newBingoButton.TryGetValue(self, out _))
                {
                    newBingoButton.Add(self, new HoldButton(self.menu, self, self.menu.Translate("PLAY<LINE>BINGO").Replace("<LINE>", "\r\n"), "NEWBINGO", new Vector2(680f, 180f), 30f));
                }
            }
            else
            {
                self.confirmExpedition.pos.x += 90;

                if (!newBingoButton.TryGetValue(self, out _))
                {
                    newBingoButton.Add(self, new HoldButton(self.menu, self, self.menu.Translate("PLAY<LINE>BINGO").Replace("<LINE>", "\r\n"), "NEWBINGO", new Vector2(590f, 180f), 30f));
                }
            }
            newBingoButton.TryGetValue(self, out var button);
            self.subObjects.Add(button);
        }

        private static void CharacterSelectPage_UpdateChallengePreview(On.Menu.CharacterSelectPage.orig_UpdateChallengePreview orig, CharacterSelectPage self)
        {
            orig.Invoke(self);

            if (BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer))
            {
                if (self.strikethroughs != null)
                {
                    for (int i = 0; i < self.strikethroughs.Count; i++)
                    {
                        if (self.strikethroughs[i] != null)
                        {
                            self.strikethroughs[i].RemoveFromContainer();
                        }
                    }
                }
                self.strikethroughs = new List<FSprite>();
                if (self.challengePreviews != null)
                {
                    for (int j = 0; j < self.challengePreviews.Count; j++)
                    {
                        self.challengePreviews[j].RemoveSprites();
                        self.challengePreviews[j].RemoveSubObject(self.challengePreviews[j]);
                    }
                    self.challengePreviews = new List<MenuLabel>();
                }
            }
        }

        public static void CharacterSelectPage_ClearStats(On.Menu.CharacterSelectPage.orig_ClearStats orig, CharacterSelectPage self)
        {
            orig.Invoke(self);

            if (newBingoButton.TryGetValue(self, out var button) && button != null)
            {
                button.RemoveSprites();
                button.RemoveSubObject(button);
                newBingoButton.Remove(self);
            }
        }

        private static string OpComboBox__GetDisplayValue(On.Menu.Remix.MixedUI.OpComboBox.orig__GetDisplayValue orig, Menu.Remix.MixedUI.OpComboBox self)
        {
            return ChallengeTools.IGT.Translate(orig(self));
        }
    }
}
