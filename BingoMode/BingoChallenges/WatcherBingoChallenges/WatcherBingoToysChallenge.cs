using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Watcher;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public enum ToyType 
    {
        SoftToy,
        WeirdToy,
        SpinToy,
        BallToy
    };

    public class WatcherBingoToysChallenge : BingoOneCycleChallenge
    {
        public List<ToyType> collected = [];

        public WatcherBingoToysChallenge()
        {
            oneCycle = new(true, "In One Cycle", 0);
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Touch all the Watcher toys in one cycle");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase([[new Icon("Symbol_BallToy", 1f, ChallengeUtils.ItemOrCreatureIconColor("BallToy")), new Icon("Symbol_WeirdToy", 1f, ChallengeUtils.ItemOrCreatureIconColor("WeirdToy"))],
            [new Counter(current, 4)],
            [new Icon("Symbol_SpinToy", 1f, ChallengeUtils.ItemOrCreatureIconColor("SpinToy")), new Icon("Symbol_SoftToy", 1f, ChallengeUtils.ItemOrCreatureIconColor("SoftToy"))]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not WatcherBingoToysChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Touching Watcher toys");
        }

        public override void Update()
        {
            base.Update();
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden) return;
            for (int i = 0; i < game.Players.Count; i++)
            {
                if (game.Players[i] != null && game.Players[i].realizedCreature is Player player && player.room != null && player.room.abstractRoom.name.ToUpperInvariant() == "WAUA_TOYS")
                {
                    for (int j = 0; j < player.room.physicalObjects.Length; j++)
                    {
                        for (int k = 0; k < player.room.physicalObjects[j].Count; k++)
                        {
                            PhysicalObject obj = player.room.physicalObjects[j][k];
                            if (obj is UrbanToys.WeirdToy t && t.interactedWith && !collected.Contains(ToyType.WeirdToy))
                            {
                                AddToyHelper(ToyType.WeirdToy);
                            }
                            else if (obj is UrbanToys.SoftToy t2 && t2.interactedWith && !collected.Contains(ToyType.SoftToy))
                            {
                                AddToyHelper(ToyType.SoftToy);
                            }
                            else if (obj is UrbanToys.SpinToy t3 && t3.interactedWith && !collected.Contains(ToyType.SpinToy))
                            {
                                AddToyHelper(ToyType.SpinToy);
                            }
                            else if (obj is UrbanToys.BallToy t4 && t4.interactedWith && !collected.Contains(ToyType.BallToy))
                            {
                                AddToyHelper(ToyType.BallToy);
                            }
                        }
                    }
                }
            }
        }

        private void AddToyHelper(ToyType toy)
        {
            collected.Add(toy);
            current++;
            UpdateDescription();
            if (current >= 4)
            {
                Plugin.logger.LogInfo("complete challenge when adding toy: " + toy);
                CompleteChallenge();
            }
            else ChangeValue();
        }

        public override void Reset()
        {
            current = 0;
            collected?.Clear();
            collected = [];
            base.Reset();
        }

        public override Challenge Generate()
        {
            return new WatcherBingoToysChallenge
            {
                oneCycle = new(true, "In One Cycle", 0),
            };
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override bool ValidForThisBingoSlugcat(SlugName slugcat, BingoData.BingoModifier modifier)
        {
            return modifier == BingoData.BingoModifier.WatcherMode;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "WatcherBingoToysChallenge",
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
                ExpLog.Log("ERROR: WatcherBingoToysChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
        }

        public override void RemoveHooks()
        {
        }

        public override List<object> Settings() => [];
    }
}
