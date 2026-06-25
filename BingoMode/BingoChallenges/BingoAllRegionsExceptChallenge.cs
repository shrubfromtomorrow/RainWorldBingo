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
    public class BingoAllRegionsExceptRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> region;
        public Randomizer<int> required;

        public override Challenge Random()
        {
            BingoAllRegionsExceptChallenge challenge = new();
            challenge.region.Value = region.Random();
            challenge.required.Value = required.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}region-{region.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}required-{required.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "AllRegionExcept").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            region = Randomizer<string>.InitDeserialize(dict["region"]);
            required = Randomizer<int>.InitDeserialize(dict["required"]);
        }
    }
    public class BingoAllRegionsExceptChallenge : BingoChallenge
    {
        public SettingBox<string> region;
        public SettingBox<int> required;
        public List<string> regionsToEnter = [];
        public int current;

        public BingoAllRegionsExceptChallenge()
        {
            region = new("", "Region", 0, listName: "regionsreal");
            regionsToEnter = [.. ChallengeUtils.AllEnterableRegions];
            required = new(0, "Amount", 1);
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Enter [<current>/<required>] regions without entering <region>")
                .Replace("<region>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(region.Value, ExpeditionData.slugcatPlayer)))
                .Replace("<required>", required.Value.ToString()).Replace("<current>", current.ToString());
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase(
                [[new Icon("TravellerA"), new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "buttonCrossB" : "buttonCrossA", 1f, Color.red)],
                [new Verse(region.Value)], 
                [new Counter(current, required.Value)]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoAllRegionsExceptChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Entering regions without visiting one");
        }

        public override void Reset()
        {
            base.Reset();
            regionsToEnter = ChallengeUtils.GetCorrectListForChallenge("regionsreal", true).ToList();
        }

        public override Challenge Generate()
        {
            List<string> regiones = ChallengeUtils.GetCorrectListForChallenge("regionsreal", true).ToList();
            string regionn = regiones[UnityEngine.Random.Range(0, regiones.Count)];
            int req = UnityEngine.Random.Range(3, regiones.Count - 4);

            return new BingoAllRegionsExceptChallenge
            {
                region = new(regionn, "Region", 0, listName: "regionsreal"),
                regionsToEnter = ChallengeUtils.AllEnterableRegions.ToList(),
                required = new(req, "Amount", 1)
            };
        }

        public void Entered(string regionName)
        {
            if (SteamTest.team == 8 || hidden || revealed || completed || TeamsCompleted[SteamTest.team] || TeamsFailed[SteamTest.team]) return;
            if (region.Value == regionName)
            {
                FailChallenge(SteamTest.team);
                return;
            }
            else if (!TeamsFailed[SteamTest.team] && regionsToEnter.Contains(regionName.ToUpperInvariant()))
            {
                regionsToEnter.Remove(regionName);

                current++;
                UpdateDescription();
                if (current >= required.Value)
                {
                    CompleteChallenge();
                }
                else
                {
                    ChangeValue();
                }
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
                "BingoAllRegionsExceptChallenge",
                "~",
                region.ToString(),
                "><",
                string.Join("|", regionsToEnter),
                "><",
                current.ToString(),
                "><",
                required.ToString(),
                "><",
                completed ? "1" : "0",
                "><",
                revealed ? "1" : "0",
            });
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                region = SettingBoxFromString(array[0]) as SettingBox<string>;
                regionsToEnter = [.. array[1].Split('|')];
                current = int.Parse(array[2], System.Globalization.NumberStyles.Any);
                required = SettingBoxFromString(array[3]) as SettingBox<int>;
                completed = (array[4] == "1");
                revealed = (array[5] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoAllRegionsExceptChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.WorldLoader.ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues += WorldLoader_AllRegionsExcept;
        }

        public override void RemoveHooks()
        {
            On.WorldLoader.ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues -= WorldLoader_AllRegionsExcept;
        }

        public override List<object> Settings() => [region, required];
    }
}