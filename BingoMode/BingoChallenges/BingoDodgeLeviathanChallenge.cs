using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoDodgeLeviathanRandomizer : ChallengeRandomizer
    {
        public override Challenge Random()
        {
            BingoDodgeLeviathanChallenge challenge = new();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            return base.Serialize(indent).Replace("__Type__", "DodgeLeviathan").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
        }
    }

    public class BingoDodgeLeviathanChallenge : BingoChallenge
    {
        //public int wasInArea;

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Dodge a Leviathan's bite");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase([[new Icon("leviathan_dodge")]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoDodgeLeviathanChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Dodging a Leviathan");
        }

        public override Challenge Generate()
        {
            return new BingoDodgeLeviathanChallenge
            {
            };
        }

        public override void Update()
        {
            base.Update();
            if (completed || hidden || revealed || TeamsCompleted[SteamTest.team] || Custom.rainWorld.processManager.upcomingProcess != null) return;
            //wasInArea = Mathf.Max(0, wasInArea - 1);
            for (int i = 0; i < game.Players.Count; i++)
            {
                if (game.Players[i] != null
                    && game.Players[i].realizedCreature != null
                    && game.Players[i].realizedCreature.room != null)
                {
                    Player player = game.Players[i].realizedCreature as Player;
                    Room room = player.room;

                    for (int j = 0; j < room.physicalObjects.Length; j++)
                    {
                        for (int k = 0; k < room.physicalObjects[j].Count; k++)
                        {
                            if (!room.physicalObjects[j][k].slatedForDeletetion && room.physicalObjects[j][k] is BigEel levi && levi.jawCharge > 0f && levi.InBiteArea(player.bodyChunks[1].pos, 10f))
                            {
                                CompleteChallenge();
                                return;
                            }
                        }
                    }
                }
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
            return true;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoDodgeLeviathanChallenge",
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
                ExpLog.Log("ERROR: BingoDodgeLeviathanChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            //On.BigEel.JawsSnap += BigEel_JawsSnap;
            //On.BigEel.Update += BigEel_Update;
        }

        public override void RemoveHooks()
        {
            //On.BigEel.JawsSnap -= BigEel_JawsSnap;
            //On.BigEel.Update -= BigEel_Update;
        }

        public override List<object> Settings() => [];
    }
}
