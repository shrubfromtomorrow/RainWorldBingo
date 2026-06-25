using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using CreatureType = CreatureTemplate.Type;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoDepthsRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> crit;

        public override Challenge Random()
        {
            BingoDepthsChallenge challenge = new();
            challenge.crit.Value = crit.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}crit-{crit.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "Depths").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            crit = Randomizer<string>.InitDeserialize(dict["crit"]);
        }
    }

    public class BingoDepthsChallenge : BingoChallenge
    {
        public SettingBox<string> crit;

        public BingoDepthsChallenge()
        {
            crit = new("", "Creature Type", 0, listName: "depths");
        }

        public override void UpdateDescription()
        {
            if (ChallengeTools.creatureNames == null)
            {
                ChallengeTools.CreatureName(ref ChallengeTools.creatureNames);
            }
            description = ChallengeTools.IGT.Translate("Drop a <crit> into the depths drop room (SB_D06)")
                .Replace("<crit>", ChallengeUtils.CreatureSingularNames(crit.Value));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase(
                [[Icon.FromEntityName(crit.Value), new Icon("deathpiticon")],
                [new Verse("SB_D06")]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoDepthsChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Dropping a creature in the depth pit");
        }

        public override Challenge Generate()
        {
            return new BingoDepthsChallenge
            {
                crit = new(ChallengeUtils.GetCorrectListForChallenge("depths")[UnityEngine.Random.Range(0, ChallengeUtils.GetCorrectListForChallenge("depths").Length - 2)], "Creature Type", 0, listName: "depths")
            };
        }

        public override void Update()
        {
            base.Update();
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden) return;
            for (int i = 0; i < game.Players.Count; i++)
            {
                if (game.Players[i] != null && game.Players[i].realizedCreature is Player player && player.room != null && player.room.abstractRoom.name.ToLowerInvariant() == "sb_d06")
                {
                    for (int j = 0; j < player.room.updateList.Count; j++)
                    {
                        if (player.room.updateList[j] is Creature c && c.Template.type.value == crit.Value && c.mainBodyChunk != null && c.mainBodyChunk.pos.y < 2266f)
                        {
                            CompleteChallenge();
                            return;
                        }
                    }
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
                "BingoDepthsChallenge",
                "~",
                crit.ToString(),
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
                crit = SettingBoxFromString(array[0]) as SettingBox<string>;
                completed = (array[1] == "1");
                revealed = (array[2] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoDepthsChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
        }

        public override void RemoveHooks()
        {
        }

        public override List<object> Settings() => [crit];
    }
}