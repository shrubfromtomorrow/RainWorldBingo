using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Watcher;
using UnityEngine;
using Menu.Remix;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class WatcherBingoHatchMothGrubChallenge : BingoOneCycleChallenge
    {
        public int current;
        public SettingBox<int> amount;
        public SettingBox<string> region;
        public SettingBox<bool> differentRegions;
        public List<string> hatchRegions = [];

        public WatcherBingoHatchMothGrubChallenge()
        {
            amount = new(0, "Amount", 0);
            oneCycle = new(false, "At once", 1);
        }

        public override void UpdateDescription()
        {
            description = ChallengeTools.IGT.Translate("Hatch [<current>/<amount>] Moth Grubs <onecycle>")
                .Replace("<current>", current.ToString())
                .Replace("<amount>", amount.Value.ToString())
                .Replace("<onecycle>", oneCycle.Value ? ChallengeTools.IGT.Translate("in one cycle") : "");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new(
                [[new Icon("Kill_MothGrub", 1f, ChallengeUtils.ItemOrCreatureIconColor("MothGrub")), new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.white, 90), new Icon("Kill_SmallMoth", 1f, ChallengeUtils.ItemOrCreatureIconColor("SmallMoth"))],
                [new Counter(current, amount.Value)]]);
            if (oneCycle.Value)
            {
                phrase.InsertWord(new Icon("cycle_limit"), 1);
            }
            return phrase;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not WatcherBingoHatchMothGrubChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Hatching Moth Grubs");
        }

        public override Challenge Generate()
        {
            WatcherBingoHatchMothGrubChallenge ch = new();

            ch.amount = new(UnityEngine.Random.Range(1, 3), "Amount", 0);
            ch.oneCycle = new(UnityEngine.Random.value < 0.2f, "At once", 1);
            return ch;
        }

        public void Hatch()
        {
            if (completed || revealed || hidden || TeamsCompleted[SteamTest.team]) return;

            Progress();
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
            return slugcat == WatcherEnums.SlugcatStatsName.Watcher;
        }

        public override void Reset()
        {
            base.Reset();
            current = 0;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "WatcherBingoHatchMothGrubChallenge",
                "~",
                oneCycle.ToString(),
                "><",
                current.ToString(),
                "><",
                amount.ToString(),
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
                oneCycle = SettingBoxFromString(array[0]) as SettingBox<bool>;
                current = int.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                amount = SettingBoxFromString(array[2]) as SettingBox<int>;
                completed = (array[3] == "1");
                revealed = (array[4] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: WatcherBingoHatchMothGrubChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.ShelterDoor.Close += Watcher_ShelterDoor_Close;
        }

        public override void RemoveHooks()
        {
            On.ShelterDoor.Close -= Watcher_ShelterDoor_Close;
        }

        public override List<object> Settings() => [amount, oneCycle];
    }
}