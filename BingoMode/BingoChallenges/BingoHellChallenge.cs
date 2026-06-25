using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoHellRandomizer : ChallengeRandomizer
    {
        public Randomizer<int> amount;

        public override Challenge Random()
        {
            BingoHellChallenge challenge = new();
            challenge.amount.Value = amount.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "Hell").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
        }
    }

    public class BingoHellChallenge : BingoChallenge
    {
        public int current;
        public SettingBox<int> amount;
        public override bool RequireSave() => false;
        public override bool ReverseChallenge() => true;

        public override void UpdateDescription()
        {
            description = ChallengeTools.IGT.Translate("Do not die before completing [<current>/<amount>] bingo challenges")
                .Replace("<current>", current.ToString())
                .Replace("<amount>", amount.Value.ToString());
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase() => new(
            [[new Icon("completechallenge"), new Counter(current, amount.Value)],
            [new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "buttonCrossB" : "buttonCrossA", 1f, Color.red), new Icon("Multiplayer_Death")]]);

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoHellChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Avoiding death before completing challenges");
        }

        public override Challenge Generate()
        {
            BingoHellChallenge ch = new();
            ch.amount = new(UnityEngine.Random.Range(1, 4), "Amount", 0);
            return ch;
        }

        public void GetChallenge()
        {
            if (!TeamsFailed[SteamTest.team] && completed && current < amount.Value)
            {
                current++;
                UpdateDescription();
                ChangeValue();
            }
        }

        public void Fail()
        {
            if (TeamsFailed[SteamTest.team] || ((TeamsFailed[SteamTest.team] || completed || TeamsCompleted[SteamTest.team]) && current >= amount.Value)) return;

            FailChallenge(SteamTest.team);
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
            return true;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoHellChallenge",
                "~",
                current.ToString(),
                "><",
                amount.ToString(),
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
                current = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                amount = SettingBoxFromString(array[1]) as SettingBox<int>;
                completed = (array[2] == "1");
                revealed = (array[3] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoHellChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.Player.Die += Player_DieHell;
            On.RainWorldGame.GoToDeathScreen += RainWorldGame_GoToDeathScreenHell;
            On.RainWorldGame.GoToStarveScreen += RainWorldGame_GoToStarveScreenHell;
        }

        public override void RemoveHooks()
        {
            On.Player.Die -= Player_DieHell;
            On.RainWorldGame.GoToDeathScreen -= RainWorldGame_GoToDeathScreenHell;
            On.RainWorldGame.GoToStarveScreen -= RainWorldGame_GoToStarveScreenHell;
        }

        public override List<object> Settings() => [amount];
    }
}