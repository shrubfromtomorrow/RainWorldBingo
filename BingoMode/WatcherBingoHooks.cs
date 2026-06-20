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
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using BingoChallenges;
    using BingoMode.BingoMenu;
    using BingoSteamworks;
    using MonoMod.RuntimeDetour;
    using MoreSlugcats;
    using Rewired.ControllerExtensions;
    using static BingoMode.BingoMenu.BingoMenuObjects;
    using static MonoMod.InlineRT.MonoModRule;

    public class WatcherBingoHooks
    {
        public static Dictionary<string, Menu.MenuScene.SceneID> landscapeLookup;
        private static Dictionary<Menu.MenuScene.SceneID, string> sceneToRegion;
        public static ConditionalWeakTable<CharacterSelectPage, BingoSymbolButton> watcherModeButton = new();
        // Track which warp points were meant to be Daemon warps but were modified for Watcher mode dynamic warping
        public static ConditionalWeakTable<WarpPoint, string> activeExDaemonWarps = new();

        private static Perk_DialWarp dialWarpPerkInstance;

        //private static void Template(ILContext il)
        //{
        //    ILCursor c = new ILCursor(il);
        //    if (c.TryGotoNext(MoveType.After, i => i.MatchCallOrCallvirt()))
        //    {
        //        c.EmitDelegate<Func<bool, bool>>((origRet) =>
        //        {
        //            if (BingoData.WatcherMode) return true;
        //            return origRet;
        //        });
        //    }
        //    else Plugin.logger.LogError("Template FAIULRE" + il);
        //}

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
            // Add watchermode button to character select menu
            On.Menu.CharacterSelectPage.ctor += CharacterSelectPage_ctor;
            // Fix character positioning
            IL.Menu.CharacterSelectPage.ctor += CharacterSelectPage_ctorIL;
            // Intercept watchermode toggle in signal
            On.Menu.ExpeditionMenu.Singal += ExpeditionMenu_Singal;
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
            IL.Watcher.WarpPoint.Update += WarpPoint_UpdateIL;
            // Slideshows load existing save rather than loading new (mainly for visiting ST in bath on first cycle, thanks salty_syrup)
            IL.Menu.SlideShow.ctor += SlideShow_ctor;
            // Replace toys ending conditional link logic to make all waua connections open
            On.WorldLoader.Preprocessing.SpinningTopEndingConditions += Preprocessing_SpinningTopEndingConditions;
            // Prevent closed off toys room camera texture from being loaded on top of the toys room with all connections open ^
            On.RoomCamera.CameraTextureSuffixManipulator += RoomCamera_CameraTextureSuffixManipulator;
            // Allow waua karma flower to spawn even while you haven't beaten ST
            On.KarmaFlower.CanSpawnKarmaFlower += KarmaFlower_CanSpawnKarmaFlower;
            // Allow spinning top to spawn during watchermode rather than only as watcher
            IL.Room.Loaded += Room_LoadedSTLoad;
            IL.World.SpawnGhost += World_SpawnGhost;
            // Drop items on back for cats that have it when sucked into warp point (like grasps)
            IL.Watcher.WarpPoint.SuckInCreatures += WarpPoint_SuckInCreatures;
            // Allow artificer to breath normally underwater
            On.Player.PyroDeathThreshold += Player_PyroDeathThreshold;
            // Allow artificer to use bubbleweed
            IL.BubbleGrass.Update += BubbleGrass_Update;
            // Allow all cats 0.8f lungfac like watcher
            On.SlugcatStats.ctor += SlugcatStats_ctor;
            // Give the glow by default in watchermode
            On.Player.ctor += Player_ctor;
            // Add dynamic warp points in shattered WARA_P24 and ancient urban WAUA_E02B and outer rim WORA_DESERT6 for escape as non-watcher
            On.Room.Loaded += Room_Loaded2;
            // Sluhvengers slideshow
            IL.Menu.SlideShow.ctor += SlideShow_ctor1;
            // Shartered terrance st always there not watcher
            On.Watcher.SpinningTopData.FromString += SpinningTopData_FromString;
            // "No" warp fatigue if not watcher
            On.Region.HasWarpFatigueResistance += Region_HasWarpFatigueResistance;
            // Let gourmand render barnacles into pieces with his bare fat cheeks
            On.Watcher.Barnacle.Violence += Barnacle_Violence;
            IL.Watcher.Barnacle.Collide += Barnacle_Collide;
            // Angler explodes cell and completes goal
            IL.Watcher.Angler.JawsSlamShut += Angler_JawsSlamShut;
            // monomod like, uses the static constructor for this class just when applying the hook which uses DLC enums which aren't there rahh
            if (ModManager.MSC)
            {
                // Custom Gourmand vomit items
                On.MoreSlugcats.GourmandCombos.RandomStomachItem += GourmandCombos_RandomStomachItem;
            }
            // Moths of all types have food amounts
            On.StaticWorld.InitSmallMoth += StaticWorld_InitSmallMoth;
            On.StaticWorld.InitBigMoth += StaticWorld_InitBigMoth;
            // Boneshakers have food amounts
            On.StaticWorld.InitRattler += StaticWorld_InitRattler;
            // Spearmaster gets poisoned when stabbing tardigrades
            On.Spear.HitSomething += Spear_HitSomething;
            // Tricks to stop from going to void bath slideshow (act like toys room ST) and prevent setting seenvoidbathslideshow from being set (so ST continues spawning)
            new ILHook(typeof(Watcher.SpinningTop).GetProperty("BathScene").GetGetMethod(), SpinningTop_BathScene);
            new ILHook(typeof(Watcher.SpinningTop).GetProperty("BedroomScene").GetGetMethod(), SpinningTop_BedroomScene);
            // Make mothgrubs a little easier to carry
            IL.Watcher.MothGrub.ctor += MothGrub_ctor;
            // Let arti grab mothgrubs
            On.Player.IsCreatureLegalToHoldWithoutStun += Player_IsCreatureLegalToHoldWithoutStun; ;
            // Gourmand crafting options
            IL.MoreSlugcats.GourmandCombos.GetFilteredLibraryData += GourmandCombos_GetFilteredLibraryData;
            // Add actual crafts;
            IL.MoreSlugcats.GourmandCombos.InitCraftingLibrary += GourmandCombos_InitCraftingLibrary;
            // Special exception for graffiti bombs as a consumable
            On.MoreSlugcats.GourmandCombos.CraftingResults += GourmandCombos_CraftingResults;
            // Rainmeter shows for saint
            IL.HUD.RainMeter.Draw += RainMeter_Draw;
            // Custom ripple ladder sleeping kitties
            IL.Menu.MenuScene.BuildRippleSleepScene += MenuScene_BuildRippleSleepScene;
            // Make ex-Daemon warps check for and consume karma reinforcement (property of salty syrup incorporated)
            IL.OverWorld.InitiateSpecialWarp_WarpPoint += OverWorldOnInitiateSpecialWarp_WarpPoint;
            // Delay precast of ex-Daemon warps until the last possible moment (property of salty syrup incorporated)
            IL.Watcher.WarpPoint.Update += WarpPointOnUpdate;

            #region test
            #endregion
        }

        private static void WarpPointOnUpdate(ILContext il)
        {
            ILCursor c = new(il);

            try
            {
                // Call to WarpPrecast
                c.GotoNext(x => x.MatchCallOrCallvirt(typeof(WarpPoint).GetMethod(nameof(WarpPoint.WarpPrecast))));

                // Detour to get the local variable index of player
                int playerLocal = -1;
                c.GotoPrev(x => x.MatchLdloc(out playerLocal),
                    x => x.MatchLdfld(typeof(Player).GetField(nameof(Player.warpExhausionTime))));
                
                // Right before if statement checking canPreCast
                c.GotoNext(MoveType.Before,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld(typeof(WarpPoint).GetField(nameof(WarpPoint.canPreCast), 
                        BindingFlags.NonPublic | BindingFlags.Instance)));

                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, playerLocal);
                c.EmitDelegate(SetCanPrecast);
            }
            catch (Exception e)
            {
                Plugin.logger.LogError(e);
                Plugin.logger.LogError("WarpPointOnUpdate FAILURE" + il);
            }
            return;
            
            static void SetCanPrecast(WarpPoint self, Player player)
            {
                if (BingoData.BingoMode 
                    && BingoData.WatcherMode 
                    && ExpeditionData.slugcatPlayer != SlugNameWatcher.Watcher
                    // The warp we're using is an ex-Daemon warp
                    && activeExDaemonWarps.TryGetValue(self, out _))
                {
                    // This is a sin I'm sorry
                    float num3 = 1f + Mathf.Pow(Mathf.InverseLerp(self.PullRadius, 10f, Vector2.Distance(self.pos, player.mainBodyChunk.pos)), 0.5f) * 3f;
                    num3 = self.guaranteeTrigger
                        ? 1f
                        : player.touchedNoInputCounter > 0
                            ? player.touchedNoInputCounter >= 20
                                ? num3 + Custom.LerpMap(player.touchedNoInputCounter, 20f, 120f, 0f, 3f)
                                : num3 * Custom.LerpMap(player.touchedNoInputCounter, 1f, 20f, 0.5f, 1f)
                            : !(self.triggerTime < self.triggerActivationTime / 2f)
                                ? Custom.LerpMap(self.triggerTime, self.triggerActivationTime / 2f,
                                    self.triggerActivationTime, -0.5f, -4f)
                                : num3 * 0.5f;
                    self.canPreCast = self.triggerTime + num3 >= self.triggerActivationTime;

                    if (player.room?.game?.cameras[0]?.hud?.karmaMeter != null && !player.KarmaIsReinforced)
                    {
                        player.room.game.cameras[0].hud.karmaMeter.blinkRedCounter = 2; // lol, lmao even
                    }
                }
            }
        }

        private static void OverWorldOnInitiateSpecialWarp_WarpPoint(ILContext il)
        {
            ILCursor c = new(il);

            try
            {
                // Call to ChooseDynamicWarpTarget
                c.GotoNext(x =>
                    x.MatchCallOrCallvirt(typeof(WarpPoint).GetMethod(nameof(WarpPoint.ChooseDynamicWarpTarget))));
                c.GotoPrev(x => x.MatchLdcI4(0));
                // After false is loaded for the badWarp argument
                c.GotoPrev(MoveType.After, x => x.MatchLdcI4(0));

                c.Emit(OpCodes.Ldarg_1);
                c.EmitDelegate(UseBadWarp);
            }
            catch (Exception e)
            {
                Plugin.logger.LogError(e);
                Plugin.logger.LogError("OverWorldOnInitiateSpecialWarp_WarpPoint FAILURE" + il);
            }
            return;
            
            static bool UseBadWarp(bool origRet, ISpecialWarp callback)
            {
                if (BingoData.BingoMode 
                    && BingoData.WatcherMode 
                    && ExpeditionData.slugcatPlayer != SlugNameWatcher.Watcher
                    // The warp we're using is an ex-Daemon warp
                    && callback.getSourceRoom().updateList.FirstOrDefault(u => u is WarpPoint) is WarpPoint w
                    && activeExDaemonWarps.TryGetValue(w, out _))
                {
                    RainWorldGame game = callback.getSourceRoom().game;
                    if (game.FirstAlivePlayer.realizedCreature is not Player p) return origRet;
                    
                    if (p.KarmaIsReinforced)
                    {
                        // Consume the reinforcement as part of the precast why not
                        game.GetStorySession.saveState.deathPersistentSaveData.reinforcedKarma = false;
                        game.cameras[0].hud?.karmaMeter.UpdateGraphic();
                        if (game.cameras[0].hud != null) game.cameras[0].hud.karmaMeter.forceVisibleCounter = Mathf.Max(game.cameras[0].hud.karmaMeter.forceVisibleCounter, 120);
                        return false;
                    }
                    return true;
                }
                return origRet;
            }
        }

        private static WarpPoint.WarpPointData WarpPoint_CreateOverrideData(On.Watcher.WarpPoint.orig_CreateOverrideData orig, World world, string oldRoom, string chosenRoom, Vector2? chosenDestPosition, bool limitedUse, bool playerCreated)
        {
            WarpPoint.WarpPointData origRet = orig(world, oldRoom, chosenRoom, chosenDestPosition, limitedUse, playerCreated);
            if (BingoData.BingoMode && BingoData.WatcherMode && ExpeditionData.slugcatPlayer != SlugNameWatcher.Watcher && playerCreated)
            {
                origRet.oneWay = true;
                origRet.oneWayEntrance = true;
                return origRet;
            }
            return origRet;
        }

        private static Player.BlackListReason Player_IsBlacklistedRoomFromDynamicWarpPoints(On.Player.orig_IsBlacklistedRoomFromDynamicWarpPoints orig, Player self, Room rm, bool rippleTearCheck)
        {
            Player.BlackListReason origRet = orig(self, rm, rippleTearCheck);
            if (BingoData.BingoMode && BingoData.WatcherMode && ExpeditionData.slugcatPlayer != SlugNameWatcher.Watcher && origRet == Player.BlackListReason.OtherWarps)
            {
                return Player.BlackListReason.None;
            }
            return origRet;
        }

        private static void Player_CamoUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After, i => i.MatchCallOrCallvirt(typeof(Player).GetMethod("get_watcherDynamicWarpInput"))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<bool, Player, bool>>((origRet, player) =>
                {
                    if (!origRet) return origRet;
                    if (BingoData.BingoMode && BingoData.WatcherMode && ExpeditionData.slugcatPlayer != SlugNameWatcher.Watcher)
                    {
                        if (player.room.warpPoints.Count > 0)
                        {
                            bool flag = false;
                            foreach (WarpPoint wp in player.room.warpPoints)
                            {
                                if (wp.Data.rippleWarp) flag = true;
                                break;
                            }
                            if (flag) return true;
                        }
                        return false;
                    }
                    return origRet;
                });
            }
            else Plugin.logger.LogError("Template FAIULRE" + il);
        }

        private static void Player_WatcherUpdate(On.Player.orig_WatcherUpdate orig, Player self)
        {
            orig(self);
            if (BingoData.BingoMode && BingoData.WatcherMode && ExpeditionData.slugcatPlayer != SlugNameWatcher.Watcher) self.CamoUpdate();
        }

        private static void Player_watcherDynamicWarpInput(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After, x => x.MatchCallOrCallvirt(typeof(Player).GetMethod("get_CanSpawnDynamicWarpPoints"))))
            {
                c.EmitDelegate<Func<bool, bool>>((origRet) =>
                {
                    if (BingoData.BingoMode && BingoData.WatcherMode && ExpeditionData.slugcatPlayer != SlugNameWatcher.Watcher) return true;
                    return origRet;
                });
            }
            else Plugin.logger.LogError("get_CanSpawnDynamicWarpPoints FAIULRE1" + il);
            if (c.TryGotoNext(MoveType.After, x => x.MatchCallOrCallvirt(typeof(ExtEnum<SlugName>).GetMethod("op_Equality"))))
            {
                c.EmitDelegate<Func<bool, bool>>((origRet) =>
                {
                    if (BingoData.BingoMode && BingoData.WatcherMode && ExpeditionData.slugcatPlayer != SlugNameWatcher.Watcher) return true;
                    return origRet;
                });
            }
            else Plugin.logger.LogError("get_CanSpawnDynamicWarpPoints FAIULRE2" + il);
        }

        private static void MenuScene_BuildRippleSleepScene(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After, i => i.MatchLdstr("ripple - flat - watcher")))
            {
                c.EmitDelegate<Func<string, string>>((origRet) =>
                {
                    if (BingoData.WatcherMode && ExpeditionData.slugcatPlayer != SlugNameWatcher.Watcher) return ($"ripple - flat - {ExpeditionData.slugcatPlayer}");
                    return origRet;
                });
            }
            else Plugin.logger.LogError("MenuScene_BuildRippleSleepScene FAIULRE1" + il);
            if (c.TryGotoNext(MoveType.After, i => i.MatchLdstr("ripple - flat - watcher - b")))
            {
                c.EmitDelegate<Func<string, string>>((origRet) =>
                {
                    if (BingoData.WatcherMode && ExpeditionData.slugcatPlayer != SlugNameWatcher.Watcher) return ($"ripple - flat -  {ExpeditionData.slugcatPlayer} - b");
                    return origRet;
                });
            }
            else Plugin.logger.LogError("MenuScene_BuildRippleSleepScene FAIULRE2" + il);
            if (c.TryGotoNext(MoveType.After, i => i.MatchLdstr("ripple - 1")))
            {
                c.EmitDelegate<Func<string, string>>((origRet) =>
                {
                    if (BingoData.WatcherMode && ExpeditionData.slugcatPlayer != SlugNameWatcher.Watcher) return ($"ripple - 1 - {ExpeditionData.slugcatPlayer}");
                    return origRet;
                });
            }
            else Plugin.logger.LogError("MenuScene_BuildRippleSleepScene FAIULRE3" + il);
            if (c.TryGotoNext(MoveType.After, i => i.MatchLdstr("ripple - 1b")))
            {
                c.EmitDelegate<Func<string, string>>((origRet) =>
                {
                    if (BingoData.WatcherMode && ExpeditionData.slugcatPlayer != SlugNameWatcher.Watcher) return ($"ripple - 1b - {ExpeditionData.slugcatPlayer}");
                    return origRet;
                });
            }
            else Plugin.logger.LogError("MenuScene_BuildRippleSleepScene FAIULRE4" + il);
        }


        private static void RainMeter_Draw(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After, i => i.MatchCallOrCallvirt(typeof(Region).GetMethod(nameof(Region.IsRubiconRegion)))))
            {
                c.EmitDelegate<Func<bool, bool>>((origRet) =>
                {
                    if (BingoData.WatcherMode) return true;
                    return origRet;
                });
            }
            else Plugin.logger.LogError("MothGrub_ctor FAIULRE" + il);
        }

        private static AbstractPhysicalObject GourmandCombos_CraftingResults(On.MoreSlugcats.GourmandCombos.orig_CraftingResults orig, PhysicalObject crafter, Creature.Grasp graspA, Creature.Grasp graspB)
        {
            AbstractPhysicalObject ogRet = orig(crafter, graspA, graspB);
            if (ogRet.type == BaseAOT.GraffitiBomb)
            {
                return new GraffitiBomb.AbstractGraffitiBomb(crafter.room.world, null, crafter.abstractPhysicalObject.pos, crafter.room.game.GetNewID(), -1, -1, null)
                {
                    isFresh = false,
                    isConsumed = true
                };
            }
            else return ogRet;
        }

        private static void GourmandCombos_InitCraftingLibrary(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            // This IL hook runs after everything has already been populated because the hook happens after the static constructor so uhh, gotta copy everything
            if (c.TryGotoNext(MoveType.Before, x => x.MatchLdcI4(1), x => x.MatchStloc(0)))
            {
                int num = GourmandCombos.craftingGrid_ObjectsOnly.GetLength(0);
                int num2 = GourmandCombos.craftingGrid_CrittersOnly.GetLength(0);
                GourmandCombos.objectsLibrary[AbstractPhysicalObject.AbstractObjectType.GraffitiBomb] = num;
                num++;
                GourmandCombos.objectsLibrary[WatcherEnums.AbstractObjectType.FireSpriteLarva] = num;
                num++;
                GourmandCombos.critsLibrary[WatcherEnums.CreatureTemplateType.Frog] = num2;
                num2++;
                GourmandCombos.critsLibrary[WatcherEnums.CreatureTemplateType.Tardigrade] = num2;
                num2++;
                GourmandCombos.critsLibrary[WatcherEnums.CreatureTemplateType.Rat] = num2;
                num2++;
                GourmandCombos.critsLibrary[WatcherEnums.CreatureTemplateType.SandGrub] = num2;
                num2++;

                GourmandCombos.CraftDat[,] oldObjectsOnly = GourmandCombos.craftingGrid_ObjectsOnly;
                GourmandCombos.CraftDat[,] oldCritterObjects = GourmandCombos.craftingGrid_CritterObjects;
                GourmandCombos.CraftDat[,] oldCrittersOnly = GourmandCombos.craftingGrid_CrittersOnly;

                GourmandCombos.CraftDat[,] newObjectsOnly = new GourmandCombos.CraftDat[num, num];
                GourmandCombos.CraftDat[,] newCritterObjects = new GourmandCombos.CraftDat[num2, num];
                GourmandCombos.CraftDat[,] newCrittersOnly = new GourmandCombos.CraftDat[num2, num2];

                for (int i = 0; i < oldObjectsOnly.GetLength(0); i++)
                {
                    for (int j = 0; j < oldObjectsOnly.GetLength(1); j++)
                    {
                        newObjectsOnly[i, j] = oldObjectsOnly[i, j];
                    }
                }

                for (int i = 0; i < oldCritterObjects.GetLength(0); i++)
                {
                    for (int j = 0; j < oldCritterObjects.GetLength(1); j++)
                    {
                        newCritterObjects[i, j] = oldCritterObjects[i, j];
                    }
                }

                for (int i = 0; i < oldCrittersOnly.GetLength(0); i++)
                {
                    for (int j = 0; j < oldCrittersOnly.GetLength(1); j++)
                    {
                        newCrittersOnly[i, j] = oldCrittersOnly[i, j];
                    }
                }

                GourmandCombos.craftingGrid_ObjectsOnly = newObjectsOnly;
                GourmandCombos.craftingGrid_CritterObjects = newCritterObjects;
                GourmandCombos.craftingGrid_CrittersOnly = newCrittersOnly;
                try
                {
                    PopulateGourmandCrafts();
                }
                catch (Exception e)
                {
                    Plugin.logger.LogError(e);
                }
            }
            else
            {
                Plugin.logger.LogInfo("GourmandCombos_InitCraftingLibrary borked: " + il);
            }
        }

        private static void GourmandCombos_GetFilteredLibraryData(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.After, x => x.MatchStloc(1)))
            {
                c.Emit(OpCodes.Ldloc_0);
                c.EmitDelegate((AbstractPhysicalObject.AbstractObjectType abstractObjectType) =>
                {
                    if (abstractObjectType == WatcherEnums.AbstractObjectType.Boomerang)
                    {
                        abstractObjectType = AbstractPhysicalObject.AbstractObjectType.Rock;
                    }
                    return abstractObjectType;
                });
                c.Emit(OpCodes.Stloc_0);
                c.Emit(OpCodes.Ldloc_1);
                c.EmitDelegate((AbstractPhysicalObject.AbstractObjectType abstractObjectType2) =>
                {
                    if (abstractObjectType2 == WatcherEnums.AbstractObjectType.Boomerang)
                    {
                        abstractObjectType2 = AbstractPhysicalObject.AbstractObjectType.Rock;
                    }
                    return abstractObjectType2;
                });
                c.Emit(OpCodes.Stloc_1);
            }
            else
            {
                Plugin.logger.LogInfo("GourmandCombos_GetFilteredLibraryData borked: " + il);
            }
        }

        private static bool Player_IsCreatureLegalToHoldWithoutStun(On.Player.orig_IsCreatureLegalToHoldWithoutStun orig, Player self, Creature grabCheck)
        {
            bool origRet = orig(self, grabCheck);
            if (ModManager.Watcher && grabCheck is MothGrub) return true;
            return origRet;
        }

        private static void MothGrub_ctor(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After, i => i.MatchLdcR4(1.1f)))
            {
                c.EmitDelegate<Func<float, float>>((origRet) =>
                {
                    if (BingoData.BingoMode && (ExpeditionData.slugcatPlayer == SlugName.Red || ExpeditionData.slugcatPlayer == SlugNameMSC.Gourmand || ExpeditionData.slugcatPlayer == SlugNameMSC.Artificer)) return 0.65f;
                    else return origRet;
                });
            }
            else Plugin.logger.LogError("MothGrub_ctor FAIULRE" + il);
        }

        private static void SpinningTop_BathScene(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,i => i.MatchCall("System.String", "op_Equality")))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<bool, SpinningTop, bool>>((orig, st) =>
                {
                    if (BingoData.BingoMode) return false;
                    return orig;
                });
            }
            else Plugin.logger.LogError("SpinningTop_BathScene FAIULRE" + il);
        }

        private static void SpinningTop_BedroomScene(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After, i => i.MatchCall("System.String", "op_Equality")))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<bool, SpinningTop, bool>>((orig, st) =>
                {
                    if (BingoData.BingoMode && st.room.abstractRoom.name.ToLowerInvariant() == "waua_bath") return true;
                    return orig;
                });
            }
            else Plugin.logger.LogError("SpinningTop_BathScene FAIULRE" + il);
        }

        private static bool Spear_HitSomething(On.Spear.orig_HitSomething orig, Spear self, SharedPhysics.CollisionResult result, bool eu)
        {
            bool needle = self.Spear_NeedleCanFeed();
            if (needle && result.obj is Creature c && c is Tardigrade t && self.thrownBy != null && self.thrownBy is Player p)
            {
                Vector3 vector = Custom.RGB2HSL(t.iVars.bodyColor);
                t.room.AddObject(new PoisonInjecter(p, 0.22f, (10f + global::UnityEngine.Random.value * 8f) * 4.4f, new HSLColor(vector.x, Mathf.Lerp(vector.y, 1f, 0.5f), 0.5f).rgb));
            }
            return orig(self, result, eu); ;
        }

        private static void StaticWorld_InitRattler(On.StaticWorld.orig_InitRattler orig, List<CreatureTemplate> tempCreatureTemplates)
        {
            orig(tempCreatureTemplates);
            if (!ModManager.Watcher) return;
            tempCreatureTemplates[tempCreatureTemplates.Count - 1].meatPoints = 4;
        }

        private static void StaticWorld_InitBigMoth(On.StaticWorld.orig_InitBigMoth orig, List<CreatureTemplate> tempCreatureTemplates, CreatureTemplate batTemplate)
        {
            orig(tempCreatureTemplates, batTemplate);
            if (!ModManager.Watcher) return;
            tempCreatureTemplates[tempCreatureTemplates.Count - 1].meatPoints = 5;
        }

        private static void StaticWorld_InitSmallMoth(On.StaticWorld.orig_InitSmallMoth orig, List<CreatureTemplate> tempCreatureTemplates, CreatureTemplate bigMothTemplate, CreatureTemplate batTemplate)
        {
            orig(tempCreatureTemplates, bigMothTemplate, batTemplate);
            if (!ModManager.Watcher) return;
            tempCreatureTemplates[tempCreatureTemplates.Count - 1].meatPoints = 2;
        }

        private static AbstractPhysicalObject GourmandCombos_RandomStomachItem(On.MoreSlugcats.GourmandCombos.orig_RandomStomachItem orig, PhysicalObject caller)
        {
            if (ModManager.Watcher && ModManager.MSC)
            {
                float value = UnityEngine.Random.value;
                AbstractPhysicalObject apo;

                if (value < 0.4f) // idk 40 feels nice
                {
                    float r = UnityEngine.Random.value;
                    // using division to make it clear what the global probabilities are intended to be
                    if (r < 0.03f / 0.40f) // Rat (3%)
                    {
                        apo = new AbstractCreature(caller.room.world, StaticWorld.GetCreatureTemplate(Watcher.WatcherEnums.CreatureTemplateType.Rat), null, caller.room.GetWorldCoordinate(caller.firstChunk.pos), caller.room.game.GetNewID());
                    }
                    else if (r < (0.03f + 0.03f) / 0.40f) // Frog (3%)
                    {
                        apo = new AbstractCreature(caller.room.world, StaticWorld.GetCreatureTemplate(Watcher.WatcherEnums.CreatureTemplateType.Frog), null, caller.room.GetWorldCoordinate(caller.firstChunk.pos), caller.room.game.GetNewID());
                    }
                    else if (r < (0.03f + 0.03f + 0.05f) / 0.40f) // Sand grub (5%)
                    {
                        apo = new AbstractCreature(caller.room.world, StaticWorld.GetCreatureTemplate(Watcher.WatcherEnums.CreatureTemplateType.SandGrub), null, caller.room.GetWorldCoordinate(caller.firstChunk.pos), caller.room.game.GetNewID());
                    }
                    else if (r < (0.03f + 0.03f + 0.05f + 0.06f + 0.05f) / 0.40f) // Larvae (5%)
                    {
                        apo = new BoxWorm.Larva.AbstractLarva(caller.room.world, null, caller.room.GetWorldCoordinate(caller.firstChunk.pos), caller.room.game.GetNewID());
                    }
                    else if (r < (0.03f + 0.03f + 0.05f + 0.06f) / 0.40f) // Tardigrade (6%)
                    {
                        apo = new AbstractCreature(caller.room.world, StaticWorld.GetCreatureTemplate(Watcher.WatcherEnums.CreatureTemplateType.Tardigrade), null, caller.room.GetWorldCoordinate(caller.firstChunk.pos), caller.room.game.GetNewID());
                    }
                    else if (r < (0.03f + 0.03f + 0.05f + 0.06f + 0.05f + 0.09f) / 0.40f) // Boomerang (9%)
                    {
                        apo = new AbstractPhysicalObject(caller.room.world, Watcher.WatcherEnums.AbstractObjectType.Boomerang, null, caller.room.GetWorldCoordinate(caller.firstChunk.pos), caller.room.game.GetNewID());
                    }
                    else // Graffiti Bomb (remaining 9%)
                    {
                        apo = new GraffitiBomb.AbstractGraffitiBomb(caller.room.game.world, null, caller.room.GetWorldCoordinate(caller.firstChunk.pos), caller.room.game.GetNewID(), -1, -1, null);
                    }
                    return apo;
                }
                else
                {
                    return orig(caller);
                }
            }
            else
            {
                return orig(caller);
            }
        }

        private static void Angler_JawsSlamShut(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After, x => x.MatchIsinst(typeof(EnergyCell))))
            {
                c.EmitDelegate<Func<EnergyCell, EnergyCell>>(obj =>
                {
                    if (obj is EnergyCell cell)
                    {
                        return null;
                    }
                    return obj;
                });
            }
            else Plugin.logger.LogError("Angler_JawsSlamShut primary FAIULRE" + il);

            if (c.TryGotoNext(MoveType.Before, x => x.MatchCallOrCallvirt<UpdatableAndDeletable>("Destroy")))
            {
                ILLabel skipDestroy = c.DefineLabel();
                ILLabel keepObject = c.DefineLabel();

                c.Emit(OpCodes.Dup);
                c.EmitDelegate<Func<PhysicalObject, bool>>(obj =>
                {
                    if (obj is EnergyCell cell)
                    {
                        cell.Explode();
                        if (BingoData.BingoMode)
                        {
                            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                            {
                                if (ExpeditionData.challengeList[j] is WatcherBingoRivCellChallenge c)
                                {
                                    c.CellExploded();
                                }
                            }
                        }
                        return true;
                    }
                    return false;
                });

                c.Emit(OpCodes.Brfalse, keepObject);
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Br, skipDestroy);

                c.MarkLabel(keepObject);


                c.Index++;
                c.MarkLabel(skipDestroy);
            }
            else Plugin.logger.LogError("Angler_JawsSlamShut secondary FAIULRE" + il);

        }

        private static void Barnacle_Collide(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After, x => x.MatchCallOrCallvirt(typeof(Watcher.Barnacle).GetProperty(nameof(Watcher.Barnacle.hasShell)).GetGetMethod())))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldarg_1);
                c.EmitDelegate<Func<bool, Barnacle, PhysicalObject, bool>>((origRet, barnacle, po) =>
                {
                    if (po is Player p && p.slugcatStats.name == SlugNameMSC.Gourmand && p.SlugSlamConditions(barnacle))
                    {
                        return false;
                    }
                    return origRet;
                });
            }
            else Plugin.logger.LogError("Barnacle_Collide FAIULRE" + il);
        }

        private static void Barnacle_Violence(On.Watcher.Barnacle.orig_Violence orig, Barnacle self, BodyChunk source, Vector2? directionAndMomentum, BodyChunk hitChunk, PhysicalObject.Appendage.Pos onAppendagePos, Creature.DamageType type, float damage, float stunBonus)
        {
            if (!self.RippleViolenceCheck(source))
            {
                return;
            }
            if (source != null && source.owner is Player p && p.slugcatStats.name == SlugNameMSC.Gourmand && type == Creature.DamageType.Blunt && damage > 0.25f)
            {
                self.LoseShell();
            }
            orig(self, source, directionAndMomentum, hitChunk, onAppendagePos, type, damage, stunBonus);
        }

        private static bool Region_HasWarpFatigueResistance(On.Region.orig_HasWarpFatigueResistance orig, string name)
        {
            if (ExpeditionData.slugcatPlayer != SlugNameWatcher.Watcher)
            {
                return true;
            }
            return orig(name);
        }

        private static void SpinningTopData_FromString(On.Watcher.SpinningTopData.orig_FromString orig, SpinningTopData self, string s)
        {
            orig(self, s);
            if (BingoData.BingoMode && ExpeditionData.slugcatPlayer != SlugNameWatcher.Watcher)
            {
                if (self.spawnIdentifier == 1)
                {
                    //WARA is 1
                    if (self.rippleWarp)
                    {
                        self.rippleWarp = false;
                        self.destRoom = "WAUA_B02B";
                        self.destPos = new Vector2?(new Vector2(550f, 350f));
                    }
                }
            }
        }

        // ConvertTime(min, sec, 10s of ms)
        // first value is start time, second is fadein done, second is fadeoutstart
        private static void SlideShow_ctor1(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After, x => x.MatchStfld(typeof(Menu.SlideShow).GetField(nameof(Menu.SlideShow.playList)))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldarg_2);
                c.EmitDelegate((Menu.SlideShow self, SlideShow.SlideShowID slideShowID) =>
                {
                    if (slideShowID == BingoEnums.MenuTest)
                    {
                        self.playList.Add(new SlideShow.Scene(BingoEnums.MainMenu_Bingo, self.ConvertTime(0, 0, 0), self.ConvertTime(0, 0, 0), self.ConvertTime(1, 0, 0)));
                        self.processAfterSlideShow = ProcessManager.ProcessID.MainMenu;
                    }
                    if (slideShowID == BingoEnums.Sluhvengers)
                    {
                        //if (self.manager.musicPlayer != null)
                        //{
                        //    self.waitForMusic = "Bingo - interference";
                        //    self.stall = true;
                        //    self.manager.musicPlayer.MenuRequestsSong(self.waitForMusic, 1.5f, 0f);
                        //}
                        self.playList.Add(new SlideShow.Scene(MenuScene.SceneID.Empty, 0f, 0f, 0f));
                        SlideShow.Scene surmonk = new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_1_surmonk, self.ConvertTime(0, 0, 20), self.ConvertTime(0, 3, 20), self.ConvertTime(0, 11, 50));
                        surmonk.AddCrossFade(self.ConvertTime(0, 5, 50), 20);
                        self.playList.Add(surmonk);
                        //self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_2_surmonkportal, self.ConvertTime(0, 7, 25), self.ConvertTime(0, 7, 50), self.ConvertTime(0, 11, 50)));
                        self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_3_hunter, self.ConvertTime(0, 12, 50), self.ConvertTime(0, 13, 50), self.ConvertTime(0, 16, 50)));
                        self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_4_hunterportal, self.ConvertTime(0, 16, 75), self.ConvertTime(0, 17, 0), self.ConvertTime(0, 21, 0)));
                        self.playList.Add(new SlideShow.Scene(MenuScene.SceneID.Empty, self.ConvertTime(0, 22, 0), self.ConvertTime(0, 23, 0), self.ConvertTime(0, 23, 0)));
                        self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_5_saintportal, self.ConvertTime(0, 24, 0), self.ConvertTime(0, 25, 50), self.ConvertTime(0, 29, 0)));
                        self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_6_gour, self.ConvertTime(0, 30, 0), self.ConvertTime(0, 31, 0), self.ConvertTime(0, 34, 0)));
                        self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_7_gourportal, self.ConvertTime(0, 34, 25), self.ConvertTime(0, 34, 50), self.ConvertTime(0, 38, 50)));
                        self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_8_arti, self.ConvertTime(0, 39, 50), self.ConvertTime(0, 40, 50), self.ConvertTime(0, 43, 50)));
                        self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_9_artiportal, self.ConvertTime(0, 43, 75), self.ConvertTime(0, 44, 0), self.ConvertTime(0, 48, 0)));
                        self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_10_smportal, self.ConvertTime(0, 50, 0), self.ConvertTime(0, 52, 0), self.ConvertTime(0, 56, 0)));
                        self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_11_rivportal, self.ConvertTime(0, 57, 0), self.ConvertTime(0, 58, 0), self.ConvertTime(1, 2, 0)));
                        self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_12_riveyes, self.ConvertTime(1, 4, 0), self.ConvertTime(1, 5, 50), self.ConvertTime(1, 10, 0)));
                        self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_13_sluhvengers, self.ConvertTime(1, 12, 5), self.ConvertTime(1, 15, 0), self.ConvertTime(1, 21, 0)));
                        self.playList.Add(new SlideShow.Scene(MenuScene.SceneID.Empty, self.ConvertTime(1, 28, 0), 0f, 0f));
                        //if (self.manager.musicPlayer != null)
                        //{
                        //    self.waitForMusic = "Bingo - Interference";
                        //    self.stall = true;
                        //    self.manager.musicPlayer.MenuRequestsSong(self.waitForMusic, 1.5f, 40f);
                        //}
                        //self.playList.Add(new SlideShow.Scene(MenuScene.SceneID.Empty, 0f, 0f, 0f));
                        //self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_1_surmonk, self.ConvertTime(0, 0, 20), self.ConvertTime(0, 3, 20), self.ConvertTime(0, 7, 0)));
                        //self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_2_surmonkportal, self.ConvertTime(0, 7, 25), self.ConvertTime(0, 7, 50), self.ConvertTime(0, 11, 50)));
                        //self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_3_hunter, self.ConvertTime(0, 12, 50), self.ConvertTime(0, 13, 50), self.ConvertTime(0, 16, 50)));
                        //self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_4_hunterportal, self.ConvertTime(0, 16, 75), self.ConvertTime(0, 17, 0), self.ConvertTime(0, 21, 0)));
                        //self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_5_saintportal, self.ConvertTime(0, 22, 0), self.ConvertTime(0, 23, 0), self.ConvertTime(0, 27, 0)));
                        //self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_6_gour, self.ConvertTime(0, 28, 0), self.ConvertTime(0, 29, 0), self.ConvertTime(0, 32, 0)));
                        //self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_7_gourportal, self.ConvertTime(0, 32, 25), self.ConvertTime(0, 32, 50), self.ConvertTime(0, 36, 50)));
                        //self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_8_arti, self.ConvertTime(0, 37, 50), self.ConvertTime(0, 38, 50), self.ConvertTime(0, 41, 50)));
                        //self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_9_artiportal, self.ConvertTime(0, 41, 75), self.ConvertTime(0, 42, 0), self.ConvertTime(0, 46, 0)));
                        //self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_10_smportal, self.ConvertTime(0, 48, 0), self.ConvertTime(0, 50, 0), self.ConvertTime(0, 54, 0)));
                        //self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_11_rivportal, self.ConvertTime(0, 55, 0), self.ConvertTime(0, 56, 0), self.ConvertTime(1, 0, 0)));
                        //self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_12_riveyes, self.ConvertTime(1, 2, 0), self.ConvertTime(1, 3, 50), self.ConvertTime(1, 8, 0)));
                        //self.playList.Add(new SlideShow.Scene(BingoEnums.SluhvengersScenes.sluhvengers_13_sluhvengers, self.ConvertTime(1, 10, 5), self.ConvertTime(1, 13, 0), self.ConvertTime(1, 19, 0)));
                        //self.playList.Add(new SlideShow.Scene(MenuScene.SceneID.Empty, self.ConvertTime(1, 28, 0), 0f, 0f));
                        for (int n = 1; n < self.playList.Count; n++)
                        {
                            self.playList[n].startAt += 0.6f;
                            self.playList[n].fadeInDoneAt += 0.6f;
                            self.playList[n].fadeOutStartAt += 0.6f;
                        }
                        self.processAfterSlideShow = ProcessManager.ProcessID.MainMenu;
                    }
                });
            }
            else Plugin.logger.LogError("SlideShow_ctor1 FAIULRE" + il);
        }

        private static void Room_Loaded2(On.Room.orig_Loaded orig, Room self)
        {
            orig(self);
            if (BingoData.BingoMode && ModManager.Watcher && BingoData.WatcherMode && ExpeditionData.slugcatPlayer != WatcherEnums.SlugcatStatsName.Watcher)
            {
                string roomName = self.abstractRoom.name.ToUpperInvariant();
                if ((roomName == "WARA_P24" || roomName == "WAUA_E02B" || roomName == "WORA_DESERT6") && !self.roomSettings.placedObjects.Any(x => x.type == PlacedObject.Type.WarpPoint))
                {
                    WarpPoint warpPoint = null;
                    string room = WarpPoint.ChooseDynamicWarpTarget(self.world, self.abstractRoom.name, null, false, false, true);
                    PlacedObject placedObject = new PlacedObject(PlacedObject.Type.WarpPoint, WarpPoint.CreateOverrideData(self.world, self.abstractRoom.name, room, null, true, true));
                    placedObject.data.owner = placedObject;
                    if (self.world.game.IsStorySession)
                    {
                        (placedObject.data as WarpPoint.WarpPointData).cycleSpawnedOn = self.world.game.GetStorySession.saveState.cycleNumber;
                    }
                    (placedObject.data as WarpPoint.WarpPointData).destCam = WarpPoint.GetDestCam(placedObject.data as WarpPoint.WarpPointData);

                    switch (roomName)
                    {
                        case "WARA_P24":
                            {
                                placedObject.pos = new Vector2(650f, 281f); // call me johnson the way my numbers is magic
                                break;
                            }
                        case "WAUA_E02B":
                            {
                                placedObject.pos = new Vector2(487f, 3381f); // call me johnson the way my numbers is magic
                                break;
                            }
                        case "WORA_DESERT6":
                            {
                                placedObject.pos = new Vector2(476.5552f, 374.4548f); // call me johnson the way my numbers is magic
                                break;
                            }
                    }
                    warpPoint = self.TrySpawnWarpPoint(placedObject, true);
                    return;
                }

                WarpPoint foundWarp = (WarpPoint)self.updateList.FirstOrDefault(x => x is WarpPoint);
                WarpPoint.WarpPointData foundData = foundWarp?.Data;
                // Don't continue unless there is a Ripple warp in this room that we want to replace
                if (foundData is null 
                    || !foundData.rippleWarp
                    || self.abstractRoom.name.StartsWith("WARA", StringComparison.InvariantCultureIgnoreCase)) 
                    return;

                foundData.rippleWarp = false;
                foundData.destRegion = null;
                activeExDaemonWarps.Add(foundWarp, "Yep");
            }
        }

        private static void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);
            if (BingoData.BingoMode && ModManager.Watcher && BingoData.WatcherMode)
            {
                if (self.room != null && self.AI == null)
                {
                    (self.room.game.session as StoryGameSession).saveState.theGlow = true;
                    self.glowing = true;
                }
            }
        }

        private static void BubbleGrass_Update(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After, x => x.MatchLdsfld(typeof(SlugNameMSC).GetField(nameof(SlugNameMSC.Artificer)))))
            {
                c.Index++;
                c.EmitDelegate((bool orig) =>
                {
                    if (BingoData.BingoMode && ModManager.Watcher && BingoData.WatcherMode)
                    {
                        return false;
                    }
                    return orig;
                });
            }
            else Plugin.logger.LogError("BubbleGrass_Update primary FAIULRE" + il);
        }

        private static void SlugcatStats_ctor(On.SlugcatStats.orig_ctor orig, SlugcatStats self, SlugName slugcat, bool malnourished)
        {
            orig(self, slugcat, malnourished);
            if (ModManager.Watcher && BingoData.BingoMode && BingoData.WatcherMode && slugcat != SlugNameMSC.Rivulet)
            {
                self.lungsFac = 0.8f;
            }
        }

        private static float Player_PyroDeathThreshold(On.Player.orig_PyroDeathThreshold orig, RainWorldGame game)
        {
            float origRet = orig(game);
            if (ModManager.Watcher && BingoData.BingoMode && BingoData.WatcherMode) return 0f;
            else return origRet;
        }

        private static void WarpPoint_SuckInCreatures(ILContext il)
        {
            int playerLocalIndex = -1;
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.Before, x => x.MatchCallOrCallvirt(typeof(Creature).GetProperty(nameof(Creature.grasps)).GetGetMethod())))
            {
                c.Index--;
                if (c.Next.MatchLdloc(out var local))
                {
                    playerLocalIndex = local;
                }
                if (playerLocalIndex > -1 && c.TryGotoNext(MoveType.Before, x => x.MatchLdcI4(0)))
                {
                    c.Emit(OpCodes.Ldloc, playerLocalIndex);
                    c.EmitDelegate((Creature crit) =>
                    {
                        Player p = crit as Player; // safe
                        if (p.slugOnBack != null && p.slugOnBack.HasASlug)
                        {
                            p.slugOnBack.DropSlug();
                            if (!p.room.game.GetStorySession.importantWarpPointTransferedEntities.Contains(p.slugOnBack.slugcat.abstractPhysicalObject.ID))
                            {
                                p.room.game.GetStorySession.importantWarpPointTransferedEntities.Add(p.slugOnBack.slugcat.abstractPhysicalObject.ID);
                            }
                        }
                        if (p.spearOnBack != null && p.spearOnBack.HasASpear)
                        {
                            p.spearOnBack.DropSpear();
                            if (!p.room.game.GetStorySession.importantWarpPointTransferedEntities.Contains(p.spearOnBack.spear.abstractPhysicalObject.ID))
                            {
                                p.room.game.GetStorySession.importantWarpPointTransferedEntities.Add(p.spearOnBack.spear.abstractPhysicalObject.ID);
                            }
                        }
                    });
                }
                else Plugin.logger.LogError("WarpPoint_SuckInCreatures secondary FAIULRE" + il);
            }
            else Plugin.logger.LogError("WarpPoint_SuckInCreatures primary FAIULRE" + il);
        }

        private static void World_SpawnGhost(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After, x => x.MatchCallOrCallvirt(typeof(ExtEnum<SlugName>).GetMethod("op_Equality"))))
            {
                c.EmitDelegate<Func<bool, bool>>(orig =>
                {
                    return BingoData.WatcherMode || orig;
                });
            }
            else Plugin.logger.LogError("World_SpawnGhost FAIULRE");
        }

        private static void Room_LoadedSTLoad(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(x => x.MatchLdsfld(typeof(Watcher.WatcherEnums.PlacedObjectType), nameof(Watcher.WatcherEnums.PlacedObjectType.SpinningTopSpot))) &&
                c.TryGotoNext(MoveType.After, x => x.MatchCallOrCallvirt(typeof(ExtEnum<SlugName>).GetMethod("op_Equality"))))
            {
                c.EmitDelegate<Func<bool, bool>>(orig =>
                {
                    return BingoData.WatcherMode || orig;
                });
            }
            else Plugin.logger.LogError("Room_LoadedSTLoad FAIULRE");
        }

        private static void WarpPoint_UpdateIL(ILContext il)
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

        private static void SaveState_ctor(On.SaveState.orig_ctor orig, SaveState self, SlugName saveStateNumber, PlayerProgression progression)
        {
            orig(self, saveStateNumber, progression);
            if (BingoData.BingoMode && BingoData.slugcatPlayer == SlugNameWatcher.Watcher)
            {
                self.currentTimelinePosition = SlugcatStats.SlugcatToTimeline(SlugNameWatcher.Watcher);
                self.deathPersistentSaveData.spinningTopEncounters.Add(39); //WAUA_TOYS ST
                self.miscWorldSaveData.visitedShopRoom = true;
                self.miscWorldSaveData.seenSpinningTopDream = true;
                if (ExpeditionData.slugcatPlayer == SlugNameWatcher.Watcher)
                {
                    self.miscWorldSaveData.camoTutorialCounter++;
                    self.miscWorldSaveData.usedCamoAbility++;
                    self.miscWorldSaveData.cycleFirstStartedWarpJourney++;
                    self.miscWorldSaveData.stableWarpTutorialCounter = 5;
                    self.miscWorldSaveData.badWarpTutorialCounter++;
                    self.miscWorldSaveData.warpFatigueTutorialCounter++;
                    self.miscWorldSaveData.warpExhaustionTutorialCounter = 5;
                    self.deathPersistentSaveData.spinningTopRotEncounter = true;
                    self.miscWorldSaveData.numberOfPrinceEncounters = 5;
                    self.miscWorldSaveData.seenSpinningTopDream = true; //Dreams are generally disabled I think?
                    self.miscWorldSaveData.seenRotDream = true;
                    self.deathPersistentSaveData.maximumRippleLevel = 5f;
                    self.deathPersistentSaveData.minimumRippleLevel = 3f;
                    self.deathPersistentSaveData.rippleLevel = self.deathPersistentSaveData.minimumRippleLevel + ExpeditionGame.tempKarma / 2f;
                    self.deathPersistentSaveData.karmaCap = 9;
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

        private static List<string> SlugcatStats_SlugcatStoryRegions(On.SlugcatStats.orig_SlugcatStoryRegions orig, SlugName i)
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

        private static string WatcherShelters_ExpeditionGame_ExpeditionRandomStarts(On.Expedition.ExpeditionGame.orig_ExpeditionRandomStarts orig, RainWorld rainWorld, SlugName slug)
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

        private static List<SlugName> ExpeditionData_GetPlayableCharacters(On.Expedition.ExpeditionData.orig_GetPlayableCharacters orig)
        {
            var temp = orig();
            if (ModManager.Watcher)
            {
                temp.Add(WatcherEnums.SlugcatStatsName.Watcher);
            }
            return temp;
        }

        private static void CharacterSelectPage_ctor(On.Menu.CharacterSelectPage.orig_ctor orig, CharacterSelectPage self, Menu.Menu menu, MenuObject owner, Vector2 pos)
        {
            orig(self, menu, owner, pos);
            if (!watcherModeButton.TryGetValue(self, out _))
            {
                watcherModeButton.Add(self, new BingoSymbolButton(menu, self, "ripple5.0", "WATCHERMODE", new Vector2(376f, 543f))); // literally just made this X up, trust it works
            }
            if (watcherModeButton.TryGetValue(self, out var but))
            {
                but.roundedRect.size = new Vector2(65f, 65f);
                but.size = but.roundedRect.size;
                self.subObjects.Add(but);
            }

            if (ModManager.JollyCoop)
            {
                self.jollyToggleConfigMenu.pos.x -= 70f;
                self.jollyPlayerCountLabel.pos.x -= 70f;
            }
        }

        private static void CharacterSelectPage_ctorIL(ILContext il)
        {
            ILCursor c = new(il);
            if (c.TryGotoNext(MoveType.Before,
            x => x.MatchLdcI4(3)))
            {
                c.EmitDelegate<Func<int, int>>((exped) =>
                {
                    if (ExpeditionGame.playableCharacters[exped] == SlugNameWatcher.Watcher)
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

        private static void ExpeditionMenu_Singal(On.Menu.ExpeditionMenu.orig_Singal orig, ExpeditionMenu self, MenuObject sender, string message)
        {
            orig.Invoke(self, sender, message);
            if (self.pagesMoving) return;

            if (message == "WATCHERMODE")
            {
                BingoData.WatcherMode = !BingoData.WatcherMode;
                BingoPage.WatcherModeUIUpdate();
            }
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
                        BingoData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
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
                    if (BingoData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
                    {
                        return false;
                    }
                    return karma;
                });
            }
            else Plugin.logger.LogError("WarpMap_LoadWarpConnections FAIULRE " + il);
        }

        private static SaveState PlayerProgression_GetOrInitiateSaveState(On.PlayerProgression.orig_GetOrInitiateSaveState orig, PlayerProgression self, SlugName saveStateNumber, RainWorldGame game, ProcessManager.MenuSetup setup, bool saveAsDeathOrQuit)
        {
            SaveState saveState = orig(self, saveStateNumber, game, setup, saveAsDeathOrQuit);

            if (BingoData.BingoMode && BingoData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
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
                        BingoData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
                    {
                        return true;
                    }

                    return containsResult;
                });
            }
            else Plugin.logger.LogError("WarpMap_LoadWarpConnections FAIULRE " + il);
        }

        private static bool Challenge_ValidForThisSlugcat(On.Expedition.Challenge.orig_ValidForThisSlugcat orig, Challenge self, SlugName slugcat)
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
            if (BingoData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
            {
                //fuck you
                return new EchoChallenge
                {
                    ghost = GhostWorldPresence.GhostID.CC
                };
            }
            return orig(self);
        }
        private static bool AchievementChallenge_ValidForThisSlugcat(On.Expedition.AchievementChallenge.orig_ValidForThisSlugcat orig, AchievementChallenge self, SlugName slugcat)
        {
            if (slugcat == WatcherEnums.SlugcatStatsName.Watcher)
            {
                return false;
            }
            return orig(self, slugcat);
        }
        private static bool PearlDeliveryChallenge_ValidForThisSlugcat(On.Expedition.PearlDeliveryChallenge.orig_ValidForThisSlugcat orig, PearlDeliveryChallenge self, SlugName slugcat)
        {
            if (slugcat == WatcherEnums.SlugcatStatsName.Watcher)
            {
                return false;
            }
            return orig(self, slugcat);
        }
        private static bool NeuronDeliveryChallenge_ValidForThisSlugcat(On.Expedition.NeuronDeliveryChallenge.orig_ValidForThisSlugcat orig, NeuronDeliveryChallenge self, SlugName slugcat)
        {
            if (slugcat == WatcherEnums.SlugcatStatsName.Watcher)
            {
                return false;
            }
            return orig(self, slugcat);
        }

        public static bool ExpeditionData_CheckUnlocked(On.Expedition.ExpeditionProgression.orig_CheckUnlocked orig, ProcessManager manager, SlugName slugcat)
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

            if (sceneToRegion.TryGetValue(self.sceneID, out string region))
            {
                string folder = $"Scenes{Path.DirectorySeparatorChar}Landscape - {region}";
                string flatName = $"Landscape - {region} - Flat";
                string shadowName = $"Title_{region}_Shadow";
                string titleName = $"Title_{region}";

                self.sceneFolder = folder;

                self.AddIllustration(
                    new MenuIllustration(self.menu, self, folder, flatName, new Vector2(683f, 384f), false, true));

                if (self.menu.ID == ProcessManager.ProcessID.FastTravelScreen || self.menu.ID == ProcessManager.ProcessID.RegionsOverviewScreen)
                {
                    self.AddIllustration(
                        new MenuIllustration(self.menu, self, "", shadowName, new Vector2(0.01f, 0.01f), true, false));

                    self.AddIllustration(
                        new MenuIllustration(self.menu, self, "", titleName, new Vector2(0.01f, 0.01f), true, false));

                    self.flatIllustrations[self.flatIllustrations.Count - 1].sprite.shader = self.menu.manager.rainWorld.Shaders["MenuText"];
                }
            }

            if (self.sceneID == BingoEnums.SluhvengersScenes.sluhvengers_1_surmonk)
            {
                self.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + "sluhvengers" + Path.DirectorySeparatorChar.ToString() + "sluhvengers 1 - surmonk";
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "1 - back done", new Vector2(-120f, -87f), 3f, MenuDepthIllustration.MenuShader.Normal));
                self.AddCrossfade(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "1 - portal done", new Vector2(-120f, -87f), 3f, MenuDepthIllustration.MenuShader.Normal)
                {
                    crossfadeMethod = MenuIllustration.CrossfadeType.MaintainBackground
                });
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "2 - slug done", new Vector2(-120f, -87f), 1.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddCrossfade(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "2 - slug done2", new Vector2(-120f, -87f), 1.5f, MenuDepthIllustration.MenuShader.Normal)
                {
                    crossfadeMethod = MenuIllustration.CrossfadeType.MaintainBackground
                });
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "3 - fruit done", new Vector2(-120f, -87f), 1.3f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "4 - fore done", new Vector2(-120f, -87f), 1f, MenuDepthIllustration.MenuShader.Normal));

                //self.AddIllustration(new MenuIllustration(self.menu, self, self.sceneFolder, "sluhvengers 1 - surmonk - flat", (new Vector2(1366f, 768f)) / 2, false, true));
            }
            else if (self.sceneID == BingoEnums.SluhvengersScenes.sluhvengers_2_surmonkportal)
            {
                self.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + "sluhvengers" + Path.DirectorySeparatorChar.ToString() + "sluhvengers 2 - surmonkportal";
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "1 - portal done", new Vector2(-120f, -87f), 6f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "2 - slug done", new Vector2(-120f, -87f), 3f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "3 - fruit done", new Vector2(-120f, -87f), 2.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "4 - fore done", new Vector2(-120f, -87f), 2f, MenuDepthIllustration.MenuShader.Normal));

                //self.AddIllustration(new MenuIllustration(self.menu, self, self.sceneFolder, "sluhvengers 2 - surmonkportal - flat", (new Vector2(1366f, 768f))/2, false, true));
            }
            else if (self.sceneID == BingoEnums.SluhvengersScenes.sluhvengers_3_hunter)
            {
                self.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + "sluhvengers" + Path.DirectorySeparatorChar.ToString() + "sluhvengers 3 - hunter";
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "1 - back done", new Vector2(-120f, -87f), 6f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "2 - deer done", new Vector2(-120f, -87f), 5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "3 - backgrass done", new Vector2(-120f, -87f), 3f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "4 - hunter done", new Vector2(-120f, -87f), 2f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "5 - foregrass done", new Vector2(-120f, -87f), 1.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "6 - fore done", new Vector2(-120f, -87f), 1f, MenuDepthIllustration.MenuShader.Normal));

                //self.AddIllustration(new MenuIllustration(self.menu, self, self.sceneFolder, "sluhvengers 3 - hunter - flat", (new Vector2(1366f, 768f))/2, false, true));
            }
            else if (self.sceneID == BingoEnums.SluhvengersScenes.sluhvengers_4_hunterportal)
            {
                self.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + "sluhvengers" + Path.DirectorySeparatorChar.ToString() + "sluhvengers 4 - hunterportal";
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "1 - back done", new Vector2(-120f, -87f), 6f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "2 - deer done", new Vector2(-120f, -87f), 5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "3 - backgrass done", new Vector2(-120f, -87f), 3f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "4 - hunter done", new Vector2(-120f, -87f), 2.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "5 - portal done", new Vector2(-120f, -87f), 2f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "6 - foregrass done", new Vector2(-120f, -87f), 1.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "7 - fore done", new Vector2(-120f, -87f), 1f, MenuDepthIllustration.MenuShader.Normal));

                //self.AddIllustration(new MenuIllustration(self.menu, self, self.sceneFolder, "sluhvengers 4 - hunterportal - flat", (new Vector2(1366f, 768f))/2, false, true));
            }
            else if (self.sceneID == BingoEnums.SluhvengersScenes.sluhvengers_5_saintportal)
            {
                self.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + "sluhvengers" + Path.DirectorySeparatorChar.ToString() + "sluhvengers 5 - saintportal";
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "1 - back done", new Vector2(-120f, -87f), 6f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "2 - arch done", new Vector2(-120f, -87f), 5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "3 - back details done", new Vector2(-120f, -87f), 3.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "4 - portal done", new Vector2(-120f, -87f), 3f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "5 - saint done", new Vector2(-120f, -87f), 2.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "6 - rebar snow1 done", new Vector2(-120f, -87f), 2f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "7 - fore done", new Vector2(-120f, -87f), 1.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "8 - snow2 done", new Vector2(-120f, -87f), 1f, MenuDepthIllustration.MenuShader.Normal));

                //self.AddIllustration(new MenuIllustration(self.menu, self, self.sceneFolder, "sluhvengers 5 - saintportal - flat", (new Vector2(1366f, 768f))/2, false, true));
            }
            else if (self.sceneID == BingoEnums.SluhvengersScenes.sluhvengers_6_gour)
            {
                self.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + "sluhvengers" + Path.DirectorySeparatorChar.ToString() + "sluhvengers 6 - gour";
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "1 - portal done", new Vector2(-120f, -87f), 6f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "2 - portal haze done", new Vector2(-120f, -87f), 5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "3 - pillar done", new Vector2(-120f, -87f), 3.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "4 - fore done", new Vector2(-120f, -87f), 3f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "5 - gour done", new Vector2(-120f, -87f), 2.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "6 - shroom done", new Vector2(-120f, -87f), 2f, MenuDepthIllustration.MenuShader.Normal));

                //self.AddIllustration(new MenuIllustration(self.menu, self, self.sceneFolder, "sluhvengers 6 - gour - flat", (new Vector2(1366f, 768f))/2, false, true));
            }
            else if (self.sceneID == BingoEnums.SluhvengersScenes.sluhvengers_7_gourportal)
            {
                self.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + "sluhvengers" + Path.DirectorySeparatorChar.ToString() + "sluhvengers 7 - gourportal";
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "2 - portal done", new Vector2(-120f, -87f), 6f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "1 - eyes done", new Vector2(-120f, -87f), 7f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "3 - portal haze done", new Vector2(-120f, -87f), 5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "4 - pillar done", new Vector2(-120f, -87f), 3.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "5 - fore done", new Vector2(-120f, -87f), 3f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "6 - gour done", new Vector2(-120f, -87f), 2.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "7 - shroom done", new Vector2(-120f, -87f), 2f, MenuDepthIllustration.MenuShader.Normal));

                //self.AddIllustration(new MenuIllustration(self.menu, self, self.sceneFolder, "sluhvengers 7 - gourportal - flat", (new Vector2(1366f, 768f))/2, false, true));
            }
            else if (self.sceneID == BingoEnums.SluhvengersScenes.sluhvengers_8_arti)
            {
                self.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + "sluhvengers" + Path.DirectorySeparatorChar.ToString() + "sluhvengers 8 - arti";
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "1 - arti done", new Vector2(-120f, -87f), 6f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "2 - sof done", new Vector2(-120f, -87f), 4f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "3 - fore done", new Vector2(-120f, -87f), 3f, MenuDepthIllustration.MenuShader.Normal));

                //self.AddIllustration(new MenuIllustration(self.menu, self, self.sceneFolder, "sluhvengers 8 - arti - flat", (new Vector2(1366f, 768f))/2, false, true));
            }
            else if (self.sceneID == BingoEnums.SluhvengersScenes.sluhvengers_9_artiportal)
            {
                self.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + "sluhvengers" + Path.DirectorySeparatorChar.ToString() + "sluhvengers 9 - artiportal";
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "1 - arti done", new Vector2(-120f, -87f), 6f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "2 - sof done", new Vector2(-120f, -87f), 4f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "3 - friend done", new Vector2(-120f, -87f), 3.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "4 - fore done", new Vector2(-120f, -87f), 3f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "5 - portal done", new Vector2(-120f, -87f), 2.5f, MenuDepthIllustration.MenuShader.Normal));

                //self.AddIllustration(new MenuIllustration(self.menu, self, self.sceneFolder, "sluhvengers 9 - artiportal - flat", (new Vector2(1366f, 768f))/2, false, true));
            }
            else if (self.sceneID == BingoEnums.SluhvengersScenes.sluhvengers_10_smportal)
            {
                self.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + "sluhvengers" + Path.DirectorySeparatorChar.ToString() + "sluhvengers 10 - smportal";
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "1 - back done", new Vector2(-120f, -87f), 6f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "2 - fore done", new Vector2(-120f, -87f), 4f, MenuDepthIllustration.MenuShader.Normal));

                //self.AddIllustration(new MenuIllustration(self.menu, self, self.sceneFolder, "sluhvengers 10 - smportal - flat", (new Vector2(1366f, 768f))/2, false, true));
            }
            else if (self.sceneID == BingoEnums.SluhvengersScenes.sluhvengers_11_rivportal)
            {
                self.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + "sluhvengers" + Path.DirectorySeparatorChar.ToString() + "sluhvengers 11 - rivportal";
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "1 - back done", new Vector2(-120f, -87f), 6f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "2 - riv done", new Vector2(-120f, -87f), 4f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "3 - deets done", new Vector2(-120f, -87f), 3.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "4 - watch done", new Vector2(-120f, -87f), 3f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "5 - fore done", new Vector2(-120f, -87f), 2.5f, MenuDepthIllustration.MenuShader.Normal));

                //self.AddIllustration(new MenuIllustration(self.menu, self, self.sceneFolder, "sluhvengers 11 - rivportal - flat", (new Vector2(1366f, 768f))/2, false, true));
            }
            else if (self.sceneID == BingoEnums.SluhvengersScenes.sluhvengers_12_riveyes)
            {
                self.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + "sluhvengers" + Path.DirectorySeparatorChar.ToString() + "sluhvengers 12 - riveyes";
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "1 - back done", new Vector2(-120f, -87f), 5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "2 - eyes done", new Vector2(-120f, -87f), 2f, MenuDepthIllustration.MenuShader.Normal));

                //self.AddIllustration(new MenuIllustration(self.menu, self, self.sceneFolder, "sluhvengers 12 - riveyes - flat", (new Vector2(1366f, 768f))/2, false, true));
            }
            else if (self.sceneID == BingoEnums.SluhvengersScenes.sluhvengers_13_sluhvengers)
            {
                self.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + "sluhvengers" + Path.DirectorySeparatorChar.ToString() + "sluhvengers 13 - sluhvengers";
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "1 - back done", new Vector2(-120f, -87f), 7f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "2 - spire done", new Vector2(-120f, -87f), 6f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "3 - clouds done", new Vector2(-120f, -87f), 4.5f, MenuDepthIllustration.MenuShader.Normal));
                self.AddIllustration(new MenuDepthIllustration(self.menu, self, self.sceneFolder, "4 - squad done", new Vector2(-120f, -87f), 3f, MenuDepthIllustration.MenuShader.Normal));

                //self.AddIllustration(new MenuIllustration(self.menu, self, self.sceneFolder, "sluhvengers 13 - sluhvengers - flat", (new Vector2(1366f, 768f))/2, false, true));
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
            SlugName cat = ExpeditionGame.playableCharacters[num];
            if (BingoData.BingoSaves.ContainsKey(cat))
            {
                BingoData.WatcherMode = BingoData.BingoSaves[cat].modifier == BingoData.BingoModifier.WatcherMode;
                BingoPage.WatcherModeUIUpdate(false, false);
            }
            if (ModManager.Watcher && cat == WatcherEnums.SlugcatStatsName.Watcher)
            {
                self.slugcatScene = BingoEnums.WatcherExpeditionBackground;
            }

            if (ModManager.Watcher && BingoData.WatcherMode || cat == WatcherEnums.SlugcatStatsName.Watcher)
            {
                self.pageTitle.element = BingoPage.watcherTitle;
            }
            else
            {
                self.pageTitle.element = BingoPage.normalTitle;
            }

            if (BingoData.WatcherMode)
            {
                if (cat == SlugName.Yellow)
                {
                    self.slugcatScene = BingoEnums.LandscapeType.Landscape_WAUA;
                }
                else if (cat == SlugName.White)
                {
                    self.slugcatScene = BingoEnums.LandscapeType.Landscape_WPGA;
                }
                else if (cat == SlugName.Red)
                {
                    self.slugcatScene = BingoEnums.LandscapeType.Landscape_WBLA;
                }
                else if (ModManager.MSC && cat == SlugNameMSC.Gourmand)
                {
                    self.slugcatScene = BingoEnums.LandscapeType.Landscape_WSKB;
                }
                else if (ModManager.MSC && cat == SlugNameMSC.Saint)
                {
                    self.slugcatScene = BingoEnums.LandscapeType.Landscape_WPTA;
                }
                else if (ModManager.MSC && cat == SlugNameMSC.Spear)
                {
                    self.slugcatScene = BingoEnums.LandscapeType.Landscape_WARD;
                }
                else if (ModManager.MSC && cat == SlugNameMSC.Rivulet)
                {
                    self.slugcatScene = BingoEnums.LandscapeType.Landscape_WRRA;
                }
                else if (ModManager.MSC && cat == SlugNameMSC.Artificer)
                {
                    self.slugcatScene = BingoEnums.LandscapeType.Landscape_WARE;
                }
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
                    if (BingoData.BingoMode && BingoData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher && wp.Data != null && wp.Data.destRoom != null && wp.Data.destRoom == "NARNIA")
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
            if (BingoData.BingoMode && BingoData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
            {
                self.deathPersistentSaveData.rippleLevel = 5;
            }
            orig(self, game, addFiveCycles);
        }

        private static string WarpPoint_ChooseDynamicWarpTarget(On.Watcher.WarpPoint.orig_ChooseDynamicWarpTarget orig, World world, string oldRoom, string targetRegion, bool badWarp, bool spreadingRot, bool playerCreated)
        {
            if (BingoData.BingoMode && BingoData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher)
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
            if (ModManager.Watcher && BingoData.BingoMode && ExpeditionData.slugcatPlayer == WatcherEnums.SlugcatStatsName.Watcher && !ExpeditionGame.activeUnlocks.Contains("unl-watcher-dialwarp") && self.Tree)
            {
                return Mathf.InverseLerp(1f, 0.5f, (self.Data as PlacedObject.RippleTreeData).sproutEnd);
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

        // Hunter stuff is probably redundant with how normal bingo touches flower generation but I just want to cut out the waua stuff so it'll be like this
        private static bool KarmaFlower_CanSpawnKarmaFlower(On.KarmaFlower.orig_CanSpawnKarmaFlower orig, Room room)
        {
            if (!BingoData.BingoMode) return orig(room);
            return room.game.StoryCharacter != SlugName.Red;
        }

        // gourmand crafts relegated here because of bloat
        private static void PopulateGourmandCrafts()
        {
            BaseAOT abstractObjectType = BaseAOT.GraffitiBomb;
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.Rock], 0, BaseAOT.PuffBall, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.FlyLure], 0, BaseAOT.Mushroom, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.FirecrackerPlant], 0, BaseAOT.ScavengerBomb, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.ScavengerBomb], 0, BaseAOT.FlareBomb, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.Mushroom], 0, null, WatchCTT.Rat);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.PuffBall], 0, DLCAOT.GooieDuck, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.SporePlant], 0, DLCAOT.GooieDuck, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.GraffitiBomb], 0, null, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.FlareBomb], 0, null, BaseCTT.Hazer);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.Lantern], 0, BaseAOT.PuffBall, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.VultureMask], 0, BaseAOT.DataPearl, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.NeedleEgg], 0, null, WatchCTT.Tardigrade);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.BubbleGrass], 0, null, BaseCTT.Hazer);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.KarmaFlower], 0, BaseAOT.NeedleEgg, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[DLCAOT.SingularityBomb], 0, MSCAOT.FireEgg, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.OverseerCarcass], 0, BaseAOT.DataPearl, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.DataPearl], 0, null, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.SSOracleSwarmer], 0, BaseAOT.OverseerCarcass, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[MSCAOT.FireEgg], 0, BaseAOT.ScavengerBomb, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[DLCAOT.Seed], 0, null, WatchCTT.Tardigrade);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[WatchAOT.FireSpriteLarva], 0, BaseAOT.Lantern, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.DangleFruit], 0, WatchAOT.FireSpriteLarva, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.EggBugEgg], 0, WatchAOT.FireSpriteLarva, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[DLCAOT.DandelionPeach], 0, DLCAOT.GooieDuck, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[DLCAOT.GooieDuck], 0, null, WatchCTT.SandGrub);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.SlimeMold], 0, BaseAOT.FlareBomb, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[DLCAOT.LillyPuck], 0, null, BaseCTT.Hazer);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[DLCAOT.GlowWeed], 0, null, BaseCTT.Hazer);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.WaterNut], 0, null, WatchCTT.Tardigrade);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.JellyFish], 0, null, WatchCTT.Tardigrade);

            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[BaseCTT.Hazer], GourmandCombos.objectsLibrary[abstractObjectType], 1, BaseAOT.FlareBomb, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[WatchCTT.Frog], GourmandCombos.objectsLibrary[abstractObjectType], 1, BaseAOT.FlareBomb, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[WatchCTT.Tardigrade], GourmandCombos.objectsLibrary[abstractObjectType], 1, DLCAOT.GlowWeed, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[WatchCTT.Rat], GourmandCombos.objectsLibrary[abstractObjectType], 1, BaseAOT.PuffBall, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[BaseCTT.Fly], GourmandCombos.objectsLibrary[abstractObjectType], 1, BaseAOT.PuffBall, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[BaseCTT.SmallCentipede], GourmandCombos.objectsLibrary[abstractObjectType], 1, BaseAOT.SporePlant, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[BaseCTT.VultureGrub], GourmandCombos.objectsLibrary[abstractObjectType], 1, null, BaseCTT.Hazer);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[WatchCTT.SandGrub], GourmandCombos.objectsLibrary[abstractObjectType], 1, null, BaseCTT.Hazer);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[BaseCTT.SmallNeedleWorm], GourmandCombos.objectsLibrary[abstractObjectType], 1, null, WatchCTT.SandGrub);

            abstractObjectType = WatchAOT.FireSpriteLarva;
            // item
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.Rock], 0, BaseAOT.Lantern, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.FlyLure], 0, DLCAOT.GooieDuck, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.FirecrackerPlant], 0, BaseAOT.SporePlant, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.ScavengerBomb], 0, BaseAOT.FirecrackerPlant, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.Mushroom], 0, DLCAOT.GooieDuck, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.PuffBall], 0, BaseAOT.GraffitiBomb, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.SporePlant], 0, BaseAOT.ScavengerBomb, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.FlareBomb], 0, BaseAOT.Lantern, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.Lantern], 0, BaseAOT.DataPearl, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.VultureMask], 0, BaseAOT.DataPearl, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.NeedleEgg], 0, null, WatchCTT.SandGrub);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.BubbleGrass], 0, DLCAOT.GlowWeed, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.KarmaFlower], 0, BaseAOT.DataPearl, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[DLCAOT.SingularityBomb], 0, MSCAOT.FireEgg, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.OverseerCarcass], 0, BaseAOT.DataPearl, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.DataPearl], 0, null, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[abstractObjectType], 0, null, null);

            // meal
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.SSOracleSwarmer], 0, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[MSCAOT.FireEgg], 0, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[DLCAOT.Seed], 0, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.DangleFruit], 0, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.EggBugEgg], 0, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[DLCAOT.DandelionPeach], 0, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[DLCAOT.GooieDuck], 0, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.SlimeMold], 0, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[DLCAOT.LillyPuck], 0, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[DLCAOT.GlowWeed], 0, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.WaterNut], 0, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.objectsLibrary[abstractObjectType], GourmandCombos.objectsLibrary[BaseAOT.JellyFish], 0, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[BaseCTT.Hazer], GourmandCombos.objectsLibrary[abstractObjectType], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[WatchCTT.Frog], GourmandCombos.objectsLibrary[abstractObjectType], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[WatchCTT.Tardigrade], GourmandCombos.objectsLibrary[abstractObjectType], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[WatchCTT.Rat], GourmandCombos.objectsLibrary[abstractObjectType], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[BaseCTT.Fly], GourmandCombos.objectsLibrary[abstractObjectType], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[BaseCTT.SmallCentipede], GourmandCombos.objectsLibrary[abstractObjectType], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[BaseCTT.VultureGrub], GourmandCombos.objectsLibrary[abstractObjectType], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[WatchCTT.SandGrub], GourmandCombos.objectsLibrary[abstractObjectType], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[BaseCTT.SmallNeedleWorm], GourmandCombos.objectsLibrary[abstractObjectType], 1, BaseAOT.DangleFruit, null);

            BaseCTT type = WatchCTT.Frog;
            // item
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.Rock], 1, DLCAOT.LillyPuck, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.FlyLure], 1, BaseAOT.Mushroom, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.FirecrackerPlant], 1, BaseAOT.SporePlant, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.ScavengerBomb], 1, null, BaseCTT.Snail);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.Mushroom], 1, null, WatchCTT.Rat);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.PuffBall], 1, BaseAOT.GraffitiBomb, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.SporePlant], 1, DLCAOT.LillyPuck, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.FlareBomb], 1, BaseAOT.Lantern, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.Lantern], 1, WatchAOT.FireSpriteLarva, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.VultureMask], 1, DLCAOT.Seed, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.NeedleEgg], 1, DLCAOT.Seed, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.BubbleGrass], 1, DLCAOT.LillyPuck, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.KarmaFlower], 1, null, WatchCTT.Tardigrade);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.SingularityBomb], 1, MSCAOT.FireEgg, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.OverseerCarcass], 1, BaseAOT.DataPearl, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.DataPearl], 1, null, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[type], 2, null, null);
            // meal
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.SSOracleSwarmer], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[MSCAOT.FireEgg], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.Seed], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.DangleFruit], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.EggBugEgg], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.DandelionPeach], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.GooieDuck], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.SlimeMold], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.LillyPuck], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.GlowWeed], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.WaterNut], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.JellyFish], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.Hazer], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.Frog], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.Tardigrade], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.Rat], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.Fly], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.SmallCentipede], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.VultureGrub], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.SandGrub], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.SmallNeedleWorm], 2, BaseAOT.DangleFruit, null);

            type = WatchCTT.Tardigrade;
            // item
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.Rock], 1, BaseAOT.JellyFish, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.FlyLure], 1, BaseAOT.BubbleGrass, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.FirecrackerPlant], 1, BaseAOT.JellyFish, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.ScavengerBomb], 1, BaseAOT.GraffitiBomb, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.Mushroom], 1, null, WatchCTT.Frog);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.PuffBall], 1, BaseAOT.GraffitiBomb, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.SporePlant], 1, BaseAOT.JellyFish, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.FlareBomb], 1, BaseAOT.JellyFish, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.Lantern], 1, DLCAOT.GlowWeed, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.VultureMask], 1, DLCAOT.Seed, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.NeedleEgg], 1, DLCAOT.Seed, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.BubbleGrass], 1, DLCAOT.GlowWeed, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.KarmaFlower], 1, BaseAOT.GraffitiBomb, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.SingularityBomb], 1, MSCAOT.FireEgg, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.OverseerCarcass], 1, BaseAOT.DataPearl, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.DataPearl], 1, null, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[type], 2, null, null);
            // meal
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.SSOracleSwarmer], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[MSCAOT.FireEgg], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.Seed], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.DangleFruit], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.EggBugEgg], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.DandelionPeach], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.GooieDuck], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.SlimeMold], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.LillyPuck], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.GlowWeed], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.WaterNut], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.JellyFish], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.Hazer], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.Frog], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.Tardigrade], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.Rat], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.Fly], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.SmallCentipede], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.VultureGrub], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.SandGrub], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.SmallNeedleWorm], 2, BaseAOT.DangleFruit, null);

            type = WatchCTT.Rat;
            // item
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.Rock], 1, BaseAOT.Mushroom, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.FlyLure], 1, null, BaseCTT.Fly);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.FirecrackerPlant], 1, BaseAOT.SporePlant, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.ScavengerBomb], 1, BaseAOT.PuffBall, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.Mushroom], 1, null, BaseCTT.SmallCentipede);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.PuffBall], 1, BaseAOT.GraffitiBomb, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.SporePlant], 1, null, WatchCTT.Frog);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.FlareBomb], 1, BaseAOT.Mushroom, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.Lantern], 1, BaseAOT.Mushroom, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.VultureMask], 1, DLCAOT.Seed, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.NeedleEgg], 1, DLCAOT.Seed, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.BubbleGrass], 1, null, WatchCTT.Tardigrade);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.KarmaFlower], 1, BaseAOT.PuffBall, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.SingularityBomb], 1, MSCAOT.FireEgg, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.OverseerCarcass], 1, BaseAOT.DataPearl, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.DataPearl], 1, null, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[type], 2, null, null);
            // meal
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.SSOracleSwarmer], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[MSCAOT.FireEgg], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.Seed], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.DangleFruit], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.EggBugEgg], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.DandelionPeach], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.GooieDuck], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.SlimeMold], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.LillyPuck], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.GlowWeed], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.WaterNut], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.JellyFish], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.Hazer], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.Frog], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.Tardigrade], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.Rat], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.Fly], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.SmallCentipede], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.VultureGrub], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.SandGrub], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.SmallNeedleWorm], 2, BaseAOT.DangleFruit, null);

            type = WatchCTT.SandGrub;
            // item
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.Rock], 1, BaseAOT.PuffBall, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.FlyLure], 1, BaseAOT.FirecrackerPlant, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.FirecrackerPlant], 1, BaseAOT.SporePlant, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.ScavengerBomb], 1, BaseAOT.FirecrackerPlant, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.Mushroom], 1, DLCAOT.GooieDuck, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.PuffBall], 1, BaseAOT.GraffitiBomb, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.SporePlant], 1, BaseAOT.PuffBall, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.FlareBomb], 1, BaseAOT.PuffBall, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.Lantern], 1, WatchAOT.FireSpriteLarva, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.VultureMask], 1, DLCAOT.Seed, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.NeedleEgg], 1, DLCAOT.Seed, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.BubbleGrass], 1, BaseAOT.Mushroom, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.KarmaFlower], 1, null, BaseCTT.TubeWorm);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.SingularityBomb], 1, MSCAOT.FireEgg, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.OverseerCarcass], 1, BaseAOT.DataPearl, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.DataPearl], 1, null, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[type], 2, null, null);
            // meal
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.SSOracleSwarmer], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[MSCAOT.FireEgg], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.Seed], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.DangleFruit], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.EggBugEgg], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.DandelionPeach], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.GooieDuck], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.SlimeMold], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.LillyPuck], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[DLCAOT.GlowWeed], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.WaterNut], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.objectsLibrary[BaseAOT.JellyFish], 1, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.Hazer], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.Frog], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.Tardigrade], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.Rat], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.Fly], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.SmallCentipede], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.VultureGrub], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[WatchCTT.SandGrub], 2, BaseAOT.DangleFruit, null);
            GourmandCombos.SetLibraryData(GourmandCombos.critsLibrary[type], GourmandCombos.critsLibrary[BaseCTT.SmallNeedleWorm], 2, BaseAOT.DangleFruit, null);
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
        public override bool AvailableForSlugcat(SlugName name)
        {
            return name == WatcherEnums.SlugcatStatsName.Watcher;
        }
    }
}
