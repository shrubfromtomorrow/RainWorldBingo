using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu.Remix;
using MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoMoonCloakRandomizer : ChallengeRandomizer
    {
        public Randomizer<bool> deliver;

        public override Challenge Random()
        {
            BingoMoonCloakChallenge challenge = new();
            challenge.deliver.Value = deliver.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}deliver-{deliver.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "MoonCloak").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            deliver = Randomizer<bool>.InitDeserialize(dict["deliver"]);
        }
    }

    public class BingoMoonCloakChallenge : BingoChallenge
    {
        public SettingBox<bool> deliver;

        public BingoMoonCloakChallenge()
        {
            deliver = new(false, "Deliver", 0);
        }

        public override void UpdateDescription()
        {
            description = ChallengeTools.IGT.Translate(!deliver.Value ? "Collect Moon's Cloak" : "Deliver the Cloak to Moon");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new([[new Icon("Symbol_MoonCloak", 1f, new Color(0.8f, 0.8f, 0.8f))]]);
            if (deliver.Value)
            {
                phrase.InsertWord(new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.white, 90));
                phrase.InsertWord(Icon.MOON);
            }
            return phrase;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoMoonCloakChallenge c || (c.deliver.Value != deliver.Value);
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Collecting or delivering Moon's cloak");
        }

        public override Challenge Generate()
        {
            BingoMoonCloakChallenge ch = new BingoMoonCloakChallenge
            {
                deliver = new(true, "Deliver", 0)
            };

            return ch;
        }

        public void Delivered()
        {
            if (!completed && !revealed && !TeamsCompleted[SteamTest.team] && !hidden && deliver.Value)
            {
                CompleteChallenge();
            }
        }

        public void Cloak()
        {
            if (!completed || !revealed || !TeamsCompleted[SteamTest.team] || !hidden || !deliver.Value)
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
            return ((slugcat == SlugcatStats.Name.Red || slugcat == MoreSlugcatsEnums.SlugcatStatsName.Gourmand || slugcat == SlugcatStats.Name.White || slugcat == SlugcatStats.Name.Yellow) && ModManager.MSC);
        }

        public override string ToString()
        {
            return string.Concat(
            [
                "BingoMoonCloakChallenge",
                "~",
                deliver.ToString(),
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
                deliver = SettingBoxFromString(array[0]) as SettingBox<bool>;
                completed = (array[1] == "1");
                revealed = (array[2] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoMoonCloakChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.Player.SlugcatGrab += Player_SlugcatGrabCloak;
            On.SLOracleBehaviorHasMark.MoonConversation.AddEvents += SLOracleBehavior_GrabCloak;
            if (deliver.Value) IL.Room.Loaded += Room_LoadedMoonCloak;
            On.SaveState.ctor += SaveState_ctorCloak;
        }

        public override void RemoveHooks()
        {
            On.Player.SlugcatGrab -= Player_SlugcatGrabCloak;
            On.SLOracleBehaviorHasMark.MoonConversation.AddEvents -= SLOracleBehavior_GrabCloak;
            if (deliver.Value) IL.Room.Loaded -= Room_LoadedMoonCloak;
            On.SaveState.ctor -= SaveState_ctorCloak;
        }

        public override List<object> Settings() => [deliver];
    }
}
