using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Watcher;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoEnterRegionRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> region;

        public override Challenge Random()
        {
            BingoEnterRegionChallenge challenge = new();
            challenge.region.Value = region.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}region-{region.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "EnterRegion").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            region = Randomizer<string>.InitDeserialize(dict["region"]);
        }
    }

    public class BingoEnterRegionChallenge : BingoChallenge
    {
        public SettingBox<string> region;

        public BingoEnterRegionChallenge()
        {
            region = new("", "Region", 0, listName: "regionsreal");
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Enter <region>")
                .Replace("<region>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(region.Value, ExpeditionData.slugcatPlayer)));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase([[new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.green, 90), new Verse(region.Value)]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return (challenge is not BingoEnterRegionChallenge c || c.region.Value != region.Value) && 
                (challenge is not BingoNoRegionChallenge ch || ch.region.Value != region.Value);
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Entering a region");
        }

        public override Challenge Generate()
        {
            string[] regiones = ChallengeUtils.GetCorrectListForChallenge("regionsreal", true);

            BingoEnterRegionChallenge ch = new BingoEnterRegionChallenge
            {
                region = new(regiones[UnityEngine.Random.Range(0, regiones.Length)], "Region", 0, listName: "regionsreal")
            };

            return ch;
        }

        public void Entered(string regionName)
        {
            if (completed || SteamTest.team == 8 || TeamsCompleted[SteamTest.team] || hidden || revealed || regionName != region.Value) return;
            CompleteChallenge();
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
                "BingoEnterRegionChallenge",
                "~",
                region.ToString(),
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
                region = SettingBoxFromString(array[0]) as SettingBox<string>;
                completed = (array[1] == "1");
                revealed = (array[2] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoEnterRegionChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.WorldLoader.ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues += WorldLoader_EnterRegion;
        }

        public override void RemoveHooks()
        {
            On.WorldLoader.ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues -= WorldLoader_EnterRegion;
        }

        public override List<object> Settings() => [region];
    }
}