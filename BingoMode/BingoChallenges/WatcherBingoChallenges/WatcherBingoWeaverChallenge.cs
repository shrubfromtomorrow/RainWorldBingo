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
            room = new("", "Portal Room", 0, listName: ChallengeListConstants.WeaverRooms);
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
            string[] rooms = ChallengeUtils.GetCorrectListForChallenge(ChallengeListConstants.WeaverRooms, true);
            string room = rooms[UnityEngine.Random.Range(0, rooms.Length)];
            return new WatcherBingoWeaverChallenge
            {
                region = Regex.Split(room, "_")[0],
                room = new(room, "Portal Room", 0, listName: ChallengeListConstants.WeaverRooms),
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

        public override bool ValidForThisBingoSlugcat(SlugName slugcat, BingoData.BingoModifier modifier)
        {
            return modifier == BingoData.BingoModifier.WatcherMode || slugcat == WatcherEnums.SlugcatStatsName.Watcher;
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
                var fields = ChallengeUtilsDeserializer.Parse(ChallengeNameConstants.Weaver, args);

                region = fields["Region"];
                room = SettingBoxFromString(fields["Room"]) as SettingBox<string>;
                completed = fields["Completed"] == "1";
                revealed = fields["Revealed"] == "1";
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
