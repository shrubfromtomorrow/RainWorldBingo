using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Expedition;
using MoreSlugcats;
using RWCustom;
using UnityEngine;
using Watcher;
using CreatureType = CreatureTemplate.Type;
using DLCItemType = DLCSharedEnums.AbstractObjectType;
using ItemType = AbstractPhysicalObject.AbstractObjectType;
using MSCItemType = MoreSlugcats.MoreSlugcatsEnums.AbstractObjectType;
using WatcherItemType = Watcher.WatcherEnums.AbstractObjectType;
using SlugName = SlugcatStats.Name;
using System.Threading;

namespace BingoMode.BingoChallenges
{
    public static class ChallengeUtilsFiltering
    {
        private static readonly Dictionary<(string listname, SlugName slug, bool sorted), string[]> cache = new();

        public static readonly SlugName watchername = WatcherEnums.SlugcatStatsName.Watcher;
        public static readonly SlugName survivorname = SlugName.White;
        public static readonly SlugName monkname = SlugName.Yellow;
        public static readonly SlugName huntername = SlugName.Red;
        public static readonly SlugName artiname = MoreSlugcatsEnums.SlugcatStatsName.Artificer;
        public static readonly SlugName gourname = MoreSlugcatsEnums.SlugcatStatsName.Gourmand;
        public static readonly SlugName spearname = MoreSlugcatsEnums.SlugcatStatsName.Spear;
        public static readonly SlugName rivname = MoreSlugcatsEnums.SlugcatStatsName.Rivulet;
        public static readonly SlugName saintname = MoreSlugcatsEnums.SlugcatStatsName.Saint;

        public static void ClearCache()
        {
            cache.Clear();
        }

        //public static void PrintCache()
        //{
        //    Plugin.logger.LogInfo("List rules defines " + ListRules.Count + " lists");
        //    int counter = 1;
        //    foreach (var thing in cache.Keys)
        //    {
        //        Plugin.logger.LogInfo($"({counter})List: " + thing.listname);
        //        foreach (var item in cache[thing])
        //        {
        //            Plugin.logger.LogInfo($"{item}");
        //        }
        //        counter++;
        //    }
        //}

        public static string[] GetFilteredList(string listname, string[] origList, bool sorted)
        {
            var key = (listname, ExpeditionData.slugcatPlayer, sorted);

            if (cache.TryGetValue(key, out var cached)) return cached;

            string[] result = ListRules[listname](ExpeditionData.slugcatPlayer, origList);

            if (sorted) result = result.Distinct().OrderBy(x => x).ToArray();

            cache[key] = result;
            return result;
        }

        private static readonly Dictionary<string, Func<SlugName, string[], string[]>> ListRules = new()
        {
            {
                "transport",
                (slug, baselist) =>
                {
                    string[] watcherCreatures = { "Tardigrade", "Frog", "Rat" };
                    string[] watcherForbid = { "Yeek" };
                    string[] saintForbid = { "CicadaA", "CicadaB" };
                    string[] mscCreatures = { "Yeek" };
                    string[] strongAllow = { "JetFish" };
                    string[] spearHunterArtiForbid = { "Yeek" };

                    // mb lmao
                    baselist = baselist.Where(x =>
                        (slug == watchername ? !watcherForbid.Contains(x) : !watcherCreatures.Contains(x))
                        && (ModManager.MSC || !mscCreatures.Contains(x))
                        && (slug != saintname || !saintForbid.Contains(x))
                        && (slug == huntername || slug == gourname || slug == artiname || slug == watchername || !strongAllow.Contains(x))
                        && !((slug == huntername || slug == spearname || slug == artiname) && spearHunterArtiForbid.Contains(x))
                    ).ToArray();

                    return baselist;
                }
            },
            {
                "pin",
                (slug, baselist) =>
                {
                    return new[] { "Any Creature" }.Concat(baselist).ToArray();
                }
            },
            {
                "tolls",
                (slug, baseList) =>
                {
                    string[] watcherTolls = { "WARF_G01", "WBLA_F01", "WSKD_B41" };
                    string[] artiTolls = { "LC_C10", "LC_STRIPMALLNEW", "LC_TEMPLETOLL" };
                    string[] saintTolls = { "UG_TOLL" };
                    string[] oeTolls = { "OE_TOWER04" };

                    return baseList
                        .Where(x =>
                            (slug == watchername ? watcherTolls.Contains(x) : !watcherTolls.Contains(x))

                            && (slug == artiname || !artiTolls.Contains(x))

                            && (slug == saintname || !saintTolls.Contains(x))

                            && (slug == gourname || slug == monkname || slug == survivorname || !oeTolls.Contains(x))
                        )
                        .ToArray();
                }
            },
            {
                "food",
                (slug, baseList) =>
                {
                    List<string> mutableBase = baseList.ToList();

                    string[] mscFoods = { "GooieDuck", "LillyPuck", "DandelionPeach", "GlowWeed" };
                    string[] watcherFoods = { "FireSpriteLarva", "Rat", "Tardigrade", "SandGrub", "Frog", "Barnacle" };

                    if (slug == watchername) mutableBase = mutableBase.Where(x => x != "SSOracleSwarmer").ToList();
                    if (!ModManager.MSC) mutableBase = mutableBase.Where(x => !mscFoods.Contains(x) || slug == watchername).ToList();

                    if (slug == monkname ||
                        slug == survivorname ||
                        slug == huntername ||
                        slug == rivname ||
                        slug == gourname)
                    {
                        mutableBase = mutableBase.Where(x => !watcherFoods.Contains(x)).ToList();
                    }

                    if (slug == spearname || slug == artiname)
                        mutableBase = mutableBase.Where(x => !watcherFoods.Contains(x) && x != "GlowWeed").ToList();

                    if (slug == saintname)
                        mutableBase = mutableBase.Where(x =>
                            !watcherFoods.Contains(x) &&
                            x != "EggBugEgg" &&
                            x != "DandelionPeach" &&
                            x != "SSOracleSwarmer" &&
                            x != "SmallNeedleWorm" &&
                            x != "DandelionPeach").ToList();

                    return mutableBase.ToArray();
                }
            },
            {
                "weapons",
                (slug, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] mscWeapons = { "LillyPuck" };
                    string[] watcherWeapons = { "Boomerang", "Frog", "GraffitiBomb" };
                    string[] rivForbid = { "WaterNut" };

                    if (slug != watchername) mutableBase = mutableBase.Where(x => !watcherWeapons.Contains(x)).ToList();

                    if (!ModManager.MSC) mutableBase = mutableBase.Where(x => !mscWeapons.Contains(x) || slug == watchername).ToList();

                    if (slug == rivname) mutableBase = mutableBase.Where(x => !rivForbid.Contains(x)).ToList();

                    return mutableBase.ToArray();

                }
            },
            {
                "weaponsnojelly",
                (slug, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] exclusions = { "JellyFish", "Frog" };

                    mutableBase = mutableBase.Where(x => !exclusions.Contains(x)).ToList();

                    return mutableBase.ToArray();

                }
            },
            {
                "theft",
                (slug, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] watcherItems = { "Boomerang", "GraffitiBomb" };
                    string[] watcherForbid = { "GooieDuck", "GlowWeed" };
                    string[] mscItems = { "GooieDuck", "GlowWeed", "LillyPuck" };
                    string[] hunterForbid = { "KarmaFlower" };

                    if (slug != watchername)
                    {
                        mutableBase = mutableBase.Where(x => !watcherItems.Contains(x)).ToList();

                        if (!ModManager.MSC)
                        {
                            mutableBase = mutableBase.Where(x => !mscItems.Contains(x)).ToList();
                        }
                        if (slug == huntername) mutableBase = mutableBase.Where(x => !hunterForbid.Contains(x)).ToList();
                    }
                    else
                    {
                        mutableBase = mutableBase.Where(x => !watcherForbid.Contains(x)).ToList();
                    }

                    return mutableBase.ToArray();
                }
            },
            {
                "friend",
                (slug, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] mscFriends = { "EelLizard", "SpitLizard" };
                    string[] watcherFriends = { "PeachLizard", "IndigoLizard", "BlizzardLizard", "BasiliskLizard" };
                    string[] saintFriends = { "ZoopLizard" };

                    mutableBase = mutableBase.Where(x => x != "ZoopLizard" || slug == saintname || slug == watchername).ToList();

                    if (slug != watchername) mutableBase = mutableBase.Where(x => !watcherFriends.Contains(x)).ToList();

                    if (!ModManager.MSC) mutableBase = mutableBase.Where(x => !mscFriends.Contains(x) || slug == saintname || slug == watchername).ToList();

                    return mutableBase.ToArray();

                }
            },
            {
                "pearls",
                (slug, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] noArtiSpearPearls = { "SL_chimney", "SL_bridge", "SL_moon", "SB_ravine" };
                    string[] artiPearls = { "LC", "LC_second" };
                    string[] saintForbid = { "UW" };
                    string[] spearPearls = { "DM" };
                    string[] OEForbid = { "OE" };
                    string[] watcherPearls = { "WORA_WORA",
                        "WAUA_WAUA",
                        "WPTA_DRONE",
                        "WSKC_ABSTRACT",
                        "WBLA_AUDIO_VOICEWIND1",
                        "WARE_AUDIO_VOICEWIND2",
                        "WTDA_AUDIO_JAM1",
                        "WSKD_AUDIO_JAM2",
                        "WTDB_AUDIO_JAM3",
                        "WRFB_AUDIO_JAM4",
                        "WARG_AUDIO_GROOVE",
                        "WAUA_TEXT_AUDIO_TALKSHOW",
                        "WMPA_TEXT_NOTIONOFSELF",
                        "WARB_TEXT_SECRET",
                        "WARC_TEXT_CONTEMPT",
                        "WARD_TEXT_STARDUST",
                        "WVWA_TEXT_KITESDAY"
                    };
                    string[] mscPearls = { "SI_chat3",
                        "SI_chat4",
                        "SI_chat5",
                        "VS",
                        "OE",
                        "LC",
                        "DM",
                        "LC_second",
                        "SU_filt",
                        "MS"
                    };

                    if (slug != watchername) mutableBase = mutableBase.Where(x => !watcherPearls.Contains(x)).ToList();
                    else mutableBase = mutableBase.Where(x => watcherPearls.Contains(x)).ToList();

                    if (!ModManager.MSC) mutableBase = mutableBase.Where(x => !mscPearls.Contains(x)).ToList();

                    if (slug == artiname || slug == spearname) mutableBase = mutableBase.Where(x => !noArtiSpearPearls.Contains(x)).ToList();

                    if (slug != monkname && slug != survivorname && slug != gourname) mutableBase = mutableBase.Where(x => !OEForbid.Contains(x)).ToList();

                    if (slug != spearname) mutableBase = mutableBase.Where(x => !spearPearls.Contains(x)).ToList();

                    if (slug != artiname) mutableBase = mutableBase.Where(x => !artiPearls.Contains(x)).ToList();

                    if (slug == saintname) mutableBase = mutableBase.Where(x => !saintForbid.Contains(x)).ToList();


                    return mutableBase.ToArray();

                }
            },
            {
                "craft",
                (slug, baselist) =>
                {
                    return baselist;
                }
            },
            {
                "regions",
                (slug, baselist) =>
                {
                    string[] watcherForbid = { "SU", "CC", "HI", "SH", "WDSR", "WGWR", "WHIR", "WSUR" };
                    List<string> mutableBase = new List<string>{ "Any Region" }.Concat(SlugcatStats.SlugcatStoryRegions(ExpeditionData.slugcatPlayer).Where(x => x.ToLowerInvariant() != "hr"))
                    .Concat(SlugcatStats.SlugcatOptionalRegions(ExpeditionData.slugcatPlayer)).ToList();
                    if (slug == watchername) mutableBase = mutableBase.Where(x => !watcherForbid.Contains(x)).ToList();
                    return mutableBase.ToArray();
                }
            },
            {
                "regionsreal",
                (slug, baselist) =>
                {
                    string[] watcherForbid = { "SU", "CC", "HI", "SH", "WDSR", "WGWR", "WHIR", "WSUR" };
                    List<string> mutableBase = SlugcatStats.SlugcatStoryRegions(ExpeditionData.slugcatPlayer).Where(x => x.ToLowerInvariant() != "hr").Concat(SlugcatStats.SlugcatOptionalRegions(ExpeditionData.slugcatPlayer)).ToList();
                    if (slug == watchername) mutableBase = mutableBase.Where(x => !watcherForbid.Contains(x)).ToList();
                    return mutableBase.ToArray();
                }
            },
            {
                "nootregions",
                (slug, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] noShelterRegions = { "WRSA" };

                    return mutableBase.Where(x => !noShelterRegions.Contains(x)).ToArray();
                }
            },
            {
                "popcornregions",
                (slug, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();
                    mutableBase.Add("Any Region");

                    string[] excludedpopcornregions = { "DS", "SH", "UW", "UG", "WARD", "WRFA", "WTDB", "WVWB", "WARE", "WPGA", "WRRA", "WPTA", "WSKC", "WSKA", "WTDA", "WVWA", "WARA", "WAUA", "WRSA", "WSSR" };

                    return mutableBase.Where(x => !excludedpopcornregions.Contains(x)).ToArray();
                }
            },
            {
                "pomegranateRegions",
                (slug, baselist) =>
                {
                    return baselist;
                }
            },
            {
                "echoes",
                (slug, baseList) =>
                {
                    string[] allowedRegions = ChallengeUtils.GetCorrectListForChallenge("regionsreal", true);

                    return GhostWorldPresence.GhostID.values.entries
                        .Where(ghost =>
                            ghost != "NoGhost"
                            && ghost != "SpinningTop"

                            && (slug == saintname || (ghost != "SL" && ghost != "MS"))

                            && allowedRegions.Contains(ghost)
                        )
                        .ToArray();
                }
            },
            {
                "spinners",
                (slug, baselist) =>
                {
                    //string[] spinningTopRegions = { "WARF", "WTDB", "WBLA", "WRFB", "WTDA", "WARE", "WSKC", "WVWA", "WPTA", "WARC", "WARB", "WVWB", "WARA", "WAUA" };

                    return ChallengeUtils.watcherSTSpots.Select(x => Regex.Split(Regex.Split(x, "-")[0], "_")[0]).Distinct().ToArray();
                }
            },
            {
                "weavers",
                (slug, baselist) =>
                {
                    return ChallengeUtils.watcherDWTSpots.Where(room => Regex.Split(room, "_")[0] != "WORA").ToArray();
                }
            },
            {
                "creatures",
                (slug, baselist) =>
                {
                    var allowed = CreatureType.values.entries.Where(x => ChallengeTools.creatureSpawns[slug.value].Any(g => g.creature.value == x)).Select(x => x.ToString());

                    return new[] { "Any Creature" }.Concat(allowed).ToArray();
                }
            },
            {
                "depths",
                (slug, baselist) =>
                {
                    return baselist;
                }
            },
            {
                // The architecture of this is weird. To put it simply, everything before and including SmallCentipede from food is a food, everything before VultureGrub within the food section is a non-creature edible. Everything after SmallCentipede is an item.
                "banitem",
                (slug, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] watcherBanItems = { "Boomerang", "GraffitiBomb" };

                    if (slug != watchername) mutableBase = mutableBase.Where(x => !watcherBanItems.Contains(x)).ToList();

                    return ChallengeUtils.GetCorrectListForChallenge("food").Concat(mutableBase).ToArray();
                }
            },
            {
                "unlocks",
                (slug, baselist) =>
                {
                    return BingoData.possibleTokens[0].Concat(BingoData.possibleTokens[1]).Concat(BingoData.possibleTokens[2]).Concat(BingoData.possibleTokens[3]).ToArray();
                }
            },
            {
                "chatlogs",
                (slug, baselist) =>
                {
                    return BingoData.possibleTokens[4].ToArray();
                }
            },
            {
                "passage",
                (slug, baselist) =>
                {
                    List<string> mutableBase =  WinState.EndgameID.values.entries;

                    string[] exclusions = { "Mother", "Gourmand", "Survivor" };
                    string[] watcherForbidPassages = { "Nomad", "Pilgrim", "Traveller" };

                    mutableBase = mutableBase.Where(x => !exclusions.Contains(x)).ToList();

                    if (slug == watchername) mutableBase = mutableBase.Where(x => !watcherForbidPassages.Contains(x)).ToList();

                    return mutableBase.ToArray();
                }
            },
            {
                "storable",
                (slug, baseList) =>
                {
                    string[] watcherItems = { "Boomerang", "GraffitiBomb", "FireSpriteLarva" };
                    string[] mscItems = { "GooieDuck", "LillyPuck", "DandelionPeach" };
                    string[] hunterForbid = { "KarmaFlower" };


                    return baseList
                        .Where(x =>
                            (ModManager.MSC || !mscItems.Contains(x))

                            && (slug == watchername || !watcherItems.Contains(x))

                            && (slug != huntername || !hunterForbid.Contains(x))

                            && !(ModManager.MSC &&
                                 (slug == artiname || slug == spearname) &&
                                 x == "BubbleGrass")

                            && !(ModManager.MSC &&
                                 slug == saintname &&
                                 slug != watchername &&
                                 (
                                     x == "LillyPuck" ||
                                     x == "EggBugEgg" ||
                                     x == "SmallNeedleWorm" ||
                                     x == "SSOracleSwarmer" ||
                                     x == "DandelionPeach"
                                 ))
                        )
                        .ToArray();
                }
            },
            {
                "vista",
                (slug, baselist) =>
                {
                    List<ValueTuple<string, string>> list = new List<ValueTuple<string, string>>();
                    foreach (KeyValuePair<string, Dictionary<string, Vector2>> keyValuePair in ChallengeUtils.BingoVistaLocations)
                    {
                        if (ChallengeUtils.GetCorrectListForChallenge("regionsreal", true).Contains(keyValuePair.Key))
                        {
                            foreach (KeyValuePair<string, Vector2> keyValuePair2 in keyValuePair.Value)
                            {
                                list.Add(new ValueTuple<string, string>(keyValuePair.Key, keyValuePair2.Key));
                            }
                        }
                    }
                    List<string> strings = [];
                    for (int i = 0; i < list.Count; i++)
                    {
                        strings.Add(list[i].Item2);
                    }
                    return strings.ToArray();
                }
            },
        };
    }
}
