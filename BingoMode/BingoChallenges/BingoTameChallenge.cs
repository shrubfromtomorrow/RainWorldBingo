using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu.Remix;
using MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoTameRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> crit;
        public Randomizer<int> amount;
        public Randomizer<bool> specific;

        public override Challenge Random()
        {
            BingoTameChallenge challenge = new();
            challenge.crit.Value = crit.Random();
            challenge.amount.Value = amount.Random();
            challenge.specific.Value = specific.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}crit-{crit.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}specific-{specific.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "Tame").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            crit = Randomizer<string>.InitDeserialize(dict["crit"]);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
            specific = Randomizer<bool>.InitDeserialize(dict["specific"]);
        }
    }

    public class BingoTameChallenge : BingoChallenge
    {
        public SettingBox<string> crit;
        public List<string> tamedTypes = [];
        public List<string> tamedIDs = [];
        public int current;
        public SettingBox<int> amount;
        public SettingBox<bool> specific;

        public BingoTameChallenge()
        {
            specific = new(false, "Specific Creature Type", 0);
            crit = new("", "Creature Type", 1, listName: "friend");
            amount = new(0, "Amount", 2);
            tamedTypes = [];
            tamedIDs = [];
        }

        public override void UpdateDescription()
        {
            this.description = specific.Value ? ChallengeTools.IGT.Translate("Befriend [<current>/<amount>] <crit>")
                .Replace("<crit>", ChallengeTools.creatureNames[new CreatureTemplate.Type(crit.Value).Index])
                .Replace("<current>", ValueConverter.ConvertToString(current))
                .Replace("<amount>", ValueConverter.ConvertToString(amount.Value))
                : ChallengeTools.IGT.Translate("Befriend [<current>/<amount>] unique creature types")
                .Replace("<current>", ValueConverter.ConvertToString(current))
                .Replace("<amount>", ValueConverter.ConvertToString(amount.Value));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new([[new Icon("FriendB")], [new Counter(current, amount.Value)]]);
            if (specific.Value) phrase.InsertWord(Icon.FromEntityName(crit.Value), 0);
            return phrase;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoTameChallenge c || specific.Value != c.specific.Value || (specific.Value && c.specific.Value && crit.Value != c.crit.Value);
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Befriending creatures");
        }

        public override Challenge Generate()
        {
            bool specific = UnityEngine.Random.value < 0.5f;
            var crug = ChallengeUtils.GetCorrectListForChallenge("friend")[UnityEngine.Random.Range(0, ChallengeUtils.GetCorrectListForChallenge("friend").Length)];

            return new BingoTameChallenge
            {
                specific = new SettingBox<bool>(specific, "Specific Creature Type", 0),
                crit = new(crug, "Creature Type", 1, listName: "friend"),
                amount = new(UnityEngine.Random.Range(1, 4), "Amount", 2),
                tamedTypes = [],
                tamedIDs = []
            };
        }

        public void Fren(AbstractCreature friend)
        {
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden) return;
            if (specific.Value)
            {
                if (friend.creatureTemplate.type.value != crit.Value || tamedIDs.Contains(friend.ID.ToString())) return;
                current++;
                tamedIDs.Add(friend.ID.ToString());
                UpdateDescription();
                if (current >= amount.Value) CompleteChallenge();
                else ChangeValue();
            }
            else
            {
                if (tamedTypes.Contains(friend.creatureTemplate.type.value)) return;
                current++;
                tamedTypes.Add(friend.creatureTemplate.type.value);
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

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return true;
        }

        public override void Reset()
        {
            base.Reset();
            tamedTypes?.Clear();
            tamedTypes = [];
            tamedIDs?.Clear();
            tamedIDs = [];
            current = 0;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoTameChallenge",
                "~",
                specific.ToString(),
                "><",
                crit.ToString(),
                "><",
                current.ToString(),
                "><",
                amount.ToString(),
                "><",
                completed ? "1" : "0",
                "><",
                revealed ? "1" : "0",
                "><",
                string.Join("cLtDT", tamedTypes),
                "><",
                string.Join("cLtDID", tamedIDs),
            });
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                specific = SettingBoxFromString(array[0]) as SettingBox<bool>;
                crit = SettingBoxFromString(array[1]) as SettingBox<string>;
                current = int.Parse(array[2], NumberStyles.Any, CultureInfo.InvariantCulture);
                amount = SettingBoxFromString(array[3]) as SettingBox<int>;
                completed = (array[4] == "1");
                revealed = (array[5] == "1");
                string[] arr = Regex.Split(array[6], @"cLtDT");
                tamedTypes = [.. arr];
                string[] arr2 = Regex.Split(array[7], @"cLtDID");
                tamedIDs = [.. arr2];
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoTameChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            IL.FriendTracker.Update += FriendTracker_Update;
        }

        public override void RemoveHooks()
        {
            IL.FriendTracker.Update -= FriendTracker_Update;
        }

        public override List<object> Settings() => [crit, amount, specific];
    }
}