using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using JetBrains.Annotations;
using Steamworks;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoKarmaFlowerRandomizer : ChallengeRandomizer
    {
        public Randomizer<int> amount;

        public override Challenge Random()
        {
            BingoKarmaFlowerChallenge challenge = new();
            challenge.amount.Value = amount.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "KarmaFlower").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
        }
    }

    public class BingoKarmaFlowerChallenge : BingoOneCycleChallenge
    {
        // Inherits oneCycle
        public int current;
        public SettingBox<int> amount;
        public SettingBox<string> region;
        public SettingBox<bool> differentRegions;
        public List<string> eatRegions = [];

        public BingoKarmaFlowerChallenge()
        {
            amount = new(0, "Amount", 0);
            region = new("", "Region", 1, listName: "regions");
            differentRegions = new(false, "Different Regions", 2);
            oneCycle = new(false, "In one Cycle", 3);
        }

        public override void UpdateDescription()
        {
            description = ChallengeTools.IGT.Translate("Consume [<current>/<amount>] Karma Flowers <region> <onecycle>")
                .Replace("<current>", current.ToString())
                .Replace("<amount>", amount.Value.ToString())
                .Replace("<region>", differentRegions.Value ? ChallengeTools.IGT.Translate("in different regions") : region.Value == "Any Region" ? "" : ChallengeTools.IGT.Translate("in ") + ChallengeTools.IGT.Translate(Region.GetRegionFullName(region.Value, ExpeditionData.slugcatPlayer)))
                .Replace("<onecycle>", oneCycle.Value ? ChallengeTools.IGT.Translate("in one cycle") : "");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new([[new Icon("foodSymbol"), Icon.KARMA_FLOWER]]);
            if (differentRegions.Value) {
                phrase.InsertWord(new Icon("TravellerA"));
                phrase.InsertWord(new Counter(current, amount.Value), 1);
                if (oneCycle.Value)
                {
                    phrase.InsertWord(new Icon("cycle_limit"), 1);
                }
            }
            else if (region.Value != "Any Region")
            {
                phrase.InsertWord(new Verse(region.Value), 1);
                phrase.InsertWord(new Counter(current, amount.Value), 2);
                if (oneCycle.Value)
                {
                    phrase.InsertWord(new Icon("cycle_limit"), 0);
                }
            }
            else
            {
                phrase.InsertWord(new Counter(current, amount.Value), 1, 0);
                if (oneCycle.Value)
                {
                    phrase.InsertWord(new Icon("cycle_limit"), 1);
                }
            }
            return phrase;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoKarmaFlowerChallenge c || c.differentRegions.Value != differentRegions.Value;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Consuming karma flowers");
        }

        public override Challenge Generate()
        {
            BingoKarmaFlowerChallenge ch = new();
            string r = UnityEngine.Random.value < 0.3f ? ChallengeUtils.GetCorrectListForChallenge("regionsreal")[UnityEngine.Random.Range(0, ChallengeUtils.GetCorrectListForChallenge("regionsreal").Length)] : "Any Region";

            ch.amount = new(UnityEngine.Random.Range(1, 6), "Amount", 0);
            ch.region = new(r, "Region", 1, listName: "regions");
            ch.differentRegions = new(UnityEngine.Random.value < 0.3f, "Different Regions", 2);
            ch.oneCycle = new(UnityEngine.Random.value < 0.2f, "In one Cycle", 3);
            return ch;
        }

        public void Karmad()
        {
            if (completed || revealed || hidden || TeamsCompleted[SteamTest.team]) return;

            foreach (var player in game.Players)
            {
                if (!TryGetWorldName(player, out var world)) continue;

                if (differentRegions.Value)
                {
                    if (eatRegions.Contains(world)) continue;

                    eatRegions.Add(world);
                    Progress();
                }
                else if (region.Value == "Any Region") Progress();
                else if (region.Value == world) Progress();
            }
        }

        private bool TryGetWorldName(AbstractCreature p, out string world)
        {
            world = null;
            if (p?.realizedCreature?.room?.world == null) return false;

            world = p.realizedCreature.room.world.name.ToUpperInvariant();
            return true;
        }

        private void Progress()
        {
            current++;
            UpdateDescription();

            if (current >= amount.Value) CompleteChallenge();
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
            current = 0;
            eatRegions = [];
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return slugcat != SlugcatStats.Name.Red;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoKarmaFlowerChallenge",
                "~",
                region.ToString(),
                "><",
                differentRegions.ToString(),
                "><",
                oneCycle.ToString(),
                "><",
                current.ToString(),
                "><",
                amount.ToString(),
                "><",
                string.Join("|", eatRegions),
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
                region = SettingBoxFromString(array[0]) as SettingBox<string>;
                differentRegions = SettingBoxFromString(array[1]) as SettingBox<bool>;
                oneCycle = SettingBoxFromString(array[2]) as SettingBox<bool>;
                current = int.Parse(array[3], NumberStyles.Any, CultureInfo.InvariantCulture);
                amount = SettingBoxFromString(array[4]) as SettingBox<int>;
                eatRegions = [.. array[5].Split('|')];
                completed = (array[6] == "1");
                revealed = (array[7] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoKarmaFlowerChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.Player.ObjectEaten += Player_ObjectEatenKarmaFlower;
            On.Spear.HitSomethingWithoutStopping += Spear_HitSomethingWithoutStopping;
            IL.Player.FoodInRoom_Room_bool += Player_FoodInRoom_Room_bool;
        }

        public override void RemoveHooks()
        {
            On.Player.ObjectEaten -= Player_ObjectEatenKarmaFlower;
            On.Spear.HitSomethingWithoutStopping -= Spear_HitSomethingWithoutStopping;
            IL.Player.FoodInRoom_Room_bool -= Player_FoodInRoom_Room_bool;
        }

        public override List<object> Settings() => [amount, region, differentRegions, oneCycle];
    }
}
