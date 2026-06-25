using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class WatcherBingoNoRegionChallenge : BingoChallenge
    {
        public SettingBox<string> region;

        public WatcherBingoNoRegionChallenge()
        {
            region = new("", "Region", 0, listName: "regionsreal");
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Do not enter <region>")
                .Replace("<region>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(region.Value, ExpeditionData.slugcatPlayer)));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase([[new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "buttonCrossB" : "buttonCrossA", 1f, Color.red), new Verse(region.Value)]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not WatcherBingoNoRegionChallenge c || c.region.Value != region.Value;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Avoiding a region");
        }

        public override Challenge Generate()
        {
            WatcherBingoNoRegionChallenge ch = new WatcherBingoNoRegionChallenge
            {
                region = new(ChallengeUtils.GetCorrectListForChallenge("regionsreal")[UnityEngine.Random.Range(0, ChallengeUtils.GetCorrectListForChallenge("regionsreal").Length)], "Region", 0, listName: "regionsreal")
            };

            return ch;
        }

        public override bool RequireSave() => false;
        public override bool ReverseChallenge() => true;

        public void Entered(string regionName)
        {
            if (completed && region.Value == regionName && !TeamsFailed[SteamTest.team])
            {
                FailChallenge(SteamTest.team);
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
            return slugcat == Watcher.WatcherEnums.SlugcatStatsName.Watcher;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "WatcherBingoNoRegionChallenge",
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
                ExpLog.Log("ERROR: WatcherBingoNoRegionChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.Watcher.WarpPoint.ChangeState += Watcher_WarpPoint_ChangeState_NoRegion;
        }

        public override void RemoveHooks()
        {
            On.Watcher.WarpPoint.ChangeState -= Watcher_WarpPoint_ChangeState_NoRegion;
        }

        public override List<object> Settings() => [region];
    }
}