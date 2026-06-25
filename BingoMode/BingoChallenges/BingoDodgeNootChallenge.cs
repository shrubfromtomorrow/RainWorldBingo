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
using CreatureType = CreatureTemplate.Type;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoDodgeNootRandomizer : ChallengeRandomizer
    {
        public Randomizer<int> amount;

        public override Challenge Random()
        {
            BingoDodgeNootChallenge challenge = new();
            challenge.amount.Value = amount.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "DodgeNoot").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
        }
    }

    public class BingoDodgeNootChallenge : BingoChallenge
    {
        public SettingBox<int> amount;
        public int current;

        public BingoDodgeNootChallenge()
        {
            amount = new(0, "Amount", 0);
        }

        public override void UpdateDescription()
        {
            if (ChallengeTools.creatureNames == null)
            {
                ChallengeTools.CreatureName(ref ChallengeTools.creatureNames);
            }
            description = ChallengeTools.IGT.Translate("Dodge [<current>/<amount>] Noodlefly attacks")
                .Replace("<current>", ValueConverter.ConvertToString(current))
                .Replace("<amount>", ValueConverter.ConvertToString(amount.Value));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new(
                [
                [Icon.FromEntityName("BigNeedleWorm"), new Icon("slugtarget")],
                [new Counter(current, amount.Value)]
                ]);
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Dodging Noodlefly attacks");
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoDodgeNootChallenge;
        }

        public override Challenge Generate()
        {
            return new BingoDodgeNootChallenge()
            {
                amount = new(UnityEngine.Random.Range(2, 5), "Amount", 0)
            };
        }

        public void Dodged()
        {
            if (!completed && !TeamsCompleted[SteamTest.team] && !hidden && !revealed)
            {
                current++;
                UpdateDescription();
                if (current >= amount.Value) CompleteChallenge();
                else ChangeValue();
            }
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override int Points()
        {
            return 20;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoDodgeNootChallenge",
                "~",
                amount.ToString(),
                "><",
                current.ToString(),
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
                amount = SettingBoxFromString(array[0]) as SettingBox<int>;
                current = int.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                completed = (array[2] == "1");
                revealed = (array[3] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoDodgeNootChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return slugcat.value != "Saint";
        }

        public override void AddHooks()
        {
            On.BigNeedleWorm.Swish += BigNeedleWorm_Swish;
        }

        public override void RemoveHooks()
        {
            On.BigNeedleWorm.Swish -= BigNeedleWorm_Swish;
        }

        public override List<object> Settings() => [amount];
    }
}
