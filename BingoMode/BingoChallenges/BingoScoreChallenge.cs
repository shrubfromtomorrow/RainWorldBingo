using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu.Remix;
using MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Assertions.Must;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoScoreRandomizer : ChallengeRandomizer
    {
        public Randomizer<int> target;
        public Randomizer<bool> oneCycle;

        public override Challenge Random()
        {
            BingoScoreChallenge challenge = new();
            challenge.target.Value = target.Random();
            challenge.oneCycle.Value = oneCycle.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}target-{target.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}oneCycle-{oneCycle.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "Score").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            target = Randomizer<int>.InitDeserialize(dict["target"]);
            oneCycle = Randomizer<bool>.InitDeserialize(dict["oneCycle"]);
        }
    }

    public class BingoScoreChallenge : BingoOneCycleChallenge
    {
        public SettingBox<int> target;

        public BingoScoreChallenge()
        {
            target = new(0, "Target Score", 0);
            oneCycle = new(false, "In one Cycle", 1);
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Earn [<current_score>/<score_target>] points from creature kills <onecycle>")
                .Replace("<score_target>", ValueConverter.ConvertToString<int>(target.Value)).Replace("<current_score>", ValueConverter.ConvertToString<int>(this.current))
                .Replace("<onecycle>", oneCycle.Value ? ChallengeTools.IGT.Translate("in one cycle") : "");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new Phrase(
                [[new Icon("Multiplayer_Star")],
                [new Counter(current, target.Value)]]);
            if (oneCycle.Value)
            {
                phrase.InsertWord(new Icon("cycle_limit"), 0);
            }
            return phrase;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoScoreChallenge c || c.oneCycle.Value != oneCycle.Value;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Scoring points");
        }

        public override Challenge Generate()
        {
            bool oneCycle = UnityEngine.Random.value < 0.5f;
            int num = oneCycle ? UnityEngine.Random.Range(20, 76) : UnityEngine.Random.Range(80, 201);
            return new BingoScoreChallenge
            {
                target = new(num, "Target Score", 0),
                oneCycle = new(oneCycle, "In one Cycle", 1)
            };
        }

        public override void Reset()
        {
            current = 0;
            base.Reset();
        }

        public override int Points()
        {
            float num = 1f;
            if (ModManager.MSC && ExpeditionData.slugcatPlayer == MoreSlugcatsEnums.SlugcatStatsName.Saint)
            {
                num = 1.35f;
            }
            return (int)((float)(this.target.Value / 4) * num) * (int)(this.hidden ? 2f : 1f);
        }

        public override bool CombatRequired()
        {
            return true;
        }

        public override bool RespondToCreatureKill()
        {
            return true;
        }

        public override void CreatureKilled(Creature crit, int playerNumber)
        {
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden || game == null || crit == null)
            {
                return;
            }
            int lastPoints = current;
            CreatureTemplate.Type type = crit.abstractCreature.creatureTemplate.type;

            if (type != null)
            {
                foreach (SlugcatStats.Name slug in ExpeditionData.GetPlayableCharacters())
                {
                    if (ChallengeTools.creatureSpawns[slug.value].Find((ChallengeTools.ExpeditionCreature f) => f.creature == type) != null)
                    {
                        int points = ChallengeTools.creatureSpawns[slug.value].Find((ChallengeTools.ExpeditionCreature f) => f.creature == type).points;
                        current += points;
                        break;
                    }
                }
            }
            if (current != lastPoints)
            {
                UpdateDescription();
                if (current >= target.Value)
                {
                    current = target.Value;
                    CompleteChallenge();
                }
                else ChangeValue();
            }
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoScoreChallenge",
                "~",
                ValueConverter.ConvertToString<int>(current),
                "><",
                target.ToString(),
                "><",
                oneCycle.ToString(),
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
                var fields = ChallengeUtilsDeserializer.Parse(ChallengeNameConstants.Score, args);

                current = int.Parse(fields["Score"], NumberStyles.Any, CultureInfo.InvariantCulture);
                target = SettingBoxFromString(fields["Target"]) as SettingBox<int>;
                oneCycle = SettingBoxFromString(fields["OneCycle"]) as SettingBox<bool>;
                completed = fields["Completed"] == "1";
                revealed = fields["Revealed"] == "1";

                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoScoreChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
        }

        public override void RemoveHooks()
        {
        }

        public override List<object> Settings() => [target, oneCycle];
    }
}
