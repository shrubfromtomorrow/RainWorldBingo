using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Watcher;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class WatcherBingoEnterRegionChallenge : BingoChallenge
    {
        public SettingBox<string> region;

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Enter <region>")
                .Replace("<region>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(region.Value, ExpeditionData.slugcatPlayer)));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase([[new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.green, 90), new Verse(region.Value)]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return (challenge is not WatcherBingoEnterRegionChallenge c || c.region.Value != region.Value) &&
                (challenge is not WatcherBingoNoRegionChallenge ch || ch.region.Value != region.Value);
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Entering a region");
        }

        public override Challenge Generate()
        {
            string[] regiones = ChallengeUtils.GetCorrectListForChallenge("regionsreal", true);

            WatcherBingoEnterRegionChallenge ch = new WatcherBingoEnterRegionChallenge
            {
                region = new(regiones[UnityEngine.Random.Range(0, regiones.Length)], "Region", 0, listName: "regionsreal")
            };

            return ch;
        }

        public void Entered(string regionName)
        {
            if (completed || SteamTest.team == 8 || TeamsCompleted[SteamTest.team] || hidden || revealed || regionName != region.Value) return;
            CompleteChallenge();
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
            return slugcat == WatcherEnums.SlugcatStatsName.Watcher;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "WatcherBingoEnterRegionChallenge",
                "~",
                region.ToString(),
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
                region = SettingBoxFromString(array[0]) as SettingBox<string>;
                completed = (array[1] == "1");
                revealed = (array[2] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: WatcherBingoEnterRegionChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.Watcher.WarpPoint.ChangeState += Watcher_WarpPoint_ChangeState_EnterRegion;
        }

        public override void RemoveHooks()
        {
            On.Watcher.WarpPoint.ChangeState -= Watcher_WarpPoint_ChangeState_EnterRegion;
        }

        public override List<object> Settings() => [region];
    }
}