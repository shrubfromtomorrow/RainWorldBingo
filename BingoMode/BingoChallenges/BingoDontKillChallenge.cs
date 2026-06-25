using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using CreatureType = CreatureTemplate.Type;


namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoDontKillRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> victim;

        public override Challenge Random()
        {
            BingoDontKillChallenge challenge = new();
            challenge.victim.Value = victim.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}victim-{victim.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "DontKill").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            victim = Randomizer<string>.InitDeserialize(dict["victim"]);
        }
    }

    public class BingoDontKillChallenge : BingoChallenge
    {
        public SettingBox<string> victim;

        public BingoDontKillChallenge()
        {
            victim = new("", "Creature Type", 0, listName: "creatures");
        }

        public override void UpdateDescription()
        {
            if (ChallengeTools.creatureNames == null)
            {
                ChallengeTools.CreatureName(ref ChallengeTools.creatureNames);
            }
            string newValue = "Unknown";
            try
            {
                int indexe = new CreatureType(victim.Value).index;
                if (indexe >= 0)
                {
                    newValue = ChallengeTools.IGT.Translate(ChallengeTools.creatureNames[indexe]);
                }
            }
            catch (Exception ex)
            {
                ExpLog.Log("Error getting creature name for BingoDontKillChallenge | " + ex.Message);
            }
            this.description = ChallengeTools.IGT.Translate("Never kill <victim>")
                .Replace("<victim>", victim.Value != "Any Creature" ? newValue : "a creature");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new Phrase([[new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "buttonCrossB" : "buttonCrossA", 1f, Color.red), new Icon("Multiplayer_Bones")]]);
            if (victim.Value != "Any Creature") phrase.InsertWord(Icon.FromEntityName(victim.Value));
            return phrase;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoDontKillChallenge c || (c.victim.Value != victim.Value);
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Avoiding killing creatures");
        }

        public override Challenge Generate()
        {
            float diff = UnityEngine.Random.value;
            ChallengeTools.ExpeditionCreature expeditionCreature = ChallengeTools.GetExpeditionCreature(ExpeditionData.slugcatPlayer, diff);
            return new BingoDontKillChallenge
            {
                victim = new(expeditionCreature.creature.value, "Creature Type", 0, listName: "creatures"),
            };
        }

        public override bool RespondToCreatureKill()
        {
            return true;
        }

        public override void CreatureKilled(Creature c, int playerNumber)
        {
            if (TeamsFailed[SteamTest.team] || !completed) return;
            CreatureType type = c.abstractCreature.creatureTemplate.type;
            bool flag = victim.Value == "Any Creature" || type.value == victim.Value;
            if (!flag && victim.Value == "DaddyLongLegs" && type == CreatureType.BrotherLongLegs && (c as DaddyLongLegs).colorClass)
            {
                flag = true;
            }
            if (flag)
            {
                FailChallenge(SteamTest.team);
            }
        }

        public override bool RequireSave() => false;
        public override bool ReverseChallenge() => true;


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
                "BingoDontKillChallenge",
                "~",
                victim.ToString(),
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
                victim = SettingBoxFromString(array[0]) as SettingBox<string>;
                completed = (array[1] == "1");
                revealed = (array[2] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoDontKillChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
        }

        public override void RemoveHooks()
        {
        }

        public override List<object> Settings() => [victim];
    }
}