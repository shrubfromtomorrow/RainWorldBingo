using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoMaulTypesRandomizer : ChallengeRandomizer
    {
        public Randomizer<int> amount;

        public override Challenge Random()
        {
            BingoMaulTypesChallenge challenge = new();
            challenge.amount.Value = amount.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "MaulTypes").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
        }
    }

    public class BingoMaulTypesChallenge : BingoChallenge
    {
        public int current;
        public SettingBox<int> amount;
        public List<string> doneTypes = [];

        public BingoMaulTypesChallenge()
        {
            amount = new(0, "Amount", 0);
        }

        public override void UpdateDescription()
        {
            description = ChallengeTools.IGT.Translate("Maul [<current>/<amount>] unique creature types")
                .Replace("<current>", current.ToString())
                .Replace("<amount>", amount.Value.ToString());
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new(
                [[new Icon("artimaulcrit")],
                [new Counter(current, amount.Value)]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoMaulTypesChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Mauling different types of creatures");
        }

        public override Challenge Generate()
        {
            BingoMaulTypesChallenge ch = new();
            ch.amount = new(UnityEngine.Random.Range(2, 6), "Amount", 0);
            return ch;
        }

        public void Maul(string type)
        {
            
            if (completed || revealed || hidden || TeamsCompleted[SteamTest.team] || doneTypes.Contains(type)) return;
            doneTypes.Add(type);
            current++;
            UpdateDescription();
            if (current >= (int)amount.Value) CompleteChallenge();
            else ChangeValue();
        }

        public override int Points()
        {
            return 20;
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override void Reset()
        {
            base.Reset();
            doneTypes?.Clear();
            doneTypes = [];
            current = 0;
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return slugcat == MoreSlugcatsEnums.SlugcatStatsName.Artificer;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoMaulTypesChallenge",
                "~",
                current.ToString(),
                "><",
                amount.ToString(),
                "><",
                completed ? "1" : "0",
                "><",
                revealed ? "1" : "0",
                "><",
                string.Join("|", doneTypes),
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
                doneTypes = [.. array[4].Split('|')];
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoMaulTypesChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            IL.Player.GrabUpdate += Player_GrabUpdateArtiMaulTypes;
        }

        public override void RemoveHooks()
        {
            IL.Player.GrabUpdate -= Player_GrabUpdateArtiMaulTypes;
        }

        public override List<object> Settings() => [amount];
    }
}
