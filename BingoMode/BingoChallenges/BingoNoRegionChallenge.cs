using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoNoRegionRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> region;

        public override Challenge Random()
        {
            BingoNoRegionChallenge challenge = new();
            challenge.region.Value = region.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}region-{region.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "NoRegion").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            region = Randomizer<string>.InitDeserialize(dict["region"]);
        }
    }

    public class BingoNoRegionChallenge : BingoChallenge
    {
        public SettingBox<string> region;

        public BingoNoRegionChallenge()
        {
            region = new("", "Region", 0, listName: "regionsreal");
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Do not enter <region>")
                .Replace("<region>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(region.Value, ExpeditionData.slugcatPlayer)));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase([[new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "buttonCrossB" : "buttonCrossA", 1f, Color.red), new Verse(region.Value)]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoNoRegionChallenge c || c.region.Value != region.Value;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Avoiding a region");
        }

        public override Challenge Generate()
        {
            string[] regiones = ChallengeUtils.GetCorrectListForChallenge("regionsreal", true);

            BingoNoRegionChallenge ch = new BingoNoRegionChallenge
            {
                region = new(regiones[UnityEngine.Random.Range(0, regiones.Length)], "Region", 0, listName: "regionsreal")
            };

            return ch;
        }

        public override bool RequireSave() => false;
        public override bool ReverseChallenge () => true;

        public void Entered(string regionName)
        {
            if (completed && region.Value == regionName && !TeamsFailed[SteamTest.team])
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
                "BingoNoRegionChallenge",
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
                ExpLog.Log("ERROR: BingoNoRegionChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.WorldLoader.ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues += WorldLoader_NoRegion;
        }

        public override void RemoveHooks()
        {
            On.WorldLoader.ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues -= WorldLoader_NoRegion;
        }

        public override List<object> Settings() => [region];
    }
}