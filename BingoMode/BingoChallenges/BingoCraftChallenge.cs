using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using ItemType = AbstractPhysicalObject.AbstractObjectType;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoCraftRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> craftee;
        public Randomizer<int> amount;

        public override Challenge Random()
        {
            BingoCraftChallenge challenge = new();
            challenge.craftee.Value = craftee.Random();
            challenge.amount.Value = amount.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}craftee-{craftee.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "Craft").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            craftee = Randomizer<string>.InitDeserialize(dict["craftee"]);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
        }
    }

    public class BingoCraftChallenge : BingoChallenge
    {
        public SettingBox<string> craftee;
        public SettingBox<int> amount;
        public int current;

        public BingoCraftChallenge()
        {
            craftee = new("", "Item to Craft", 0, listName: "craft");
            amount = new(0, "Amount", 1);
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate($"Craft [<current>/<amount>] <item>")
                .Replace("<current>", current.ToString())
                .Replace("<amount>", amount.Value.ToString())
                .Replace("<item>", ChallengeTools.ItemName(new(craftee.Value)));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase(
                [[new Icon("crafticon"), craftee.Value == "KarmaFlower" ? Icon.KARMA_FLOWER : Icon.FromEntityName(craftee.Value)],
                [new Counter(current, amount.Value)]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoCraftChallenge c || c.craftee.Value != craftee.Value;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Crafting items");
        }

        public override Challenge Generate()
        {
            int thingies = UnityEngine.Random.Range(1, 5);
            return new BingoCraftChallenge
            {
                craftee = new(ChallengeUtils.GetCorrectListForChallenge("craft")[UnityEngine.Random.Range(0, ChallengeUtils.GetCorrectListForChallenge("craft").Length)], "Item to Craft", 0, listName: "craft"),
                amount = new(thingies, "Amount", 1)
            };
        }

        public void Crafted(ItemType item)
        {
            if (!completed && !TeamsCompleted[SteamTest.team] && !hidden && !revealed && item.value == craftee.Value)
            {
                current += 1;
                UpdateDescription();
                if (current >= amount.Value)
                {
                    CompleteChallenge();
                }
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
            return slugcat == MoreSlugcatsEnums.SlugcatStatsName.Gourmand;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoCraftChallenge",
                "~",
                craftee.ToString(),
                "><",
                amount.ToString(),
                "><",
                current.ToString(),
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
                craftee = SettingBoxFromString(array[0]) as SettingBox<string>;
                amount = SettingBoxFromString(array[1]) as SettingBox<int>;
                current = int.Parse(array[2], System.Globalization.NumberStyles.Any);
                completed = (array[3] == "1");
                revealed = (array[4] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoCraftChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            IL.Player.SpitUpCraftedObject += Player_SpitUpCraftedObjectIL;
        }

        public override void RemoveHooks()
        {
            IL.Player.SpitUpCraftedObject -= Player_SpitUpCraftedObjectIL;
        }

        public override List<object> Settings() => [craftee, amount];
    }
}