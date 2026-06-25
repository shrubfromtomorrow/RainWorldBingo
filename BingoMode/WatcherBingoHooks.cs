using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Expedition;
using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using UnityEngine;
using Watcher;

namespace BingoMode
{
    using BingoChallenges;
    using BingoSteamworks;
    using IL.MoreSlugcats;
    using static MonoMod.InlineRT.MonoModRule;

    public class WatcherBingoHooks
    {
        public static Dictionary<string, Menu.MenuScene.SceneID> landscapeLookup;
        private static Dictionary<Menu.MenuScene.SceneID, string> sceneToRegion;

        private static Perk_DialWarp dialWarpPerkInstance;

        public static void Apply()
        {
            // Update starting savestate
            On.SaveState.ctor += SaveState_ctor;
            // Update karma for doomed burden
            On.RainWorldGame.GoToDeathScreen += RainWorldGame_GoToDeathScreen;
            // Populate watcher story regions
            On.SlugcatStats.SlugcatStoryRegions += SlugcatStats_SlugcatStoryRegions;
            // Random starts (just to get the game to shut up before bingo random starts does its thing
            On.Expedition.ExpeditionGame.ExpeditionRandomStarts += WatcherShelters_ExpeditionGame_ExpeditionRandomStarts;
            // Kill points
            On.Expedition.ChallengeTools.GenerateCreatureScores += ChallengeTools_GenerateCreatureScores;
            // Add new creatures for kill anything that accesses creature list
            On.Expedition.ChallengeTools.AppendAdditionalCreatureSpawns += ChallengeTools_AppendAdditionalCreatureSpawns;
            // Plurals for watcher items
            On.Expedition.ChallengeTools.ItemName += ChallengeTools_ItemName;
            // Save challenges after st visit
            On.SaveState.SessionEnded += SaveState_SessionEnded;
            // Remove room scripts
            On.Expedition.ExpeditionGame.IsUndesirableRoomScript += ExpeditionGame_IsUndesirableRoomScript;
            // Make ripple symbol bad when low (doomed)
            IL.Menu.KarmaLadder.KarmaSymbol.Update += KarmaLadder_KarmaSymbol_Update;
            // Remove forced wora warp
            IL.OverWorld.InitiateSpecialWarp_WarpPoint += OverWorld_InitiateSpecialWarp_WarpPoint;
            // Add watcher as playable char
            On.Expedition.ExpeditionData.GetPlayableCharacters += ExpeditionData_GetPlayableCharacters;
            // Fix character positioning
            IL.Menu.CharacterSelectPage.ctor += CharacterSelectPage_ctor;
            // Skip spinning top dialogue always
            On.Watcher.SpinningTop.StartConversation += SpinningTop_StartConversation;
            // Prevent st from increasing min max ripple
            On.Watcher.SpinningTop.NextMinMaxRippleLevel += SpinningTop_NextMinMaxRippleLevel;
            // Prevent mapdata from being set to null for passages and Load map data in expedition mode for dial warp
            IL.Menu.FastTravelScreen.ctor += FastTravelScreen_ctor;
            // Fix foodmeter pos for warp map
            On.Menu.SleepAndDeathScreen.FoodMeterXPos += SleepAndDeathScreen_FoodMeterXPos;
            // Remove vanilla Watcher regions from choice menu
            On.Menu.FastTravelScreen.SpawnChoiceMenu += FastTravelScreen_SpawnChoiceMenu;
            // Functional egg :(
            On.Expedition.ExpeditionCoreFile.FromString += ExpeditionCoreFile_FromString;
            On.Menu.StatsDialog.ResetAll_OnPressDone += StatsDialog_ResetAll_OnPressDone;
            if (!ExpeditionGame.ePos.ContainsKey("V0FSQV9QMTc=")) ExpeditionGame.ePos.Add("V0FSQV9QMTc=", new Vector2(90f, 629f));
            // Spawn toys
            IL.Room.Loaded += Room_Loaded;
            // Karma flowers always
            IL.Room.Loaded += Room_Loaded1;
            // Fill map
            On.PlayerProgression.GetOrInitiateSaveState += PlayerProgression_GetOrInitiateSaveState;
            // Consider all regions visited on map
            IL.Watcher.WarpMap.LoadWarpConnections += WarpMap_LoadWarpConnections;

            // Stop things from breaking when expedition tries to make challenges for watcher
            On.Expedition.Challenge.ValidForThisSlugcat += Challenge_ValidForThisSlugcat;
            On.Expedition.EchoChallenge.Generate += EchoChallenge_Generate;
            On.Expedition.NeuronDeliveryChallenge.ValidForThisSlugcat += NeuronDeliveryChallenge_ValidForThisSlugcat;
            On.Expedition.PearlDeliveryChallenge.ValidForThisSlugcat += PearlDeliveryChallenge_ValidForThisSlugcat;
            On.Expedition.AchievementChallenge.ValidForThisSlugcat += AchievementChallenge_ValidForThisSlugcat;

            // Get custom region arts
            On.Region.GetRegionLandscapeScene += Region_GetRegionLandscapeScene;
            On.Menu.MenuScene.BuildScene += MenuScene_BuildScene;
            // Watcher select screen background
            On.Menu.CharacterSelectPage.UpdateSelectedSlugcat += CharacterSelectPage_UpdateSelectedSlugcat;
            // Lock everyone but washa
            //On.Expedition.ExpeditionProgression.CheckUnlocked += ExpeditionData_CheckUnlocked;
            // Skip portal pair check for weaver for goal portal
            IL.Watcher.WarpPoint.ActivateWeaver += WarpPoint_ActivateWeaver;
            // Ripple 5 on passage
            On.SaveState.ApplyCustomEndGame += SaveState_ApplyCustomEndGame;
            // Fully random warps
            On.Watcher.WarpPoint.ChooseDynamicWarpTarget += WarpPoint_ChooseDynamicWarpTarget;

            // Dial warp
            dialWarpPerkInstance = new Perk_DialWarp();
            Modding.Expedition.CustomPerks.Register(new Modding.Expedition.CustomPerk[]
            {
                dialWarpPerkInstance
            });

            // Set warp egg threshold
            {
                var targetProperty = typeof(RegionState.RippleSpawnEggState).GetProperty("WarpEggThreshold", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                var targetGetter = targetProperty?.GetGetMethod(true);

                var hookMethod = RegionState_RippleSpawnEggState_WarpEggThreshold;

                new MonoMod.RuntimeDetour.Hook(targetGetter, hookMethod);
            }
            // If dial warp perk is disabled, set ripple trees to always be fully grown
            {
                var targetProperty = typeof(RippleTree).GetProperty("GoalScale");

                var targetGetter = targetProperty?.GetGetMethod(false);

                var hookMethod = RippleTree_GoalScale;

                new MonoMod.RuntimeDetour.Hook(targetGetter, hookMethod);
            }
            // If dial warp perk is enabled, always allow player to see ripplespawn (the 600f comes from playergraphics inverse lerp, I assume it's generally the max)
            {
                var targetProperty = typeof(Player).GetProperty("rippleSpawnEggReveal");

                var targetGetter = targetProperty?.GetGetMethod(false);

                var hookMethod = Player_rippleSpawnEggReveal;

                new MonoMod.RuntimeDetour.Hook(targetGetter, hookMethod);
            }

            // Temp fix for warp points that are sealed near landing locations
            IL.Watcher.WarpPoint.Update += WarpPoint_Update;
            // Slideshows load existing save rather than loading new (mainly for visiting ST in bath on first cycle, thanks salty_syrup)
            IL.Menu.SlideShow.ctor += SlideShow_ctor;
            // Replace toys ending conditional link logic to make all waua connections open
            On.WorldLoader.Preprocessing.SpinningTopEndingConditions += Preprocessing_SpinningTopEndingConditions;
            // Prevent closed off toys room camera texture from being loaded on top of the toys room with all connections open ^
            On.RoomCamera.CameraTextureSuffixManipulator += RoomCamera_CameraTextureSuffixManipulator;
            // Prevent sawVoidBathSlideshow being set to true when seeing ST in bath to allow for continued spawns
            IL.Watcher.SpinningTop.MarkSpinningTopEncountered += SpinningTop_MarkSpinningTopEncountered;
            // Allow waua karma flower to spawn even while you haven't beaten ST
            On.KarmaFlower.CanSpawnKarmaFlower += KarmaFlower_CanSpawnKarmaFlower;

        }

        private static void WarpPoint_Update(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdarg(0),
                x => x.MatchCallOrCallvirt("Watcher.WarpPoint", "get_warpSequenceInProgress")))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<bool, WarpPoint, bool>>((origWarpSequenceInProgress, wp) =>
                {
                    if (wp?.room?.game != null)
                    {
                        // If the player is landing in a room that is not the warp point room, warp sequence in progress shouldn't matter for sealing
                        if (wp.room.abstractRoom != wp.room.game.FirstAlivePlayer.Room)
                        {
                            return false;
                        }
                    }
                    return origWarpSequenceInProgress;
                });
            }
            else Plugin.logger.LogError("WarpPoint_Update FAIULRE " + il);
        }

        private static void SaveState_ctor(On.SaveState.orig_ctor orig, SaveState self, SlugcatStats.Name saveStateNumber, PlayerProgression progression)
        {
            orig(self, saveStateNumber, progression);
            if (BingoData.BingoMode && ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
            {
                self.miscWorldSaveData.camoTutorialCounter++;
                self.miscWorldSaveData.usedCamoAbility++;
                self.miscWorldSaveData.cycleFirstStartedWarpJourney++;
                self.miscWorldSaveData.stableWarpTutorialCounter = 5;
                self.miscWorldSaveData.badWarpTutorialCounter++;
                self.miscWorldSaveData.warpFatigueTutorialCounter++;
                self.miscWorldSaveData.warpExhaustionTutorialCounter = 5;
                if (saveStateNumber == WatcherEnums.SlugcatStatsName.Watcher)
                {
                    self.deathPersistentSaveData.spinningTopRotEncounter = true;
                    self.miscWorldSaveData.numberOfPrinceEncounters = 5;
                    self.miscWorldSaveData.visitedShopRoom = true; //Makes it so you spawn in the middle of Ancient Urban
                    self.miscWorldSaveData.seenSpinningTopDream = true; //Dreams are generally disabled I think?
                    self.miscWorldSaveData.seenRotDream = true;
                    self.deathPersistentSaveData.maximumRippleLevel = 5f;
                    self.deathPersistentSaveData.minimumRippleLevel = 3f;
                    self.deathPersistentSaveData.rippleLevel = self.deathPersistentSaveData.minimumRippleLevel + ExpeditionGame.tempKarma / 2f;
                    self.deathPersistentSaveData.karmaCap = 9;
                    self.deathPersistentSaveData.spinningTopEncounters.Add(39);//WAUA_TOYS ST
                    self.deathPersistentSaveData.spinningTopEncounters.Add(15);//CC
                    self.deathPersistentSaveData.spinningTopEncounters.Add(16);//SH
                    self.deathPersistentSaveData.spinningTopEncounters.Add(17);//LF
                }
            }
        }

        
        private static void RainWorldGame_GoToDeathScreen(On.RainWorldGame.orig_GoToDeathScreen orig, RainWorldGame self)
        {
            if (BingoData.BingoMode && ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
            {
                var death = self.GetStorySession.saveState.deathPersistentSaveData;
                death.karma = (int)((death.rippleLevel - death.minimumRippleLevel) * 2f);
            }
            orig(self);
        }

        private static List<string> SlugcatStats_SlugcatStoryRegions(On.SlugcatStats.orig_SlugcatStoryRegions orig, SlugcatStats.Name i)
        {
            if (i == WatcherEnums.SlugcatStatsName.Watcher)
            {
                string[] source = new string[]
               {
                    "WARA", //Shattered Terrance (Pre-Final area)
                    "WARB", //Salination
                    "WARC", //Fetid Glen
                    "WARD", //Cold Storage
                    "WARE", //Heat Ducts
                    "WARF", //Aether Ridge
                    "WARG", //The Surface
                    "WAUA", //Ancient Urban (Final area)
                    "WBLA", //Badlands
                    "WORA", //Outer Rim
                    "WPTA", //Signal Spires
                    "WRFA", //Coral Caves
                    "WRFB", //Turbulent Pump
                    "WRRA", //Rusted Wrecks
                    "WRSA", //Daemon
                    "WSKA", //Torrential Railways
                    "WSKB", //Sunbaked Alley
                    "WSKC", //Stormy Coast
                    "WSKD", //Shrouded Stacks
                    "WSSR", //Unfortunate Evolution
                    //"WDSR", //Drainage - Rot
                    //"WGWR", //Garbage - Rot
                    //"WHIR", //Industrial - Rot
                    //"WSUR", //Outskirts - Rot
                    "WTDA", //Torrid Desert
                    "WTDB", //Desolate Tract
                    "WVWA", //Verdant Waterways
                    "WVWB", //Fractured Gateways
                    "WPGA", //Pillar Grove
                    "WMPA", //Migration Path
                };
                return source.ToList<string>();
            }
            return orig(i);
        }

        private static string WatcherShelters_ExpeditionGame_ExpeditionRandomStarts(On.Expedition.ExpeditionGame.orig_ExpeditionRandomStarts orig, RainWorld rainWorld, SlugcatStats.Name slug)
        {
            return slug == WatcherEnums.SlugcatStatsName.Watcher ? "WSKB_S06" : "SU_S01";
        }

        private static void ChallengeTools_GenerateCreatureScores(On.Expedition.ChallengeTools.orig_GenerateCreatureScores orig, ref Dictionary<string, int> dict)
        {
            orig(ref dict);
            if (ModManager.Watcher)
            {
                var newDict = new Dictionary<string, int>
                {
                    {"DrillCrab", 16},
                    {"SandGrub", 2},
                    {"Rattler", 4},
                    {"FireSprite", 4},
                    {"ScavengerTemplar", 12},
                    {"ScavengerDisciple", 12},
                    {"BlizzardLizard", 25},
                    {"BasiliskLizard", 9},
                    {"IndigoLizard", 7},
                    {"PeachLizard", 5},
                    {"Rat", 1},
                    {"Frog", 1},
                    {"Tardigrade", 2},
                    {"Angler", 8},
                    {"MothGrub", 1},
                    {"TowerCrab", 16},
                    {"Barnacle", 4},
                    {"BoxWorm", 12},
                    {"BigMoth", 8},
                    {"BigSandGrub", 4},
                    // add dlc shared (gets removed below if msc on)
                    {"SpitLizard", 11},
                    {"EelLizard", 6},
                    {"ZoopLizard", 6},
                    {"BigJelly", 20}
                };

                foreach (KeyValuePair<string, int> keyValuePair in newDict)
                {
                    if (!dict.ContainsKey(keyValuePair.Key))
                    {
                        dict.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                    else
                    {
                        dict[keyValuePair.Key] = keyValuePair.Value;
                    }
                }
            }
        }

        private static void ChallengeTools_AppendAdditionalCreatureSpawns(On.Expedition.ChallengeTools.orig_AppendAdditionalCreatureSpawns orig)
        {
            orig();

            if (!ChallengeTools.creatureSpawns.TryGetValue(WatcherEnums.SlugcatStatsName.Watcher.value, out var list)) return;

            list.AddRange(new[]
            {
                MakeCrit(DLCSharedEnums.CreatureTemplateType.BigJelly, 1),
                MakeCrit(WatcherEnums.CreatureTemplateType.FireSprite, 12),
                MakeCrit(WatcherEnums.CreatureTemplateType.Rattler, 8),
                MakeCrit(DLCSharedEnums.CreatureTemplateType.ZoopLizard, 18),
                MakeCrit(DLCSharedEnums.CreatureTemplateType.EelLizard, 8),
                MakeCrit(DLCSharedEnums.CreatureTemplateType.SpitLizard, 3),
                MakeCrit(WatcherEnums.CreatureTemplateType.SmallMoth, 12),
                MakeCrit(WatcherEnums.CreatureTemplateType.Rat, 12),
                MakeCrit(WatcherEnums.CreatureTemplateType.BoxWorm, 16),
                MakeCrit(WatcherEnums.CreatureTemplateType.SandGrub, 16),
                MakeCrit(WatcherEnums.CreatureTemplateType.BigSandGrub, 16),
                MakeCrit(WatcherEnums.CreatureTemplateType.DrillCrab, 16),
                MakeCrit(WatcherEnums.CreatureTemplateType.TowerCrab, 16),
            });
        }
        private static ChallengeTools.ExpeditionCreature MakeCrit(CreatureTemplate.Type critType, int spawns)
        {
            return new ChallengeTools.ExpeditionCreature
            {
                creature = critType,
                points = ChallengeTools.creatureScores.TryGetValue(critType.value, out int num2) ? num2 : 0,
                spawns = spawns
            };
        }

        private static string ChallengeTools_ItemName(On.Expedition.ChallengeTools.orig_ItemName orig, AbstractPhysicalObject.AbstractObjectType type)
        {
            if (type == AbstractPhysicalObject.AbstractObjectType.GraffitiBomb)
            {
                return ChallengeTools.IGT.Translate("Graffiti Bombs");
            }
            if (type == WatcherEnums.AbstractObjectType.Boomerang)
            {
                return ChallengeTools.IGT.Translate("Boomerangs");
            }
            return orig(type);
        }

        private static void SaveState_SessionEnded(On.SaveState.orig_SessionEnded orig, SaveState self, RainWorldGame game, bool survived, bool newMalnourished)
        {
            orig(self, game, survived, newMalnourished);

            if (self.sessionEndingFromSpinningTopEncounter)
            {
                if (BingoData.BingoMode && !game.GetStorySession.saveState.malnourished && ExpeditionData.challengeList != null)
                {
                    Expedition.Expedition.coreFile.Save(false);
                }
            }
        }

        private static bool ExpeditionGame_IsUndesirableRoomScript(On.Expedition.ExpeditionGame.orig_IsUndesirableRoomScript orig, UpdatableAndDeletable item)
        {
            if (item is WatcherRoomSpecificScript.WAUA_TOYS || item is WatcherRoomSpecificScript.WORA_AI || item is WatcherRoomSpecificScript.WORA_DESERT6 || item is WatcherRoomSpecificScript.WORA_KarmaSigils)
            {
                return true;
            }
            return orig(item);
        }

        private static void KarmaLadder_KarmaSymbol_Update(ILContext il)
        {
            ILCursor c = new(il);
            c.TryGotoNext(MoveType.Before,
              x => x.MatchCallvirt("RainWorld", "get_ExpeditionMode"));
            c.TryGotoNext(MoveType.After,
                x => x.MatchLdfld("Menu.KarmaLadder", "moveToKarma"));
            if (c.TryGotoPrev(MoveType.After,
                x => x.MatchLdarg(0)))
            {
                c.EmitDelegate<Func<KarmaLadder.KarmaSymbol, KarmaLadder.KarmaSymbol>>((karmaSymbol) =>
                {
                    if (karmaSymbol.rippleMode && karmaSymbol.displayKarma.x == karmaSymbol.parent.minRipple)
                    {
                        if (karmaSymbol.parent.moveToKarma == karmaSymbol.parent.minRipple)
                        {
                            karmaSymbol.waitForAnimate++;
                            if (karmaSymbol.waitForAnimate == 49)
                            {
                                karmaSymbol.parent.ticksInPhase = -1;
                                karmaSymbol.parent.phase = KarmaLadder.Phase.CapBump;
                            }
                            else
                            {
                                karmaSymbol.pulsateCounter++;
                            }
                        }
                    }
                    return karmaSymbol;
                });
            }
            else Plugin.logger.LogError("KarmaLadder_KarmaSymbol_Update FAIULRE " + il);
        }

        private static void OverWorld_InitiateSpecialWarp_WarpPoint(ILContext il)
        {
            ILCursor c = new(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchCallvirt("MiscWorldSaveData", "get_highestPrinceConversationSeen")))
            {
                c.EmitDelegate<Func<int, int>>((convos) =>
                {
                    if (BingoData.BingoMode)
                    {
                        return 20000;
                    }
                    return convos;
                });
            }
            else Plugin.logger.LogError("OverWorld_InitiateSpecialWarp_WarpPoint FAIULRE " + il);
        }

        private static List<SlugcatStats.Name> ExpeditionData_GetPlayableCharacters(On.Expedition.ExpeditionData.orig_GetPlayableCharacters orig)
        {
            var temp = orig();
            if (ModManager.Watcher)
            {
                temp.Add(WatcherEnums.SlugcatStatsName.Watcher);
            }
            return temp;
        }

        private static void CharacterSelectPage_ctor(ILContext il)
        {
            ILCursor c = new(il);
            if (c.TryGotoNext(MoveType.Before,
            x => x.MatchLdcI4(3)))
            {
                c.EmitDelegate<Func<int, int>>((exped) =>
                {
                    if (ExpeditionGame.playableCharacters[exped] == Watcher.WatcherEnums.SlugcatStatsName.Watcher)
                    {
                        return 0;
                    }
                    return exped;
                });
            }

            if (c.TryGotoNext(MoveType.After,
             x => x.MatchLdcR4(525f)))
            {
                c.EmitDelegate<Func<float, float>>((exped) =>
                {
                    return exped - 55f;
                });
            }
            if (c.TryGotoNext(MoveType.After,
            x => x.MatchLdcR4(110f),
            x => x.MatchLdloc(3)))
            {
                c.EmitDelegate<Func<int, int>>((exped) =>
                {
                    if (exped == 8)
                    {
                        return 3;
                    }
                    return exped;
                });
            }

            if (c.TryGotoNext(MoveType.After,
            x => x.MatchLdcR4(110f),
            x => x.MatchLdloc(3)))
            {
                c.EmitDelegate<Func<int, int>>((exped) =>
                {
                    if (exped == 8)
                    {
                        return 3;
                    }
                    return exped;
                });
            }

            if (c.TryGotoNext(MoveType.After,
              x => x.MatchLdcR4(900f)))
            {
                c.EmitDelegate<Func<float, float>>((exped) =>
                {
                    return exped + 55f;
                });
            }

            if (c.TryGotoNext(MoveType.After,
            x => x.MatchLdcR4(875f)))
            {
                c.EmitDelegate<Func<float, float>>((exped) =>
                {
                    return exped + 55f;
                });
            }

            if (c.TryGotoNext(MoveType.After,
            x => x.MatchLdcR4(440f)))
            {
                c.EmitDelegate<Func<float, float>>((exped) =>
                {
                    return exped - 55f;
                });
            }
            return;
        }

        private static void SpinningTop_StartConversation(On.Watcher.SpinningTop.orig_StartConversation orig, SpinningTop self)
        {
            orig(self);
            if (BingoData.BingoMode)
            {
                self.currentConversation = new SpinningTop.SpinningTopConversation(Conversation.ID.None, self, self.room.game.cameras[0].hud.dialogBox);
                self.currentConversation.events.Add(new Conversation.TextEvent(self.currentConversation, 0, "...", 300));
            }
        }

        private static Vector2 SpinningTop_NextMinMaxRippleLevel(On.Watcher.SpinningTop.orig_NextMinMaxRippleLevel orig, Room room)
        {
            if (BingoData.BingoMode)
            {
                Vector2 vec2 = new Vector2(1, 1);
                vec2.x = (room.game.session as StoryGameSession).saveState.deathPersistentSaveData.minimumRippleLevel;
                vec2.y = (room.game.session as StoryGameSession).saveState.deathPersistentSaveData.maximumRippleLevel;
                return vec2;
            }
            return orig(room);
        }

        public static void FastTravelScreen_ctor(ILContext il)
        {
            ILCursor c = new(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(ModManager), nameof(ModManager.Expedition))))
            {
                c.EmitDelegate<Func<bool, bool>>(expedition =>
                {
                    if (BingoData.BingoMode &&
                        ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
                    {
                        return false;
                    }

                    return expedition;
                });
            }
            else Plugin.logger.LogError("FastTravelScreen_ctor dial warp FAIULRE " + il);

            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchCall("Menu.FastTravelScreen", "get_WarpPointModeActive")))
            {
                c.EmitDelegate<Func<FastTravelScreen, FastTravelScreen>>((self) =>
                {
                    if (BingoData.BingoMode)
                    {
                        if (self.IsFastTravelScreen)
                        {
                            self.warpPointModeAvailable = false;
                        }
                    }
                    return self;
                });
            }
            else Plugin.logger.LogError("FastTravelScreen_ctor passage FAIULRE " + il);
        }

        public static float SleepAndDeathScreen_FoodMeterXPos(On.Menu.SleepAndDeathScreen.orig_FoodMeterXPos orig, SleepAndDeathScreen self, float down)
        {
            if (self.UsesWarpMap)
            {
                return Custom.LerpMap(self.manager.rainWorld.options.ScreenSize.x, 1024f, 1366f, self.manager.rainWorld.options.ScreenSize.x / 2f - 110f, 540f);
            }
            return orig(self, down);
        }

        private static void FastTravelScreen_SpawnChoiceMenu(On.Menu.FastTravelScreen.orig_SpawnChoiceMenu orig, FastTravelScreen self)
        {
            if (self.activeMenuSlugcat == WatcherEnums.SlugcatStatsName.Watcher)
            {
                if (self.IsFastTravelScreen)
                {
                    for (int i = self.accessibleRegions.Count - 1; 0 <= i; i--)
                    {
                        if (Region.IsWatcherVanillaRegion(self.manager.rainWorld.progression.regionNames[self.accessibleRegions[i]]))
                        {
                            self.accessibleRegions.Remove(self.accessibleRegions[i]);
                        }
                    }
                }
            }
            orig(self);
        }

        private static void SleepAndDeathScreen_GetDataFromGame(ILContext il)
        {
            ILCursor c = new(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld("ModManager", "Watcher")))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<bool, SleepAndDeathScreen, bool>>((self, package) =>
                {
                    if (!package.RippleLadderMode)
                    {
                        return false;
                    }
                    return self;
                });
            }
            else Plugin.logger.LogError("FastTravelScreen_ctor FAIULRE " + il);
        }

        private static void StatsDialog_ResetAll_OnPressDone(On.Menu.StatsDialog.orig_ResetAll_OnPressDone orig, Menu.StatsDialog self, Menu.Remix.MixedUI.UIfocusable trigger)
        {
            orig(self, trigger);
            LongerArray();
        }
        public static void LongerArray()
        {
            int[] colors = new int[20];
            for (int i = 0; i < ExpeditionData.ints.Length; i++)
            {
                colors[i] = ExpeditionData.ints[i];
            }
            ExpeditionData.ints = colors;
        }
        private static void ExpeditionCoreFile_FromString(On.Expedition.ExpeditionCoreFile.orig_FromString orig, ExpeditionCoreFile self, string saveString)
        {
            orig(self, saveString);
            LongerArray();
        }

        private static void Room_Loaded(ILContext il)
        {
            ILCursor c = new(il);
            c.Index = 4900;
            if (c.TryGotoNext(MoveType.After,
            x => x.MatchLdfld("DeathPersistentSaveData", "sawVoidBathSlideshow")))
            {
                c.EmitDelegate<Func<bool, bool>>((room) =>
                {
                    if (BingoData.BingoMode)
                    {
                        return true;
                    }
                    return room;
                });
            }
            if (c.TryGotoNext(MoveType.After,
            x => x.MatchLdfld("DeathPersistentSaveData", "sawVoidBathSlideshow")))
            {
                c.EmitDelegate<Func<bool, bool>>((room) =>
                {
                    if (BingoData.BingoMode)
                    {
                        return true;
                    }
                    return room;
                });
            }
            if (c.TryGotoNext(MoveType.After,
            x => x.MatchLdfld("DeathPersistentSaveData", "sawVoidBathSlideshow")))
            {
                c.EmitDelegate<Func<bool, bool>>((room) =>
                {
                    if (BingoData.BingoMode)
                    {
                        return true;
                    }
                    return room;
                });
            }
            if (c.TryGotoNext(MoveType.After,
            x => x.MatchLdfld("DeathPersistentSaveData", "sawVoidBathSlideshow")))
            {
                c.EmitDelegate<Func<bool, bool>>((room) =>
                {
                    if (BingoData.BingoMode)
                    {
                        return true;
                    }
                    return room;
                });
            }
        }

        public static void Room_Loaded1(ILContext il)
        {
            ILCursor c = new(il);

            bool one = c.TryGotoNext(MoveType.Before,
                x => x.MatchLdstr("Preventing natural KarmaFlower spawn"));

            if (one && c.TryGotoPrev(MoveType.After,
                x => x.MatchLdsfld("ModManager", "Expedition")))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<bool, Room, bool>>((karma, room) =>
                {
                    if (ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
                    {
                        return false;
                    }
                    return karma;
                });
            }
            else Plugin.logger.LogError("WarpMap_LoadWarpConnections FAIULRE " + il);
        }

        private static SaveState PlayerProgression_GetOrInitiateSaveState(On.PlayerProgression.orig_GetOrInitiateSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber, RainWorldGame game, ProcessManager.MenuSetup setup, bool saveAsDeathOrQuit)
        {
            SaveState saveState = orig(self, saveStateNumber, game, setup, saveAsDeathOrQuit);

            if (BingoData.BingoMode && ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
            {
                Dictionary<string, string> watcherMapPortals = BingoData.FillWatcherMapRegions();
                if (saveState.miscWorldSaveData.discoveredWarpPoints.Count == 0)
                {
                    foreach (var kvp in watcherMapPortals)
                    {
                        saveState.miscWorldSaveData.discoveredWarpPoints[kvp.Key] = kvp.Value;
                    }
                    if (ExpeditionGame.activeUnlocks.Contains("unl-watcher-dialwarp"))
                    {
                        saveState.miscWorldSaveData.hasRippleEggWarpAbility = true;
                        saveState.miscWorldSaveData.rippleEggsCollected = Plugin.PluginInstance.BingoConfig.DialCharged.Value ? Plugin.PluginInstance.BingoConfig.DialAmount.Value : 0;
                        saveState.miscWorldSaveData.rippleEggsToRespawn.Clear();
                    }
                }
            }
            return saveState;
        }

        private static void WarpMap_LoadWarpConnections(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt(typeof(List<string>).GetProperty("Item").GetGetMethod()),
                x => x.MatchCallOrCallvirt(typeof(string).GetMethod(nameof(string.ToLowerInvariant))),
                x => x.MatchCallOrCallvirt(typeof(List<string>).GetMethod(nameof(List<string>.Contains)))))
            {
                c.EmitDelegate<Func<bool, bool>>(containsResult =>
                {
                    if (BingoData.BingoMode &&
                        ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
                    {
                        return true;
                    }

                    return containsResult;
                });
            }
            else Plugin.logger.LogError("WarpMap_LoadWarpConnections FAIULRE " + il);
        }

        private static bool Challenge_ValidForThisSlugcat(On.Expedition.Challenge.orig_ValidForThisSlugcat orig, Challenge self, SlugcatStats.Name slugcat)
        {
            if (slugcat == WatcherEnums.SlugcatStatsName.Watcher)
            {
                if (self is EchoChallenge)
                {
                    return false;
                }
            }
            return orig(self, slugcat);
        }
        private static Challenge EchoChallenge_Generate(On.Expedition.EchoChallenge.orig_Generate orig, EchoChallenge self)
        {
            if (ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
            {
                //fuck you
                return new EchoChallenge
                {
                    ghost = GhostWorldPresence.GhostID.CC
                };
            }
            return orig(self);
        }
        private static bool AchievementChallenge_ValidForThisSlugcat(On.Expedition.AchievementChallenge.orig_ValidForThisSlugcat orig, AchievementChallenge self, SlugcatStats.Name slugcat)
        {
            if (slugcat == WatcherEnums.SlugcatStatsName.Watcher)
            {
                return false;
            }
            return orig(self, slugcat);
        }
        private static bool PearlDeliveryChallenge_ValidForThisSlugcat(On.Expedition.PearlDeliveryChallenge.orig_ValidForThisSlugcat orig, PearlDeliveryChallenge self, SlugcatStats.Name slugcat)
        {
            if (slugcat == WatcherEnums.SlugcatStatsName.Watcher)
            {
                return false;
            }
            return orig(self, slugcat);
        }
        private static bool NeuronDeliveryChallenge_ValidForThisSlugcat(On.Expedition.NeuronDeliveryChallenge.orig_ValidForThisSlugcat orig, NeuronDeliveryChallenge self, SlugcatStats.Name slugcat)
        {
            if (slugcat == WatcherEnums.SlugcatStatsName.Watcher)
            {
                return false;
            }
            return orig(self, slugcat);
        }

        public static bool ExpeditionData_CheckUnlocked(On.Expedition.ExpeditionProgression.orig_CheckUnlocked orig, ProcessManager manager, SlugcatStats.Name slugcat)
        {
            if (slugcat != WatcherEnums.SlugcatStatsName.Watcher)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static MenuScene.SceneID Region_GetRegionLandscapeScene(On.Region.orig_GetRegionLandscapeScene orig, string regionAcro)
        {
            MenuScene.SceneID origReturn = orig.Invoke(regionAcro);

            if (origReturn != MenuScene.SceneID.Empty)
                return origReturn;

            if (landscapeLookup == null)
                BuildLandscapeLookup();

            if (landscapeLookup.TryGetValue(regionAcro, out var scene))
                return scene;

            return origReturn;
        }

        private static void BuildLandscapeLookup()
        {
            landscapeLookup = new Dictionary<string, Menu.MenuScene.SceneID>();

            var fields = typeof(BingoEnums.LandscapeType).GetFields(
                BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(Menu.MenuScene.SceneID) &&
                    field.Name.StartsWith("Landscape_"))
                {
                    string regionCode = field.Name.Substring("Landscape_".Length);
                    var value = (Menu.MenuScene.SceneID)field.GetValue(null);

                    if (value != null)
                        landscapeLookup[regionCode] = value;
                }
            }
        }

        private static void MenuScene_BuildScene(On.Menu.MenuScene.orig_BuildScene orig, Menu.MenuScene self)
        {
            orig.Invoke(self);

            if (self.sceneID == null)
                return;

            if (sceneToRegion == null)
                BuildSceneRegionMap();

            if (!sceneToRegion.TryGetValue(self.sceneID, out string region))
                return;

            string folder = $"Scenes{Path.DirectorySeparatorChar}Landscape - {region}";
            string flatName = $"Landscape - {region} - Flat";
            string shadowName = $"Title_{region}_Shadow";
            string titleName = $"Title_{region}";

            self.sceneFolder = folder;

            self.AddIllustration(
                new MenuIllustration(self.menu, self, folder, flatName, new Vector2(683f, 384f), false, true));

            self.AddIllustration(
                new MenuIllustration(self.menu, self, "", shadowName, new Vector2(0.01f, 0.01f), true, false));

            if (self.menu.ID == ProcessManager.ProcessID.FastTravelScreen || self.menu.ID == ProcessManager.ProcessID.RegionsOverviewScreen)
            {
                self.AddIllustration(
                    new MenuIllustration(self.menu, self, "", shadowName, new Vector2(0.01f, 0.01f), true, false));

                self.AddIllustration(
                    new MenuIllustration(self.menu, self, "", titleName, new Vector2(0.01f, 0.01f), true, false));

                self.flatIllustrations[self.flatIllustrations.Count - 1].sprite.shader = self.menu.manager.rainWorld.Shaders["MenuText"];
            }
        }

        private static void BuildSceneRegionMap()
        {
            sceneToRegion = new Dictionary<Menu.MenuScene.SceneID, string>();

            var fields = typeof(BingoEnums.LandscapeType).GetFields(
                BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(Menu.MenuScene.SceneID) &&
                    field.Name.StartsWith("Landscape_"))
                {
                    var value = (Menu.MenuScene.SceneID)field.GetValue(null);
                    if (value != null)
                    {
                        string region = field.Name.Substring("Landscape_".Length);
                        sceneToRegion[value] = region;
                    }
                }
            }
        }

        private static void CharacterSelectPage_UpdateSelectedSlugcat(On.Menu.CharacterSelectPage.orig_UpdateSelectedSlugcat orig, CharacterSelectPage self, int num)
        {
            orig(self, num);
            if (ModManager.Watcher && ExpeditionGame.playableCharacters[num] == WatcherEnums.SlugcatStatsName.Watcher)
            {
                self.slugcatScene = BingoEnums.WatcherExpeditionBackground;
            }
        }

        private static void WarpPoint_ActivateWeaver(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchLdloc(out _),
                    x => x.MatchLdcI4(out _),
                    x => x.MatchCeq(),
                    x => x.MatchLdloc(out _),
                    x => x.MatchOr()
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<bool, WarpPoint, bool>>((cur, wp) =>
                {
                    if (BingoData.BingoMode && ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher && wp.Data != null && wp.Data.destRoom != null && wp.Data.destRoom == "NARNIA")
                    {
                        return false;
                    }
                    else
                    {
                        return cur;
                    }
                });
            }
            else Plugin.logger.LogError("WarpPoint_ActivateWeaver FAIULRE " + il);
        }

        private static void SaveState_ApplyCustomEndGame(On.SaveState.orig_ApplyCustomEndGame orig, SaveState self, RainWorldGame game, bool addFiveCycles)
        {
            if (BingoData.BingoMode && ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
            {
                self.deathPersistentSaveData.rippleLevel = 5;
            }
            orig(self, game, addFiveCycles);
        }

        private static string WarpPoint_ChooseDynamicWarpTarget(On.Watcher.WarpPoint.orig_ChooseDynamicWarpTarget orig, World world, string oldRoom, string targetRegion, bool badWarp, bool spreadingRot, bool playerCreated)
        {
            if (BingoData.BingoMode && ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
            {
                List<string> weaverGoalRooms = [];
                for (int i = 0; i < ExpeditionData.challengeList.Count; i++)
                {
                    if (ExpeditionData.challengeList[i] is WatcherBingoWeaverChallenge c && !c.completed && !c.TeamsCompleted[SteamTest.team] && !c.revealed)
                    {
                        weaverGoalRooms.Add(c.room.Value.ToUpperInvariant());
                    }
                }

                List<string> list = [];
                if (targetRegion != null && targetRegion.ToLowerInvariant() == "wora")
                {
                    list = WarpPoint.GetAvailableOuterRimWarpTargets(world, oldRoom, false);
                }
                else if (badWarp)
                {
                    list = WarpPoint.GetAvailableBadWarpTargets(world, oldRoom);
                }
                else
                {
                    if (targetRegion != null)
                    {
                        list = ChallengeUtils.watcherDWTSpots.Where(x => Regex.Split(x, "_")[0] == targetRegion.ToUpperInvariant() && !weaverGoalRooms.Contains(x.ToUpperInvariant())).ToList();
                    }
                    else
                    {
                        foreach (string spot in ChallengeUtils.watcherDWTSpots)
                        {
                            string region = Regex.Split(spot, "_")[0];
                            // no same region or rotted or wora
                            if (region != world.name && region != "WORA" && region != "WHIR" && region != "WSUR" && region != "WDSR" && region != "WGWR")
                            {
                                if (!weaverGoalRooms.Contains(spot.ToUpperInvariant())) list.Add(spot);
                            }
                        }
                    }
                }
                if (list.Count == 0)
                {
                    Plugin.logger.LogInfo("List count is 0, target region was: " + targetRegion);
                    return null;
                }
                return list[UnityEngine.Random.Range(0, list.Count)];
            }
            else
            {
                return orig(world, oldRoom, targetRegion, badWarp, spreadingRot, playerCreated);
            }
        }

        private static int RegionState_RippleSpawnEggState_WarpEggThreshold(Func<int> orig)
        {
            if (ModManager.Watcher && BingoData.BingoMode && ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
            {
                return Plugin.PluginInstance.BingoConfig.DialAmount.Value;
            }
            return orig();
        }

        private static float RippleTree_GoalScale(Func<RippleTree, float> orig, RippleTree self)
        {
            if (ModManager.Watcher && BingoData.BingoMode && ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher && !ExpeditionGame.activeUnlocks.Contains("unl-watcher-dialwarp"))
            {
                return 1f;
            }
            return orig(self);
        }

        private static float Player_rippleSpawnEggReveal(Func<Player, float> orig, Player self)
        {
            if (ModManager.Watcher && BingoData.BingoMode && ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher && ExpeditionGame.activeUnlocks.Contains("unl-watcher-dialwarp"))
            {
                return 600f;
            }
            return orig(self);
        }

        private static void SlideShow_ctor(ILContext il)
        {
            ILCursor c = new(il);

            FieldInfo[] relevantSlideShowIDs = [.. new string[]
            {
                "DreamSpinningTop",
                "DreamRot",
                "DreamVoidWeaver",
                "DreamTerrace",
                "EndingVoidBath"
            }.Select(s => typeof(Watcher.WatcherEnums.SlideShowID).GetField(s))];

            foreach (FieldInfo f in relevantSlideShowIDs)
            {
                c.GotoNext(x => x.MatchLdsfld(f));
                c.GotoNext(MoveType.After, x => x.MatchStfld(typeof(Menu.SlideShow).GetField(nameof(Menu.SlideShow.processAfterSlideShow))));

                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate(SetStartGameCondition);
            }

            static void SetStartGameCondition(Menu.SlideShow self)
            {
                self.manager.menuSetup.startGameCondition = ProcessManager.MenuSetup.StoryGameInitCondition.Load;
            }
        }

        private static bool? Preprocessing_SpinningTopEndingConditions(On.WorldLoader.Preprocessing.orig_SpinningTopEndingConditions orig, string text, RainWorldGame game)
        {
            bool? result = orig(text, game);
            if (result == null) return result;
            if (!BingoData.BingoMode) return result;

            if (text == "ToysEnding" || text == "PostBathScene")
            {
                return true;
            }
            return result;
        }

        private static string RoomCamera_CameraTextureSuffixManipulator(On.RoomCamera.orig_CameraTextureSuffixManipulator orig, RoomCamera self, string roomName, int camPos)
        {
            if (!BingoData.BingoMode) return orig(self, roomName, camPos);
            return "_" + (camPos + 1).ToString() + ".png";
        }

        private static void SpinningTop_MarkSpinningTopEncountered(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchStfld(typeof(DeathPersistentSaveData).GetField(nameof(DeathPersistentSaveData.sawVoidBathSlideshow)))))
            {
                c.EmitDelegate<Func<bool, bool>>(originalValue =>
                {
                    if (BingoData.BingoMode) return false;
                    return originalValue;
                });
            }
            else Plugin.logger.LogError("SpinningTop_MarkSpinningTopEncountered FAIULRE " + il);
        }

        // Hunter stuff is probably redundant with how normal bingo touches flower generation but I just want to cut out the waua stuff so it'll be like this
        private static bool KarmaFlower_CanSpawnKarmaFlower(On.KarmaFlower.orig_CanSpawnKarmaFlower orig, Room room)
        {
            if (!BingoData.BingoMode) return orig(room);
            return room.game.StoryCharacter != SlugcatStats.Name.Red;
        }
    }

    public class Perk_DialWarp : Modding.Expedition.CustomPerk
    {
        public override string ID
        {
            get
            {
                return "unl-watcher-dialwarp";
            }
        }
        public override bool UnlockedByDefault
        {
            get
            {
                return true;
            }
        }
        public override Color Color
        {
            get
            {
                return RainWorld.RippleColor;
            }
        }
        public override string SpriteName
        {
            get
            {
                return "Symbol_DialWarpPerk";
            }
        }
        public override string ManualDescription
        {
            get
            {
                return this.Description;
            }
        }
        public override string Description
        {
            get
            {
                return BingoData.globalMenu.Translate("Dial Warp (Ripple Egg Warp) is unlocked from the start. <LINE>Watcher Exclusive").Replace("<LINE>", "\r\n");
            }
        }
        public override string DisplayName
        {
            get
            {
                return BingoData.globalMenu.Translate("Dial Warp");
            }
        }
        public override string Group
        {
            get
            {
                return "Bingo";
            }
        }
        public override bool AvailableForSlugcat(SlugcatStats.Name name)
        {
            return name == WatcherEnums.SlugcatStatsName.Watcher;
        }
    }
}
