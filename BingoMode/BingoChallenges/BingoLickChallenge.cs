using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoLickRandomizer : ChallengeRandomizer
    {
        public Randomizer<int> amount;
        public override Challenge Random()
        {
            BingoLickChallenge challenge = new();
            challenge.amount.Value = amount.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "Lick").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
        }
    }
    public class BingoLickChallenge : BingoChallenge
    {
        public int current;
        public SettingBox<int> amount;
        public List<string> lickers = [];

        public BingoLickChallenge()
        {
            amount = new(0, "Amount", 0);
        }

        public override void UpdateDescription()
        {
            description = ChallengeTools.IGT.Translate("Get licked by [<current>/<amount>] unique lizards")
                .Replace("<current>", current.ToString())
                .Replace("<amount>", amount.Value.ToString());
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new(
                [[new Icon("lizlick")],
                [new Counter(current, amount.Value)]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoLickChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Getting licked by lizards");
        }

        public override Challenge Generate()
        {
            BingoLickChallenge ch = new();
            ch.amount = new(UnityEngine.Random.Range(1, 6), "Amount", 0);
            return ch;
        }

        public void Licked(Lizard licker)
        {
            if (!completed && !revealed && !hidden && !TeamsCompleted[SteamTest.team] && !lickers.Contains(licker.abstractCreature.ID.ToString()))
            {
                lickers.Add(licker.abstractCreature.ID.ToString());
                current++;
                UpdateDescription();
                if (current >= amount.Value) CompleteChallenge();
                else ChangeValue();
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

        public override void Reset()
        {
            base.Reset();
            current = 0;
            lickers?.Clear();
            lickers = [];
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return true;
        }

        public override string ToString()
        {
            return string.Concat(
            [
                "BingoLickChallenge",
                "~",
                current.ToString(),
                "><",
                amount.ToString(),
                "><",
                completed ? "1" : "0",
                "><",
                revealed ? "1" : "0",
                "><",
                string.Join("|", lickers),
            ]);
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
                lickers = [.. array[4].Split('|')];
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoLickChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.LizardTongue.Update += LizardTongue_Update;
        }

        public override void RemoveHooks()
        {
            On.LizardTongue.Update -= LizardTongue_Update;
        }

        public override List<object> Settings() => [amount];
    }
}
