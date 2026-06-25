using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu.Remix;
using MoreSlugcats;
using Watcher;
using CreatureType = CreatureTemplate.Type;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class WatcherBingoCreaturePortalChallenge : BingoChallenge
    {
        public SettingBox<int> amount;
        public int current;
        public SettingBox<string> crit;
        public Dictionary<EntityID, List<string>> creaturePortals = [];

        public WatcherBingoCreaturePortalChallenge()
        {
            amount = new(0, "Amount", 0);
            crit = new("", "Creature Type", 1, listName: "transport");
        }

        public override void UpdateDescription()
        {
            if (ChallengeTools.creatureNames == null)
            {
                ChallengeTools.CreatureName(ref ChallengeTools.creatureNames);
            }
            this.description = ChallengeTools.IGT.Translate("Transport the same <crit> through [<current>/<amount>] portals")
                .Replace("<current>", ValueConverter.ConvertToString(current))
                .Replace("<amount>", ValueConverter.ConvertToString(amount.Value))
                .Replace("<crit>", ChallengeTools.creatureNames[new CreatureType(crit.Value).Index].TrimEnd('s'));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase(
                [[Icon.FromEntityName(crit.Value), new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.white, 90), new Icon("portal")],
                [new Counter(current, amount.Value)]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not WatcherBingoCreaturePortalChallenge g || (g.crit.Value != crit.Value && !(g.crit.Value.Contains("Cicada") && crit.Value.Contains("Cicada")));
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Transporting the same creature through portals");
        }

        public override Challenge Generate()
        {
            string[] crits = ChallengeUtils.GetCorrectListForChallenge("transport");
            return new WatcherBingoCreaturePortalChallenge
            {
                amount = new(UnityEngine.Random.Range(1, 4), "Amount", 0),
                crit = new(crits[UnityEngine.Random.Range(0, crits.Length)], "Creature Type", 1, listName: "transport")
            };
        }

        public void Entered(WarpPoint warpPoint, List<AbstractPhysicalObject> objects)
        {
            if (hidden || revealed || TeamsCompleted[SteamTest.team] || completed) return;
            string to = warpPoint.room.abstractRoom.name.ToUpperInvariant();
            string from = warpPoint.Data.destRoom.ToUpperInvariant();
            string warp = "";

            foreach (var portal in ChallengeUtils.watcherPortals)
            {
                var parts = portal.Split('-');
                if (parts[0] == to || parts[1] == to)
                    warp = portal;
            }
            

            foreach (var spot in ChallengeUtils.watcherSTSpots)
            {
                var parts = spot.Split('-');
                if (parts[0] == to || parts[1] == to)
                    warp = spot;
            }

            // dynamic warp, we use a lexicographical sort to make sure that going either way through a dyn warp counts as the same warp (there is no real notion of to and from, just the constituent ends)
            if (warp == "")
            {
                string[] toFromSorted = new string[] { to, from };
                Array.Sort(toFromSorted);
                warp = "DYNAMICENTRY-" + toFromSorted[0] + "-" + toFromSorted[1];
            }

            
            List<AbstractCreature> foundCreatures = [];
            bool addedPortalCreatures = false;


            // Player isn't real yet
            for (int i = 0; i < game.Players.Count; i++)
            {
                if (game.Players[i] != null && game.Players[i].realizedCreature is Player player && warpPoint.room != null)
                {
                    if (!addedPortalCreatures)
                    {
                        foundCreatures.AddRange(objects.Select(x => x).OfType<AbstractCreature>().Where(c => c.creatureTemplate.type.value == crit.Value));
                        addedPortalCreatures = true;
                    }
                    if (player.objectInStomach is AbstractCreature stomacreature && stomacreature.creatureTemplate.type.value == crit.Value)
                    {
                        if (!foundCreatures.Contains(stomacreature)) foundCreatures.Add(stomacreature);
                    }
                }
            }

            if (foundCreatures.Count == 0) return;

            foreach (var portalCrit in foundCreatures)
            {
                EntityID id = portalCrit.ID;
                if (!creaturePortals.ContainsKey(id))
                {
                    Plugin.logger.LogInfo("Adding new creature to dict + " + id + " " + warp);
                    creaturePortals.Add(id, [warp]);
                }
                else
                {
                    if (!creaturePortals[id].Contains(warp))
                    {
                        Plugin.logger.LogInfo("Adding new warp to creature in dict + " + id + " " + warp);
                        creaturePortals[id].Add(warp);
                    }
                }
            }
            foundCreatures.Sort(delegate (AbstractCreature one, AbstractCreature two)
            {
                int count1 = creaturePortals[one.ID].Count;
                int count2 = creaturePortals[two.ID].Count;
                return count2.CompareTo(count1);
            });
            int last = current;
            current = creaturePortals[foundCreatures[0].ID].Count;
            UpdateDescription();
            if (current >= amount.Value) CompleteChallenge();
            else if (last != current) ChangeValue();
        }

        public override void Reset()
        {
            base.Reset();

            creaturePortals?.Clear();
            creaturePortals = [];
            current = 0;
        }

        public override int Points()
        {
            return amount.Value * 10;
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return slugcat == WatcherEnums.SlugcatStatsName.Watcher;
        }

        public string CreatureportalsToString()
        {
            List<string> joinLater = [];

            foreach (var kvp in creaturePortals)
            {
                joinLater.Add(kvp.Key.ToString() + "|" + string.Join("|", kvp.Value));
            }

            if (joinLater.Count == 0) return "empty";

            return string.Join("%", joinLater);
        }

        public Dictionary<EntityID, List<string>> CreatureportalsFromString(string from)
        {
            Dictionary<EntityID, List<string>> portals = [];

            if (from == "empty") return portals;

            string[] gateCrits = from.Split('%');
            for (int i = 0; i < gateCrits.Length; i++)
            {
                string[] split = gateCrits[i].Split('|');
                EntityID id = EntityID.FromString(split[0]);
                List<string> rooms = [];
                for (int r = 1; r < split.Length; r++)
                {
                    rooms.Add(split[r]);
                }
                portals[id] = rooms;
            }

            return portals;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "WatcherBingoCreaturePortalChallenge",
                "~",
                crit.ToString(),
                "><",
                current.ToString(),
                "><",
                amount.ToString(),
                "><",
                CreatureportalsToString(),
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
                crit = SettingBoxFromString(array[0]) as SettingBox<string>;
                current = int.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                amount = SettingBoxFromString(array[2]) as SettingBox<int>;
                creaturePortals = CreatureportalsFromString(array[3]);
                completed = (array[4] == "1");
                revealed = (array[5] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: WatcherBingoCreaturePortalChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.Watcher.WarpPoint.ChangeState += Watcher_WarpPoint_ChangeState_CreaturePortal;
        }

        public override void RemoveHooks()
        {
            On.Watcher.WarpPoint.ChangeState -= Watcher_WarpPoint_ChangeState_CreaturePortal;
        }

        public override List<object> Settings() => [amount, crit];
    }
}