using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu.Remix;
using MoreSlugcats;
using RWCustom;
using UnityEngine;
using Watcher;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class WatcherBingoWeaverChallenge : BingoChallenge
    {
        public SettingBox<string> room;
        public string region;
        public WatcherBingoWeaverChallenge()
        {
            room = new("", "Portal Room", 0, listName: "weaverrooms");
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Visit The Weaver in <region>").Replace("<region>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(region, ExpeditionData.slugcatPlayer)));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new Phrase([[new Icon("weaver")],
            [new Verse(region)]]);
            return phrase;
        }

        public void Meet()
        {
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden) return;
            UpdateDescription();
            CompleteChallenge();
        }

        public override int Points()
        {
            return 20;
        }

        public override Challenge Generate()
        {
            string[] rooms = ChallengeUtils.GetCorrectListForChallenge("weaverrooms", true);
            string room = rooms[UnityEngine.Random.Range(0, rooms.Length)];
            return new WatcherBingoWeaverChallenge
            {
                region = Regex.Split(room, "_")[0],
                room = new(room, "Portal Room", 0, listName: "weaverrooms"),
            };
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not WatcherBingoWeaverChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Visiting The Weaver");
        }

        public override void Reset()
        {
            base.Reset();
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return slugcat == WatcherEnums.SlugcatStatsName.Watcher;
        }

        public override string ToString()
        {
            return string.Concat(
            [
                "WatcherBingoWeaverChallenge",
                "~",
                region,
                "><",
                room.ToString(),
                "><",
                completed ? "1" : "0",
                "><",
                revealed ? "1" : "0",
            ]);
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                region = array[0];
                room = SettingBoxFromString(array[1]) as SettingBox<string>;
                completed = (array[2] == "1");
                revealed = (array[3] == "1");
                UpdateDescription();
            }
            catch (System.Exception ex)
            {
                ExpLog.Log("ERROR: WatcherBingoWeaverChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.Watcher.VoidWeaver.DefaultBehavior.StartMonologue += Watcher_VoidWeaver_DefaultBehavior_StartMonologue;
            On.Room.Loaded += Watcher_Room_Loaded;
        }

        public override void RemoveHooks()
        {
            On.Watcher.VoidWeaver.DefaultBehavior.StartMonologue -= Watcher_VoidWeaver_DefaultBehavior_StartMonologue;
            On.Room.Loaded -= Watcher_Room_Loaded;
        }

        public override List<object> Settings() => [room];
    }
}
