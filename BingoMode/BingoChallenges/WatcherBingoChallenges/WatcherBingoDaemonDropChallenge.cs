using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges.WatcherBingoChallenges
{
    using static ChallengeHooks;
    public class WatcherBingoDaemonDropChallenge : BingoChallenge
    {
        public SettingBox<string> crit;

        public WatcherBingoDaemonDropChallenge()
        {
            crit = new("", "Creature Type", 0, listName: ChallengeListConstants.Daemon);
        }

        public override void UpdateDescription()
        {
            if (ChallengeTools.creatureNames == null)
            {
                ChallengeTools.CreatureName(ref ChallengeTools.creatureNames);
            }
            description = ChallengeTools.IGT.Translate("Drop a <crit> in the bottom of Daemon")
                .Replace("<crit>", ChallengeUtils.CreatureSingularNames(crit.Value));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase(
                [[Icon.FromEntityName(crit.Value)],
                [new Icon("deathpiticon")],
                [new Verse("WRSA")]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not WatcherBingoDaemonDropChallenge;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Dropping a creature in the bottom of Daemon");
        }

        public override Challenge Generate()
        {
            return new WatcherBingoDaemonDropChallenge
            {
                crit = new(ChallengeUtils.GetCorrectListForChallenge(ChallengeListConstants.Daemon)[UnityEngine.Random.Range(0, ChallengeUtils.GetCorrectListForChallenge(ChallengeListConstants.Daemon).Length)], "Creature Type", 0, listName: ChallengeListConstants.Daemon)
            };
        }

        public override void Update()
        {
            base.Update();
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden) return;
            for (int i = 0; i < game.Players.Count; i++)
            {
                if (game.Players[i] != null && game.Players[i].realizedCreature is Player player && player.room != null && player.room.abstractRoom.name.Split('_')[0].ToUpperInvariant() == "WRSA")
                {
                    for (int j = 0; j < player.room.updateList.Count; j++)
                    {
                        if (player.room.updateList[j] is Creature c && c.Template.type.value == crit.Value && c.mainBodyChunk != null && c.mainBodyChunk.pos.y < -228f)
                        {
                            CompleteChallenge();
                            return;
                        }
                    }
                }
            }
        }

        public override int Points()
        {
            return 15;
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override bool ValidForThisBingoSlugcat(SlugName slugcat, BingoData.BingoModifier modifier)
        {
            return slugcat == SlugNameWatcher.Watcher;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "WatcherBingoDaemonDropChallenge",
                "~",
                crit.ToString(),
                "><",
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
                crit = SettingBoxFromString(array[0]) as SettingBox<string>;
                completed = (array[1] == "1");
                revealed = (array[2] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: WatcherBingoDaemonDropChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
        }

        public override void RemoveHooks()
        {
        }

        public override List<object> Settings() => [crit];
    }
}
