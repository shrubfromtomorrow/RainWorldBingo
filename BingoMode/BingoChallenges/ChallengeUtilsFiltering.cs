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
using System.Threading;
using static BingoMode.BingoData;

namespace BingoMode.BingoChallenges
{
    public static class ChallengeUtilsFiltering
    {
        private static readonly Dictionary<(BingoModifier BingoModifier, string listname, SlugName slug, bool sorted), string[]> cache = new();

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

        public static string[] GetFilteredList(string listname, string[] origList, bool sorted)
        {
            BingoModifier mode = BingoData.GetBingoModifier();
            var key = (mode, listname, ExpeditionData.slugcatPlayer, sorted);

            if (cache.TryGetValue(key, out var cached)) return cached;

            string[] result = ListRules[listname](ExpeditionData.slugcatPlayer, mode, origList);

            if (sorted) result = result.Distinct().OrderBy(x => x).ToArray();

            cache[key] = result;
            return result;
        }

        private static readonly Dictionary<string, Func<SlugName, BingoModifier, string[], string[]>> ListRules = new()
        {
            {
                ChallengeListConstants.Transport,
                (slug, mode, baselist) =>
                {
                    string[] watcherCreatures = { "Tardigrade", "Frog", "Rat" };
                    string[] watcherForbid = { "Yeek" };
                    string[] saintForbid = { "CicadaA", "CicadaB" };
                    string[] mscCreatures = { "Yeek" };
                    string[] strongAllow = { "JetFish" };
                    string[] spearHunterArtiForbid = { "Yeek" };

                    SlugName tempSlug = (mode == BingoModifier.WatcherMode) ? watchername : slug;

                    // mb lmao
                    baselist = baselist.Where(x =>
                        (tempSlug == watchername ? !watcherForbid.Contains(x) : !watcherCreatures.Contains(x))
                        && (ModManager.MSC || !mscCreatures.Contains(x))
                        && (tempSlug != saintname || !saintForbid.Contains(x))
                        && (tempSlug == huntername || tempSlug == gourname || tempSlug == artiname || tempSlug == watchername || !strongAllow.Contains(x))
                        && !((tempSlug == huntername || tempSlug == spearname || tempSlug == artiname) && spearHunterArtiForbid.Contains(x))
                    ).ToArray();

                    return baselist;
                }
            },
            {
                ChallengeListConstants.Pin,
                (slug, mode, baselist) =>
                {
                    return new[] { "Any Creature" }.Concat(baselist).ToArray();
                }
            },
            {
                ChallengeListConstants.Tolls,
                (slug, mode, baseList) =>
                {
                    string[] watcherTolls = { "WARF_G01", "WBLA_F01", "WSKD_B41" };
                    string[] artiTolls = { "LC_C10", "LC_STRIPMALLNEW", "LC_TEMPLETOLL" };
                    string[] saintTolls = { "UG_TOLL" };
                    string[] oeTolls = { "OE_TOWER04" };

                    SlugName tempSlug = (mode == BingoModifier.WatcherMode) ? watchername : slug;

                    return baseList
                        .Where(x =>
                            (tempSlug == watchername ? watcherTolls.Contains(x) : !watcherTolls.Contains(x))

                            && (tempSlug == artiname || !artiTolls.Contains(x))

                            && (tempSlug == saintname || !saintTolls.Contains(x))

                            && (tempSlug == gourname || tempSlug == monkname || tempSlug == survivorname || !oeTolls.Contains(x))
                        )
                        .ToArray();
                }
            },
            {
                ChallengeListConstants.Food,
                (slug, mode, baseList) =>
                {
                    List<string> mutableBase = baseList.ToList();

                    string[] mscFoods = { "GooieDuck", "LillyPuck", "DandelionPeach", "GlowWeed" };
                    string[] watcherFoods = { "FireSpriteLarva", "Rat", "Tardigrade", "SandGrub", "Frog", "Barnacle" };

                    SlugName tempSlug = (mode == BingoModifier.WatcherMode) ? watchername : slug;

                    if (tempSlug == watchername) mutableBase = mutableBase.Where(x => x != "SSOracleSwarmer").ToList();
                    if (!ModManager.MSC) mutableBase = mutableBase.Where(x => !mscFoods.Contains(x) || tempSlug == watchername).ToList();

                    if (tempSlug == monkname ||
                        tempSlug == survivorname ||
                        tempSlug == huntername ||
                        tempSlug == rivname ||
                        tempSlug == gourname)
                    {
                        mutableBase = mutableBase.Where(x => !watcherFoods.Contains(x)).ToList();
                    }

                    if (tempSlug == spearname || tempSlug == artiname)
                        mutableBase = mutableBase.Where(x => !watcherFoods.Contains(x) && x != "GlowWeed").ToList();

                    if (tempSlug == saintname)
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
                ChallengeListConstants.Weapons,
                (slug, mode, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] mscWeapons = { "LillyPuck" };
                    string[] watcherWeapons = { "Boomerang", "Frog", "GraffitiBomb" };
                    string[] rivForbid = { "WaterNut" };

                    SlugName tempSlug = (mode == BingoModifier.WatcherMode) ? watchername : slug;

                    if (tempSlug != watchername) mutableBase = mutableBase.Where(x => !watcherWeapons.Contains(x)).ToList();

                    if (!ModManager.MSC) mutableBase = mutableBase.Where(x => !mscWeapons.Contains(x) || tempSlug == watchername).ToList();

                    if (tempSlug == rivname) mutableBase = mutableBase.Where(x => !rivForbid.Contains(x)).ToList();

                    return mutableBase.ToArray();
                }
            },
            {
                ChallengeListConstants.WeaponsNoJelly,
                (slug, mode, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] exclusions = { "JellyFish", "Frog" };

                    mutableBase = mutableBase.Where(x => !exclusions.Contains(x)).ToList();

                    return mutableBase.ToArray();
                }
            },
            {
                ChallengeListConstants.Theft,
                (slug, mode, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] watcherItems = { "Boomerang", "GraffitiBomb" };
                    string[] watcherForbid = { "GooieDuck", "GlowWeed" };
                    string[] mscItems = { "GooieDuck", "GlowWeed", "LillyPuck" };
                    string[] hunterForbid = { "KarmaFlower" };

                    SlugName tempSlug = (mode == BingoModifier.WatcherMode) ? watchername : slug;

                    if (tempSlug != watchername)
                    {
                        mutableBase = mutableBase.Where(x => !watcherItems.Contains(x)).ToList();

                        if (!ModManager.MSC)
                        {
                            mutableBase = mutableBase.Where(x => !mscItems.Contains(x)).ToList();
                        }
                        if (tempSlug == huntername) mutableBase = mutableBase.Where(x => !hunterForbid.Contains(x)).ToList();
                    }
                    else
                    {
                        mutableBase = mutableBase.Where(x => !watcherForbid.Contains(x)).ToList();
                    }

                    return mutableBase.ToArray();
                }
            },
            {
                ChallengeListConstants.Friend,
                (slug, mode, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] mscFriends = { "EelLizard", "SpitLizard" };
                    string[] watcherFriends = { "PeachLizard", "IndigoLizard", "BlizzardLizard", "BasiliskLizard" };
                    string[] saintFriends = { "ZoopLizard" };

                    SlugName tempSlug = (mode == BingoModifier.WatcherMode) ? watchername : slug;

                    mutableBase = mutableBase.Where(x => x != "ZoopLizard" || tempSlug == saintname || tempSlug == watchername).ToList();

                    if (tempSlug != watchername) mutableBase = mutableBase.Where(x => !watcherFriends.Contains(x)).ToList();

                    if (!ModManager.MSC) mutableBase = mutableBase.Where(x => !mscFriends.Contains(x) || tempSlug == saintname || tempSlug == watchername).ToList();

                    return mutableBase.ToArray();

                }
            },
            {
                ChallengeListConstants.Pearls,
                (slug, mode, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] noArtiSpearPearls = { "SL_chimney", "SL_bridge", "SL_moon", "SB_ravine" };
                    string[] artiPearls = { "LC", "LC_second" };
                    string[] saintForbid = { "UW" };
                    string[] spearPearls = { "DM" };
                    string[] OEForbid = { "OE" };
                    string[] watcherModeForbid = { "WORA_WORA" };
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

                    SlugName tempSlug = (mode == BingoModifier.WatcherMode) ? watchername : slug;

                    if (tempSlug != watchername) mutableBase = mutableBase.Where(x => !watcherPearls.Contains(x)).ToList();
                    else mutableBase = mutableBase.Where(x => watcherPearls.Contains(x)).ToList();

                    if (tempSlug == SlugNameWatcher.Watcher && slug != SlugNameWatcher.Watcher) mutableBase = mutableBase.Where(x => !watcherModeForbid.Contains(x)).ToList();

                    if (!ModManager.MSC) mutableBase = mutableBase.Where(x => !mscPearls.Contains(x)).ToList();

                    if (tempSlug == artiname || tempSlug == spearname) mutableBase = mutableBase.Where(x => !noArtiSpearPearls.Contains(x)).ToList();

                    if (tempSlug != monkname && tempSlug != survivorname && tempSlug != gourname) mutableBase = mutableBase.Where(x => !OEForbid.Contains(x)).ToList();

                    if (tempSlug != spearname) mutableBase = mutableBase.Where(x => !spearPearls.Contains(x)).ToList();

                    if (tempSlug != artiname) mutableBase = mutableBase.Where(x => !artiPearls.Contains(x)).ToList();

                    if (tempSlug == saintname) mutableBase = mutableBase.Where(x => !saintForbid.Contains(x)).ToList();


                    return mutableBase.ToArray();

                }
            },
            {
                ChallengeListConstants.Craft,
                (slug, mode, baselist) =>
                {
                    if (mode == BingoModifier.WatcherMode)
                    {
                        return [.. baselist, "FireSpriteLarva", "GraffitiBomb"];
                    }
                    return baselist;
                }
            },
            {
                ChallengeListConstants.Regions,
                (slug, mode, baselist) =>
                {
                    string[] watcherForbid = { "SU", "CC", "HI", "SH", "WDSR", "WGWR", "WHIR", "WSUR" };
                    string[] watcherModeForbid = { "WRSA" };
                    SlugName tempSlug = (mode == BingoModifier.WatcherMode) ? watchername : slug;
                    List<string> mutableBase = new List<string>{ "Any Region" }.Concat(SlugcatStats.SlugcatStoryRegions(tempSlug).Where(x => x.ToLowerInvariant() != "hr"))
                    .Concat(SlugcatStats.SlugcatOptionalRegions(tempSlug)).ToList();
                    if (tempSlug == watchername) mutableBase = mutableBase.Where(x => !watcherForbid.Contains(x)).ToList();
                    if (tempSlug != slug) mutableBase = mutableBase.Where(x => !watcherModeForbid.Contains(x)).ToList();
                    return mutableBase.ToArray();
                }
            },
            {
                ChallengeListConstants.RegionsReal,
                (slug, mode, baselist) =>
                {
                    string[] watcherForbid = { "SU", "CC", "HI", "SH", "WDSR", "WGWR", "WHIR", "WSUR", "WRSA" };
                    SlugName tempSlug = (mode == BingoModifier.WatcherMode) ? watchername : slug;
                    List<string> mutableBase = SlugcatStats.SlugcatStoryRegions(tempSlug).Where(x => x.ToLowerInvariant() != "hr").Concat(SlugcatStats.SlugcatOptionalRegions(tempSlug)).ToList();
                    if (tempSlug == watchername) mutableBase = mutableBase.Where(x => !watcherForbid.Contains(x)).ToList();
                    return mutableBase.ToArray();
                }
            },
            {
                ChallengeListConstants.NootRegions,
                (slug, mode, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] noShelterRegions = { "WRSA" };

                    return mutableBase.Where(x => !noShelterRegions.Contains(x)).ToArray();
                }
            },
            {
                ChallengeListConstants.PopcornRegions,
                (slug, mode, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();
                    mutableBase.Add("Any Region");

                    string[] excludedpopcornregions = { "DS", "SH", "UW", "UG", "WARD", "WRFA", "WTDB", "WVWB", "WARE", "WPGA", "WRRA", "WPTA", "WSKC", "WSKA", "WTDA", "WVWA", "WARA", "WAUA", "WRSA", "WSSR" };

                    return mutableBase.Where(x => !excludedpopcornregions.Contains(x)).ToArray();
                }
            },
            {
                ChallengeListConstants.ShelterRegions,
                (slug, mode, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] forbid = { "WRSA" };
                    return mutableBase.Where(x => !forbid.Contains(x)).ToArray();
                }
            },
            {
                ChallengeListConstants.PomegranateRegions,
                (slug, mode, baselist) =>
                {
                    return baselist;
                }
            },
            {
                ChallengeListConstants.Echoes,
                (slug, mode, baseList) =>
                {
                    string[] allowedRegions = ChallengeUtils.GetCorrectListForChallenge(ChallengeListConstants.RegionsReal, true);
                    SlugName tempSlug = (mode == BingoModifier.WatcherMode) ? watchername : slug;

                    return GhostWorldPresence.GhostID.values.entries
                        .Where(ghost =>
                            ghost != "NoGhost"
                            && ghost != "SpinningTop"

                            && (tempSlug == saintname || (ghost != "SL" && ghost != "MS"))

                            && allowedRegions.Contains(ghost)
                        )
                        .ToArray();
                }
            },
            {
                ChallengeListConstants.Spinners,
                (slug, mode, baselist) =>
                {
                    //string[] spinningTopRegions = { "WARF", "WTDB", "WBLA", "WRFB", "WTDA", "WARE", "WSKC", "WVWA", "WPTA", "WARC", "WARB", "WVWB", "WARA", "WAUA" };

                    return ChallengeUtils.watcherSTSpots.Select(x => Regex.Split(Regex.Split(x, "-")[0], "_")[0]).Distinct().ToArray();
                }
            },
            {
                ChallengeListConstants.WeaverRooms,
                (slug, mode, baselist) =>
                {
                    return ChallengeUtils.watcherDWTSpots.Where(room => Regex.Split(room, "_")[0] != "WORA").ToArray();
                }
            },
            {
                ChallengeListConstants.Creatures,
                (slug, mode, baselist) =>
                {
                    var allowed = CreatureType.values.entries.Where(x => ChallengeTools.creatureSpawns[(mode == BingoModifier.WatcherMode) ? watchername.value : slug.value].Any(g => g.creature.value == x)).Select(x => x.ToString());

                    return new[] { "Any Creature" }.Concat(allowed).ToArray();
                }
            },
            {
                ChallengeListConstants.Depths,
                (slug, mode, baselist) =>
                {
                    return baselist;
                }
            },
            {
                ChallengeListConstants.Daemon,
                (slug, mode, baselist) =>
                {
                    return baselist;
                }
            },
            {
                // The architecture of this is weird. To put it simply, everything before and including SmallCentipede from food is a food, everything before VultureGrub within the food section is a non-creature edible. Everything after SmallCentipede is an item.
                ChallengeListConstants.BanItem,
                (slug, mode, baselist) =>
                {
                    List<string> mutableBase = baselist.ToList();

                    string[] watcherBanItems = { "Boomerang", "GraffitiBomb" };
                    SlugName tempSlug = (mode == BingoModifier.WatcherMode) ? watchername : slug;

                    if (tempSlug != watchername) mutableBase = mutableBase.Where(x => !watcherBanItems.Contains(x)).ToList();

                    return ChallengeUtils.GetCorrectListForChallenge(ChallengeListConstants.Food).Concat(mutableBase).ToArray();
                }
            },
            {
                ChallengeListConstants.Unlocks,
                (slug, mode, baselist) =>
                {
                    return BingoData.possibleTokens[0].Concat(BingoData.possibleTokens[1]).Concat(BingoData.possibleTokens[2]).Concat(BingoData.possibleTokens[3]).ToArray();
                }
            },
            {
                ChallengeListConstants.ChatLogs,
                (slug, mode, baselist) =>
                {
                    return BingoData.possibleTokens[4].ToArray();
                }
            },
            {
                ChallengeListConstants.Passage,
                (slug, mode, baselist) =>
                {
                    List<string> mutableBase =  WinState.EndgameID.values.entries;

                    string[] exclusions = { "Gourmand", "Survivor" };
                    string[] nonHunterForbidPassages = { "Mother" };
                    string[] watcherForbidPassages = { "Nomad", "Pilgrim", "Traveller" };
                    SlugName tempSlug = (mode == BingoModifier.WatcherMode) ? watchername : slug;
                    mutableBase = mutableBase.Where(x => !exclusions.Contains(x)).ToList();

                    if (tempSlug == watchername) mutableBase = mutableBase.Where(x => !watcherForbidPassages.Contains(x)).ToList();
                    if (BingoData.WatcherMode ? ExpeditionData.slugcatPlayer != huntername : tempSlug != huntername) mutableBase = mutableBase.Where(x => !nonHunterForbidPassages.Contains(x)).ToList();

                    return mutableBase.ToArray();
                }
            },
            {
                ChallengeListConstants.Storable,
                (slug, mode, baseList) =>
                {
                    string[] watcherItems = { "Boomerang", "GraffitiBomb", "FireSpriteLarva" };
                    string[] mscItems = { "GooieDuck", "LillyPuck", "DandelionPeach" };
                    string[] hunterForbid = { "KarmaFlower" };
                    SlugName tempSlug = (mode == BingoModifier.WatcherMode) ? watchername : slug;

                    return baseList
                        .Where(x =>
                            (ModManager.MSC || !mscItems.Contains(x) || tempSlug == watchername)

                            && (tempSlug == watchername || !watcherItems.Contains(x))

                            && (slug != huntername || !hunterForbid.Contains(x))

                            && !(ModManager.MSC &&
                                 (tempSlug == artiname || tempSlug == spearname) &&
                                 x == "BubbleGrass")

                            && !(ModManager.MSC &&
                                 tempSlug == saintname &&
                                 tempSlug != watchername &&
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
                ChallengeListConstants.Vista,
                (slug, mode, baselist) =>
                {
                    List<ValueTuple<string, string>> list = new List<ValueTuple<string, string>>();
                    foreach (KeyValuePair<string, Dictionary<string, Vector2>> keyValuePair in ChallengeUtils.BingoVistaLocations)
                    {
                        if (ChallengeUtils.GetCorrectListForChallenge(ChallengeListConstants.RegionsReal, true).Contains(keyValuePair.Key))
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
