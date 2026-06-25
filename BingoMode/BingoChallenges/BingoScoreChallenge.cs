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

        public int score;

        public BingoScoreChallenge()
        {
            target = new(0, "Target Score", 0);
            oneCycle = new(false, "In one Cycle", 1);
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Earn [<current_score>/<score_target>] points from creature kills <onecycle>")
                .Replace("<score_target>", ValueConverter.ConvertToString<int>(target.Value)).Replace("<current_score>", ValueConverter.ConvertToString<int>(this.score))
                .Replace("<onecycle>", oneCycle.Value ? ChallengeTools.IGT.Translate("in one cycle") : "");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new Phrase(
                [[new Icon("Multiplayer_Star")],
                [new Counter(score, target.Value)]]);
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
            score = 0;
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
            int lastPoints = score;
            CreatureTemplate.Type type = crit.abstractCreature.creatureTemplate.type;

            if (type != null)
            {
                foreach (SlugcatStats.Name slug in ExpeditionData.GetPlayableCharacters())
                {
                    if (ChallengeTools.creatureSpawns[slug.value].Find((ChallengeTools.ExpeditionCreature f) => f.creature == type) != null)
                    {
                        int points = ChallengeTools.creatureSpawns[slug.value].Find((ChallengeTools.ExpeditionCreature f) => f.creature == type).points;
                        score += points;
                        break;
                    }
                }
            }
            if (score != lastPoints)
            {
                UpdateDescription();
                if (score >= target.Value)
                {
                    score = target.Value;
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
                ValueConverter.ConvertToString<int>(score),
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
                string[] array = Regex.Split(args, "><");
                score = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                target = SettingBoxFromString(array[1]) as SettingBox<int>;
                oneCycle = SettingBoxFromString(array[2]) as SettingBox<bool>;
                completed = (array[3] == "1");
                revealed = (array[4] == "1");
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
