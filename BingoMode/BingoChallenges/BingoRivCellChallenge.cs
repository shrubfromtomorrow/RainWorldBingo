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

    public class BingoRivCellRandomizer : ChallengeRandomizer
    {
        public override Challenge Random()
        {
            BingoRivCellChallenge challenge = new();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            return base.Serialize(indent).Replace("__Type__", "RivCell").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
        }
    }

    public class BingoRivCellChallenge : BingoChallenge
    {
        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Feed the Rarefaction Cell to a Leviathan (completes if you die)");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase([[new Icon("Symbol_EnergyCell"), new Icon("Kill_BigEel", 1f, ChallengeUtils.ItemOrCreatureIconColor("BigEel"))]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoRivCellChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Feeding the Rarefaction Cell to a Leviathan");
        }

        public void CellExploded()
        {
            if (!completed && !revealed && !TeamsCompleted[SteamTest.team] && !hidden) CompleteChallenge();
        }

        public override Challenge Generate()
        {
            return new BingoRivCellChallenge
            {
            };
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
            return slugcat == MoreSlugcatsEnums.SlugcatStatsName.Rivulet;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoRivCellChallenge",
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
                ExpLog.Log("ERROR: BingoRivCellChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.MoreSlugcats.EnergyCell.Explode += EnergyCell_Explode;
            //On.MoreSlugcats.EnergyCell.Use += EnergyCell_Use;
            On.MoreSlugcats.EnergyCell.Update += EnergyCell_Update;
            IL.Room.Loaded += Room_LoadedEnergyCell;
        }

        public override void RemoveHooks()
        {
            On.MoreSlugcats.EnergyCell.Explode -= EnergyCell_Explode;
            //On.MoreSlugcats.EnergyCell.Use -= EnergyCell_Use;
            On.MoreSlugcats.EnergyCell.Update -= EnergyCell_Update;
            IL.Room.Loaded -= Room_LoadedEnergyCell;
        }

        public override List<object> Settings() => [];

        public override bool RequireSave() => false;
    }
}
