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

namespace BingoMode.BingoChallenges.WatcherBingoChallenges
{
    using static ChallengeHooks;
    public class WatcherBingoCollectRippleSpawnChallenge : BingoOneCycleChallenge
    {
        public int current;
        public SettingBox<int> amount;

        public WatcherBingoCollectRippleSpawnChallenge()
        {
            amount = new(0, "Amount", 0);
            oneCycle = new(false, "In one Cycle", 1);
        }

        public override void UpdateDescription()
        {
            description = ChallengeTools.IGT.Translate("Collect [<current>/<amount>] ripplespawn eggs <onecycle>")
                .Replace("<current>", current.ToString())
                .Replace("<amount>", amount.Value.ToString())
                .Replace("<onecycle>", oneCycle.Value ? ChallengeTools.IGT.Translate("in one cycle") : "");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new Phrase(
                [[new Icon("ripplespawn", 1f)],
                [new Counter(current, amount.Value)]]);
            if (oneCycle.Value) phrase.InsertWord(new Icon("cycle_limit"), 0, 1);
            return phrase;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not WatcherBingoCollectRippleSpawnChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Collecting ripplespawn eggs");
        }

        public override Challenge Generate()
        {
            WatcherBingoCollectRippleSpawnChallenge ch = new();
            ch.amount = new(UnityEngine.Random.Range(2, 26), "Amount", 0);
            ch.oneCycle = new(UnityEngine.Random.value < 0.2f, "In one Cycle", 1);
            return ch;
        }

        public void Pop()
        {
            if (!completed && !revealed && !hidden && !TeamsCompleted[SteamTest.team])
            {
                current++;
                UpdateDescription();
                if (current >= (int)amount.Value) CompleteChallenge();
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
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return slugcat == WatcherEnums.SlugcatStatsName.Watcher;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "WatcherBingoCollectRippleSpawnChallenge",
                "~",
                current.ToString(),
                "><",
                amount.ToString(),
                "><",
                oneCycle.ToString(),
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
                current = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                amount = SettingBoxFromString(array[1]) as SettingBox<int>;
                oneCycle = SettingBoxFromString(array[2]) as SettingBox<bool>;
                completed = (array[3] == "1");
                revealed = (array[4] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: WatcherBingoCollectRippleSpawnChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.VoidSpawnEgg.Pop += Watcher_VoidSpawnEgg_Pop;
        }

        public override void RemoveHooks()
        {
            On.VoidSpawnEgg.Pop -= Watcher_VoidSpawnEgg_Pop;
        }

        public override List<object> Settings() => [amount, oneCycle];
    }
}
