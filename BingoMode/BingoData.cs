using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Expedition;
using Menu;
using RWCustom;
using ItemType = AbstractPhysicalObject.AbstractObjectType;

namespace BingoMode
{
    using System.IO;
    using System.Text.RegularExpressions;
    using BingoChallenges;
    using BingoMode.DiscordSDK;
    using BingoSteamworks;
    using MoreSlugcats;
    using Steamworks;
    using UnityEngine;
    using Watcher;

    public static class BingoData
    {
        public static bool BingoMode;
        public static bool MultiplayerGame;
        public static Dictionary<SlugcatStats.Name, BingoSaveData> BingoSaves = []; // slug and board size
        public static List<Challenge> availableBingoChallenges;
        public static List<string> challengeTokens = [];
        public static List<string>[] possibleTokens = new List<string>[5];
        public static int[] heldItemsTime;
        public static List<string> appliedChallenges = [];
        // This prevents the same creatures being hit by the same sources multiple times
        public static Dictionary<Creature, List<EntityID>> blacklist = [];
        public static Dictionary<EntityID, List<string>> hitTimeline = [];
        public static ExpeditionMenu globalMenu;
        public static LobbySettings globalSettings = new LobbySettings();
        public static string BingoDen = "random";
        public static string normalBingoBoard;
        public static List<int> TeamsInBingo = [0];
        public static bool SpectatorMode = false;
        public static bool CreateKarmaFlower = false;
        public static Dictionary<string, List<string>> pinnableCreatureRegions;
        public static int RandomStartingSeed = -1;
        public static Dictionary<SlugcatStats.Name, List<string>> bannedChallenges = [];

        private static bool? _moonDeadOverride;

        public static bool MoonDead
        {
            get => _moonDeadOverride ?? ExpeditionData.challengeList.Any(x => x is BingoGreenNeuronChallenge c && c.moon.Value);
            set => _moonDeadOverride = value;
        }

        public static void ResetMoonDeadOverride()
        {
            _moonDeadOverride = null;
        }

        public enum BingoGameMode
        {
            Bingo,
            Lockout,
            Blackout,
            LockoutNoTies
        }

        public class BingoSaveData
        {
            public int size;
            public SteamNetworkingIdentity hostID;
            public bool isHost;
            public string playerWhiteList;
            public int team;
            public BingoGameMode gamemode;
            public bool showedWin;
            public bool firstCycleSaved;
            public bool passageUsed;
            public string teamsInBingo;
            public bool songPlayed;

            public BingoSaveData(int size, bool showedWin, int team, bool firstCycleSaved, bool passageUsed)
            {
                this.size = size;
                this.showedWin = showedWin;
                this.team = team;
                this.firstCycleSaved = firstCycleSaved;
                this.passageUsed = passageUsed;
            }

            public BingoSaveData(int size, int team, SteamNetworkingIdentity hostID, bool isHost, string playerWhiteList, BingoGameMode gamemode, bool showedWin, bool firstCycleSaved, bool passageUsed, string teamsInBingo, bool songPlayed)
            {
                this.size = size;
                this.team = team;
                this.hostID = hostID;
                this.isHost = isHost;
                this.playerWhiteList = playerWhiteList;
                this.gamemode = gamemode;
                this.showedWin = showedWin;
                this.firstCycleSaved = firstCycleSaved;
                this.passageUsed = passageUsed;
                this.teamsInBingo = teamsInBingo;
                this.songPlayed = songPlayed;
            }
        }

        public static void SaveChallengeBlacklistFor(SlugcatStats.Name slug)
        {
            TryGenerateDefaultBlacklistFor(slug);

            string text = string.Join(";", bannedChallenges[slug]);

            File.WriteAllText(Application.persistentDataPath +
                Path.DirectorySeparatorChar.ToString() +
                "Bingo" +
                Path.DirectorySeparatorChar.ToString() +
                "blacklist-" +
                slug.value +
                ".txt",
                text);
        }

        public static void LoadAllBannedChallengeLists(SlugcatStats.Name slug)
        {
            try
            {
                string path = Application.persistentDataPath +
                Path.DirectorySeparatorChar.ToString() +
                "Bingo" +
                Path.DirectorySeparatorChar.ToString() +
                "blacklist-" +
                slug.value +
                ".txt";

                if (!File.Exists(path))
                {
                    SaveChallengeBlacklistFor(slug);
                    return;
                }

                string data = File.ReadAllText(path);

                bannedChallenges[slug] = string.IsNullOrEmpty(data) ? [] : data.Split(';').ToList();
            }
            catch
            {
                Plugin.logger.LogError("Failed to load banned challenge list for " + slug.value);
                SaveChallengeBlacklistFor(slug);
            }
        }

        private static void TryGenerateDefaultBlacklistFor(SlugcatStats.Name slug)
        {
            if (!bannedChallenges.ContainsKey(slug))
            {
                bannedChallenges[slug] = new List<string>
                {
                    nameof(BingoHellChallenge),
                    nameof(BingoDontKillChallenge),
                    nameof(BingoDontUseItemChallenge),
                    nameof(BingoNoNeedleTradingChallenge),
                    nameof(BingoNoRegionChallenge),
                };
                if (slug == WatcherEnums.SlugcatStatsName.Watcher) bannedChallenges[slug].Add(nameof(WatcherBingoHatchMothGrubChallenge));
            }
        }

        public static bool IsCurrentSaveLockout()
        {
            return BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer) && 
                (BingoSaves[ExpeditionData.slugcatPlayer].gamemode == BingoGameMode.Lockout || BingoSaves[ExpeditionData.slugcatPlayer].gamemode == BingoGameMode.LockoutNoTies);
        }

        public static List<int> TeamsStringToList(string teams)
        {
            List<int> teamsList = [];

            for (int i = 0; i < 8; i++)
            {
                if (teams[i] == '1') teamsList.Add(i);
            }

            return teamsList;
        }

        public static string TeamsListToString(List<int> teams)
        {
            StringBuilder builder = new("00000000");

            for (int i = 0; i < 8; i++)
            {
                if (teams.Contains(i)) builder[i] = '1';
            }

            return builder.ToString();
        }

        public static List<Challenge> GetAdequateChallengeList(SlugcatStats.Name slug)
        {
            List<Challenge> list = [.. availableBingoChallenges];
            list.RemoveAll(x => !x.ValidForThisSlugcat(slug));

            if (slug == WatcherEnums.SlugcatStatsName.Watcher)
            {
                CullIllegalWatcherChallenges(list);
            }
            return list;
        }

        //watchermethod. Doing this here instead of per challenge to keep it more tidy and destructible
        public static void CullIllegalWatcherChallenges(List<Challenge> chals)
        {
            var illegals = new HashSet<Type>
            {
                typeof(BingoPearlDeliveryChallenge),
                typeof(BingoNeuronDeliveryChallenge),
                typeof(BingoDepthsChallenge),
                typeof(BingoEchoChallenge),
                typeof(BingoIteratorChallenge),
                typeof(BingoEnterRegionChallenge),
                typeof(BingoNoRegionChallenge),
                typeof(BingoEchoChallenge),
                typeof(BingoEnterRegionFromChallenge),
                typeof(BingoCreatureGateChallenge),
                typeof(BingoAllRegionsExceptChallenge),
                typeof(BingoTransportChallenge),
            };

            chals.RemoveAll(x => illegals.Contains(x.GetType()));
        }

        public static List<Challenge> GetValidChallengeList(SlugcatStats.Name slug)
        {
            List<Challenge> list = [.. availableBingoChallenges];
            list.RemoveAll(x => !x.ValidForThisSlugcat(slug));
            list.RemoveAll(x => bannedChallenges[slug].Contains(x.GetType().Name));

            if (slug == WatcherEnums.SlugcatStatsName.Watcher)
            {
                CullIllegalWatcherChallenges(list);
            }
            return list;
        }

        public static void InitializeBingo()
        {
            BingoMode = true;
            appliedChallenges = [];
            HookAll(ExpeditionData.challengeList, false);
            HookAll(ExpeditionData.challengeList, true);
            heldItemsTime = new int[ExtEnum<ItemType>.values.Count];
            blacklist = [];
            hitTimeline = [];
            BingoRichPresence.bingoRPTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public static void RedoTokens()
        {
            challengeTokens.Clear();
            foreach (Challenge challenge in ExpeditionData.challengeList)
            {
                if ((challenge is BingoUnlockChallenge c1 &&
                        !c1.TeamsCompleted[SteamTest.team] &&
                        !challengeTokens.Contains(c1.unlock.Value)))
                {
                    challengeTokens.Add(c1.unlock.Value);
                }
                if ((challenge is BingoBroadcastChallenge d1 &&
                        !d1.TeamsCompleted[SteamTest.team] &&
                        !challengeTokens.Contains(d1.chatlog.Value)))
                {
                    challengeTokens.Add(d1.chatlog.Value);
                }
            }
        }

        public static void FinishBingo()
        {
            ExpeditionData.ClearActiveChallengeList();
            BingoMode = false;
            Expedition.Expedition.coreFile.Save(false);
        }

        public static void HookAll(IEnumerable<Challenge> challenges, bool add)
        {
            if (!BingoMode) return;
            // literally what is this syntax
            foreach (BingoChallenge challenge in from challenge in challenges where challenge is BingoChallenge select challenge)
            {
                string name = (challenge as Challenge).ChallengeName();
                //
                if (add && !appliedChallenges.Contains(name))
                {
                    challenge.AddHooks();
                    appliedChallenges.Add(name);
                    //
                }
                else if (!add)
                {
                    challenge.RemoveHooks();
                    //
                }
            }
        }

        public static void FillPossibleTokens(SlugcatStats.Name slug)
        {
            possibleTokens[0] = []; // blue
            possibleTokens[1] = []; // gold
            possibleTokens[2] = []; // red
            possibleTokens[3] = []; // green
            possibleTokens[4] = []; // white
            if (slug == WatcherEnums.SlugcatStatsName.Watcher)
            {
                PopulateWatcherUnlocks();
            }
            else
            {
                foreach (var kvp in Custom.rainWorld.regionBlueTokens)
                {
                    // FUCKING WATCHER DEVS AND regionBlueTokensAccessibility
                    if (ModManager.Watcher && SlugcatStats.SlugcatStoryRegions(WatcherEnums.SlugcatStatsName.Watcher).Contains(kvp.Key.ToUpperInvariant())) continue;
                    for (int n = 0; n < kvp.Value.Count; n++)
                    {
                        if (!Custom.rainWorld.regionBlueTokensAccessibility.ContainsKey(kvp.Key)) continue;
                        if (Custom.rainWorld.regionBlueTokensAccessibility[kvp.Key][n].Contains(slug))
                        {
                            
                            possibleTokens[0].Add(kvp.Value[n].value);
                        }
                    }
                }
                foreach (var kvp in Custom.rainWorld.regionGoldTokens)
                {
                    // FUCKING WATCHER DEVS AND regionGoldTokensAccessibility
                    if (ModManager.Watcher && SlugcatStats.SlugcatStoryRegions(WatcherEnums.SlugcatStatsName.Watcher).Contains(kvp.Key.ToUpperInvariant())) continue;
                    for (int n = 0; n < kvp.Value.Count; n++)
                    {
                        if (!Custom.rainWorld.regionGoldTokensAccessibility.ContainsKey(kvp.Key)) continue;
                        if (kvp.Key.ToLowerInvariant() == "lc" && slug != MoreSlugcatsEnums.SlugcatStatsName.Artificer) continue;
                        if (kvp.Key.ToLowerInvariant() == "cl" && slug != MoreSlugcatsEnums.SlugcatStatsName.Saint) continue;
                        if (kvp.Key.ToLowerInvariant() == "rm" && slug != MoreSlugcatsEnums.SlugcatStatsName.Rivulet) continue;
                        if (Custom.rainWorld.regionGoldTokensAccessibility[kvp.Key][n].Contains(slug))
                        {
                            possibleTokens[1].Add(kvp.Value[n].value);
                        }
                    }
                }
                foreach (var kvp in Custom.rainWorld.regionRedTokens)
                {
                    // FUCKING WATCHER DEVS AND regionRedTokensAccessibility
                    if (ModManager.Watcher && SlugcatStats.SlugcatStoryRegions(WatcherEnums.SlugcatStatsName.Watcher).Contains(kvp.Key.ToUpperInvariant())) continue;
                    if (!Custom.rainWorld.regionRedTokensAccessibility.ContainsKey(kvp.Key)) continue;
                    if (kvp.Key.ToLowerInvariant() == "lc" && slug != MoreSlugcatsEnums.SlugcatStatsName.Artificer) continue;
                    if (kvp.Key.ToLowerInvariant() == "cl" && slug != MoreSlugcatsEnums.SlugcatStatsName.Saint) continue;
                    if (kvp.Key.ToLowerInvariant() == "rm" && slug != MoreSlugcatsEnums.SlugcatStatsName.Rivulet) continue;
                    for (int n = 0; n < kvp.Value.Count; n++)
                    {
                        if (Custom.rainWorld.regionRedTokensAccessibility[kvp.Key][n].Contains(slug) && ChallengeUtils.GetCorrectListForChallenge("regionsreal", true).Contains(kvp.Key.ToUpperInvariant()))
                        {
                            possibleTokens[2].Add(kvp.Value[n].value + "-safari");
                        }
                    }
                }
                // Painfully hardcoded because greentokenaccessiblity sucks ass
                if (SlugcatStats.IsSlugcatFromMSC(slug))
                {
                    foreach (var kvp in Custom.rainWorld.regionGreenTokens)
                    {
                        for (int n = 0; n < kvp.Value.Count; n++)
                        {
                            if ((slug == MoreSlugcatsEnums.SlugcatStatsName.Rivulet && kvp.Key.ToLowerInvariant() == "ms") || 
                                (slug == MoreSlugcatsEnums.SlugcatStatsName.Gourmand && kvp.Key.ToLowerInvariant() == "oe") || 
                                (slug == MoreSlugcatsEnums.SlugcatStatsName.Saint && kvp.Key.ToLowerInvariant() == "cl") ||
                                (slug == MoreSlugcatsEnums.SlugcatStatsName.Artificer && kvp.Key.ToLowerInvariant() == "lc") ||
                                (slug == MoreSlugcatsEnums.SlugcatStatsName.Spear && kvp.Key.ToLowerInvariant() == "dm"))
                            {
                            
                                possibleTokens[3].Add(kvp.Value[n].value);
                            }
                        }
                    }
                }

                if (slug == MoreSlugcatsEnums.SlugcatStatsName.Spear)
                {
                    foreach (var kvp in Custom.rainWorld.regionGreyTokens)
                    {
                        for (int n = 0; n < kvp.Value.Count; n++)
                        {
                            if (!kvp.Value[n].value.ToLowerInvariant().Contains("broadcast")) {
                                possibleTokens[4].Add(kvp.Value[n].value);
                            }
                        }
                    }
                }
            }
        }

        public static void PopulateWatcherUnlocks()
        {
            var excludedItems = new[]
            {
                WatcherEnums.SandboxUnlockID.Millipede,
                WatcherEnums.SandboxUnlockID.GrappleSnake,
                WatcherEnums.SandboxUnlockID.WeirdToy,
                WatcherEnums.SandboxUnlockID.SoftToy,
                WatcherEnums.SandboxUnlockID.SpinToy,
                WatcherEnums.SandboxUnlockID.BallToy,
                WatcherEnums.SandboxUnlockID.Rattler,
                WatcherEnums.SandboxUnlockID.RotDangleFruit,
                WatcherEnums.SandboxUnlockID.RotLizard,
                WatcherEnums.SandboxUnlockID.RotLoach,
                WatcherEnums.SandboxUnlockID.RotSeedCob,
                WatcherEnums.SandboxUnlockID.FireSprite,
                WatcherEnums.SandboxUnlockID.ScavengerDisciple,
                WatcherEnums.SandboxUnlockID.SandGrub,
            };
            var excludedLevels = new[]
            {
                WatcherEnums.LevelUnlockID.HP
            };

            // GREAT GOOGLY MOOGLY HE'S REFLECTING
            possibleTokens[0] = typeof(WatcherEnums.SandboxUnlockID)
                .GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(MultiplayerUnlocks.SandboxUnlockID) && !excludedItems.Contains((MultiplayerUnlocks.SandboxUnlockID)f.GetValue(null)))
                .Select(f => f.Name)
                .ToList();
            possibleTokens[1] = typeof(WatcherEnums.LevelUnlockID)
                .GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(MultiplayerUnlocks.LevelUnlockID) && !excludedLevels.Contains((MultiplayerUnlocks.LevelUnlockID)f.GetValue(null)))
                .Select(f => f.Name)
                .ToList();
            possibleTokens[3] = typeof(WatcherEnums.SlugcatUnlockID)
                .GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(MultiplayerUnlocks.SlugcatUnlockID))
                .Select(f => f.Name)
                .ToList();
        }

        public static Dictionary<string, string> FillWatcherMapRegions()
        {
            Dictionary<string, string> portalsRaw = new Dictionary<string, string>();

            foreach (var region in SlugcatStats.SlugcatStoryRegions(WatcherEnums.SlugcatStatsName.Watcher).ConvertAll(s => s.ToLowerInvariant()))
            {
                if (Custom.rainWorld.regionWarpRooms.ContainsKey(region))
                {
                    foreach (var warp in Custom.rainWorld.regionWarpRooms[region])
                    {
                        string room = warp.Split(':')[0].ToLowerInvariant();

                        string settingsPath = AssetManager.ResolveFilePath("World" + Path.DirectorySeparatorChar + region + "-rooms" + Path.DirectorySeparatorChar + room + "_settings.txt");

                        if (!File.Exists(settingsPath))
                        {
                            continue;
                        }

                        foreach (string line in File.ReadLines(settingsPath))
                        {
                            if (!line.StartsWith("PlacedObjects:"))
                                continue;

                            string raw = line.Substring("PlacedObjects:".Length);

                            string validated = Custom.ValidateSpacedDelimiter(raw, ",");
                            string[] objects = Regex.Split(validated, ", ");

                            foreach (string obj in objects)
                            {
                                string trimmed = obj.Trim();
                                if (trimmed.StartsWith("WarpPoint>"))
                                {
                                    portalsRaw[trimmed] = room.ToUpperInvariant();
                                }
                            }
                        }
                    }

                }
            }

            Dictionary<string, string> watcherMapPortals = new Dictionary<string, string>();
            foreach (var portal in portalsRaw)
            {
                string[] array = Regex.Split(portal.Key, "><");

                PlacedObject obj = new PlacedObject(PlacedObject.Type.None, null);
                obj.FromString(array);

                var data = obj.data as WarpPoint.WarpPointData;
                if (data == null) continue;

                string key = NewWarpPointIdentifyingString(data, portal.Value);
                watcherMapPortals[key] = obj.data.owner.ToString();
            }
            return watcherMapPortals;
        }

        // ripoff of WarpPointIdentifyingString but don't use game for timeline because ew
        private static string NewWarpPointIdentifyingString(WarpPoint.WarpPointData data, string sourceRoom)
        {
            var timeline = data.sourceTimeline ?? SlugcatStats.Timeline.Watcher;

            return $"{sourceRoom}:{timeline}:{data.uuidPair}";
        }
    }
}
