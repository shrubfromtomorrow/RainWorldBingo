using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
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

namespace BingoMode.BingoChallenges
{
    public static class ChallengeNameConstants
    {
        public const string Achievement = "achievement";
        public const string Toll = "toll";
        public const string Damage = "damage";
        public const string DontUseItem = "dontuseitem";
        public const string Eat = "eat";
        public const string Echo = "echo";
        public const string HatchNoodle = "hatchnoodle";
        public const string ItemHoard = "itemhoard";
        public const string KarmaFlower = "karmaflower";
        public const string Kill = "kill";
        public const string PearlHoard = "pearlhoard";
        public const string Popcorn = "popcorn";
        public const string Score = "score";
        public const string Steal = "steal";
        public const string Tame = "tame";
        public const string CollectRippleSpawn = "collectripplespawn";
        public const string CreaturePortal = "creatureportal";
        public const string OpenMelons = "openmelons";
        public const string Weaver = "weaver";
    }

    public static class ChallengeListConstants
    {
        public const string Transport = "transport";
        public const string Pin = "pin";
        public const string Tolls = "tolls";
        public const string Food = "food";
        public const string Weapons = "weapons";
        public const string WeaponsNoJelly = "weaponsnojelly";
        public const string Theft = "theft";
        public const string Friend = "friend";
        public const string Pearls = "pearls";
        public const string Craft = "craft";
        public const string Regions = "regions";
        public const string RegionsReal = "regionsreal";
        public const string NootRegions = "nootregions";
        public const string PopcornRegions = "popcornregions";
        public const string PomegranateRegions = "pomegranateregions";
        public const string ShelterRegions = "shelterregions";
        public const string Echoes = "echoes";
        public const string Spinners = "spinners";
        public const string WeaverRooms = "weaverrooms";
        public const string Creatures = "creatures";
        public const string Depths = "depths";
        public const string Daemon = "daemon";
        public const string BanItem = "banitem";
        public const string Unlocks = "unlocks";
        public const string ChatLogs = "chatlogs";
        public const string Passage = "passage";
        public const string ExpObject = "expobject";
        public const string Vista = "vista";
        public const string Storable = "storable";
    }

    public static class ChallengeUtils
    {
        public static Dictionary<string, Dictionary<string, Vector2>> BingoVistaLocations;
        public static string[] AllGates = [];
        public static string[] AllEnterableRegions = [];
        // character - region:count
        public static Dictionary<string, Dictionary<string, int>> RegionShelterCount; 
        public static List<string> watcherRegions;
        public static List<string> watcherSTSpots;
        public static List<string> watcherPortals;
        public static List<string> watcherDWTSpots;

        public static void Apply()
        {
            On.Expedition.ChallengeTools.ItemName += ChallengeTools_ItemName;
            On.Expedition.ChallengeTools.CreatureName += ChallengeTools_CreatureName;
            On.Menu.ExpeditionMenu.ExpeditionSetup += ExpeditionMenu_ExpeditionSetup;
            FetchGatesFromFile();
            FetchAllEnterableRegions();
            FetchShelterCount();
        }

        private static void ExpeditionMenu_ExpeditionSetup(On.Menu.ExpeditionMenu.orig_ExpeditionSetup orig, Menu.ExpeditionMenu self)
        {
            orig.Invoke(self);

            // god I hate case sensitivity
            BingoVistaLocations = ChallengeTools.VistaLocations.ToDictionary(x => x.Key, x => new Dictionary<string, Vector2>(x.Value, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
            if (watcherRegions == null)
            {
                PopulateWatcherData();
            }
        }

        public static string ItemOrCreatureIconName(string thing)
        {
            int data = GetIconIntData(thing);
            thing = GetIconType(thing);
            string elementName = ItemSymbol.SpriteNameForItem(new(thing, false), data);
            if (elementName == "Futile_White")
            {
                elementName = CreatureSymbol.SpriteNameOfCreature(new IconSymbol.IconSymbolData(new CreatureType(thing, false), ItemType.Creature, data));
            }
            return elementName;
        }

        public static Color ItemOrCreatureIconColor(string thing)
        {
            int data = GetIconIntData(thing);
            thing = GetIconType(thing);
            Color color = ItemSymbol.ColorForItem(new(thing, false), data);
            if (color == Menu.Menu.MenuRGB(Menu.Menu.MenuColors.MediumGrey))
            {
                color = CreatureSymbol.ColorOfCreature(new IconSymbol.IconSymbolData(new CreatureType(thing, false), ItemType.Creature, data));
            }
            return color;
        }

        // watcher touches this
        private static int GetIconIntData(string thing) => thing switch
        {
            "Centipede" => 2,
            "BigCentipede" => 3,
            "FireSpear" => 1,
            "ElectricSpear" => 2,
            "HellSpear" => 3,
            "AltSkyWhale" => 1,
            "ProtoLizard" => 2,
            _ => 0
        };

        private static string GetIconType(string thing) => thing switch
        {
            "BigCentipede" => "Centipede",
            "FireSpear" => "Spear",
            "ElectricSpear" => "Spear",
            "HellSpear" => "Spear",
            "AltSkyWhale" => "SkyWhale",
            "ProtoLizard" => "IndigoLizard",
            _ => thing
        };

        public static string[] GetCorrectListForChallenge(string listName, bool sorted = false)
        {
            string ln = listName;
            //bool addEmpty = false;
            //if (ln[0] == '_')
            //{
            //    addEmpty = true;
            //    ln = ln.Substring(1);
            //}
            switch (ln)
            {
                case ChallengeListConstants.Transport: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Transport, Transportable, sorted);
                case ChallengeListConstants.Pin: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Pin, Pinnable, sorted);
                case ChallengeListConstants.Tolls: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Tolls, BombableOutposts, sorted);
                case ChallengeListConstants.Food: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Food, FoodTypes, sorted);
                case ChallengeListConstants.Weapons: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Weapons, Weapons, sorted);
                case ChallengeListConstants.WeaponsNoJelly: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.WeaponsNoJelly, GetCorrectListForChallenge(ChallengeListConstants.Weapons), sorted);
                case ChallengeListConstants.Theft: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Theft, StealableStolable, sorted);
                case ChallengeListConstants.Friend: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Friend, Befriendable, sorted);
                case ChallengeListConstants.Pearls: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Pearls, CollectablePearls, sorted);
                case ChallengeListConstants.Craft: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Craft, CraftableItems, sorted);
                case ChallengeListConstants.Regions: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Regions, null, sorted);
                case ChallengeListConstants.RegionsReal: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.RegionsReal, null, sorted);
                case ChallengeListConstants.NootRegions: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.NootRegions, GetCorrectListForChallenge(ChallengeListConstants.Regions), sorted);
                case ChallengeListConstants.PopcornRegions: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.PopcornRegions, GetCorrectListForChallenge(ChallengeListConstants.RegionsReal), sorted);
                case ChallengeListConstants.PomegranateRegions: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.PomegranateRegions, PomegranateRegions, sorted);
                case ChallengeListConstants.ShelterRegions: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.ShelterRegions, GetCorrectListForChallenge(ChallengeListConstants.Regions), sorted);
                case ChallengeListConstants.Echoes: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Echoes, null, sorted);
                //No clean way to get all spots because CheckForRegionGhost doesn't work for spinning top
                case ChallengeListConstants.Spinners: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Spinners, null, sorted);
                case ChallengeListConstants.WeaverRooms: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.WeaverRooms, null, sorted);
                case ChallengeListConstants.Creatures: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Creatures, null, sorted);
                case ChallengeListConstants.Depths: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Depths, Depthable, sorted);
                case ChallengeListConstants.Daemon: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Daemon, Daemonable, sorted);
                case ChallengeListConstants.BanItem: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.BanItem, Bannable, sorted);
                // this one fucking sucks
                case ChallengeListConstants.Unlocks: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Unlocks, null, sorted);
                case ChallengeListConstants.ChatLogs: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.ChatLogs, null, sorted);
                case ChallengeListConstants.Passage: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Passage, null, sorted);
                case ChallengeListConstants.ExpObject: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Storable, Storable, sorted);
                case ChallengeListConstants.Vista: return ChallengeUtilsFiltering.GetFilteredList(ChallengeListConstants.Vista, null, sorted);
            }
            return ["Whoops something went wrong"];
        }

        public static string ChallengeTools_ItemName(On.Expedition.ChallengeTools.orig_ItemName orig, ItemType type)
        {
            InGameTranslator translator = ChallengeTools.IGT;
            // Weapons
            if (type == ItemType.Spear) return translator.Translate("Spears");
            if (type == ItemType.Rock) return translator.Translate("Rocks");
            if (type == ItemType.SporePlant) return translator.Translate("Bee Hives");
            if (type == ItemType.DataPearl) return translator.Translate("Pearls");
            if (type == ItemType.GraffitiBomb) return translator.Translate("Graffiti Bombs");
            if (type == WatcherItemType.Boomerang) return translator.Translate("Boomerangs");
            // Food items
            if (type == ItemType.DangleFruit) return translator.Translate("Blue Fruit");
            if (type == ItemType.SSOracleSwarmer) return translator.Translate("Pebbles' Neurons");
            if (type == ItemType.EggBugEgg) return translator.Translate("Eggbug Eggs");
            if (type == ItemType.WaterNut) return translator.Translate("Bubble Fruit");
            if (type == ItemType.SlimeMold) return translator.Translate("Slime Mold");
            if (type == ItemType.BubbleGrass) return translator.Translate("Bubble Grass");
            if (type == DLCItemType.GlowWeed) return translator.Translate("Glow Weed");
            if (type == DLCItemType.DandelionPeach) return translator.Translate("Dandelion Peaches");
            if (type == DLCItemType.LillyPuck) return translator.Translate("Lillypucks");
            if (type == DLCItemType.GooieDuck) return translator.Translate("Gooieducks");
            if (type == WatcherItemType.FireSpriteLarva) return translator.Translate("Fire Sprite Larvae");
            // Other
            if (type == ItemType.KarmaFlower) return translator.Translate("Karma Flowers");


            return orig.Invoke(type);
        }

        public static void ChallengeTools_CreatureName(On.Expedition.ChallengeTools.orig_CreatureName orig, ref string[] creatureNames)
        {
            orig.Invoke(ref creatureNames);
            creatureNames[(int)CreatureType.SmallNeedleWorm] = ChallengeTools.IGT.Translate("Small Noodleflies");
            creatureNames[(int)CreatureType.VultureGrub] = ChallengeTools.IGT.Translate("Vulture Grubs");
            creatureNames[(int)CreatureType.Hazer] = ChallengeTools.IGT.Translate("Hazers");
            creatureNames[(int)CreatureType.Salamander] = ChallengeTools.IGT.Translate("Salamanders");
            creatureNames[(int)CreatureType.Spider] = ChallengeTools.IGT.Translate("Coalescipedes");
            if (ModManager.MSC) creatureNames[(int)DLCSharedEnums.CreatureTemplateType.Yeek] = ChallengeTools.IGT.Translate("Yeeks");
            if (ModManager.Watcher) creatureNames[(int)WatcherEnums.CreatureTemplateType.SandGrub] = ChallengeTools.IGT.Translate("Sand Grubs");
            if (ModManager.Watcher) creatureNames[(int)WatcherEnums.CreatureTemplateType.Tardigrade] = ChallengeTools.IGT.Translate("Tardigrades");
            if (ModManager.Watcher) creatureNames[(int)WatcherEnums.CreatureTemplateType.Rat] = ChallengeTools.IGT.Translate("Rats");
            if (ModManager.Watcher) creatureNames[(int)WatcherEnums.CreatureTemplateType.Frog] = ChallengeTools.IGT.Translate("Frogs");
            if (ModManager.Watcher) creatureNames[(int)WatcherEnums.CreatureTemplateType.Barnacle] = ChallengeTools.IGT.Translate("Barnacles");
        }

        public static string CreatureSingularNames(string type)
        {
            if (type == CreatureType.SmallNeedleWorm.value) return ChallengeTools.IGT.Translate("Small Noodlefly");
            else return ChallengeTools.IGT.Translate(ChallengeTools.creatureNames[new CreatureType(type).Index].TrimEnd('s'));
        }

        public static List<string> CreatureOriginRegions(string type, SlugName slug)
        {
            List<string> r = [];
            switch (type)
            {
                case "CicadaA":
                case "CicadaB":
                    r.AddRange(["SU", "LF", "SI"]);
                    if (slug != MoreSlugcatsEnums.SlugcatStatsName.Rivulet) r.Add("VS");
                    break;
                case "Hazer":
                    r.AddRange(["HI", "GW", "SL", slug == MoreSlugcatsEnums.SlugcatStatsName.Saint ? "UG" : "DS", "LF"]);
                    break;
                case "VultureGrub":
                    r.AddRange(["HI", "GW", "CC", "LF"]);
                    break;
                case "JetFish":
                    r.Add((slug == MoreSlugcatsEnums.SlugcatStatsName.Artificer || slug == MoreSlugcatsEnums.SlugcatStatsName.Spear) ? "LM" : "SL");
                    break;
                case "Yeek":
                    r.Add("OE"); if (slug == MoreSlugcatsEnums.SlugcatStatsName.Saint || slug == MoreSlugcatsEnums.SlugcatStatsName.Rivulet)
                    {
                        r.AddRange(["SB", "LF"]);
                        r.Remove("OE");
                    }
                    break;
            }

            return r;
        }

        private static void FetchAllEnterableRegions()
        {
            string path = AssetManager.ResolveFilePath(Path.Combine("world", "regions.txt"));
            if (File.Exists(path))
            {
                AllEnterableRegions = File.ReadAllLines(path);
            }
        }

        private static void FetchShelterCount()
        {
            RegionShelterCount = new Dictionary<string, Dictionary<string, int>>();
            List<SlugName> playableChars = ExpeditionData.GetPlayableCharacters();
            Dictionary<string, string[]> propertyCache = new();

            foreach (SlugName slug in playableChars)
            {
                RegionShelterCount[slug.value] = new Dictionary<string, int>();
                foreach (string regionName in AllEnterableRegions)
                {
                    RegionShelterCount[slug.value][regionName] = 0;
                }
            }

            foreach(string regionName in AllEnterableRegions)
            {
                string[] worldFileLines = File.ReadAllLines(AssetManager.ResolveFilePath($"World{Path.DirectorySeparatorChar}{regionName}{Path.DirectorySeparatorChar}world_{regionName}.txt"));

                int shelterCount = 0;

                foreach (string line in worldFileLines)
                {
                    if (line.EndsWith(": SHELTER", StringComparison.OrdinalIgnoreCase))
                    {
                        shelterCount++;
                    }
                }

                foreach (string cat in RegionShelterCount.Keys)
                {
                    RegionShelterCount[cat][regionName] = shelterCount;
                }
            }

            foreach (string regionName in AllEnterableRegions)
            {
                foreach (string cat in RegionShelterCount.Keys)
                {
                    string specificCatPath = AssetManager.ResolveFilePath($"World{Path.DirectorySeparatorChar}{regionName}{Path.DirectorySeparatorChar}Properties-{cat}.txt");
                    string defaultPath = AssetManager.ResolveFilePath($"World{Path.DirectorySeparatorChar}{regionName}{Path.DirectorySeparatorChar}Properties.txt");

                    string path;

                    if (File.Exists(specificCatPath)) path = specificCatPath;
                    else if (File.Exists(defaultPath)) path = defaultPath;
                    else path = null;

                    string[] propertiesLines;

                    if (path == null)
                    {
                        propertiesLines = Array.Empty<string>();
                    }
                    else if (!propertyCache.TryGetValue(path, out propertiesLines))
                    {
                        propertiesLines = File.ReadAllLines(path);
                        propertyCache[path] = propertiesLines;
                    }

                    foreach (string line in propertiesLines)
                    {
                        string[] parts = line.Split(':');

                        if (parts.Length < 3) continue;

                        if (parts[0].Trim() == "Broken Shelters" && parts[1].Trim().Equals(cat, StringComparison.OrdinalIgnoreCase))
                        {
                            int brokenCount = parts[2].Split(',').Count(s => !string.IsNullOrWhiteSpace(s));

                            RegionShelterCount[cat][regionName] -= brokenCount;
                        }
                    }
                }
            }
        }

        private static void FetchGatesFromFile()
        {
            List<string> gatesToAdd = [];
            string path = AssetManager.ResolveFilePath(Path.Combine("world", "gates", "enterableGateCombos.txt"));
            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    try
                    {
                        string actualLine = line;
                        if (line.StartsWith("MSC-"))
                        {
                            if (!ModManager.MSC) continue;
                            actualLine = line.Substring(4);
                        }
                        string[] gate = actualLine.Split('_');
                        string regionNames = gate[0] + "_" + gate[1];
                        gatesToAdd.Add(regionNames);
                        //
                    }
                    catch
                    {
                        Plugin.logger.LogError("Couldnt read gate " + line);
                    }
                }
            }
            AllGates = gatesToAdd.ToArray();
        }

        public static readonly string[] Depthable =
        {
            "Hazer",
            "VultureGrub",
            "SmallNeedleWorm",
            "TubeWorm",
            "SmallCentipede",
            "Snail",
            "LanternMouse",
        };

        public static readonly string[] Daemonable =
        {
            "Hazer",
            "VultureGrub",
            "Tardigrade",
        };

        public static readonly string[] Transportable =
        {
            "JetFish",
            "Hazer",
            "VultureGrub",
            "CicadaA",
            "CicadaB",
            "Yeek",
            "Tardigrade",
            "Frog",
            "Rat"
        };

        public static readonly string[] Pinnable =
        {
            "CicadaA",
            "CicadaB",
            "Scavenger",
            "BlackLizard",
            "PinkLizard",
            "BlueLizard",
            "YellowLizard",
            "WhiteLizard",
            "GreenLizard",
            "Salamander",
            "Dropbug",
            "Snail",
            "Centipede",
            "Centiwing",
            "LanternMouse"
        };

        public static readonly string[] BombableOutposts =
        {
            "SU_C02",
            "GW_C05",
            "GW_C11",
            "LF_E03",
            "OE_TOWER04",
            "UG_TOLL",
            "LC_C10",
            "LC_STRIPMALLNEW",
            "LC_TEMPLETOLL",
            "WARF_G01",
            "WBLA_F01",
            "WSKD_B41"
        };

        public static string NameForPearl(string pearl)
        {
            switch (pearl)
            {
                case "SU": return "Light Blue";
                case "UW": return "Pale Green";
                case "SH": return "Deep Magenta";
                case "LF_bottom": return "Bright Red";
                case "LF_west": return "Deep Pink";
                case "SL_moon": return "Pale Yellow";
                case "SL_chimney": return "Bright Magenta";
                case "SL_bridge": return "Bright Purple";
                case "DS": return "Bright Green";
                case "GW": return "Viridian";
                case "CC": return "Gold";
                case "HI": return "Bright Blue";
                case "SB_filtration": return "Teal";
                case "SB_ravine": return "Dark Magenta";
                case "SI_top": return "Dark Blue";
                case "SI_west": return "Dark Green";
                case "SI_chat3": return "Dark Purple";
                case "SI_chat4": return "Olive Green";
                case "SI_chat5": return "Dark Magenta";
                case "VS": return "Deep Purple";
                case "OE": return "Light Purple";
                case "DM": return "Light Yellow";
                case "LC": return "Deep Green";
                case "LC_second": return "Bronze";
                case "SU_filt": return "Light Pink";
                case "MS": return "Dull Yellow";
                case "WARG_AUDIO_GROOVE": return "audio";
                case "WSKD_AUDIO_JAM2": return "audio";
                case "WSKC_ABSTRACT": return "Dark Teal";
                case "WBLA_AUDIO_VOICEWIND1": return "audio";
                case "WARD_TEXT_STARDUST": return "Bright Viridian";
                case "WARE_AUDIO_VOICEWIND2": return "audio";
                case "WARB_TEXT_SECRET": return "Dark Purple";
                case "WARC_TEXT_CONTEMPT": return "Pale Pink";
                case "WPTA_DRONE": return "Beige";
                case "WRFB_AUDIO_JAM4": return "audio";
                case "WTDA_AUDIO_JAM1": return "audio";
                case "WTDB_AUDIO_JAM3": return "audio";
                case "WVWA_TEXT_KITESDAY": return "Amber";
                case "WMPA_TEXT_NOTIONOFSELF": return "Pale Viridian";
                case "WORA_WORA": return "Light Green";
                case "WAUA_WAUA": return "Orange";
                case "WAUA_TEXT_AUDIO_TALKSHOW": return "Light Magenta";
                default: return "Erm, pearl colored pearl";
            }
        }

        public static string NameForUnlock(string unlock)
        {
            // If it's a safari, get the region, if it's not a safari (arena), also gets the region
            string[] region = Regex.Split(unlock, "-");

            // safari or arena
            if (AllEnterableRegions.Contains(region[0]))
            {
                // safari
                if (region.Length > 1)
                {
                    return ChallengeTools.IGT.Translate("<region> Safari").Replace("<region>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(region[0], BingoData.slugcatPlayer)));
                }
                else
                {
                    return ChallengeTools.IGT.Translate("<region> Arenas").Replace("<region>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(region[0], BingoData.slugcatPlayer)));
                }
            }
            else
            {
                // Specials
                switch (unlock)
                {
                    case "filter": return ChallengeTools.IGT.Translate("Filtration System Arenas");
                    case "gutter": return ChallengeTools.IGT.Translate("Gutter Arenas");
                    case "GWold": return ChallengeTools.IGT.Translate("Past Garbage Wastes");
                    default: return ChallengeTools.IGT.Translate(unlock);
                }
            }
        }

        public static readonly string[] FoodTypes =
        {
            "DangleFruit",
            "EggBugEgg",
            "WaterNut",
            "SlimeMold",
            "JellyFish",
            "Mushroom",
            "SSOracleSwarmer",
            "FireSpriteLarva",

            // MSC
            "GooieDuck",
            "LillyPuck",
            "DandelionPeach",
            "GlowWeed",

            // Crits. Put DLC items after vulturegrub but before small centi so that smallcentipede remains the divider (customizerdialog)
            "VultureGrub",
            "Rat",
            "Tardigrade",
            "SandGrub",
            "Frog",
            "Barnacle",
            "Hazer",
            "SmallNeedleWorm",
            "Fly",
            "SmallCentipede",
        };

        public static readonly string[] Weapons =
        {
            "Any Weapon",
            "Spear",
            "Rock",
            "ScavengerBomb",
            "JellyFish",
            "PuffBall",
            "LillyPuck",
            "WaterNut",
            "Boomerang",
            "Frog",
            "GraffitiBomb"
        };

        public static readonly string[] StealableStolable =
        {
            "Spear",
            "Rock",
            "ScavengerBomb",
            "GraffitiBomb",
            "Boomerang",
            "Lantern",
            "GooieDuck",
            "GlowWeed",
            "DataPearl",
            "FirecrackerPlant",
            "SporePlant",
            "FlareBomb",
            "LillyPuck",
            "KarmaFlower",
            "PuffBall",
            "FlyLure"
        };

        public static readonly string[] Bannable =
        {
            "Lantern",
            "PuffBall",
            "VultureMask",
            "ScavengerBomb",
            "FirecrackerPlant",
            "BubbleGrass",
            "Rock",
            "DataPearl",
            "Boomerang",
            "GraffitiBomb"
        };

        public static readonly string[] Befriendable =
        {
            "GreenLizard",
            "PinkLizard",
            "Salamander",
            "YellowLizard",
            "BlackLizard",
            "CyanLizard",
            "WhiteLizard",
            "BlueLizard",
            "EelLizard",
            "SpitLizard",
            "ZoopLizard",
            "RedLizard",
            "PeachLizard",
            "IndigoLizard",
            "BlizzardLizard",
            "BasiliskLizard"
        };

        public static readonly string[] PomegranateRegions = { "Any Region", "WTDB", "WARC", "WVWB", "WPGA", "WRRA", "WTDA", "WVWA" };

        public static readonly string[] CollectablePearls =
        {
            "UW",
            "SH",
            "LF_bottom",
            "LF_west",
            "SL_moon",
            "SL_bridge",
            "SL_chimney",
            "DS",
            "CC",
            "GW",
            "HI",
            "SB_filtration",
            "SB_ravine",
            "SU",
            "SI_top",
            "SI_west",
            "SI_chat3",
            "SI_chat4",
            "SI_chat5",
            "VS",
            "OE",
            "LC",
            "DM",
            "LC_second",
            "SU_filt",
            "MS",
            "WORA_WORA",
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
            "WVWA_TEXT_KITESDAY",
        };

        public static readonly string[] CraftableItems =
        {
            "FlareBomb",
            "SporePlant",
            "ScavengerBomb",
            "JellyFish",
            "DataPearl",
            "BubbleGrass",
            "FlyLure",
            "SlimeMold",
            "FirecrackerPlant",
            "PuffBall",
            "Mushroom",
            "Lantern",
            "GlowWeed",
            "GooieDuck",
            "FireEgg",
            "KarmaFlower"
        };

        public static readonly string[] Storable =
        {
            "FirecrackerPlant",
            "SporePlant",
            "FlareBomb",
            "FlyLure",
            "JellyFish",
            "Lantern",
            "Mushroom",
            "PuffBall",
            "ScavengerBomb",
            "VultureMask",
            "DangleFruit",
            "SlimeMold",
            "BubbleGrass",
            "EggBugEgg",
            "KarmaFlower",

            // MSC
            "GooieDuck",
            "LillyPuck",
            "DandelionPeach",

            // watcher
            "FireSpriteLarva",
            "GraffitiBomb",
            "Boomerang",
        };

        public static void PopulateWatcherData()
        {
            watcherRegions = SlugcatStats.SlugcatStoryRegions(WatcherEnums.SlugcatStatsName.Watcher).Select(r => r.ToLowerInvariant()).ToList();
            List<string> rawPortals = new List<string>();
            List<string> rawSTSpots = new List<string>();
            List<string> rawDWTSpots = new List<string>();

            foreach (var region in watcherRegions)
            {
                if (Custom.rainWorld.regionWarpRooms.ContainsKey(region))
                {
                    foreach (var warp in Custom.rainWorld.regionWarpRooms[region])
                    {
                        rawPortals.Add(warp);
                    }
                }
                if (Custom.rainWorld.regionSpinningTopRooms.ContainsKey(region))
                {
                    foreach (var st in Custom.rainWorld.regionSpinningTopRooms[region])
                    {
                        rawSTSpots.Add(st);
                    }
                }
                if (Custom.rainWorld.regionDynamicWarpTargets.ContainsKey(region))
                {
                    foreach (var dt in Custom.rainWorld.regionDynamicWarpTargets[region])
                    {
                        rawDWTSpots.Add(dt);
                    }
                }
            }

            watcherPortals = new List<string>();
            foreach (var line in rawPortals)
            {
                var parts = line.Split(':');
                if (parts.Length < 4) continue;

                string origin = parts[0].ToUpperInvariant();
                string dest = parts[3].ToUpperInvariant();

                var ordered = new[] { origin, dest };
                string portalKey = $"{ordered[0]}-{ordered[1]}".ToUpperInvariant();

                if (!watcherPortals.Contains(portalKey))
                {
                    watcherPortals.Add(portalKey);
                }
            }

            watcherSTSpots = new List<string>();
            foreach (var line in rawSTSpots)
            {
                var parts = line.Split(':');
                if (parts.Length < 3) continue;

                string origin = parts[0].ToUpperInvariant();
                string dest = parts[2].ToUpperInvariant();

                var ordered = new[] { origin, dest };
                string STKey = $"{ordered[0]}-{ordered[1]}".ToUpperInvariant();

                if (!watcherSTSpots.Contains(STKey))
                {
                    watcherSTSpots.Add(STKey);
                }
            }

            watcherDWTSpots = new List<string>();
            foreach (var line in rawDWTSpots)
            {
                var parts = line.Split(':');
                string DWTKey = parts[0].ToUpperInvariant();

                if (!watcherDWTSpots.Contains(DWTKey))
                {
                    watcherDWTSpots.Add(DWTKey);
                }
            }
        }
    }
}
