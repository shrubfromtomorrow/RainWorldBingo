using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoGourmandCrushRandomizer : ChallengeRandomizer
    {
        public Randomizer<int> amount;

        public override Challenge Random()
        {
            BingoGourmandCrushChallenge challenge = new();
            challenge.amount.Value = amount.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "GourmandCrush").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
        }
    }

    public class BingoGourmandCrushChallenge : BingoChallenge
    {
        public List<EntityID> crushed = [];
        public int current;
        public SettingBox<int> amount;

        public BingoGourmandCrushChallenge()
        {
            amount = new(0, "Amount", 0);
        }

        public override void UpdateDescription()
        {
            description = ChallengeTools.IGT.Translate("Crush [<current>/<amount>] unique creatures by falling")
                .Replace("<current>", current.ToString())
                .Replace("<amount>", amount.Value.ToString());
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase(
                [[new Icon("gourmcrush")],
                [new Counter(current, amount.Value)]]);
        }

        public override Challenge Generate()
        {
            return new BingoGourmandCrushChallenge
            {
                amount = new(UnityEngine.Random.Range(1, 9), "Amount", 0)
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Crushing creatures");
        }

        public override int Points()
        {
            return 20;
        }

        public override void Reset()
        {
            current = 0;
            crushed?.Clear();
            crushed = [];
            base.Reset();
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoGourmandCrushChallenge c;
        }

        public void Crush(EntityID id)
        {
            if (completed || revealed || hidden || TeamsCompleted[SteamTest.team] || crushed.Contains(id)) return;
            crushed.Add(id);
            current++;
            UpdateDescription();
            if (current >= (int)amount.Value) CompleteChallenge();
            else ChangeValue();
        }


        public override bool CombatRequired()
        {
            return true;
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return slugcat == MoreSlugcatsEnums.SlugcatStatsName.Gourmand;
        }
        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoGourmandCrushChallenge",
                "~",
                current.ToString(),
                "><",
                amount.ToString(),
                "><",
                completed ? "1" : "0",
                "><",
                revealed ? "1" : "0",
                "><",
                string.Join("|", crushed),
            });
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                current = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                amount = SettingBoxFromString(array[1]) as SettingBox<int>;
                completed = (array[2] == "1");
                revealed = (array[3] == "1");
                string[] arr = array[4].Split('|');
                crushed = [];
                if (arr != null && arr.Length > 0)
                {
                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (arr[i] != string.Empty) crushed.Add(EntityID.FromString(arr[i]));
                    }
                }
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoGourmandCrushChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            IL.Player.Collide += Player_SlugslamIL;
        }

        public override void RemoveHooks()
        {
            IL.Player.Collide -= Player_SlugslamIL;
        }

        public override List<object> Settings() => [amount];
    }
}
