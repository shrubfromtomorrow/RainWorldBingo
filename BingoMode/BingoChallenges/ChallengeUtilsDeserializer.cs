using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BingoMode.BingoChallenges
{
    public static class ChallengeUtilsDeserializer
    {
        public static Dictionary<string, string> Parse(string id, string args)
        {
            if (!Classes.TryGetValue(id, out var versions))
                throw new InvalidOperationException($"No definition registered for {id}");

            string[] segments = Regex.Split(args, "><");

            var (_, parse) = versions.FirstOrDefault(v => v.Matches(segments));

            if (parse is null)
                throw new InvalidOperationException($"No version matched {id} with {segments.Length} segments");

            var fields = new Dictionary<string, string>();
            parse(segments, fields);
            return fields;
        }

        public static readonly Dictionary<string, List<(Func<string[], bool> Matches, Action<string[], Dictionary<string, string>> Parse)>> Classes = new()
        {
            {
                ChallengeNameConstants.Achievement,
                [
                    (
                        Matches: segs => segs.Length == 3,
                        Parse: (segs, fields) =>
                        {
                            var p = segs[0].Split('|');
                            p[p.Length - 1] = ChallengeListConstants.Passage; // Old boards using "Wpassage"
                            fields["ID"] = string.Join("|", p);
                            fields["Completed"] = segs[1];
                            fields["Revealed"] = segs[2];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.Toll,
                [
                    (
                        Matches: segs => segs.Length == 4,
                        Parse: (segs, fields) =>
                        {
                            var p = segs[0].Split('|');
                            p[1] = p[1].ToUpperInvariant();

                            fields["RoomName"] = string.Join("|", p);
                            fields["Pass"] = segs[1];
                            fields["Completed"] = segs[2];
                            fields["Revealed"] = segs[3];

                            fields["Specific"] = "System.Boolean|true|Specific toll|0|NULL";
                            fields["Current"] = "0";
                            fields["Amount"] = "System.Int32|3|Amount|1|NULL";
                            fields["Bombed"] = "empty";
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 8,
                        Parse: (segs, fields) =>
                        {
                            var p = segs[1].Split('|');
                            p[1] = p[1].ToUpperInvariant();
                            p[p.Length - 1] = ChallengeListConstants.Tolls;

                            fields["Specific"] = segs[0];
                            fields["RoomName"] = string.Join("|", p);
                            fields["Pass"] = segs[2];
                            fields["Current"] = segs[3];
                            fields["Amount"] = segs[4];
                            fields["Bombed"] = segs[5];
                            fields["Completed"] = segs[6];
                            fields["Revealed"] = segs[7];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.Damage,
                [
                    (
                        Matches: segs => segs.Length == 6,
                        Parse: (segs, fields) =>
                        {
                            fields["Weapon"] = segs[0];
                            fields["Victim"] = segs[1];
                            fields["Current"] = segs[2];
                            fields["Amount"] = segs[3];
                            fields["OneCycle"] = "System.Boolean|false|In One Cycle|3|NULL";
                            fields["Region"] = "System.String|Any Region|Region|4|" + ChallengeListConstants.Regions;
                            fields["Completed"] = segs[4];
                            fields["Revealed"] = segs[5];
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 8,
                        Parse: (segs, fields) =>
                        {
                            fields["Weapon"] = segs[0];
                            fields["Victim"] = segs[1];
                            fields["Current"] = segs[2];
                            fields["Amount"] = segs[3];
                            fields["OneCycle"] = segs[4];
                            fields["Region"] = segs[5];
                            fields["Completed"] = segs[6];
                            fields["Revealed"] = segs[7];
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 9,
                        Parse: (segs, fields) =>
                        {
                            fields["Weapon"] = segs[0];
                            fields["Victim"] = segs[1];
                            fields["Current"] = segs[2];
                            fields["Amount"] = segs[3];
                            fields["OneCycle"] = segs[4];
                            fields["Region"] = segs[5];
                            fields["Completed"] = segs[7];
                            fields["Revealed"] = segs[8];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.DontUseItem,
                [
                    (
                        Matches: segs => segs.Length == 5,
                        Parse: (segs, fields) =>
                        {
                            var p = segs[0].Split('|');
                            p[p.Length - 1] = ChallengeListConstants.BanItem; // Old boards using "Wbanitem"
                            fields["Item"] = string.Join("|", p);
                            fields["IsFood"] = segs[1];
                            fields["Completed"] = segs[2];
                            fields["Revealed"] = segs[3];
                            fields["IsCreature"] = segs[4];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.Eat,
                [
                    (
                        Matches: segs => segs.Length == 6,
                        Parse: (segs, fields) =>
                        {
                            fields["Amount"] = segs[0];
                            fields["Current"] = segs[1];
                            fields["IsCreature"] = segs[2];
                            fields["FoodType"] = segs[3];
                            fields["Starve"] = "System.Boolean|false|While Starving|2|NULL";
                            fields["Completed"] = segs[4];
                            fields["Revealed"] = segs[5];
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 7,
                        Parse: (segs, fields) =>
                        {
                            fields["Amount"] = segs[0];
                            fields["Current"] = segs[1];
                            fields["IsCreature"] = segs[2];
                            var p = segs[3].Split('|');
                            p[p.Length - 1] = ChallengeListConstants.Food; // Old boards using "Wfood"
                            fields["FoodType"] = string.Join("|", p);
                            fields["Starve"] = segs[4];
                            fields["Completed"] = segs[5];
                            fields["Revealed"] = segs[6];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.Echo,
                [
                    (
                        Matches: segs => segs.Length == 4,
                        Parse: (segs, fields) =>
                        {
                            fields["Specific"] = "System.Boolean|true|Specific Echo|0|NULL";
                            fields["Ghost"] = segs[0];
                            fields["Starve"] = segs[1];
                            fields["Current"] = "0";
                            fields["Amount"] = "System.Int32|3|Amount|1|NULL";
                            fields["Completed"] = segs[2];
                            fields["Revealed"] = segs[3];
                            fields["Visited"] = "";
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 8,
                        Parse: (segs, fields) =>
                        {
                            fields["Specific"] = segs[0];
                            fields["Ghost"] = segs[1];
                            fields["Starve"] = segs[2];
                            fields["Current"] = segs[3];
                            fields["Amount"] = segs[4];
                            fields["Completed"] = segs[5];
                            fields["Revealed"] = segs[6];
                            fields["Visited"] = segs[7];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.HatchNoodle,
                [
                    (
                        Matches: segs => segs.Length == 5,
                        Parse: (segs, fields) =>
                        {
                            fields["Region"] = "System.String|Any Region|Region|1|" + ChallengeListConstants.NootRegions;
                            fields["DifferentRegions"] = "System.Boolean|false|Different Regions|2|NULL";
                            fields["OneCycle"] = segs[2];
                            fields["Current"] = segs[0];
                            fields["Amount"] = segs[1];
                            fields["HatchRegions"] = "";
                            fields["Completed"] = segs[3];
                            fields["Revealed"] = segs[4];
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 8,
                        Parse: (segs, fields) =>
                        {
                            var p = segs[0].Split('|');
                            p[p.Length - 1] = ChallengeListConstants.NootRegions; // Old boards using "regions"
                            fields["Region"] = string.Join("|", p);
                            fields["DifferentRegions"] = segs[1];
                            fields["OneCycle"] = segs[2];
                            fields["Current"] = segs[3];
                            fields["Amount"] = segs[4];
                            fields["HatchRegions"] = segs[5];
                            fields["Completed"] = segs[6];
                            fields["Revealed"] = segs[7];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.ItemHoard,
                [
                    (
                        Matches: segs => segs.Length == 4,
                        Parse: (segs, fields) =>
                        {
                            fields["AnyShelter"] = "System.Boolean|false|Any Shelter|2|NULL";
                            fields["Current"] = "0";
                            fields["Amount"] = segs[0];
                            fields["Target"] = segs[1];
                            fields["Region"] = "System.String|Any Region|Region|4|" + ChallengeListConstants.Regions;
                            fields["Completed"] = segs[2];
                            fields["Revealed"] = segs[3];
                            fields["Collected"] = "";
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 7,
                        Parse: (segs, fields) =>
                        {
                            fields["AnyShelter"] = segs[0];
                            fields["Current"] = segs[1];
                            fields["Amount"] = segs[2];
                            fields["Target"] = segs[3];
                            fields["Region"] = "System.String|Any Region|Region|4|" + ChallengeListConstants.Regions;
                            fields["Completed"] = segs[4];
                            fields["Revealed"] = segs[5];
                            fields["Collected"] = segs[6];
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 8,
                        Parse: (segs, fields) =>
                        {
                            fields["AnyShelter"] = segs[0];
                            fields["Current"] = segs[1];
                            fields["Amount"] = segs[2];
                            fields["Target"] = segs[3];
                            fields["Region"] = segs[4];
                            fields["Completed"] = segs[5];
                            fields["Revealed"] = segs[6];
                            fields["Collected"] = segs[7];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.KarmaFlower,
                [
                    (
                        Matches: segs => segs.Length == 4,
                        Parse: (segs, fields) =>
                        {
                            fields["Region"] = "System.String|Any Region|Region|1|" + ChallengeListConstants.Regions;
                            fields["DifferentRegions"] = "System.Boolean|false|Different Regions|2|NULL";
                            fields["OneCycle"] = "System.Boolean|false|In one Cycle|3|NULL";
                            fields["Current"] = segs[0];
                            fields["Amount"] = segs[1];
                            fields["EatRegions"] = "";
                            fields["Completed"] = segs[2];
                            fields["Revealed"] = segs[3];
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 8,
                        Parse: (segs, fields) =>
                        {
                            fields["Region"] = segs[0];
                            fields["DifferentRegions"] = segs[1];
                            fields["OneCycle"] = segs[2];
                            fields["Current"] = segs[3];
                            fields["Amount"] = segs[4];
                            fields["EatRegions"] = segs[5];
                            fields["Completed"] = segs[6];
                            fields["Revealed"] = segs[7];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.Kill,
                [
                    (
                        Matches: segs => segs.Length == 11 && !segs[8].Contains("mushroom"),
                        Parse: (segs, fields) =>
                        {
                            fields["Crit"] = segs[0];
                            fields["Weapon"] = segs[1];
                            fields["Amount"] = segs[2];
                            fields["Current"] = segs[3];
                            fields["Region"] = segs[4];
                            fields["OneCycle"] = segs[6];
                            fields["DeathPit"] = segs[7];
                            fields["Starve"] = segs[8];
                            fields["Shrooms"] = "System.Boolean|false|While under mushroom effect|8|NULL";
                            fields["Completed"] = segs[9];
                            fields["Revealed"] = segs[10];
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 11 && segs[8].Contains("mushroom"),
                        Parse: (segs, fields) =>
                        {
                            fields["Crit"] = segs[0];
                            fields["Weapon"] = segs[1];
                            fields["Amount"] = segs[2];
                            fields["Current"] = segs[3];
                            fields["Region"] = segs[4];
                            fields["OneCycle"] = segs[5];
                            fields["DeathPit"] = segs[6];
                            fields["Starve"] = segs[7];
                            fields["Shrooms"] = segs[8];
                            fields["Completed"] = segs[9];
                            fields["Revealed"] = segs[10];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.PearlHoard,
                [
                    (
                        Matches: segs => segs.Length == 5,
                        Parse: (segs, fields) =>
                        {
                            fields["Common"] = segs[0];
                            fields["AnyShelter"] = "System.Boolean|false|Any Shelter|2|NULL";
                            fields["Current"] = "0";
                            fields["Amount"] = segs[1];
                            fields["Region"] = segs[2];
                            fields["Completed"] = segs[3];
                            fields["Revealed"] = segs[4];
                            fields["Collected"] = "";
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 8,
                        Parse: (segs, fields) =>
                        {
                            fields["Common"] = segs[0];
                            fields["AnyShelter"] = segs[1];
                            fields["Current"] = segs[2];
                            fields["Amount"] = segs[3];
                            fields["Region"] = segs[4];
                            fields["Completed"] = segs[5];
                            fields["Revealed"] = segs[6];
                            fields["Collected"] = segs[7];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.Popcorn,
                [
                    (
                        Matches: segs => segs.Length == 4,
                        Parse: (segs, fields) =>
                        {
                            fields["Region"] = "System.String|Any Region|Region|1|" + ChallengeListConstants.PopcornRegions;
                            fields["DifferentRegions"] = "System.Boolean|false|Different Regions|2|NULL";
                            fields["OneCycle"] = "System.Boolean|false|In one Cycle|3|NULL";
                            fields["Current"] = segs[0];
                            fields["Amount"] = segs[1];
                            fields["PopRegions"] = "";
                            fields["Completed"] = segs[2];
                            fields["Revealed"] = segs[3];
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 8,
                        Parse: (segs, fields) =>
                        {
                            var p = segs[0].Split('|');
                            p[p.Length - 1] = ChallengeListConstants.PopcornRegions; // Old boards using "popcornRegions"
                            fields["Region"] = string.Join("|", p);
                            fields["DifferentRegions"] = segs[1];
                            fields["OneCycle"] = segs[2];
                            fields["Current"] = segs[3];
                            fields["Amount"] = segs[4];
                            fields["PopRegions"] = segs[5];
                            fields["Completed"] = segs[6];
                            fields["Revealed"] = segs[7];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.Score,
                [
                    (
                        Matches: segs => segs.Length == 3,
                        Parse: (segs, fields) =>
                        {
                            fields["Score"] = "0";
                            fields["Target"] = segs[0];
                            fields["OneCycle"] = "System.Boolean|true|In one Cycle|1|NULL";
                            fields["Completed"] = segs[1];
                            fields["Revealed"] = segs[2];
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 4,
                        Parse: (segs, fields) =>
                        {
                            fields["Score"] = segs[0];
                            fields["Target"] = segs[1];
                            fields["OneCycle"] = "System.Boolean|false|In one Cycle|1|NULL";
                            fields["Completed"] = segs[2];
                            fields["Revealed"] = segs[3];
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 5,
                        Parse: (segs, fields) =>
                        {
                            fields["Score"] = segs[0];
                            fields["Target"] = segs[1];
                            fields["OneCycle"] = segs[2];
                            fields["Completed"] = segs[3];
                            fields["Revealed"] = segs[4];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.Steal,
                [
                    (
                        Matches: segs => segs.Length == 6,
                        Parse: (segs, fields) =>
                        {
                            var p = segs[0].Split('|');
                            p[p.Length - 1] = ChallengeListConstants.Theft; // Old boards using "Wtheft"
                            fields["Subject"] = string.Join("|", p);
                            fields["Toll"] = segs[1];
                            fields["Current"] = segs[2];
                            fields["Amount"] = segs[3];
                            fields["Completed"] = segs[4];
                            fields["Revealed"] = segs[5];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.Tame,
                [
                    (
                        Matches: segs => segs.Length == 3,
                        Parse: (segs, fields) =>
                        {
                            fields["Specific"] = "System.Boolean|true|Specific Creature Type|0|NULL";
                            fields["Crit"] = segs[0];
                            fields["Current"] = "0";
                            fields["Amount"] = "System.Int32|1|Amount|3|NULL";
                            fields["Completed"] = segs[1];
                            fields["Revealed"] = segs[2];
                            fields["TamedTypes"] = "";
                            fields["TamedIDs"] = "";
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 7,
                        Parse: (segs, fields) =>
                        {
                            fields["Specific"] = segs[0];
                            var p = segs[1].Split('|');
                            p[p.Length - 1] = ChallengeListConstants.Friend; // Old boards using "Wfriend"
                            fields["Crit"] = string.Join("|", p);
                            fields["Current"] = segs[2];
                            fields["Amount"] = segs[0].Contains("true") ? "System.Int32|1|Amount|2|NULL" : segs[3];
                            fields["Completed"] = segs[4];
                            fields["Revealed"] = segs[5];
                            fields["TamedTypes"] = segs[6];
                            fields["TamedIDs"] = "";
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 8,
                        Parse: (segs, fields) =>
                        {
                            fields["Specific"] = segs[0];
                            var p = segs[1].Split('|');
                            p[p.Length - 1] = ChallengeListConstants.Friend; // Old boards using "Wfriend"
                            fields["Crit"] = string.Join("|", p);
                            fields["Current"] = segs[2];
                            fields["Amount"] = segs[3];
                            fields["Completed"] = segs[4];
                            fields["Revealed"] = segs[5];
                            fields["TamedTypes"] = segs[6];
                            fields["TamedIDs"] = segs[7];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.CollectRippleSpawn,
                [
                    (
                        Matches: segs => segs.Length == 4,
                        Parse: (segs, fields) =>
                        {
                            fields["Current"] = segs[0];
                            fields["Amount"] = segs[1];
                            fields["OneCycle"] = "System.Boolean|false|In one Cycle|1|NULL";
                            fields["Completed"] = segs[2];
                            fields["Revealed"] = segs[3];
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 5,
                        Parse: (segs, fields) =>
                        {
                            fields["Current"] = segs[0];
                            fields["Amount"] = segs[1];
                            fields["OneCycle"] = segs[2];
                            fields["Completed"] = segs[3];
                            fields["Revealed"] = segs[4];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.CreaturePortal,
                [
                    (
                        Matches: segs => segs.Length == 6,
                        Parse: (segs, fields) =>
                        {
                            var p = segs[0].Split('|');
                            p[p.Length - 1] = ChallengeListConstants.Transport; // Old boards using "Wtransport"
                            fields["Crit"] = string.Join("|", p);
                            fields["Current"] = segs[1];
                            fields["Amount"] = segs[2];
                            fields["CreaturePortals"] = segs[3];
                            fields["Completed"] = segs[4];
                            fields["Revealed"] = segs[5];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.OpenMelons,
                [
                    (
                        Matches: segs => segs.Length == 4,
                        Parse: (segs, fields) =>
                        {
                            fields["Region"] = "System.String|Any Region|Region|1|" + ChallengeListConstants.PomegranateRegions;
                            fields["DifferentRegions"] = "System.Boolean|false|Different Regions|2|NULL";
                            fields["OneCycle"] = "System.Boolean|false|In one Cycle|3|NULL";
                            fields["Current"] = segs[0];
                            fields["Amount"] = segs[1];
                            fields["OpenRegions"] = "";
                            fields["Completed"] = segs[2];
                            fields["Revealed"] = segs[3];
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 5,
                        Parse: (segs, fields) =>
                        {
                            fields["Region"] = "System.String|Any Region|Region|1|" + ChallengeListConstants.PomegranateRegions;
                            fields["DifferentRegions"] = "System.Boolean|false|Different Regions|2|NULL";
                            fields["OneCycle"] = segs[2];
                            fields["Current"] = segs[0];
                            fields["Amount"] = segs[1];
                            fields["OpenRegions"] = "";
                            fields["Completed"] = segs[3];
                            fields["Revealed"] = segs[4];
                        }
                    ),
                    (
                        Matches: segs => segs.Length == 8,
                        Parse: (segs, fields) =>
                        {
                            var p = segs[0].Split('|');
                            p[p.Length - 1] = ChallengeListConstants.PomegranateRegions; // Old boards using "Wpoms"
                            fields["Region"] = string.Join("|", p);
                            fields["DifferentRegions"] = segs[1];
                            fields["OneCycle"] = segs[2];
                            fields["Current"] = segs[3];
                            fields["Amount"] = segs[4];
                            fields["OpenRegions"] = segs[5];
                            fields["Completed"] = segs[6];
                            fields["Revealed"] = segs[7];
                        }
                    ),
                ]
            },
            {
                ChallengeNameConstants.Weaver,
                [
                    (
                        Matches: segs => segs.Length == 4,
                        Parse: (segs, fields) =>
                        {
                            fields["Region"] = segs[0];
                            var p = segs[1].Split('|');
                            p[p.Length - 1] = ChallengeListConstants.WeaverRooms; // Old boards using "WweaverRooms"
                            fields["Room"] = string.Join("|", p);
                            fields["Completed"] = segs[2];
                            fields["Revealed"] = segs[3];
                        }
                    ),
                ]
            }
        };
    }
}