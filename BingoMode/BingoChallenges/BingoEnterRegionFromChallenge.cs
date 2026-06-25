using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Watcher;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoEnterRegionFromRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> from;
        public Randomizer<string> to;

        public override Challenge Random()
        {
            BingoEnterRegionFromChallenge challenge = new();
            challenge.from.Value = from.Random();
            challenge.to.Value = to.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}from-{from.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}to-{to.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "EnterRegionFrom").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            from = Randomizer<string>.InitDeserialize(dict["from"]);
            to = Randomizer<string>.InitDeserialize(dict["to"]);
        }
    }

    public class BingoEnterRegionFromChallenge : BingoChallenge
    {
        public SettingBox<string> from;
        public SettingBox<string> to;

        public BingoEnterRegionFromChallenge()
        {
            from = new("", "From", 0, listName: "regionsreal");
            to = new("", "To", 0, listName: "regionsreal");
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("First time entering <to> must be from <from>")
                .Replace("<to>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(to.Value, ExpeditionData.slugcatPlayer)))
                .Replace("<from>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(from.Value, ExpeditionData.slugcatPlayer)));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            //return new Phrase(
            //    [[new Verse(from.Value)],
            //    [new Icon("keyShiftA", 1f, new Color(66f / 255f, 135f / 255f, 1f), 180f)],
            //    [new Verse(to.Value)]]);
            return new Phrase([[new Verse(from.Value), new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, new Color(66f / 255f, 135f / 255f, 1f), 90f), new Verse(to.Value)]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoEnterRegionFromChallenge;// c || (c.to.Value != to.Value && c.from.Value != from.Value);
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Entering a region from another region");
        }

        public string FixSlugSpecificRegions(string gateName)
        {
            string[] regions = ChallengeUtils.GetCorrectListForChallenge("regionsreal", true);
            if (regions.Contains("UG") && gateName.Contains("DS"))
            {
                gateName = gateName.Replace("DS", "UG");
            }
            if (regions.Contains("CL") && gateName.Contains("SH"))
            {
                gateName = gateName.Replace("SH", "CL");
                gateName = gateName.Replace("UW", "SH");
            }
            //if (regions.Contains("RM") && gateName.Contains("SS"))
            //{
            //    gateName = gateName.Replace("SS", "RM");
            //}
            if (regions.Contains("LM") && gateName.Contains("SL"))
            {
                gateName = gateName.Replace("SL", "LM");
            }
            return gateName;
        }

        public override Challenge Generate()
        {
            List<string> gates = [.. ChallengeUtils.AllGates.ToArray()];

            if (ExpeditionData.slugcatPlayer.value == "Saint") gates.RemoveAll(x => x.Contains("UW"));

            string gateName = gates[UnityEngine.Random.Range(0, gates.Count)];
            gateName = FixSlugSpecificRegions(gateName);            

            string[] regiones = gateName.Split('_');

            BingoEnterRegionFromChallenge ch = new BingoEnterRegionFromChallenge
            {
                from = new(regiones[0], "From", 0, listName: "regionsreal"),
                to = new(regiones[1], "To", 0, listName: "regionsreal")
            };

            return ch;
        }

        public void Gate(string fromWorld, string toWorld)
        {
            if (completed || TeamsCompleted[SteamTest.team] || hidden || revealed || TeamsFailed[SteamTest.team]) return;

            fromWorld = FixSlugSpecificRegions(fromWorld).ToUpperInvariant();
            toWorld = FixSlugSpecificRegions(toWorld).ToUpperInvariant();

            string fromValue = from.Value.ToUpperInvariant();
            string toValue = to.Value.ToUpperInvariant();

            bool prevCheck = fromWorld == from.Value.ToUpperInvariant();
            bool newCheck = toWorld == to.Value.ToUpperInvariant();

            if (prevCheck && newCheck)
            {
                CompleteChallenge();
                return;
            }

            if (newCheck)
            {
                FailChallenge(SteamTest.team);
            }
        }

        public override int Points()
        {
            return 20;
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return true;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoEnterRegionFromChallenge",
                "~",
                from.ToString(),
                "><",
                to.ToString(),
                "><",
                completed ? "1" : "0",
                "><",
                revealed ? "1" : "0"
            });
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                from = SettingBoxFromString(array[0]) as SettingBox<string>;
                to = SettingBoxFromString(array[1]) as SettingBox<string>;
                completed = (array[2] == "1");
                revealed = (array[3] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoEnterRegionFromChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.WorldLoader.ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues += WorldLoader_EnterRegionFrom;
        }

        public override void RemoveHooks()
        {
            On.WorldLoader.ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues -= WorldLoader_EnterRegionFrom;
        }

        public override List<object> Settings() => [from, to];
    }
}