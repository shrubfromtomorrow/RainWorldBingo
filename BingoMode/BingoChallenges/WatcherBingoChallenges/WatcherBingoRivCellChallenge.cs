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

    public class WatcherBingoRivCellChallenge : BingoChallenge
    {
        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Feed the Rarefaction Cell to an Angler (completes if you die)");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase([[new Icon("Symbol_EnergyCell"), new Icon("Kill_Angler", 1f, ChallengeUtils.ItemOrCreatureIconColor("BigEel"))]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not WatcherBingoRivCellChallenge && challenge is not BingoRivCellChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Feeding the Rarefaction Cell to an Angler");
        }

        public void CellExploded()
        {
            if (!completed && !revealed && !TeamsCompleted[SteamTest.team] && !hidden) CompleteChallenge();
        }

        public override Challenge Generate()
        {
            return new WatcherBingoRivCellChallenge
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

        public override bool ValidForThisBingoSlugcat(SlugName slugcat, BingoData.BingoModifier modifier)
        {
            return modifier == BingoData.BingoModifier.WatcherMode && slugcat == SlugNameMSC.Rivulet;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "WatcherBingoRivCellChallenge",
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
                ExpLog.Log("ERROR: WatcherBingoRivCellChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            // the interaction is added in Angler_JawsSlamShut and thus the call is from there
            On.MoreSlugcats.EnergyCell.Update += Watcher_EnergyCell_Update;
            IL.Room.Loaded += Watcher_Room_LoadedEnergyCell;
        }

        public override void RemoveHooks()
        {
            On.MoreSlugcats.EnergyCell.Update -= Watcher_EnergyCell_Update;
            IL.Room.Loaded -= Watcher_Room_LoadedEnergyCell;
        }

        public override List<object> Settings() => [];

        public override bool RequireSave() => false;
    }
}
