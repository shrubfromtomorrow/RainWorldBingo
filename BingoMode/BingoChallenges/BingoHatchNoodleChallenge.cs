using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using UnityEngine;
using Menu.Remix;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoHatchNoodleRandomizer : ChallengeRandomizer
    {
        public Randomizer<int> amount;
        public Randomizer<bool> atOnce;

        public override Challenge Random()
        {
            BingoHatchNoodleChallenge challenge = new();
            challenge.amount.Value = amount.Random();
            challenge.oneCycle.Value = atOnce.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}atOnce-{atOnce.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "HatchNoodle").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
            atOnce = Randomizer<bool>.InitDeserialize(dict["atOnce"]);
        }
    }

    public class BingoHatchNoodleChallenge : BingoOneCycleChallenge
    {
        public int current;
        public SettingBox<int> amount;
        public SettingBox<string> region;
        public SettingBox<bool> differentRegions;
        public List<string> hatchRegions = [];

        public BingoHatchNoodleChallenge()
        {
            amount = new(0, "Amount", 0);
            region = new("", "Region", 1, listName: "nootregions");
            differentRegions = new(false, "Different Regions", 2);
            oneCycle = new(false, "At once", 3);
        }

        public override void UpdateDescription()
        {
            description = ChallengeTools.IGT.Translate("Hatch [<current>/<amount>] Noodlefly eggs <region> <onecycle>")
                .Replace("<current>", current.ToString())
                .Replace("<amount>", amount.Value.ToString())
                .Replace("<region>", differentRegions.Value ? ChallengeTools.IGT.Translate("in different regions") : region.Value == "Any Region" ? "" : ChallengeTools.IGT.Translate("in ") + ChallengeTools.IGT.Translate(Region.GetRegionFullName(region.Value, ExpeditionData.slugcatPlayer)))
                .Replace("<onecycle>", oneCycle.Value ? ChallengeTools.IGT.Translate("in one cycle") : "");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new(
                [[new Icon("needleEggSymbol", 1f, ChallengeUtils.ItemOrCreatureIconColor("needleEggSymbol")), new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.white, 90), new Icon("Kill_SmallNeedleWorm", 1f, ChallengeUtils.ItemOrCreatureIconColor("SmallNeedleWorm"))]]);
            if (differentRegions.Value)
            {
                phrase.InsertWord(new Icon("TravellerA"), 1);
                phrase.InsertWord(new Counter(current, amount.Value), 2);
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
                    phrase.InsertWord(new Icon("cycle_limit"), 2);
                }
            }
            else
            {
                if (oneCycle.Value)
                {
                    phrase.InsertWord(new Counter(current, amount.Value), 2);
                    phrase.InsertWord(new Icon("cycle_limit"), 1);
                }
                else
                {
                    phrase.InsertWord(new Counter(current, amount.Value), 1);
                }
            }
            return phrase;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoHatchNoodleChallenge c;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Hatching Noodlefly eggs");
        }

        public override Challenge Generate()
        {
            BingoHatchNoodleChallenge ch = new();
            string r = UnityEngine.Random.value < 0.3f ? ChallengeUtils.GetCorrectListForChallenge("nootregions")[UnityEngine.Random.Range(0, ChallengeUtils.GetCorrectListForChallenge("nootregions").Length)] : "Any Region";
            // Can't have onecycle and different regions
            bool oneCycle = UnityEngine.Random.value < 0.2f;

            ch.amount = new(UnityEngine.Random.Range(1, 4), "Amount", 0);
            ch.region = new(r, "Region", 1, listName: "nootregions");
            ch.differentRegions = new(oneCycle ? false : UnityEngine.Random.value < 0.3f, "Different Regions", 2);
            ch.oneCycle = new(oneCycle, "At once", 3);
            return ch;
        }

        public void Hatch()
        {
            if (completed || revealed || hidden || TeamsCompleted[SteamTest.team]) return;

            foreach (var player in game.Players)
            {
                if (!TryGetWorldName(player, out var world)) continue;

                if (differentRegions.Value)
                {
                    if (hatchRegions.Contains(world)) continue;

                    hatchRegions.Add(world);
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
            return amount.Value * 10;
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return slugcat.value != "Saint";
        }

        public override void Reset()
        {
            base.Reset();
            current = 0;
            hatchRegions = [];
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoHatchNoodleChallenge",
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
                string.Join("|", hatchRegions),
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
                hatchRegions = [.. array[5].Split('|')];
                completed = (array[6] == "1");
                revealed = (array[7] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoHatchNoodleChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.ShelterDoor.Close += ShelterDoor_Close;
        }

        public override void RemoveHooks()
        {
            On.ShelterDoor.Close -= ShelterDoor_Close;
        }

        public override List<object> Settings() => [amount, region, differentRegions, oneCycle];
    }
}