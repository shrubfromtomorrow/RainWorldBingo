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

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoTradeRandomizer : ChallengeRandomizer
    {
        public Randomizer<int> amount;

        public override Challenge Random()
        {
            BingoTradeChallenge challenge = new();
            challenge.amount.Value = amount.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "Trade").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
        }
    }

    public class BingoTradeChallenge : BingoChallenge
    {
        public SettingBox<int> amount;
        public int current;
        public List<EntityID> bannedIDs;

        public BingoTradeChallenge()
        {
            amount = new(0, "Value", 0);
            bannedIDs = [];
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Trade [<current>/<amount>] worth of value to Scavenger Merchants")
                .Replace("<amount>", ValueConverter.ConvertToString(amount.Value))
                .Replace("<current>", ValueConverter.ConvertToString(current));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase(
                [[new Icon("scav_merchant")],
                [new Counter(current, amount.Value)]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoTradeChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Trading items to merchants");
        }

        public override Challenge Generate()
        {
            int amou = UnityEngine.Random.Range(5, 21);

            return new BingoTradeChallenge
            {
                amount = new(amou, "Value", 0),
                bannedIDs = []
            };
        }

        public void Traded(int pnts, EntityID ID)
        {
            if (completed || TeamsCompleted[SteamTest.team] || revealed || hidden) return;
            
            if (!bannedIDs.Contains(ID))
            {
                current += pnts;
                bannedIDs.Add(ID);
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
            return amount.Value * 10;
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override void Reset()
        {
            base.Reset();
            current = 0;
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return slugcat != MoreSlugcatsEnums.SlugcatStatsName.Artificer;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoTradeChallenge",
                "~",
                current.ToString(),
                "><",
                amount.ToString(),
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
                current = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                amount = SettingBoxFromString(array[1]) as SettingBox<int>;
                completed = (array[2] == "1");
                revealed = (array[3] == "1");
                bannedIDs = [];
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoTradeChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.Room.ctor += Room_ctor;
            On.Room.Unloaded += Room_Unloaded;
            On.Player.ReleaseGrasp += Player_ReleaseGrasp;
            On.Scavenger.Grab += Scavenger_GrabTrade;
        }

        public override void RemoveHooks()
        {
            On.Room.ctor -= Room_ctor;
            On.Room.Unloaded -= Room_Unloaded;
            On.Player.ReleaseGrasp -= Player_ReleaseGrasp;
            On.Scavenger.Grab -= Scavenger_GrabTrade;
        }

        public override List<object> Settings() => [amount];
    }
}