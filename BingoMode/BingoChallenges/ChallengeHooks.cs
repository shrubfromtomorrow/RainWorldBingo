using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Reflection;
using BingoMode.BingoChallenges.WatcherBingoChallenges;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu;
using Menu.Remix;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MoreSlugcats;
using RWCustom;
using UnityEngine;
using Watcher;
using CreatureType = CreatureTemplate.Type;
using DLCItemType = DLCSharedEnums.AbstractObjectType;
using ItemType = AbstractPhysicalObject.AbstractObjectType;
using MSCItemType = MoreSlugcats.MoreSlugcatsEnums.AbstractObjectType;

namespace BingoMode.BingoChallenges
{
    public class SettingBox<T> : IStrongBox // Basically just strongbox but i needed morer data to store (i dont know if this works to be honest)
    {
        public Type type;
        public string name;
        public int index;
        public T Value;
        public string listName;
        public SettingBox(T value, string displayName, int index, string listName = null)
        {
            Value = value;
            type = typeof(T);
            name = displayName;
            this.index = index;
            this.listName = listName;
        }

        object IStrongBox.Value
        {
            get
            {
                return Value;
            }
            set
            {
                Value = (T)((object)value);
            }
        }

        public override string ToString()
        {
            string excl = "NULL";
            if (listName != null)
            {
                excl = listName;
            }
            return string.Concat(
                type.ToString(),
                "|",
                ValueConverter.ConvertToString(Value),
                "|",
                name,
                "|",
                index.ToString(),
                "|",
                excl
                );
        }
    }

    public static class ChallengeHooks
    {
        public static object SettingBoxFromString(string save)
        {
            string[] settings = save.Split('|');
            try
            {
                //
                object bocks = null;
                //int[] locks = null;
                string listName = null;
                if (settings[4] != "NULL")
                {
                    //List<int> tempLocks = [];
                    //foreach (var g in settings[4].Split(','))
                    //{
                    //    tempLocks.Add(int.Parse(g));
                    //}
                    //locks = tempLocks.ToArray();
                    listName = settings[4];
                }
                switch (settings[0])
                {
                    case "Int32":
                    case "Int64":
                    case "System.Int32":
                    case "System.Int64":
                        //
                        bocks = new SettingBox<int>(int.Parse(settings[1]), settings[2], int.Parse(settings[3]), listName);
                        break;
                    case "Boolean":
                    case "System.Boolean":
                        //
                        bocks = new SettingBox<bool>(settings[1].ToLowerInvariant() == "true", settings[2], int.Parse(settings[3]), listName);
                        break;
                    case "String":
                    case "System.String":
                        //
                        bocks = new SettingBox<string>(settings[1], settings[2], int.Parse(settings[3]), listName);
                        break;
                }
                //
                //
                return bocks;
            }
            catch(Exception ex)
            {
                
                foreach (var j in settings)
                {
                    
                }; 
                // Kind of an obnoxious log
                //Plugin.logger.LogError("Failed to recreate SettingBox from string!" + ex);
                return null;
            }
        }

        // Runtime detour hooks
        public static Hook tokenColorHook;
        public static Hook placeKarmaFlowerHook;

        // Sporecloud/graffiticloud fix for owner recognition (UpdatableAndDeletable)
        public static Dictionary<UpdatableAndDeletable, EntityID> ownerOfUAD = [];
        public static ConditionalWeakTable<Room, List<EntityID>> playerTradeItems = new ConditionalWeakTable<Room, List<EntityID>>();

        // Normal/IL hooks
        public static void Apply()
        {
            // Clearing stuff that needs to be cleared at the end of the cycle
            On.SaveState.SessionEnded += ClearBs;

            // For damage and kill challenges, i put them here since theres so many and both challenges would have to do the same hooks
            // watcher touches this
            On.Spear.HitSomething += Spear_HitSomething;
            On.Rock.HitSomething += Rock_HitSomething;
            On.ScavengerBomb.HitSomething += ScavengerBomb_HitSomething;
            On.MoreSlugcats.LillyPuck.HitSomething += LillyPuck_HitSomething;
            IL.Explosion.Update += Explosion_Update;
            IL.SporeCloud.Update += SporeCloud_Update;
            IL.JellyFish.Collide += JellyFish_Collide;
            IL.PuffBall.Explode += PuffBall_Explode;
            On.Boomerang.HitSomething += Boomerang_HitSomething;
            IL.GraffitiBomb.Explode += GraffitiBomb_Explode;
            IL.GraffitiCloud.Update += GraffitiCloud_Update;
            //IL.FlareBomb.Update += FlareBomb_Update;

            // No need for unlocked slugs
            IL.Expedition.ChallengeTools.ParseCreatureSpawns += ChallengeTools_ParseCreatureSpawnsIL;
            On.Expedition.ChallengeTools.ParseCreatureSpawns += ChallengeTools_ParseCreatureSpawns;

            // Many challenges use this and I want to make it general purpose
            IL.Room.Loaded += Room_LoadedKarmaFlower;
            placeKarmaFlowerHook = new(typeof(Player).GetProperty("PlaceKarmaFlower", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetGetMethod(), Player_PlaceKarmaFlower_get);

            // This is for BingoOneCycleChallenge
            On.SaveState.SessionEnded += SaveState_SessionEnded;
        }

        public static void SaveState_SessionEnded(On.SaveState.orig_SessionEnded orig, SaveState self, RainWorldGame game, bool survived, bool newMalnourished)
        {
            Plugin.logger.LogInfo("Session ended");
            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoOneCycleChallenge c)
                {
                    c.EndCycle();
                }
            }
            orig.Invoke(self, game, survived, newMalnourished);
        }

        public static bool Scavenger_GrabTrade(On.Scavenger.orig_Grab orig, Scavenger self, PhysicalObject obj, int graspUsed, int chunkGrabbed, Creature.Grasp.Shareability shareability, float dominance, bool overrideEquallyDominant, bool pacifying)
        {
            if (obj != null && self.room != null && self.abstractCreature.abstractAI is ScavengerAbstractAI ai && ai.squad != null &&
                self.room.abstractRoom.scavengerTrader &&
                ai.squad.missionType == ScavengerAbstractAI.ScavengerSquad.MissionID.Trade && playerTradeItems.TryGetValue(self.room, out var items))
            {
                if (items.Any(x => obj.abstractPhysicalObject.ID == x))
                {
                    for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                    {
                        if (ExpeditionData.challengeList[j] is BingoTradeChallenge c)
                        {
                            c.Traded(self.AI.CollectScore(obj, false), obj.abstractPhysicalObject.ID);
                        }
                    }
                }
            }

            return orig.Invoke(self, obj, graspUsed, chunkGrabbed, shareability, dominance, overrideEquallyDominant, pacifying);
        }

        public static void Player_ReleaseGrasp(On.Player.orig_ReleaseGrasp orig, Player self, int grasp)
        {
            if (self.grasps[grasp] != null && self.room != null && playerTradeItems.TryGetValue(self.room, out var items))
            {
                items.Add(self.grasps[grasp].grabbed.abstractPhysicalObject.ID);
            }

            orig.Invoke(self, grasp);
        }

        public static void Room_Unloaded(On.Room.orig_Unloaded orig, Room self)
        {
            orig.Invoke(self);

            if (self.abstractRoom.scavengerTrader && playerTradeItems.TryGetValue(self, out var items))
            {
                playerTradeItems.Remove(self);
            }
        }

        public static void Room_ctor(On.Room.orig_ctor orig, Room self, RainWorldGame game, World world, AbstractRoom abstractRoom, bool devUI)
        {
            orig.Invoke(self, game, world, abstractRoom, devUI);

            if (!abstractRoom.scavengerTrader) return;

            if (playerTradeItems.TryGetValue(self, out var items))
            {
                items = [];
            }
            else
            {
                playerTradeItems.Add(self, []);
            }
        }

        // we HATE constructors fuck them all we hate them, use this instead
        public static void KarmaLadder_AddEndgameMeters(On.Menu.KarmaLadder.orig_AddEndgameMeters orig, KarmaLadder self)
        {
            orig.Invoke(self);
            if (self.endGameMeters.Count == 0)
            {
                return;
            }

            foreach (var meter in self.endGameMeters)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoAchievementChallenge c &&
                        meter.tracker.ID.value.ToUpperInvariant() == c.ID.Value.ToUpperInvariant() &&
                        meter.tracker.GoalFullfilled)
                    {
                        c.GetAchievement();
                    }
                }
            }

        }

        public static void Ghost_StartConversation(On.Ghost.orig_StartConversation orig, Ghost self)
        {
            orig.Invoke(self);

            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoEchoChallenge c)
                {
                    c.SeeGhost(self.worldGhost.ghostID.value);
                }
            }
        }

        public static void RainWorldGame_GoToStarveScreenHell(On.RainWorldGame.orig_GoToStarveScreen orig, RainWorldGame self)
        {
            orig.Invoke(self);

            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoHellChallenge hell)
                {
                    hell.Fail();
                }
            }
        }

        public static void RainWorldGame_GoToDeathScreenHell(On.RainWorldGame.orig_GoToDeathScreen orig, RainWorldGame self)
        {
            orig.Invoke(self);

            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoHellChallenge hell)
                {
                    hell.Fail();
                }
            }
        }

        public static void Player_DieHell(On.Player.orig_Die orig, Player self)
        {
            orig.Invoke(self);

            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoHellChallenge hell)
                {
                    hell.Fail();
                }
            }
        }

        public static void SaveState_ctorHalcyon(On.SaveState.orig_ctor orig, SaveState self, SlugcatStats.Name saveStateNumber, PlayerProgression progression)
        {
            orig.Invoke(self, saveStateNumber, progression);

            self.miscWorldSaveData.halcyonStolen = true;
        }

        public static void CLOracleBehavior_Update_Iterator(On.MoreSlugcats.CLOracleBehavior.orig_Update orig, CLOracleBehavior self, bool eu)
        {
            orig.Invoke(self, eu);

            if (self.hasNoticedPlayer)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoIteratorChallenge c && !c.moon.Value)
                    {
                        c.MeetPebbles();
                    }
                }
            }
        }

        public static void CLOracleBehavior_Update_SaintDelivery(On.MoreSlugcats.CLOracleBehavior.orig_Update orig, CLOracleBehavior self, bool eu)
        {
            orig.Invoke(self, eu);

            if (self.FocusedOnHalcyon)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoSaintDeliveryChallenge c)
                    {
                        c.Delivered();
                    }
                }
            }
        }

        public static void SSOracleGetGreenNeuron_HoldingNeuronUpdate(On.SSOracleBehavior.SSOracleGetGreenNeuron.orig_HoldingNeuronUpdate orig, SSOracleBehavior.SSOracleGetGreenNeuron self, bool eu)
        {
            orig.Invoke(self, eu);

            if (!self.holdingNeuron) return;
            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoGreenNeuronChallenge c && !c.moon.Value)
                {
                    c.Delivered();
                }
            }
        }

        public static bool Scavenger_Grab(On.Scavenger.orig_Grab orig, Scavenger self, PhysicalObject obj, int graspUsed, int chunkGrabbed, Creature.Grasp.Shareability shareability, float dominance, bool overrideEquallyDominant, bool pacifying)
        {
            if (self.room != null && self.abstractCreature.abstractAI is ScavengerAbstractAI ai && ai.squad != null && 
                self.room.abstractRoom.scavengerTrader &&
                ai.squad.missionType == ScavengerAbstractAI.ScavengerSquad.MissionID.Trade)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoTradeTradedChallenge t)
                    {
                        if (t.traderItems.ContainsKey(obj.abstractPhysicalObject.ID))
                        {
                            string room = self.room.abstractRoom.name;
                            //
                            if (t.traderItems[obj.abstractPhysicalObject.ID].ToLowerInvariant() != room.ToLowerInvariant())
                            {
                                t.Traded(obj.abstractPhysicalObject.ID, room);
                            }
                        }
                    }
                }
            }

            return orig.Invoke(self, obj, graspUsed, chunkGrabbed, shareability, dominance, overrideEquallyDominant, pacifying);
        }

        public static void Player_FoodInRoom_Room_bool(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchStfld<DeathPersistentSaveData>("reinforcedKarma")
                ))
            {
                c.EmitDelegate<Action>(() =>
                {
                    for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                    {
                        if (ExpeditionData.challengeList[j] is BingoKarmaFlowerChallenge c)
                        {
                            c.Karmad();
                        }
                    }
                });
            }
            else Plugin.logger.LogError("Player_FoodInRoom_Room_bool FAILED" + il);
        }

        public static void Room_LoadedBlessedNeedles(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdfld<AbstractSpear>("needle")
                ))
            {
                c.Emit(OpCodes.Ldloc, 11);
                c.EmitDelegate<Func<bool, AbstractSpear, bool>>((orig, spear) =>
                {
                    if (ExpeditionData.challengeList.Any(x => x is BingoTradeTradedChallenge c && c.traderItems.Keys.Count > 0 && c.traderItems.Keys.Contains(spear.ID)))
                    {
                        orig = false;
                    }

                    return orig;
                });
            }
            else Plugin.logger.LogError("Room_LoadedBlessedNeedles FAILED" + il);
        }

        private static void ChallengeTools_ParseCreatureSpawns(On.Expedition.ChallengeTools.orig_ParseCreatureSpawns orig)
        {
            BingoData.pinnableCreatureRegions = [];
            orig.Invoke();
        }

        private static void ChallengeTools_ParseCreatureSpawnsIL(ILContext il)
        {
            ILCursor b = new(il);
            if (b.TryGotoNext(MoveType.After,
                x => x.MatchLdstr("FAILED TO PARSE: ")
                ))
            {
                b.Index += 4;
                b.MoveAfterLabels();

                b.Emit(OpCodes.Ldloc, 16);
                b.Emit(OpCodes.Ldloc, 6);
                b.Emit(OpCodes.Ldloc, 1);
                b.EmitDelegate<Action<string, string, SlugcatStats.Name>>((creature, region, slug) =>
                {
                    if (string.IsNullOrEmpty(creature) || !ChallengeUtils.GetCorrectListForChallenge("pin").Contains(creature)) return;
                    string regionString = slug.value + "_" + region;
                    if (!BingoData.pinnableCreatureRegions.ContainsKey(creature))
                    {
                        BingoData.pinnableCreatureRegions.Add(creature, [regionString]);
                        return;
                    }
                    if (BingoData.pinnableCreatureRegions[creature] == null) BingoData.pinnableCreatureRegions[creature] = [];
                    if (!BingoData.pinnableCreatureRegions[creature].Contains(regionString)) BingoData.pinnableCreatureRegions[creature].Add(regionString);
                });
            }
            else Plugin.logger.LogError("ChallengeTools_ParseCreatureSpawns FAILED" + il);

            ILCursor c = new(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld("Expedition.ExpeditionGame", "unlockedExpeditionSlugcats")
                ))
            {
                c.EmitDelegate<Func<List<SlugcatStats.Name>, List<SlugcatStats.Name>>>((orig) =>
                {
                    List<SlugcatStats.Name> slugs = [];
                    foreach (string name in ExtEnum<SlugcatStats.Name>.values.entries)
                    {
                        slugs.Add(new(name, false));
                    }
                    return slugs;
                });
            }
            else Plugin.logger.LogError("ChallengeTools_ParseCreatureSpawns FAILED" + il);
        }

        public static void BigEel_Update(On.BigEel.orig_Update orig, BigEel self, bool eu)
        {
            if (ExpeditionData.challengeList.Any(x => x is BingoDodgeLeviathanChallenge d && !d.TeamsCompleted[SteamTest.team] && !d.completed))
            {
                self.jawChargeFatigue = 0f;
            }
            orig.Invoke(self, eu);
        }

        public static void ScavengerAI_RecognizePlayerOfferingGift(On.ScavengerAI.orig_RecognizePlayerOfferingGift orig, ScavengerAI self, Tracker.CreatureRepresentation subRep, Tracker.CreatureRepresentation objRep, bool objIsMe, PhysicalObject item)
        {
            orig.Invoke(self, subRep, objRep, objIsMe, item);

            if (self.giftForMe == item.abstractPhysicalObject && item is Spear spear && spear.IsNeedle)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoNoNeedleTradingChallenge c)
                    {
                        c.Traded();
                    }
                }
            }
        }

        public static void Player_GrabUpdateArtiMaulTypes(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdstr("Mauled target"),
                x => x.MatchStelemRef(),
                x => x.MatchCallOrCallvirt("RWCustom.Custom", "Log")
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, 8);
                c.EmitDelegate<Action<Player, int>>((self, grasp) =>
                {
                    for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                    {
                        if (ExpeditionData.challengeList[j] is BingoMaulTypesChallenge c)
                        {
                            c.Maul((self.grasps[grasp].grabbed as Creature).Template.type.value);
                        }
                    }
                });
            }
            else Plugin.logger.LogError("Player_GrabUpdateArtiMaulX FAILURE " + il);
        }

        public static void Player_GrabUpdateArtiMaulX(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdstr("Mauled target"),
                x => x.MatchStelemRef(),
                x => x.MatchCallOrCallvirt("RWCustom.Custom", "Log")
                ))
            {
                c.EmitDelegate<Action>(() =>
                {
                    for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                    {
                        if (ExpeditionData.challengeList[j] is BingoMaulXChallenge c)
                        {
                            c.Maul();
                        }
                    }
                });
            }
            else Plugin.logger.LogError("Player_GrabUpdateArtiMaulX FAILURE " + il);
        }

        public static void EnergyCell_Update(On.MoreSlugcats.EnergyCell.orig_Update orig, EnergyCell self, bool eu)
        {
            if (ExpeditionData.challengeList.Any(x => x is BingoRivCellChallenge c && (c.TeamsCompleted[SteamTest.team] || c.completed))) self.KeepOff();
            orig.Invoke(self, eu);
        }

        public static void Room_LoadedEnergyCell(ILContext il)
        {
            ILCursor b = new(il);
            if (b.TryGotoNext(
                x => x.MatchLdsfld("Expedition.ExpeditionData", "startingDen")
                ) &&
                b.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt<WorldCoordinate>(".ctor")
                ))
            {
                b.Emit(OpCodes.Ldarg_0);
                // Hate. Let me tell you how much I've come to HATE this local index since I've been born
                b.Emit(OpCodes.Ldloc, 137);
                b.EmitDelegate<Action<Room, WorldCoordinate>>((room, pos) =>
                {
                    AbstractWorldEntity existingFucker = room.abstractRoom.entities.FirstOrDefault(x => x is AbstractPhysicalObject o && o.type == MSCItemType.EnergyCell);
                    if (existingFucker != null)
                    {
                        room.abstractRoom.RemoveEntity(existingFucker);
                    }

                    AbstractPhysicalObject startItem = new(room.world, MSCItemType.EnergyCell, null, new WorldCoordinate(room.abstractRoom.index, room.shelterDoor.playerSpawnPos.x, room.shelterDoor.playerSpawnPos.y, 0), room.game.GetNewID());
                    room.abstractRoom.entities.Add(startItem);
                    startItem.Realize();
                });
            }
            else Plugin.logger.LogError("Room_LoadedEnergyCell IL FAILURE " + il);
        }

        public static void EnergyCell_Use(On.MoreSlugcats.EnergyCell.orig_Use orig, EnergyCell self, bool forced)
        {
            if (ExpeditionData.challengeList.Any(x => x is BingoRivCellChallenge c && c.TeamsCompleted[SteamTest.team])) return;
            orig.Invoke(self, forced);
        }

        public static void EnergyCell_Explode(On.MoreSlugcats.EnergyCell.orig_Explode orig, EnergyCell self)
        {
            orig.Invoke(self);

            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoRivCellChallenge c)
                {
                    c.CellExploded();
                }
            }
        }

        public static void Spear_HitSomethingWithoutStopping(On.Spear.orig_HitSomethingWithoutStopping orig, Spear self, PhysicalObject obj, BodyChunk chunk, PhysicalObject.Appendage appendage)
        {
            orig.Invoke(self, obj, chunk, appendage);

            if (obj is not KarmaFlower || !self.IsNeedle || !self.spearmasterNeedle_hasConnection) return;
            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoKarmaFlowerChallenge c)
                {
                    c.Karmad();
                }
            }
        }

        // false allows a flower to spawn
        public static void Room_LoadedKarmaFlower(ILContext il)
        {
            ILCursor c = new(il);
            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchBrfalse(out _),
                x => x.MatchLdstr("Preventing natural KarmaFlower spawn")
                ))
            {
                c.EmitDelegate<Func<bool, bool>>((orig) =>
                {
                    //if (ExpeditionData.challengeList.Any(x => x is BingoKarmaFlowerChallenge c && (c.TeamsCompleted[SteamTest.team] || c.completed))) return orig;
                    if (ExpeditionData.challengeList.Any(x => x is BingoKarmaFlowerChallenge || 
                        (x is BingoItemHoardChallenge c && c.target.Value == "KarmaFlower") ||
                        (x is BingoCraftChallenge c2 && c2.craftee.Value == "KarmaFlower") ||
                        (x is BingoStealChallenge c3 && c3.subject.Value == "KarmaFlower"))) {
                        return false;
                    }
                    return orig;
                });
            }
            else Plugin.logger.LogError("Room_LoadedKarmaFlower FAILURE " + il);
        }

        public static void Player_SpitUpCraftedObjectIL(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(
                x => x.MatchLdloc(0),
                x => x.MatchLdfld<AbstractPhysicalObject>("realizedObject"),
                x => x.MatchLdarg(0),
                x => x.MatchCallOrCallvirt<Player>("FreeHand"),
                x => x.MatchCallOrCallvirt<Player>("SlugcatGrab")
                ) && c.TryGotoNext(MoveType.After,
                x => x.MatchLdloc(0),
                x => x.MatchLdfld<AbstractPhysicalObject>("realizedObject"),
                x => x.MatchLdarg(0),
                x => x.MatchCallOrCallvirt<Player>("FreeHand"),
                x => x.MatchCallOrCallvirt<Player>("SlugcatGrab")
                ))
            {
                c.Emit(OpCodes.Ldloc_0);
                c.EmitDelegate<Action<AbstractPhysicalObject>>((obj) =>
                {
                    for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                    {
                        if (ExpeditionData.challengeList[j] is BingoCraftChallenge c)
                        {
                            c.Crafted(obj.type);
                        }
                    }
                });
            }
            else Plugin.logger.LogError("Player_SpitUpCraftedObjectIL FAILURE " + il);
        }

        public static void ShelterDoor_Close(On.ShelterDoor.orig_Close orig, ShelterDoor self)
        {
            orig.Invoke(self);

            if (self.Broken || self.closedFac != 0f) return;
            int eggs = 0;

            foreach (var entity in self.room.abstractRoom.entities)
            {
                if (entity is AbstractPhysicalObject p && p.type == ItemType.NeedleEgg)
                {
                    eggs++;
                }
            }

            if (eggs == 0) return;
            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoHatchNoodleChallenge c)
                {
                    for (int e = 0; e < eggs; e++)
                    {
                        c.Hatch();
                    }
                }
            }
        }

        public static void Challenge_CompleteChallengeIL(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdstr("unl-passage")
                ))
            {
                c.Index++;
                c.EmitDelegate<Func<bool, bool>>((orig) =>
                {
                    if (BingoData.BingoMode) orig = false;
                    return orig;
                });
            }
            else Plugin.logger.LogError("Uh oh, Challenge_CompleteChallengeIL il fucked up " + il);
        }

        //public static void Challenge_CompleteChallenge(On.Expedition.Challenge.orig_CompleteChallenge orig, Challenge self)
        //{
        //    if (self.completed) return;
        //    if (self is BingoChallenge c)
        //    {
        //        if (self.hidden) return; // Hidden means locked out here in bingo
        //        if (c.RequireSave() && !self.revealed) // I forgot what this does
        //        {
        //            self.revealed = true;
        //            return;
        //        }
        //
        //        if (SteamTest.LobbyMembers.Count > 0)
        //        {
        //            SteamTest.BroadcastCompletedChallenge(self);
        //        }
        //    }
        //
        //    orig.Invoke(self);
        //    if (BingoData.BingoMode) Expedition.Expedition.coreFile.Save(false);
        //}

        //public static void WinState_CycleCompleted(ILContext il)
        //{
        //    ILCursor c = new(il);
        //
        //    if (c.TryGotoNext(
        //        x => x.MatchStloc(42),
        //        x => x.MatchLdloc(42),
        //        x => x.MatchIsinst<AchievementChallenge>()
        //        ))
        //    {
        //        c.Index++;
        //        c.Emit(OpCodes.Ldloc, 42);
        //        c.Emit(OpCodes.Ldarg_0);
        //        c.EmitDelegate<Action<Challenge, WinState>>((ch, self) =>
        //        {
        //            if (ch is BingoAchievementChallenge c) c.CheckAchievementProgress(self);
        //        });
        //    }
        //    else Plugin.logger.LogError("Uh oh, WinState_CycleCompleted il fucked up " + il);
        //}

        public static void FlareBomb_Update(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt<Room>("VisualContact")
                ))
            {
                c.Index += 2;
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc_0);
                c.EmitDelegate<Action<FlareBomb, int>>((self, i) =>
                {
                    
                    if (BingoData.BingoMode && self.thrownBy != null && self.thrownBy.abstractCreature.creatureTemplate.type == CreatureType.Slugcat && self.room.abstractRoom.creatures[i].realizedCreature is Creature victim)
                    {
                        ReportHit(self.abstractPhysicalObject.type.value, victim, self.abstractPhysicalObject.ID);
                    }
                });
            }
            else Plugin.logger.LogError("Uh oh, FlareBomb_Update il fucked up " + il);
        }

        public static void PuffBall_Explode(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchNewobj<SporeCloud>(),
                x => x.MatchCallOrCallvirt<Room>("AddObject")
                ))
            {
                c.Index++;
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<SporeCloud, PuffBall, SporeCloud>>((orig, self) =>
                {
                    if (BingoData.BingoMode && self.thrownBy != null && self.thrownBy.abstractCreature.creatureTemplate.type == CreatureType.Slugcat)
                    {
                        ownerOfUAD[orig] = self.abstractPhysicalObject.ID;
                    }

                    return orig;
                });
            }
            else Plugin.logger.LogError("Uh oh, PuffBall_Explode il fucked up " + il);
        }

        // So we recognize the explosion before the creature dies!!!
        public static void Explosion_Update(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(
                x => x.MatchLdloc(6),
                x => x.MatchLdcI4(-1),
                x => x.MatchBle(out ILLabel lable)
                ) &&
                c.TryGotoNext(MoveType.Before,
                x => x.MatchStloc(12)
                ))
            {
                c.Index--;
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc_2);
                c.Emit(OpCodes.Ldloc_3);
                c.EmitDelegate<Action<Explosion, int, int>>((explosion, j, k) =>
                {
                    if (BingoData.BingoMode && explosion.sourceObject != null && explosion.killTagHolder is Player && explosion.room.physicalObjects[j][k] is Creature victim)
                    {
                        if (!victim.dead) ReportHit(explosion.sourceObject.abstractPhysicalObject.type.value, victim, explosion.sourceObject.abstractPhysicalObject.ID);
                    }
                });
            }
            else Plugin.logger.LogError("Uh oh, Explosion_Update il fucked up " + il);
        }

        public static void ReportHit(string weapon, Creature victim, EntityID source, bool report = true)
        {
            if (weapon == null || victim == null) return;

            if (source != null && report)
            {
                if (BingoData.blacklist.TryGetValue(victim, out var gruh) && gruh.Contains(source)) return;
                if (!BingoData.blacklist.ContainsKey(victim)) BingoData.blacklist.Add(victim, []);
                if (BingoData.blacklist.TryGetValue(victim, out var list) && !list.Contains(source)) list.Add(source);
            }

            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoDamageChallenge c)
                {
                    c.Hit(weapon, victim);
                }
            }

            EntityID id = victim.abstractCreature.ID;
            if (!BingoData.hitTimeline.ContainsKey(id)) BingoData.hitTimeline.Add(id, []); 
            if (BingoData.hitTimeline.TryGetValue(id, out var gru) && (gru.Count == 0 || gru.Last() != weapon)) { gru.Remove(weapon); gru.Add(weapon);  }
        }

        public static void Creature_UpdateIL(ILContext il)
        {
            ILCursor c = new(il);
        
            if (c.TryGotoNext(
                x => x.MatchLdstr("{0} Fell out of room!")
                ) && 
                c.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt<Creature>("Die")
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<Creature>>((self) =>
                {
                    if (self.killTag != null && self.killTag.creatureTemplate.type == CreatureType.Slugcat && self.killTag.realizedCreature is Player p)
                    {
                        for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                        {
                            if (ExpeditionData.challengeList[j] is BingoKillChallenge c)
                            {
                                c.DeathPit(self, p);
                            }
                        }
                    }
                });
            }
            else Plugin.logger.LogError("Uh oh, Creature_UpdateIL il fucked up " + il);
        }

        public static void Player_SlugcatGrab(On.Player.orig_SlugcatGrab orig, Player self, PhysicalObject obj, int graspUsed)
        {
            orig.Invoke(self, obj, graspUsed);

            if (obj is Creature crit)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoTransportChallenge c)
                    {
                        c.Grabbed(crit);
                    }
                }
            }
        }

        public static void Player_SlugcatGrabNoStealExploit(On.Player.orig_SlugcatGrab orig, Player self, PhysicalObject obj, int graspUsed)
        {
            orig.Invoke(self, obj, graspUsed);

            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoStealChallenge c && !c.toll.Value)
                {
                    c.checkedIDs.Add(obj.abstractPhysicalObject.ID);
                }
            }
        }

        public static bool LillyPuck_HitSomething(On.MoreSlugcats.LillyPuck.orig_HitSomething orig, LillyPuck self, SharedPhysics.CollisionResult result, bool eu)
        {
            if (BingoData.BingoMode && self.thrownBy is Player && result.obj is Creature victim && !victim.dead)
            {
                ReportHit(self.abstractPhysicalObject.type.value, victim, self.abstractPhysicalObject.ID, false);
            }

            return orig.Invoke(self, result, eu);
        }

        private static bool ScavengerBomb_HitSomething(On.ScavengerBomb.orig_HitSomething orig, ScavengerBomb self, SharedPhysics.CollisionResult result, bool eu)
        {
            if (BingoData.BingoMode && self.thrownBy is Player && result.obj is Creature victim && !victim.dead)
            {
                ReportHit(self.abstractPhysicalObject.type.value, victim, self.abstractPhysicalObject.ID, true);
            }

            return orig.Invoke(self, result, eu);
        }

        public static bool Rock_HitSomething(On.Rock.orig_HitSomething orig, Rock self, SharedPhysics.CollisionResult result, bool eu)
        {
            if (BingoData.BingoMode && self.thrownBy is Player && result.obj is Creature victim && !victim.dead)
            {
                ReportHit(self.abstractPhysicalObject.type.value, victim, self.abstractPhysicalObject.ID, false);
            }

            return orig.Invoke(self, result, eu);
        }

        public static bool Spear_HitSomething(On.Spear.orig_HitSomething orig, Spear self, SharedPhysics.CollisionResult result, bool eu)
        {
            if (BingoData.BingoMode && self.thrownBy is Player && result.obj is Creature victim && !victim.dead)
            {
                ReportHit(self.abstractPhysicalObject.type.value, victim, self.abstractPhysicalObject.ID, false);
            }

            return orig.Invoke(self, result, eu);
        }

        public static List<Challenge> revealInMemory = [];
        private static void ClearBs(On.SaveState.orig_SessionEnded orig, SaveState self, RainWorldGame game, bool survived, bool newMalnourished)
        {
            if (BingoData.BingoMode)
            {
                if (survived && !newMalnourished)
                {
                    revealInMemory = [];
                
                    for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                    {
                        if (ExpeditionData.challengeList[j] is BingoChallenge g && g.RequireSave() && g.revealed)
                        {
                            revealInMemory.Add(g);
                            g.revealed = false;
                        }
                    }
                }
                 
                ownerOfUAD.Clear();
                BingoData.hitTimeline.Clear();
                BingoData.blacklist.Clear();
                BingoData.heldItemsTime = new int[ExtEnum<ItemType>.values.Count];
            }

            orig.Invoke(self, game, survived, newMalnourished);
        }

        public static void ScavengerBomb_Explode(On.ScavengerBomb.orig_Explode orig, ScavengerBomb self, BodyChunk hitChunk)
        {
            orig.Invoke(self, hitChunk);

            if (self.room.abstractRoom.scavengerOutpost && self.room.updateList.Find(x => x is ScavengerOutpost) is ScavengerOutpost outpost && Custom.DistLess(self.firstChunk.pos, outpost.placedObj.pos, 500f))
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoBombTollChallenge c)
                    {
                        for (int k = 0; k < outpost.playerTrackers.Count; k++)
                        {
                            if (outpost.playerTrackers[k] != null)
                            {
                                c.Boom(self.room.abstractRoom.name, outpost.playerTrackers[k].PlayerOnOtherSide);
                            }
                        }
                    }
                }
            }
        }

        public static void FriendTracker_Update(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(
                x => x.MatchStfld<FriendTracker>("friendRel")))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<FriendTracker>>((self) =>
                {
                    for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                    {
                        if (ExpeditionData.challengeList[j] is BingoTameChallenge c)
                        {
                            c.Fren(self.AI.creature);
                        }
                    }
                });
            }
            else Plugin.logger.LogError("Uh oh, FriendTracker_Update il fucked up " + il);
        }

        // worldName is being loaded, game.world.region.name is currently loaded. null check to prevent progress check on goals when loading first region
        public static void WorldLoader_EnterRegionFrom(On.WorldLoader.orig_ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues orig, WorldLoader self, RainWorldGame game, SlugcatStats.Name playerCharacter, SlugcatStats.Timeline timelinePosition, bool singleRoomWorld, string worldName, Region region, RainWorldGame.SetupValues setupValues)
        {
            orig.Invoke(self, game, playerCharacter, timelinePosition, singleRoomWorld, worldName, region, setupValues);
            if (game != null && game.world != null)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoEnterRegionFromChallenge EnterRegionFrom)
                    {
                        EnterRegionFrom.Gate(game.world.region.name, worldName);
                    }
                }
            }
        }

        public static void WorldLoader_Transport(On.WorldLoader.orig_ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues orig, WorldLoader self, RainWorldGame game, SlugcatStats.Name playerCharacter, SlugcatStats.Timeline timelinePosition, bool singleRoomWorld, string worldName, Region region, RainWorldGame.SetupValues setupValues)
        {
            orig.Invoke(self, game, playerCharacter, timelinePosition, singleRoomWorld, worldName, region, setupValues);
            if (game != null && game.world != null)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoTransportChallenge transport)
                    {
                        transport.Gate(worldName);
                    }
                }
            }
        }

        public static void WorldLoader_CreatureGate(On.WorldLoader.orig_ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues orig, WorldLoader self, RainWorldGame game, SlugcatStats.Name playerCharacter, SlugcatStats.Timeline timelinePosition, bool singleRoomWorld, string worldName, Region region, RainWorldGame.SetupValues setupValues)
        {
            orig.Invoke(self, game, playerCharacter, timelinePosition, singleRoomWorld, worldName, region, setupValues);
            if (game != null && game.world != null)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoCreatureGateChallenge creatureGate)
                    {
                        creatureGate.Gate(game.Players[0].Room.name);
                    }
                }
            }
        }

        public static void WorldLoader_EnterRegion(On.WorldLoader.orig_ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues orig, WorldLoader self, RainWorldGame game, SlugcatStats.Name playerCharacter, SlugcatStats.Timeline timelinePosition, bool singleRoomWorld, string worldName, Region region, RainWorldGame.SetupValues setupValues)
        {
            orig.Invoke(self, game, playerCharacter, timelinePosition, singleRoomWorld, worldName, region, setupValues);
            if (game != null && game.world != null)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoEnterRegionChallenge enterRegion)
                    {
                        enterRegion.Entered(worldName);
                    }
                }
            }
        }

        public static void WorldLoader_AllRegionsExcept(On.WorldLoader.orig_ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues orig, WorldLoader self, RainWorldGame game, SlugcatStats.Name playerCharacter, SlugcatStats.Timeline timelinePosition, bool singleRoomWorld, string worldName, Region region, RainWorldGame.SetupValues setupValues)
        {
            orig.Invoke(self, game, playerCharacter, timelinePosition, singleRoomWorld, worldName, region, setupValues);
            // This one doesn't check if the game or world is null because I want to count the starting region (popular demand as well)
            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoAllRegionsExceptChallenge allExcept)
                {
                    allExcept.Entered(worldName);
                }
            }
        }

        public static void WorldLoader_NoRegion(On.WorldLoader.orig_ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues orig, WorldLoader self, RainWorldGame game, SlugcatStats.Name playerCharacter, SlugcatStats.Timeline timelinePosition, bool singleRoomWorld, string worldName, Region region, RainWorldGame.SetupValues setupValues)
        {
            orig.Invoke(self, game, playerCharacter, timelinePosition, singleRoomWorld, worldName, region, setupValues);
            if (game != null && game.world != null)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoNoRegionChallenge noRegion)
                    {
                        noRegion.Entered(worldName);
                    }
                }
            }
        }

        public static void Player_GrabUpdate(On.Player.orig_GrabUpdate orig, Player self, bool eu)
        {
            orig.Invoke(self, eu);

            // This is so if you dual wield something it doesnt add time twice
            ItemType ignore = null;
            for (int i = 0; i < self.grasps.Length; i++)
            {
                if (self.grasps[i] != null)
                {
                    ItemType heldType = self.grasps[i].grabbed.abstractPhysicalObject.type;
                    if (heldType != ignore)
                    {
                        ignore = heldType;
                        BingoData.heldItemsTime[(int)heldType]++;
                        //
                    }
                }
            }
        }

        public static void Player_ThrowObject(On.Player.orig_ThrowObject orig, Player self, int grasp, bool eu)
        {
            if (self.grasps[grasp] != null && self.grasps[grasp].grabbed is not Creature)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoDontUseItemChallenge c && !c.isFood && self.grasps[grasp].grabbed is Weapon)
                    {
                        c.Used(self.grasps[grasp].grabbed.abstractPhysicalObject.type);
                    }
                }
            }

            orig.Invoke(self, grasp, eu);
        }

        public static void SLOracleWakeUpProcedure_NextPhase(On.SLOracleWakeUpProcedure.orig_NextPhase orig, SLOracleWakeUpProcedure self)
        {
            orig.Invoke(self);

            if (self.phase == SLOracleWakeUpProcedure.Phase.GoToRoom || self.phase == SLOracleWakeUpProcedure.Phase.GoToAboveOracle)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoGreenNeuronChallenge c && c.moon.Value)
                    {
                        c.Delivered();
                    }
                }
            }
        }

        public static void SaveState_ctor(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(
                x => x.MatchCallOrCallvirt<RainWorld>("get_ExpeditionMode")
                ) && c.TryGotoNext(
                x => x.MatchCallOrCallvirt<SLOrcacleState>("set_neuronsLeft")
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<int, SaveState, int>>((orig, self) =>
                {
                    for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                    {
                        if (ExpeditionData.challengeList[j] is BingoGreenNeuronChallenge c && c.moon.Value && c.CompletedByAny())
                        {
                            // MoonDead is not really necessary but it's a good piece of information to expose
                            BingoData.MoonDead = false;
                        }
                    }
                    if (BingoData.MoonDead) orig = 0;

                    return orig;
                });
            }
            else Plugin.logger.LogError("Uh oh, SaveState_ctor il fucked up " + il);
        }

        public static void SeedCob_HitByWeapon(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(
                x => x.MatchCallOrCallvirt<ModManager>("get_DLCShared")
                ))
            {
                c.Emit(OpCodes.Ldarg_1);
                c.EmitDelegate<Action<Weapon>>((weapon) =>
                {
                    if (weapon.thrownBy is not Player p) return;

                    if (p.slugcatStats.name == MoreSlugcatsEnums.SlugcatStatsName.Spear && weapon is Spear spear)
                    {
                        if (!spear.IsNeedle || (spear.spearmasterNeedle_fadecounter != spear.spearmasterNeedle_fadecounter_max)) return;
                    }

                    foreach (var challenge in ExpeditionData.challengeList)
                    {
                        if (challenge is BingoPopcornChallenge c)
                        {
                            c.Pop();
                        }
                    }
                });
            }
            else Plugin.logger.LogError("Uh oh, SeedCob_HitByWeapon il fucked up " + il);
        }

        //public static void ScavengerAI_RecognizeCreatureAcceptingGift1(ILContext il)
        //{
        //    ILCursor c = new(il);
        //
        //    if (c.TryGotoNext(MoveType.After,
        //        x => x.MatchCallOrCallvirt<ItemTracker>("RepresentationForObject"),
        //        x => x.MatchStfld<DiscomfortTracker>("uncomfortableItem")
        //        ))
        //    {
        //        c.Emit(OpCodes.Ldarg_0);
        //        c.Emit(OpCodes.Ldarg, 4);
        //        c.EmitDelegate<Action<ScavengerAI, PhysicalObject>>((self, item) =>
        //        {
        //            if (self.tradeSpot != null && (self.creature.abstractAI as ScavengerAbstractAI).squad.missionType == ScavengerAbstractAI.ScavengerSquad.MissionID.Trade)
        //            {
        //                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
        //                {
        //                    if (ExpeditionData.challengeList[j] is BingoTradeChallenge c)
        //                    {
        //                        c.Traded(self.CollectScore(item, false), item.abstractPhysicalObject.ID);
        //                    }
        //                }
        //            }
        //        });
        //    }
        //    else Plugin.logger.LogError("Uh oh, ScavengerAI_RecognizeCreatureAcceptingGift1 il fucked up " + il);
        //}

        public static void ScavengerAI_RecognizeCreatureAcceptingGiftNeedles(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt<ItemTracker>("RepresentationForObject"),
                x => x.MatchStfld<DiscomfortTracker>("uncomfortableItem")
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldarg, 4);
                c.EmitDelegate<Action<ScavengerAI, PhysicalObject>>((self, item) =>
                {
                    
                    if (item is Spear spear && spear.IsNeedle)
                    {
                        for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                        {
                            if (ExpeditionData.challengeList[j] is BingoNoNeedleTradingChallenge c)
                            {
                                c.Traded();
                            }
                        }
                    }
                });
            }
            else Plugin.logger.LogError("Uh oh, ScavengerAI_RecognizeCreatureAcceptingGiftNeedles il fucked up " + il);
        }

        public static void ScavengerAI_RecognizeCreatureAcceptingGift2(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt<ItemTracker>("RepresentationForObject"),
                x => x.MatchStfld<DiscomfortTracker>("uncomfortableItem")
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldarg, 4);
                c.Emit(OpCodes.Ldloc, 15);
                c.EmitDelegate<Action<ScavengerAI, PhysicalObject, int>>((self, item, i) =>
                {
                    if (self.tradeSpot != null && (self.creature.abstractAI as ScavengerAbstractAI).squad.missionType == ScavengerAbstractAI.ScavengerSquad.MissionID.Trade)
                    {
                        for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                        {
                            if (ExpeditionData.challengeList[j] is BingoTradeTradedChallenge t)
                            {
                                EntityID givenItem = self.scavenger.room.socialEventRecognizer.ownedItemsOnGround[i].item.abstractPhysicalObject.ID;
                                EntityID receivedItem = item.abstractPhysicalObject.ID;
                                string room = self.creature.Room.name;



                                if (givenItem != receivedItem && !t.traderItems.ContainsKey(givenItem)) t.traderItems.Add(givenItem, room);
                                //t.Traded(receivedItem, room);
                            }
                        }
                    }
                });
            }
            else Plugin.logger.LogError("Uh oh, ScavengerAI_RecognizeCreatureAcceptingGift2 il fucked up " + il);
        }

        public static void PlayerTracker_Update(On.ScavengerOutpost.PlayerTracker.orig_Update orig, ScavengerOutpost.PlayerTracker self)
        {
            orig.Invoke(self);

            for (int j = 0; j < self.player.realizedCreature.grasps.Length; j++)
            {
                if (self.player.realizedCreature.grasps[j] != null)
                {
                    int k = 0;
                    while (k < self.outpost.outPostProperty.Count)
                    {
                        if (self.player.realizedCreature.grasps[j].grabbed.abstractPhysicalObject.ID == self.outpost.outPostProperty[k].ID)
                        {
                            bool gruh = false;
                            for (int w = 0; w < ExpeditionData.challengeList.Count; w++)
                            {
                                if (ExpeditionData.challengeList[w] is BingoStealChallenge c)
                                {
                                    
                                    c.Stoled(self.outpost.outPostProperty[k], true);
                                    gruh = true;
                                }
                            }
                            if (gruh) break;
                        }
                        k++;
                    }
                }
            }
        }

        public static void SocialEventRecognizer_Theft(On.SocialEventRecognizer.orig_Theft orig, SocialEventRecognizer self, PhysicalObject item, Creature theif, Creature victim)
        {
            orig.Invoke(self, item, theif, victim);

            if (theif is Player && victim is Scavenger)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoStealChallenge c)
                    {
                        c.Stoled(item.abstractPhysicalObject, false);
                    }
                }
            }
        }

        //public static void Bee_Attach(On.SporePlant.Bee.orig_Attach orig, SporePlant.Bee self, BodyChunk chunk)
        //{
        //    if (BingoData.BingoMode && chunk.owner is Creature victim && !victim.dead)
        //    {
        //        ReportHit(self.owner.abstractPhysicalObject.type, victim, self.owner.abstractPhysicalObject.ID);
        //    }
        //
        //    orig.Invoke(self, chunk);
        //}

        public static void SporeCloud_Update(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(
                x => x.MatchCallOrCallvirt<AbstractCreature>("get_realizedCreature")
                ))
            {
                c.Index += 3;
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc_3);
                c.EmitDelegate<Action<SporeCloud, int>>((self, i) =>
                {
                    if (!self.slatedForDeletetion && BingoData.BingoMode && ownerOfUAD.ContainsKey(self) && self != null && self.killTag != null && self.killTag.creatureTemplate.type == CreatureType.Slugcat)
                    {
                        Creature victim = self.room.abstractRoom.creatures[i].realizedCreature;
                        if (victim != null && !victim.dead && Custom.DistLess(self.pos, victim.mainBodyChunk.pos, self.rad + victim.mainBodyChunk.rad + 20f))
                        {
                            // Make string
                            ReportHit("PuffBall", victim, ownerOfUAD[self]);
                        }
                    }
                });
            }
            else Plugin.logger.LogError("Uh oh, SporeCloud_Update il fucked up " + il);
        }

        public static void JellyFish_Collide(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(
                x => x.MatchLdarg(1),
                x => x.MatchIsinst<BigEel>()
                ))
            {
                c.Index++;
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldarg_1);
                c.EmitDelegate<Action<JellyFish, PhysicalObject>>((self, obj) =>
                {
                    if (BingoData.BingoMode && self.thrownBy is Player)
                    {
                        ReportHit(self.abstractPhysicalObject.type.value, obj as Creature, self.abstractPhysicalObject.ID, false);
                    }
                });
            }
            else Plugin.logger.LogError("JellyFish_Collide failed! " + il);
        }

        public static bool MiscProgressionData_GetTokenCollected(On.PlayerProgression.MiscProgressionData.orig_GetTokenCollected_string_bool orig, PlayerProgression.MiscProgressionData self, string tokenString, bool sandbox)
        {
            
            if (BingoData.challengeTokens.Contains(tokenString)) return false;
            return orig.Invoke(self, tokenString, sandbox);
        }

        public static bool MiscProgressionData_GetTokenCollected_SlugcatUnlockID(On.PlayerProgression.MiscProgressionData.orig_GetTokenCollected_SlugcatUnlockID orig, PlayerProgression.MiscProgressionData self, MultiplayerUnlocks.SlugcatUnlockID classToken)
        {
            
            if (BingoData.challengeTokens.Contains(classToken.value)) return false;
            return orig.Invoke(self, classToken);
        }

        public static bool MiscProgressionData_GetTokenCollected_SafariUnlockID(On.PlayerProgression.MiscProgressionData.orig_GetTokenCollected_SafariUnlockID orig, PlayerProgression.MiscProgressionData self, MultiplayerUnlocks.SafariUnlockID safariToken)
        {
            
            if (BingoData.challengeTokens.Contains(safariToken.value + "-safari")) return false;
            return orig.Invoke(self, safariToken);
        }

        public static bool MiscProgressionData_GetBroadcastListened(On.PlayerProgression.MiscProgressionData.orig_GetBroadcastListened orig, PlayerProgression.MiscProgressionData self, ChatlogData.ChatlogID chatlog)
        {
            if (BingoData.challengeTokens.Contains(chatlog.value)) return false;
            return orig.Invoke(self, chatlog);
        }

        public static void CollectToken_Pop(On.CollectToken.orig_Pop orig, CollectToken self, Player player)
        {
            if (self.expand > 0f)
            {
                return;
            }

            if (self.placedObj.data is CollectToken.CollectTokenData d)
            {
                string tokenId = d.tokenString + (d.isRed ? "-safari" : "");

                if (BingoData.challengeTokens.Contains(tokenId) &&
                    ExpeditionData.challengeList.Any(x =>
                        (x is BingoUnlockChallenge b1 && b1.unlock.Value == tokenId) ||
                            (x is BingoBroadcastChallenge b2 && b2.chatlog.Value == tokenId)))
                {
                    foreach (Challenge ch in ExpeditionData.challengeList)
                    {
                        if (ch is BingoUnlockChallenge b &&
                            !b.completed &&
                            !b.TeamsCompleted[SteamTest.team] &&
                            !b.revealed &&
                            !b.hidden &&
                            b.unlock.Value == tokenId)
                        {
                            ch.CompleteChallenge();
                        }
                        else if (ch is BingoBroadcastChallenge br &&
                                 !br.completed &&
                                 !br.TeamsCompleted[SteamTest.team] &&
                                 !br.revealed &&
                                 !br.hidden &&
                                 br.chatlog.Value == tokenId)
                        {
                            ch.CompleteChallenge();
                        }
                    }
                    self.expandAroundPlayer = player;
                    self.expand = 0.01f;
                    self.room.PlaySound(SoundID.Token_Collect, self.pos);

                    for (int i = 0; i < 10; i++)
                    {
                        self.room.AddObject(new CollectToken.TokenSpark(
                            self.pos + Custom.RNV() * 2f,
                            Custom.RNV() * 11f * UnityEngine.Random.value + Custom.DirVec(player.mainBodyChunk.pos, self.pos) * 5f * UnityEngine.Random.value,
                            self.GoldCol(self.glitch),
                            self.underWaterMode));
                    }

                    return;
                }
            }

            orig.Invoke(self, player);
        }

        public delegate Color orig_TokenColor(CollectToken self);
        public static Color CollectToken_TokenColor_get(orig_TokenColor orig, CollectToken self)
        {
            if (self.placedObj.data is CollectToken.CollectTokenData d && BingoData.challengeTokens.Contains(d.tokenString + (d.isRed ? "-safari" : ""))) return Color.white;//Color.Lerp(orig.Invoke(self), Color.white, 0.8f);
            else return orig.Invoke(self);
        }

        public delegate bool orig_PlaceKarmaFlower(Player self);
        // false prevents a death flower from spawning
        public static bool Player_PlaceKarmaFlower_get(orig_PlaceKarmaFlower orig, Player self)
        {
            //if (ExpeditionData.challengeList.Any(x => x is BingoKarmaFlowerChallenge ||
            //            (x is BingoItemHoardChallenge c && c.target.Value == "KarmaFlower" && (c.TeamsCompleted[SteamTest.team] || c.completed)) ||
            //            (x is BingoCraftChallenge c2 && c2.craftee.Value == "KarmaFlower" && (c2.TeamsCompleted[SteamTest.team] || c2.completed)) ||
            //            (x is BingoStealChallenge c3 && c3.subject.Value == "KarmaFlower" && (c3.TeamsCompleted[SteamTest.team] || c3.completed))))
            //{
            //    return orig.Invoke(self);
            //}
            if (BingoData.BingoMode)
            {
                return false;
            }
            else
            {
                return orig(self);
            }
        }

        public static void Room_LoadedUnlock(ILContext il)
        {
            ILCursor c = new(il);
            if (c.TryGotoNext(
                x => x.MatchLdfld("PlacedObject", "active")
                ) &&
                c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld("ModManager", "Expedition")
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, 29);
                c.EmitDelegate<Func<bool, Room, int, bool>>((orig, self, i) =>
                {
                    if (self.roomSettings.placedObjects[i].data is CollectToken.CollectTokenData c && BingoData.challengeTokens.Contains(c.tokenString + (c.isRed ? "-safari" : ""))) orig = false;
                    return orig;
                });
            }
            else Plugin.logger.LogError("Challenge room loaded threw!!! " + il);
        }
        
        public static void Room_LoadedGreenNeuron(ILContext il)
        {
            ILCursor b = new(il);
            if (b.TryGotoNext(
                x => x.MatchLdsfld("Expedition.ExpeditionData", "startingDen")
                ) &&
                b.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt<WorldCoordinate>(".ctor")
                ))
            {
                b.Emit(OpCodes.Ldarg_0);
                b.Emit(OpCodes.Ldloc, 137);
                b.EmitDelegate<Action<Room, WorldCoordinate>>((room, pos) =>
                {
                    AbstractWorldEntity existingFucker = room.abstractRoom.entities.FirstOrDefault(x => x is AbstractPhysicalObject o && o.type == ItemType.NSHSwarmer);
                    if (existingFucker != null)
                    {
                        room.abstractRoom.RemoveEntity(existingFucker);
                    }

                    AbstractPhysicalObject startItem = new(room.world, ItemType.NSHSwarmer, null, new WorldCoordinate(room.abstractRoom.index, room.shelterDoor.playerSpawnPos.x, room.shelterDoor.playerSpawnPos.y, 0), room.game.GetNewID());
                    room.abstractRoom.entities.Add(startItem);
                    startItem.Realize();
                });
            }
            else Plugin.logger.LogError("Room_LoadedGreenNeuron IL ERROR " + il);
        }
        
        public static void Room_LoadedHalcyon(ILContext il)
        {
            ILCursor c = new(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdloc(88),
                x => x.MatchCallOrCallvirt(typeof(List<PlacedObject>).GetMethod("get_Item")),
                x => x.MatchLdfld<PlacedObject>("active")
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, 88);
                c.EmitDelegate<Func<bool, Room, int, bool>>((orig, self, i) =>
                {
                    PlacedObject obj = self.roomSettings.placedObjects[i];
                    if (obj.type == PlacedObject.Type.UniqueDataPearl &&
                        self.game.session is StoryGameSession session && !session.saveState.ItemConsumed(self.world, false, self.abstractRoom.index, i) &&
                        (obj.data as PlacedObject.DataPearlData).pearlType == MoreSlugcatsEnums.DataPearlType.RM)
                    {
                        return false;
                    }
                    return orig;
                });
            }
            else Plugin.logger.LogError("Room_LoadedHalcyon 1 FAILURE " + il);

            ILCursor b = new(il);
            if (b.TryGotoNext(
                x => x.MatchLdsfld("Expedition.ExpeditionData", "startingDen")
                ) &&
                b.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt<WorldCoordinate>(".ctor")
                ))
            {
                b.Emit(OpCodes.Ldarg_0);
                b.Emit(OpCodes.Ldloc, 137);
                b.EmitDelegate<Action<Room, WorldCoordinate>>((room, pos) =>
                {
                    AbstractWorldEntity existingFucker = room.abstractRoom.entities.FirstOrDefault(x => x is AbstractPhysicalObject o && o.type == MSCItemType.HalcyonPearl);
                    if (existingFucker != null)
                    {
                        room.abstractRoom.RemoveEntity(existingFucker);
                    }

                    AbstractPhysicalObject startItem = new DataPearl.AbstractDataPearl(room.world, MSCItemType.HalcyonPearl, null, new WorldCoordinate(room.abstractRoom.index, room.shelterDoor.playerSpawnPos.x, room.shelterDoor.playerSpawnPos.y, 0), room.game.GetNewID(), -1, -1, null, MoreSlugcatsEnums.DataPearlType.RM);
                    room.abstractRoom.entities.Add(startItem);
                    startItem.Realize();
                });
            }
            else Plugin.logger.LogError("Room_LoadedHalcyon 2 FAILURE " + il);
        }

        public static void Player_ObjectEaten(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
        {

            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoEatChallenge c)
                {
                    c.FoodEated(edible, self);
                }
            }
            // Invoke after so player malnourishment is correct
            orig.Invoke(self, edible);
        }

        public static void Player_ObjectEatenSeed(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
        {
            orig.Invoke(self, edible);

            if (edible is not PhysicalObject p || p.abstractPhysicalObject.type != DLCItemType.Seed) return;
            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoSaintPopcornChallenge c)
                {
                    c.Consume();
                }
            }
        }

        public static void Player_ObjectEaten2(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
        {
            orig.Invoke(self, edible);

            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoDontUseItemChallenge g && g.isFood)
                {
                    g.Eated(edible);
                }
            }
        }

        public static void Player_ObjectEatenKarmaFlower(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
        {
            orig.Invoke(self, edible);

            if (edible is not KarmaFlower) return;
            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoKarmaFlowerChallenge c)
                {
                    c.Karmad();
                }
            }
        }

        public static void Player_SlugcatGrabCloak(On.Player.orig_SlugcatGrab orig, Player self, PhysicalObject obj, int graspUsed)
        {
            orig.Invoke(self, obj, graspUsed);

            if (obj.abstractPhysicalObject.type == MSCItemType.MoonCloak && self.room.abstractRoom.name.ToLowerInvariant() == "ms_farside")
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoMoonCloakChallenge c && !c.deliver.Value)
                    {
                        c.Cloak();
                    }
                }
            }
        }

        //For debugging moonCloak and Timeline, make sure to uncomment the BingoMoonCloak hooks so its used
        public static void SaveState_ctorCloak(On.SaveState.orig_ctor orig, SaveState self, SlugcatStats.Name saveStateNumber, PlayerProgression progression)
        {
            progression.miscProgressionData.cloakTimelinePosition = null;

            orig.Invoke(self, saveStateNumber, progression);

            self.miscWorldSaveData.moonGivenRobe = false;

        }

        public static void Room_LoadedMoonCloak(ILContext il)
        {
            ILCursor b = new(il);
            if (b.TryGotoNext(
                x => x.MatchLdsfld("Expedition.ExpeditionData", "startingDen")
                ) &&
                b.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt<WorldCoordinate>(".ctor")
                ))
            {
                b.Emit(OpCodes.Ldarg_0);
                b.Emit(OpCodes.Ldloc, 137);
                b.EmitDelegate<Action<Room, WorldCoordinate>>((room, pos) =>
                {
                    AbstractWorldEntity existingFucker = room.abstractRoom.entities.FirstOrDefault(x => x is AbstractPhysicalObject o && o.type == MSCItemType.MoonCloak);
                    if (existingFucker != null)
                    {
                        room.abstractRoom.RemoveEntity(existingFucker);
                    }

                    AbstractPhysicalObject startItem = new AbstractConsumable(room.world, MSCItemType.MoonCloak, null, new WorldCoordinate(room.abstractRoom.index, room.shelterDoor.playerSpawnPos.x, room.shelterDoor.playerSpawnPos.y, 0), room.game.GetNewID(), -1, -1, null);
                    room.abstractRoom.entities.Add(startItem);
                    startItem.Realize();
                });
            }
            else Plugin.logger.LogError("Room_MoonCloak IL FAILURE " + il);
        }

        public static void SLOracleBehavior_GrabCloak(On.SLOracleBehaviorHasMark.MoonConversation.orig_AddEvents orig, SLOracleBehaviorHasMark.MoonConversation self)
        {
            orig.Invoke(self);

            if (self.describeItem == MoreSlugcatsEnums.MiscItemType.MoonCloak)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoMoonCloakChallenge c && c.deliver.Value)
                    {
                        c.Delivered();
                    }
                }
            }
        }

        public static void Player_SlugslamIL(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.After,
                        x => x.MatchLdcI4(9),
                        x => x.MatchLdloca(out _),
                        x => x.MatchCall(typeof(float).GetMethod("ToString", Type.EmptyTypes)),
                        x => x.MatchStelemRef(),
                        x => x.MatchCall(typeof(RWCustom.Custom).GetMethod("Log", new[] { typeof(string[]) }))
                ))
            {
                c.Emit(OpCodes.Ldloc, 7);
                c.Emit(OpCodes.Ldarg, 1);
                c.EmitDelegate<Action<float, PhysicalObject>>((num, crit) =>
                {
                    if (num > 0.25f)
                    {
                        for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                        {
                            if (ExpeditionData.challengeList[j] is BingoGourmandCrushChallenge c)
                            {
                                c.Crush((crit as Creature).abstractCreature.ID);
                            }
                        }
                    }
                });
            }
            else Plugin.logger.LogError("Player_SlugslamIL FAILURE " + il);
        }

        public static void SSOracleBehavior_SeePlayer(On.SSOracleBehavior.orig_SeePlayer orig, SSOracleBehavior self)
        {
            orig.Invoke(self);

            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoIteratorChallenge c)
                {
                    // These fucking rw devs man
                    if (self.oracle.ID == MoreSlugcatsEnums.OracleID.DM)
                    {
                        c.MeetMoon();
                    }
                    else
                    {
                        c.MeetPebbles();
                    }
                }
            }
        }

        public static void RMOracleBehavior_Update(On.MoreSlugcats.SSOracleRotBehavior.orig_Update orig, SSOracleRotBehavior self, bool eu)
        {
            orig.Invoke(self, eu);

            if (self.hasNoticedPlayer)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoIteratorChallenge c && !c.moon.Value)
                    {
                        c.MeetPebbles();
                    }
                }
            }
        }

        public static void SLOracleBehaviorHasMark_InitateConversation(On.SLOracleBehaviorHasMark.orig_InitateConversation orig, SLOracleBehaviorHasMark self)
        {
            orig.Invoke(self);

            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoIteratorChallenge c && c.moon.Value)
                {
                    c.MeetMoon();
                }
            }
        }

        public static void BigNeedleWorm_Swish(On.BigNeedleWorm.orig_Swish orig,  BigNeedleWorm self)
        {
            orig.Invoke(self);

            if (self.impaleChunk != null && self.impaleChunk.owner is Player)
            {
                return;
            }
            if (self.BigAI.focusCreature.representedCreature.realizedCreature is Player p && self.swishCounter == 0f)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoDodgeNootChallenge c)
                    {
                        c.Dodged();
                    }
                }
            }
        }

        public static void LizardTongue_Update(On.LizardTongue.orig_Update orig, LizardTongue self)
        {
            orig.Invoke(self);
            if (self.state == LizardTongue.State.Attatched && self.attached.owner is Player)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is BingoLickChallenge c)
                    {
                        c.Licked(self.lizard);
                    }
                }
            }
        }

        #region watcher
        public static void Watcher_WarpPoint_ChangeState_EnterRegion(On.Watcher.WarpPoint.orig_ChangeState orig, WarpPoint self, WarpPoint.State state)
        {
            orig(self, state);
            // This is the state transition, so understand that we are not yet in the room, so destination region is appropriate
            if (state == WarpPoint.State.SpawnItems)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is WatcherBingoEnterRegionChallenge wEnterRegion)
                    {
                        wEnterRegion.Entered(self.room.world.name.ToUpperInvariant());
                    }
                }
            }
        }

        public static void Watcher_WarpPoint_ChangeState_NoRegion(On.Watcher.WarpPoint.orig_ChangeState orig, WarpPoint self, WarpPoint.State state)
        {
            orig(self, state);
            if (state == WarpPoint.State.SpawnItems)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is WatcherBingoNoRegionChallenge wNoRegion)
                    {
                        wNoRegion.Entered(self.room.world.name.ToUpperInvariant());
                    }
                }
            }
        }

        public static void Watcher_WarpPoint_ChangeState_AllRegionsExcept(On.Watcher.WarpPoint.orig_ChangeState orig, WarpPoint self, WarpPoint.State state)
        {
            orig(self, state);
            if (state == WarpPoint.State.SpawnItems)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is WatcherBingoAllRegionsExceptChallenge wAllExcept)
                    {
                        wAllExcept.Entered(self.room.world.name.ToUpperInvariant());
                    }
                }
            }
        }

        // copied from room.loaded
        public static void Watcher_Room_Loaded_AllRegionsExcept(On.Room.orig_Loaded orig, Room self)
        {
            if (self.game == null) return;
            if (self.abstractRoom.firstTimeRealized && ModManager.Expedition && self.game.rainWorld.ExpeditionMode && self.abstractRoom.shelter && self.abstractRoom.name == ExpeditionData.startingDen && self.game.rainWorld.progression.currentSaveState.cycleNumber == 0 && self.game.world.rainCycle.CycleProgression <= 0f)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is WatcherBingoAllRegionsExceptChallenge c)
                    {
                        c.Entered(self.world.name.ToUpperInvariant());
                    }
                }
            }
            orig(self);
        }

        public static void Watcher_WarpPoint_ChangeState_CreaturePortal(On.Watcher.WarpPoint.orig_ChangeState orig, WarpPoint self, WarpPoint.State state)
        {
            // Spawn items happens once the world is loaded so the current room is the one the player has just landed in
            orig(self, state);
            if (state == WarpPoint.State.SpawnItems)
            {
                for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
                {
                    if (ExpeditionData.challengeList[j] is WatcherBingoCreaturePortalChallenge wCreaturePortal)
                    {
                        wCreaturePortal.Entered(self, self.room.game.GetStorySession.pendingWarpPointTransferObjects);
                    }
                }
            }
        }

        public static void Watcher_SpinningTop_StartConversation(On.Watcher.SpinningTop.orig_StartConversation orig, SpinningTop self)
        {
            orig(self);
            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is WatcherBingoSpinningTopChallenge c)
                {
                    for (int i = 0; i < self.room.game.Players.Count; i++)
                    {
                        c.SeeSpin(self.room.world.region.name, (self.room.game.Players[i].realizedCreature as Player).Malnourished);
                    }
                }
            }
        }

        public static void Watcher_Pomegranate_Smash(On.Pomegranate.orig_Smash orig, Pomegranate self)
        {
            orig.Invoke(self);

            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is WatcherBingoOpenMelonsChallenge c)
                {
                    c.Open();
                }
            }
        }

        public static void Watcher_VoidSpawnEgg_Pop(On.VoidSpawnEgg.orig_Pop orig, VoidSpawnEgg self)
        {
            orig.Invoke(self);

            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is WatcherBingoCollectRippleSpawnChallenge c)
                {
                    c.Pop();
                }
            }
        }

        public static void Watcher_PrinceBehavior_InitateConversation(On.Watcher.PrinceBehavior.orig_InitateConversation orig, PrinceBehavior self)
        {
            orig.Invoke(self);

            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is WatcherBingoPrinceChallenge c)
                {
                    c.Meet();
                }
            }
        }

        public static void Watcher_VoidWeaver_DefaultBehavior_StartMonologue(On.Watcher.VoidWeaver.DefaultBehavior.orig_StartMonologue orig, VoidWeaver.DefaultBehavior self)
        {
            orig.Invoke(self);
            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is WatcherBingoWeaverChallenge c && c.region == self.owner.room.abstractRoom.world.name.ToUpperInvariant())
                {
                    c.Meet();
                }
            }
        }

        public static bool Boomerang_HitSomething(On.Boomerang.orig_HitSomething orig, Boomerang self, SharedPhysics.CollisionResult result, bool eu)
        {
            if (BingoData.BingoMode && self.thrownBy is Player && result.obj is Creature victim && !victim.dead)
            {
                ReportHit(self.abstractPhysicalObject.type.value, victim, self.abstractPhysicalObject.ID, false);
            }
            return orig.Invoke(self, result, eu);
        }

        public static void GraffitiBomb_Explode(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchNewobj<GraffitiCloud>(),
                x => x.MatchCallOrCallvirt<Room>("AddObject")
                ))
            {
                c.Index++;
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<GraffitiCloud, GraffitiBomb, GraffitiCloud>>((orig, self) =>
                {
                    if (BingoData.BingoMode && self.thrownBy != null && self.thrownBy.abstractCreature.creatureTemplate.type == CreatureType.Slugcat)
                    {
                        ownerOfUAD[orig] = self.abstractPhysicalObject.ID;
                    }

                    return orig;
                });
            }
            else Plugin.logger.LogError("Uh oh, GraffitiBomb_Explode il fucked up " + il);
        }

        public static void GraffitiCloud_Update(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(
                x => x.MatchCallOrCallvirt<AbstractCreature>("get_realizedCreature")
                ))
            {
                c.Index += 3;
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, 5);
                c.EmitDelegate<Action<GraffitiCloud, int>>((self, i) =>
                {
                    if (!self.slatedForDeletetion && BingoData.BingoMode && ownerOfUAD.ContainsKey(self) && self != null && self.thrownBy != null && self.thrownBy.creatureTemplate.type == CreatureType.Slugcat)
                    {
                        Creature victim = self.room.abstractRoom.creatures[i].realizedCreature;
                        if (victim != null && !victim.dead && Custom.DistLess(self.pos, victim.mainBodyChunk.pos, self.rad + victim.mainBodyChunk.rad + 20f))
                        {
                            ReportHit("GraffitiBomb", victim, ownerOfUAD[self]);
                        }
                    }
                });
            }
            else Plugin.logger.LogError("Uh oh, GraffitiBomb_Update il fucked up " + il);
        }

        public static void Frog_Thrown(On.Watcher.Frog.orig_Thrown orig, Frog self, Creature thrownBy, Vector2 thrownPos, Vector2? firstFrameTraceFromPos, IntVector2 throwDir, float frc, bool eu)
        {
            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoDamageChallenge c && c.weapon.Value == "Frog" && !c.completed && !c.TeamsCompleted[SteamTest.team] && !c.revealed && !c.frogsThrown.Contains(self.abstractCreature.ID.ToString()))
                {
                    c.frogsThrown.Add(self.abstractCreature.ID.ToString());
                }
            }
            orig.Invoke(self, thrownBy, thrownPos, firstFrameTraceFromPos, throwDir, frc, eu);
        }

        public static void Frog_Jump(On.Watcher.Frog.orig_Jump orig, Watcher.Frog self, Vector2 jumpDir, bool ignoreNoJumps)
        {
            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoDamageChallenge c && c.weapon.Value == "Frog" && !c.completed && !c.TeamsCompleted[SteamTest.team] && !c.revealed && c.frogsThrown.Contains(self.abstractCreature.ID.ToString()))
                {
                    c.frogsThrown.Remove(self.abstractCreature.ID.ToString());
                }
            }
            orig.Invoke(self, jumpDir, ignoreNoJumps);
        }

        public static void Frog_Attach(On.Watcher.Frog.orig_Attach orig, Frog self, BodyChunk chunk, bool suckedFood = false)
        {
            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is BingoDamageChallenge c && c.weapon.Value == "Frog" && chunk.owner is Creature victim && !victim.dead && !c.completed && !c.TeamsCompleted[SteamTest.team] && !c.revealed && c.frogsThrown.Contains(self.abstractCreature.ID.ToString()))
                {
                    ReportHit(self.abstractCreature.creatureTemplate.name, victim, self.abstractPhysicalObject.ID, false);
                    c.frogsThrown.Remove(self.abstractCreature.ID.ToString());
                }
            }
            orig.Invoke(self, chunk, suckedFood);
        }

        public static void Watcher_Room_Loaded(On.Room.orig_Loaded orig, Room self)
        {
            orig(self);
            if (self.game == null) return;
            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is WatcherBingoWeaverChallenge c && !c.completed && !c.TeamsCompleted[SteamTest.team] && !c.revealed && c.room.Value == self.abstractRoom.name.ToUpperInvariant())
                {
                    Vector2 pos = new Vector2(0, 0);

                    foreach (var thing in self.roomSettings.placedObjects)
                    {
                        if (thing.type == PlacedObject.Type.DynamicWarpTarget)
                        {
                            pos = thing.pos;
                        }
                    }
                    PlacedObject po = new PlacedObject(PlacedObject.Type.WarpPoint, null);

                    WarpPoint.WarpPointData warpPointData = new WarpPoint.WarpPointData(po);
                    po.data = warpPointData;
                    po.pos = pos;

                    (warpPointData).cycleSpawnedOn = self.world.game.GetStorySession.saveState.cycleNumber - 6;
                    (warpPointData).cycleExpiry = 5;
                    (warpPointData).destRoom = "NARNIA";
                    (warpPointData).destTimeline = self.world.game.TimelinePoint;

                    WarpPoint warpPoint = new WarpPoint(self, po);
                    self.AddObject(warpPoint);
                }
            }
        }

        public static void Watcher_ShelterDoor_Close(On.ShelterDoor.orig_Close orig, ShelterDoor self)
        {
            orig.Invoke(self);

            if (self.Broken || self.closedFac != 0f) return;
            int grubbies = 0;

            foreach (var entity in self.room.abstractRoom.entities)
            {
                if (entity is AbstractCreature c && c.creatureTemplate.type == WatcherEnums.CreatureTemplateType.MothGrub)
                {
                    grubbies++;
                }
            }

            if (grubbies == 0) return;
            for (int j = 0; j < ExpeditionData.challengeList.Count; j++)
            {
                if (ExpeditionData.challengeList[j] is WatcherBingoHatchMothGrubChallenge c)
                {
                    for (int e = 0; e < grubbies; e++)
                    {
                        c.Hatch();
                    }
                }
            }
        }

        #endregion
    }
}
