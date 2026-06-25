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

    public class BingoPearlDeliveryRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> region;

        public override Challenge Random()
        {
            BingoPearlDeliveryChallenge challenge = new();
            challenge.region.Value = region.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}region-{region.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "PearlDelivery").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            region = Randomizer<string>.InitDeserialize(dict["region"]);
        }
    }

    public class BingoPearlDeliveryChallenge : BingoChallenge
    {
        public SettingBox<string> region;
        public int iterator = -1;

        public BingoPearlDeliveryChallenge()
        {
            region = new("", "Pearl from Region", 0, listName: "regions");
        }

        public override void UpdateDescription()
        {
            region.Value = region.Value.Substring(0, 2);
            string newValue = (ModManager.MSC && ExpeditionData.slugcatPlayer == MoreSlugcatsEnums.SlugcatStatsName.Artificer) ? ChallengeTools.IGT.Translate("Five Pebbles") : ChallengeTools.IGT.Translate("Looks To The Moon");
            this.description = ChallengeTools.IGT.Translate("Deliver the <region> colored pearl to <iterator>").Replace("<region>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(region.Value, ExpeditionData.slugcatPlayer))).Replace("<iterator>", newValue);
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase(
                [[new Verse(region.Value), Icon.DATA_PEARL],
                [new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.white, 180)],
                [ExpeditionData.slugcatPlayer == MoreSlugcatsEnums.SlugcatStatsName.Artificer ? Icon.PEBBLES : Icon.MOON]]);
        }

        public override void Update()
        {
            base.Update();
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden) return;
            if (this.iterator == -1)
            {
                this.iterator = ((ModManager.MSC && ExpeditionData.slugcatPlayer == MoreSlugcatsEnums.SlugcatStatsName.Artificer) ? 1 : 0);
            }
            for (int i = 0; i < this.game.Players.Count; i++)
            {
                if (this.game.Players[i] != null && this.game.Players[i].realizedCreature != null && this.game.Players[i].realizedCreature.room != null && (this.game.Players[i].realizedCreature.room.abstractRoom.name == ((this.iterator == 0) ? "SL_AI" : "SS_AI") || (ModManager.MSC && ExpeditionData.slugcatPlayer == MoreSlugcatsEnums.SlugcatStatsName.Spear && this.game.Players[i].realizedCreature.room.abstractRoom.name == "DM_AI")))
                {
                    for (int j = 0; j < this.game.Players[i].realizedCreature.room.updateList.Count; j++)
                    {
                        if (this.game.Players[i].realizedCreature.room.updateList[j] is DataPearl && ChallengeTools.ValidRegionPearl(this.region.Value, (this.game.Players[i].realizedCreature.room.updateList[j] as DataPearl).AbstractPearl.dataPearlType) && ((this.game.Players[i].realizedCreature.room.updateList[j] as DataPearl).firstChunk.pos.x > ((this.iterator == 0) ? 1400f : 0f) || (ModManager.MSC && ExpeditionData.slugcatPlayer == MoreSlugcatsEnums.SlugcatStatsName.Spear)))
                        {
                            this.CompleteChallenge();
                        }
                    }
                }
            }
        }

        public override Challenge Generate()
        {
            string[] slugcatStoryRegions = SlugcatStats.SlugcatStoryRegions(ExpeditionData.slugcatPlayer).ToArray();
            List<string> list = new List<string>();
            for (int i = 0; i < slugcatStoryRegions.Length; i++)
            {
                if (!ChallengeTools.PearlRegionBlackList().Contains(slugcatStoryRegions[i]))
                {
                    list.Add(slugcatStoryRegions[i]);
                }
            }
            string text = "SU"; 
            if (list.Count > 0) text = list[UnityEngine.Random.Range(0, list.Count)];
            return new BingoPearlDeliveryChallenge
            {
                region = new(text, "Pearl from Region", 0, listName: "regions")
            };
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Delivering colored pearls");
        }

        public override int Points()
        {
            return ((ModManager.MSC && ExpeditionData.slugcatPlayer == MoreSlugcatsEnums.SlugcatStatsName.Spear) ? 50 : (30 * (int)(this.hidden ? 2f : 1f))) + 10;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoPearlDeliveryChallenge c || c.region.Value != region.Value;
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return (!ModManager.MSC || !(slugcat == MoreSlugcatsEnums.SlugcatStatsName.Saint)) && (ModManager.MSC || !(slugcat == SlugcatStats.Name.Yellow));
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoPearlDeliveryChallenge",
                "~",
                region.ToString(),
                "><",
                this.completed ? "1" : "0",
                "><",
                this.revealed ? "1" : "0",
            });
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                this.region = SettingBoxFromString(array[0]) as SettingBox<string>;
                this.completed = (array[1] == "1");
                this.revealed = (array[2] == "1");
                this.UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoPearlDeliveryChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override bool CanBeHidden()
        {
            return false;
        }

        public override void AddHooks()
        {
        }

        public override void RemoveHooks()
        {
        }

        public override List<object> Settings() => [region];
    }
}
