using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu.Remix;
using MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using PearlType = DataPearl.AbstractDataPearl.DataPearlType;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoCollectPearlRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> pearl;
        public Randomizer<int> amount;
        public Randomizer<bool> specific;

        public override Challenge Random()
        {
            BingoCollectPearlChallenge challenge = new();
            challenge.pearl.Value = pearl.Random();
            challenge.amount.Value = amount.Random();
            challenge.specific.Value = specific.Random();
            challenge.region = challenge.pearl.Value.Substring(0, 2);
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}pearl-{pearl.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}specific-{specific.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "CollectPearl").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            pearl = Randomizer<string>.InitDeserialize(dict["pearl"]);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
            specific = Randomizer<bool>.InitDeserialize(dict["specific"]);
        }
    }

    public class BingoCollectPearlChallenge : BingoChallenge
    {
        public SettingBox<string> pearl; //PearlType
        public List<string> collected = [];
        public string region;
        public int current;
        public SettingBox<int> amount;
        public SettingBox<bool> specific;

        public BingoCollectPearlChallenge()
        {
            pearl = new("", "Pearl", 1, listName: "pearls");
            collected = [];
            amount = new (0, "Amount", 3);
            specific = new(false, "Specific Pearl", 0);
        }

        public override void UpdateDescription()
        {
            region = Regex.Split(pearl.Value, "_")[0];
            if (ExpeditionData.slugcatPlayer == MoreSlugcatsEnums.SlugcatStatsName.Saint && region == "DS") region = "UG";
            if ((ExpeditionData.slugcatPlayer == MoreSlugcatsEnums.SlugcatStatsName.Spear || ExpeditionData.slugcatPlayer == MoreSlugcatsEnums.SlugcatStatsName.Artificer) && region == "MS") region = "GW";

            this.description = specific.Value ? ChallengeTools.IGT.Translate("Collect the <pearl> pearl from <region>")
                .Replace("<region>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(region, ExpeditionData.slugcatPlayer)))
                .Replace("<pearl>", ChallengeTools.IGT.Translate(ChallengeUtils.NameForPearl(pearl.Value)))
                : ChallengeTools.IGT.Translate("Collect [<current>/<amount>] colored pearls")
                .Replace("<current>", ValueConverter.ConvertToString(current))
                .Replace("<amount>", specific.Value ? "1" : ValueConverter.ConvertToString(amount.Value));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            if (specific.Value)
            {
                return new Phrase(
                    [[new Verse(region.Length == 4 ? pearl.Value.Substring(pearl.Value.LastIndexOf('_') + 1) : pearl.Value)],
                    [new Icon("Symbol_Pearl", 1f, DataPearl.UniquePearlMainColor(new(region.Length == 4 ? pearl.Value.Substring(5) : pearl.Value, false))) { background = new FSprite("radialgradient") }]]);
            }
            return new Phrase(
                [[Icon.PEARL_HOARD_COLOR],
                [new Counter(current, amount.Value)]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoCollectPearlChallenge c || ((c.specific.Value && specific.Value) && c.pearl.Value != pearl.Value) || (c.specific.Value != specific.Value);
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Collecting pearls");
        }

        public void PickedUp(PearlType type)
        {
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden) return;
            if (specific.Value)
            {
                if (type.value != (region.Length == 4 ? pearl.Value.Substring(pearl.Value.IndexOf('_') + 1) : pearl.Value)) return;
                current = 1;
                UpdateDescription();
                CompleteChallenge();
            }
            else
            {
                current++;
                collected.Add(type.value);
                UpdateDescription(); 
                if (current >= amount.Value) CompleteChallenge();
                else ChangeValue();
            }
        }

        public override void Update()
        {
            base.Update();

            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden) return;
            for (int i = 0; i < game.Players.Count; i++)
            {
                if (game.Players[i] != null 
                    && game.Players[i].realizedCreature != null 
                    && game.Players[i].realizedCreature.room != null)
                {
                    for (int g = 0; g < game.Players[i].realizedCreature.grasps.Length; g++)
                    {
                        if (game.Players[i].realizedCreature.grasps[g] != null && game.Players[i].realizedCreature.grasps[g].grabbed is DataPearl p && ((!specific.Value && DataPearl.PearlIsNotMisc(p.AbstractPearl.dataPearlType) && !collected.Contains(p.AbstractPearl.dataPearlType.value)) || specific.Value))
                        {
                            PickedUp(p.AbstractPearl.dataPearlType);
                            return;
                        }
                    }
                }
            }
        }

        public override Challenge Generate()
        {
            bool specifi = UnityEngine.Random.value < 0.5f;
            string p = ChallengeUtils.GetCorrectListForChallenge("pearls")[UnityEngine.Random.Range(0, ChallengeUtils.GetCorrectListForChallenge("pearls").Length)];
            BingoCollectPearlChallenge chal = new()
            {
                specific = new SettingBox<bool>(specifi, "Specific Pearl", 0),
                collected = []
            };
            chal.pearl = new(p, "Pearl", 1, listName: "pearls");
            chal.region = Regex.Split(p, "_")[0];
            chal.amount = new(UnityEngine.Random.Range(1, 5), "Amount", 3);

            return chal;
        }

        public override void Reset()
        {
            base.Reset();
            current = 0;
            collected?.Clear();
            collected = [];
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
                "BingoCollectPearlChallenge",
                "~",
                specific.ToString(),
                "><",
                pearl.ToString(),
                "><",
                current.ToString(),
                "><",
                amount.ToString(),
                "><",
                completed ? "1" : "0",
                "><",
                revealed ? "1" : "0",
                "><",
                string.Join("cLtD", collected),
            });
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                specific = SettingBoxFromString(array[0]) as SettingBox<bool>;
                pearl = SettingBoxFromString(array[1]) as SettingBox<string>;
                region = pearl == null ? "noregion" : Regex.Split(pearl.Value, "_")[0];
                current = int.Parse(array[2], NumberStyles.Any, CultureInfo.InvariantCulture);
                amount = SettingBoxFromString(array[3]) as SettingBox<int>;
                completed = (array[4] == "1");
                revealed = (array[5] == "1");
                string[] arr = Regex.Split(array[6], "cLtD");
                collected = [.. arr];

                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoCollectPearlChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
        }

        public override void RemoveHooks()
        {
        }

        public override List<object> Settings() => [amount, specific, pearl];
    }
}
