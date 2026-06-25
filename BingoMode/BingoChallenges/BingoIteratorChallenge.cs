using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using MoreSlugcats;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;
    using static MonoMod.InlineRT.MonoModRule;

    public class BingoIteratorRandomizer : ChallengeRandomizer
    {
        public Randomizer<bool> moon;

        public override Challenge Random()
        {
            BingoIteratorChallenge challenge = new();
            challenge.moon.Value = moon.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}moon-{moon.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "Iterator").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            moon = Randomizer<bool>.InitDeserialize(dict["moon"]);
        }
    }

    public class BingoIteratorChallenge : BingoChallenge
    {
        public SettingBox<bool> moon;

        public BingoIteratorChallenge()
        {
            moon = new(false, "Looks to the Moon", 0);
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Visit <iterator>")
                .Replace("<iterator>", moon.Value ? ChallengeTools.IGT.Translate("Looks To The Moon") : ChallengeTools.IGT.Translate("Five Pebbles"));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase([[new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.white, 90), moon.Value ? Icon.MOON : Icon.PEBBLES]]);
        }

        public void MeetPebbles()
        {
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden || moon.Value) return;
            UpdateDescription();
            CompleteChallenge();
        }

        public void MeetMoon()
        {
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden || !moon.Value) return;
            UpdateDescription();
            CompleteChallenge();
        }

        public override int Points()
        {
            return 20;
        }

        public override Challenge Generate()
        {
            // Exclude moon for arti and hunter
            bool flag = (ModManager.MSC && ExpeditionData.slugcatPlayer == MoreSlugcatsEnums.SlugcatStatsName.Artificer) || ExpeditionData.slugcatPlayer == SlugcatStats.Name.Red;
            return new BingoIteratorChallenge
            {
                moon = new(flag ? false : Random.value < 0.5f, "Looks to the Moon", 0)
            };
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoIteratorChallenge c || c.moon.Value != moon.Value;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Visiting iterators");
        }

        public override string ToString()
        {
            return string.Concat(
            [
                "BingoIteratorChallenge",
                "~",
                moon.ToString(),
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
                moon = SettingBoxFromString(array[0]) as SettingBox<bool>;
                completed = (array[1] == "1");
                revealed = (array[2] == "1");
                UpdateDescription();
            }
            catch (System.Exception ex)
            {
                ExpLog.Log("ERROR: BingoIteratorChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.SSOracleBehavior.SeePlayer += SSOracleBehavior_SeePlayer;
            On.SLOracleBehaviorHasMark.InitateConversation += SLOracleBehaviorHasMark_InitateConversation;
            On.MoreSlugcats.CLOracleBehavior.Update += CLOracleBehavior_Update_Iterator;
            On.MoreSlugcats.SSOracleRotBehavior.Update += RMOracleBehavior_Update;
        }

        public override void RemoveHooks()
        {
            On.SSOracleBehavior.SeePlayer -= SSOracleBehavior_SeePlayer;
            On.SLOracleBehaviorHasMark.InitateConversation -= SLOracleBehaviorHasMark_InitateConversation;
            On.MoreSlugcats.CLOracleBehavior.Update -= CLOracleBehavior_Update_Iterator;
            On.MoreSlugcats.SSOracleRotBehavior.Update -= RMOracleBehavior_Update;
        }

        public override List<object> Settings() => [moon];
    }
}
