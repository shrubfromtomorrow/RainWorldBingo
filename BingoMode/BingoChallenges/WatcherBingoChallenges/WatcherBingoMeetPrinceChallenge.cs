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

    public class WatcherBingoPrinceChallenge : BingoChallenge
    {

        public WatcherBingoPrinceChallenge()
        {
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Visit The Prince");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new Phrase([[new Icon("prince")]]);
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
            return new WatcherBingoPrinceChallenge
            {
            };
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not WatcherBingoPrinceChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Visiting The Prince");
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return slugcat == Watcher.WatcherEnums.SlugcatStatsName.Watcher;
        }

        public override string ToString()
        {
            return string.Concat(
            [
                "WatcherBingoPrinceChallenge",
                "~",
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
                completed = (array[0] == "1");
                revealed = (array[1] == "1");
                UpdateDescription();
            }
            catch (System.Exception ex)
            {
                ExpLog.Log("ERROR: WatcherBingoPrinceChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.Watcher.PrinceBehavior.InitateConversation += Watcher_PrinceBehavior_InitateConversation;
        }

        public override void RemoveHooks()
        {
            On.Watcher.PrinceBehavior.InitateConversation -= Watcher_PrinceBehavior_InitateConversation;
        }

        public override List<object> Settings() => [];
    }
}
