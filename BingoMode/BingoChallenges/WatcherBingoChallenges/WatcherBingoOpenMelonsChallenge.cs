using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using MoreSlugcats;
using Watcher;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class WatcherBingoOpenMelonsChallenge : BingoOneCycleChallenge
    {
        public int current;
        public SettingBox<int> amount;
        public SettingBox<string> region;
        public SettingBox<bool> differentRegions;
        public List<string> openRegions = [];

        public WatcherBingoOpenMelonsChallenge()
        {
            amount = new(0, "Amount", 0);
            region = new("", "Region", 1, listName: "pomegranateregions");
            differentRegions = new(false, "Different Regions", 2);
            oneCycle = new(false, "In one Cycle", 3);
        }

        public override void UpdateDescription()
        {
            description = ChallengeTools.IGT.Translate("Open [<current>/<amount>] pomegranates <region> <onecycle>")
                .Replace("<current>", current.ToString())
                .Replace("<amount>", amount.Value.ToString())
                .Replace("<region>", differentRegions.Value ? ChallengeTools.IGT.Translate("in different regions") : region.Value == "Any Region" ? "" : ChallengeTools.IGT.Translate("in ") + ChallengeTools.IGT.Translate(Region.GetRegionFullName(region.Value, ExpeditionData.slugcatPlayer)))
                .Replace("<onecycle>", oneCycle.Value ? ChallengeTools.IGT.Translate("in one cycle") : "");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new Phrase(
                [[new Icon("Symbol_Pomegranate", 1f, new Color(0.27f, 0.71f, 0.19f))]]);
            if (differentRegions.Value)
            {
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
            return challenge is not WatcherBingoOpenMelonsChallenge c || (c.region.Value != region.Value && c.differentRegions.Value != differentRegions.Value) || c.oneCycle.Value != oneCycle.Value || c.differentRegions.Value != differentRegions.Value;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Opening pomegranates");
        }

        public override Challenge Generate()
        {
            WatcherBingoOpenMelonsChallenge ch = new();
            string r = UnityEngine.Random.value < 0.3f ? ChallengeUtils.GetCorrectListForChallenge("pomegranateregions")[UnityEngine.Random.Range(0, ChallengeUtils.GetCorrectListForChallenge("pomegranateregions").Length)] : "Any Region";

            ch.amount = new(UnityEngine.Random.Range(2, 6), "Amount", 0);
            ch.region = new(r, "Region", 1, listName: "pomegranateregions");
            ch.differentRegions = new(UnityEngine.Random.value < 0.3f, "Different Regions", 2);
            ch.oneCycle = new(false, "In one Cycle", 3);
            return ch;
        }

        public void Open()
        {
            if (completed || revealed || hidden || TeamsCompleted[SteamTest.team]) return;

            foreach (var player in game.Players)
            {
                if (!TryGetWorldName(player, out var world)) continue;

                if (differentRegions.Value)
                {
                    if (openRegions.Contains(world)) continue;

                    openRegions.Add(world);
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
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return slugcat == WatcherEnums.SlugcatStatsName.Watcher;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "WatcherBingoOpenMelonsChallenge",
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
                string.Join("|", openRegions),
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
                openRegions = [.. array[5].Split('|')];
                completed = (array[6] == "1");
                revealed = (array[7] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: WatcherBingoOpenMelonsChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.Pomegranate.Smash += Watcher_Pomegranate_Smash;
        }

        public override void RemoveHooks()
        {
            On.Pomegranate.Smash -= Watcher_Pomegranate_Smash;
        }

        public override List<object> Settings() => [amount, region, differentRegions, oneCycle];
    }
}