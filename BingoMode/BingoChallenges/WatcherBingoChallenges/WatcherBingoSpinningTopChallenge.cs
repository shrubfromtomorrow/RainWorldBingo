using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu.Remix;
using MoreSlugcats;
using RWCustom;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Watcher;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class WatcherBingoSpinningTopChallenge : BingoChallenge
    {
        public SettingBox<string> spinner; //Region
        public SettingBox<bool> starve;
        public SettingBox<bool> specific;
        public int current;
        public SettingBox<int> amount;
        public List<string> visited = [];

        public WatcherBingoSpinningTopChallenge()
        {
            specific = new(false, "Specific location", 0);
            spinner = new("", "Region", 1, listName: "spinners");
            amount = new(0, "Amount", 2);
            starve = new(false, "While Starving", 3);
        }

        public override void UpdateDescription()
        {
            this.description = specific.Value ?
                ChallengeTools.IGT.Translate("Visit Spinning Top in <location>" + (starve.Value ? " while starving" : ""))
                .Replace("<location>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(spinner.Value, ExpeditionData.slugcatPlayer)))
                :
                ChallengeTools.IGT.Translate("Visit Spinning Top <amount> times" + (starve.Value ? " while starving" : ""))
                .Replace("<amount>", specific.Value ? "1" : ValueConverter.ConvertToString(amount.Value));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new([[new Icon("spinningtop")], [specific.Value ? new Verse(spinner.Value) : new Counter(current, amount.Value)]]);
            if (starve.Value) phrase.InsertWord(new Icon("MartyrB"), 1);
            return phrase;
        }

        public void SeeSpin(string spin, bool starved)
        {
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden) return;
            if (specific.Value)
            {
                if (spin != spinner.Value || starve.Value && !starved) return;
                UpdateDescription();
                CompleteChallenge();
            }
            else
            {
                if (visited.Contains(spin) || starve.Value && !starved) return;
                current++;
                visited.Add(spin);
                UpdateDescription();
                if (current >= amount.Value) CompleteChallenge();
                else ChangeValue();
            }
        }

        public override int Points()
        {
            return 20;
        }

        public override Challenge Generate()
        {
            return new WatcherBingoSpinningTopChallenge
            {
                specific = new SettingBox<bool>(Random.value < 0.5f, "Specific location", 0),
                spinner = new(ChallengeUtils.GetCorrectListForChallenge("spinners")[Random.Range(0, ChallengeUtils.GetCorrectListForChallenge("spinners").Length)], "Region", 1, listName: "spinners"),
                amount = new(Random.Range(2, 8), "Amount", 2),
                starve = new(Random.value < 0.1f, "While Starving", 3)
            };
        }

        public override bool RequireSave() => false;

        public override bool CombatRequired()
        {
            return false;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not WatcherBingoSpinningTopChallenge c || (c.spinner.Value != spinner.Value && c.specific.Value != specific.Value);
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Visiting Spinning Top");
        }

        public override void Reset()
        {
            base.Reset();
            visited?.Clear();
            visited = [];
            current = 0;
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return slugcat == WatcherEnums.SlugcatStatsName.Watcher;
        }

        public override string ToString()
        {
            return string.Concat(
            [
                "WatcherBingoSpinningTopChallenge",
                "~",
                specific.ToString(),
                "><",
                spinner.ToString(),
                "><",
                starve.ToString(),
                "><",
                current.ToString(),
                "><",
                amount.ToString(),
                "><",
                completed ? "1" : "0",
                "><",
                revealed ? "1" : "0",
                "><",
                string.Join("|", visited),
            ]);
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                specific = SettingBoxFromString(array[0]) as SettingBox<bool>;
                spinner = SettingBoxFromString(array[1]) as SettingBox<string>;
                starve = SettingBoxFromString(array[2]) as SettingBox<bool>;
                current = int.Parse(array[3], NumberStyles.Any, CultureInfo.InvariantCulture);
                amount = SettingBoxFromString(array[4]) as SettingBox<int>;
                completed = (array[5] == "1");
                revealed = (array[6] == "1");
                visited = [.. array[7].Split('|')];
                UpdateDescription();
            }
            catch (System.Exception ex)
            {
                ExpLog.Log("ERROR: WatcherBingoSpinningTopChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.Watcher.SpinningTop.StartConversation += Watcher_SpinningTop_StartConversation;
        }

        public override void RemoveHooks()
        {
            On.Watcher.SpinningTop.StartConversation -= Watcher_SpinningTop_StartConversation;
        }

        public override List<object> Settings() => [spinner, specific, amount, starve];
    }
}
