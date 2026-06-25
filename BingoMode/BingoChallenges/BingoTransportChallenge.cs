using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu.Remix;
using MoreSlugcats;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using CreatureType = CreatureTemplate.Type;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoTransportRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> from;
        public Randomizer<string> to;
        public Randomizer<string> crit;

        public override Challenge Random()
        {
            BingoTransportChallenge challenge = new();
            challenge.from.Value = from.Random();
            challenge.to.Value = to.Random();
            challenge.crit.Value = crit.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}from-{from.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}to-{to.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}crit-{crit.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "Transport").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            from = Randomizer<string>.InitDeserialize(dict["from"]);
            to = Randomizer<string>.InitDeserialize(dict["to"]);
            crit = Randomizer<string>.InitDeserialize(dict["crit"]);
        }
    }

    public class BingoTransportChallenge : BingoChallenge
    {
        public SettingBox<string> from;
        public SettingBox<string> to;
        public SettingBox<string> crit;
        public List<EntityID> origins = [];

        public BingoTransportChallenge()
        {
            from = new("", "From Region", 0, listName: "regions");
            to = new("", "To Region", 1, listName: "regions");
            crit = new("", "Creature Type", 2, listName: "transport");
            origins = [];
        }

        public override void UpdateDescription()
        {
            if (ChallengeTools.creatureNames == null)
            {
                ChallengeTools.CreatureName(ref ChallengeTools.creatureNames);
            }
            description = ChallengeTools.IGT.Translate("Transport a <crit><from><to>")
                .Replace("<crit>", ChallengeUtils.CreatureSingularNames(crit.Value))
                .Replace("<from>", from.Value != "Any Region" ? (to.Value == "Any Region" ? ChallengeTools.IGT.Translate(" out of ") : ChallengeTools.IGT.Translate(" from ")) + ChallengeTools.IGT.Translate(Region.GetRegionFullName(from.Value, ExpeditionData.slugcatPlayer)) : "")
                .Replace("<to>", to.Value != "Any Region" ? (ChallengeTools.IGT.Translate(" to ") + ChallengeTools.IGT.Translate(Region.GetRegionFullName(to.Value, ExpeditionData.slugcatPlayer))) : "");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new(
                [[Icon.FromEntityName(crit.Value)],
                [new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.white, 90)]]);
            if (from.Value != "Any Region") phrase.InsertWord(new Verse(from.Value), 1, 0);
            if (to.Value != "Any Region") phrase.InsertWord(new Verse(to.Value), 1);
            return phrase;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoTransportChallenge c || (crit.Value != c.crit.Value && (from.Value != c.from.Value || to.Value != c.to.Value));
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Transporting creatures");
        }

        public override Challenge Generate()
        {
            // hate this this took me like half an hour
            SlugcatStats.Name slug = ExpeditionData.slugcatPlayer;
            string[] possible = ChallengeUtils.GetCorrectListForChallenge("transport");
            string crug = possible[Random.Range(0, possible.Length - (ModManager.MSC && slug != SlugcatStats.Name.Red && slug != MoreSlugcatsEnums.SlugcatStatsName.Spear && slug != MoreSlugcatsEnums.SlugcatStatsName.Artificer ? 0 : 1))];
            List<string> origRegions = ChallengeUtils.CreatureOriginRegions(crug, slug);
            List<string> allRegions = crug == "JetFish" ? ["SB"] : [.. ChallengeUtils.GetCorrectListForChallenge("regionsreal", true)];
            string fromage = Random.value < 0.5f ? "Any Region" : origRegions[Random.Range(0, origRegions.Count)];
            allRegions.Remove(fromage);
            allRegions.Remove("MS");
            string toto = fromage == "Any Region" || Random.value < 0.5f ? allRegions[Random.Range(0, allRegions.Count)] : "Any Region";
            return new BingoTransportChallenge
            {
                from = new(fromage, "From Region", 0, listName: "regions"),
                to = new(toto, "To Region", 1, listName: "regions"),
                crit = new(crug, "Creature Type", 2, listName: "transport"),
                origins = []
            };
        }

        public void Grabbed(Creature c)
        {
            if (!completed && !revealed && !hidden && !TeamsCompleted[SteamTest.team] && c.Template.type.value == crit.Value)
            {
                string rr = c.room.world.region.name;
                if ((rr == from.Value || from.Value == "Any Region") && !origins.Contains(c.abstractCreature.ID))
                {
                    origins.Add(c.abstractCreature.ID);
                }
            }
        }

        public void Gate(string regionName)
        {
            if (regionName != to.Value && to.Value != "Any Region") return;
            
            bool g = false;
            for (int i = 0; i < game.Players.Count; i++)
            {
                if (game.Players[i] != null && game.Players[i].realizedCreature is Player player && player.room != null)
                {
                    foreach (var cc in player.room.updateList)
                    {
                        if (cc is Creature crib && crib.Template.type.value == crit.Value && origins.Contains(crib.abstractCreature.ID))
                        {
                            g = true;
                            
                            break;
                        }
                    }
                    if (!g && player.objectInStomach is AbstractCreature stomacreature && stomacreature.creatureTemplate.type.value == crit.Value && origins.Contains(stomacreature.ID))
                    {
                        g = true;
                        
                    }
                    if (g) break;
                }
            }

            if (g && !completed && !revealed)
            {
                CompleteChallenge();
            }
        }

        public override void Reset()
        {
            base.Reset();
            origins = [];
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

        // Origins later
        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoTransportChallenge",
                "~",
                from.ToString(),
                "><",
                to.ToString(),
                "><",
                crit.ToString(),
                "><",
                string.Join("|", origins),
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
                from = SettingBoxFromString(array[0]) as SettingBox<string>;
                to = SettingBoxFromString(array[1]) as SettingBox<string>;
                crit = SettingBoxFromString(array[2]) as SettingBox<string>;
                string[] arr = array[3].Split('|');
                origins = [];
                if (arr != null && arr.Length > 0)
                {
                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (arr[i] != string.Empty) origins.Add(EntityID.FromString(arr[i]));
                    }
                }
                completed = (array[4] == "1");
                revealed = (array[5] == "1");
                UpdateDescription();
            }
            catch (System.Exception ex)
            {
                ExpLog.Log("ERROR: BingoTransportChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.Player.SlugcatGrab += Player_SlugcatGrab;
            On.WorldLoader.ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues += WorldLoader_Transport;
        }

        public override void RemoveHooks()
        {
            On.Player.SlugcatGrab -= Player_SlugcatGrab;
            On.WorldLoader.ctor_RainWorldGame_Name_Timeline_bool_string_Region_SetupValues -= WorldLoader_Transport;
        }

        public override List<object> Settings() => [from, to, crit];
    }
}