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

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class WatcherBingoPearlDeliveryChallenge : BingoChallenge
    {
        public SettingBox<string> pearl;
        public SettingBox<bool> common;
        public string region;

        public WatcherBingoPearlDeliveryChallenge()
        {
            pearl = new("", "Pearl", 0, listName: ChallengeListConstants.Pearls);
            common = new(false, "Common", 1);
        }

        public override void UpdateDescription()
        {
            region = Regex.Split(pearl.Value, "_")[0];
            this.description = ChallengeTools.IGT.Translate("Deliver <article> <type> pearl to the pearl reader in Ancient Urban").Replace("<article>", common.Value ? "a" : "the")
                .Replace("<type>", common.Value ? ChallengeTools.IGT.Translate("common") : ChallengeTools.IGT.Translate(Region.GetRegionFullName(region, BingoData.slugcatPlayer)));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            if (common.Value)
            {
                return new Phrase(
                    [[Icon.DATA_PEARL],
                [new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.white, 180)],
                [new Icon("pearlreader")]]);
            }
            else
            {
                return new Phrase(
                    [[new Verse(region), new Icon("Symbol_Pearl", 1f, DataPearl.UniquePearlMainColor(new(region.Length == 4 ? pearl.Value.Substring(5) : pearl.Value, false))) { background = new FSprite("radialgradient") }],
                [new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.white, 180)],
                [new Icon("pearlreader")]]);
            }
        }

        public override Challenge Generate()
        {
            bool b = UnityEngine.Random.value < 0.25f;
            string p = ChallengeUtils.GetCorrectListForChallenge(ChallengeListConstants.Pearls)[UnityEngine.Random.Range(0, ChallengeUtils.GetCorrectListForChallenge(ChallengeListConstants.Pearls).Length)];
            WatcherBingoPearlDeliveryChallenge chal = new()
            {
                pearl = new(p, "Pearl", 0, listName: ChallengeListConstants.Pearls),
                common = new(b, "Common", 1),
                region = Regex.Split(p, "_")[0]
            };

            return chal;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Delivering pearls to the pearl reader");
        }

        public void Delivered(DataPearl p)
        {
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden) return;
            string id = p.abstractPhysicalObject.ID.ToString();
            bool isMisc = p.AbstractPearl.dataPearlType.value == DataPearl.AbstractDataPearl.DataPearlType.Misc.value
                          || p.AbstractPearl.dataPearlType.value == DataPearl.AbstractDataPearl.DataPearlType.Misc2.value;
            bool isColored = !isMisc || p is PebblesPearl;

            if (common.Value && isMisc)
            {
                UpdateDescription();
                CompleteChallenge();
            }
            else if (!common.Value && isColored && (p.AbstractPearl.dataPearlType.value == (region.Length == 4 ? pearl.Value.Substring(pearl.Value.IndexOf('_') + 1) : pearl.Value)))
            {
                UpdateDescription();
                CompleteChallenge();
            }
        }

        public override int Points()
        {
            return 25;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not WatcherBingoPearlDeliveryChallenge c || c.common.Value != common.Value || ((!c.common.Value && !common.Value) && c.region != region);
        }

        public override bool ValidForThisBingoSlugcat(SlugName slugcat, BingoData.BingoModifier modifier)
        {
            return modifier == BingoData.BingoModifier.WatcherMode || slugcat == SlugNameWatcher.Watcher;
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "WatcherBingoPearlDeliveryChallenge",
                "~",
                pearl.ToString(),
                "><",
                common.ToString(),
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
                this.pearl = SettingBoxFromString(array[0]) as SettingBox<string>;
                this.common = SettingBoxFromString(array[1]) as SettingBox<bool>;
                region = pearl == null ? "noregion" : Regex.Split(pearl.Value, "_")[0];
                this.completed = (array[2] == "1");
                this.revealed = (array[3] == "1");
                this.UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: WatcherBingoPearlDeliveryChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override bool CanBeHidden()
        {
            return false;
        }

        public override void AddHooks()
        {
            On.Watcher.PearlReader.HaltPearl += Watcher_PearlReader_HaltPearl;
        }

        public override void RemoveHooks()
        {
            On.Watcher.PearlReader.HaltPearl -= Watcher_PearlReader_HaltPearl;
        }

        public override List<object> Settings() => [pearl, common];
    }
}
