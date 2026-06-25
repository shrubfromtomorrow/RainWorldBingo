using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoSaintDeliveryRandomizer : ChallengeRandomizer
    {
        public override Challenge Random()
        {
            BingoSaintDeliveryChallenge challenge = new();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            return base.Serialize(indent).Replace("__Type__", "SaintDelivery").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
        }
    }

    public class BingoSaintDeliveryChallenge : BingoChallenge
    {
        public override void UpdateDescription()
        {
            description = ChallengeTools.IGT.Translate("Deliver the music pearl to Five Pebbles");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase([[new Icon("memoriespearl"), new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.white, 90), Icon.PEBBLES]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoSaintDeliveryChallenge c;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Delivering the music pearl to Five Pebbles");
        }

        public override Challenge Generate()
        {
            return new BingoSaintDeliveryChallenge
            {
            };
        }

        public void Delivered()
        {
            if (!completed && !revealed && !TeamsCompleted[SteamTest.team] && !hidden)
            {
                CompleteChallenge();
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
            return slugcat == MoreSlugcatsEnums.SlugcatStatsName.Saint;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoSaintDeliveryChallenge",
                "~",
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
                completed = (array[0] == "1");
                revealed = (array[1] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoSaintDeliveryChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.SaveState.ctor += SaveState_ctorHalcyon;
            On.MoreSlugcats.CLOracleBehavior.Update += CLOracleBehavior_Update_SaintDelivery;
            IL.Room.Loaded += Room_LoadedHalcyon;
        }

        public override void RemoveHooks()
        {
            On.SaveState.ctor -= SaveState_ctorHalcyon;
            On.MoreSlugcats.CLOracleBehavior.Update -= CLOracleBehavior_Update_SaintDelivery;
            IL.Room.Loaded -= Room_LoadedHalcyon;
        }

        public override List<object> Settings() => [];
    }
}
