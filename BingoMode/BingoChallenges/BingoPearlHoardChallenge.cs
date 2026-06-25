using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu.Remix;
using MoreSlugcats;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;
    using static MonoMod.InlineRT.MonoModRule;

    public class BingoPearlHoardRandomizer : ChallengeRandomizer
    {
        public Randomizer<bool> common;
        public Randomizer<string> region;
        public Randomizer<int> amount;
        public Randomizer<bool> anyShelter;

        public override Challenge Random()
        {
            BingoPearlHoardChallenge challenge = new();
            challenge.common.Value = common.Random();
            challenge.region.Value = region.Random();
            challenge.amount.Value = amount.Random();
            challenge.anyShelter.Value = anyShelter.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}common-{common.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}region-{region.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}anyShelter-{anyShelter.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "PearlHoard").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            common = Randomizer<bool>.InitDeserialize(dict["common"]);
            region = Randomizer<string>.InitDeserialize(dict["region"]);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
            anyShelter = Randomizer<bool>.InitDeserialize(dict["anyShelter"]);
        }
    }

    public class BingoPearlHoardChallenge : BingoChallenge
    {
        public SettingBox<bool> common;
        public int current;
        public SettingBox<int> amount;
        public SettingBox<bool> anyShelter;
        public SettingBox<string> region;
        public List<string> collected = [];

        public BingoPearlHoardChallenge()
        {
            common = new(false, "Common Pearls", 0);
            amount = new(0, "Amount", 1);
            anyShelter = new(false, "Any Shelter", 2);
            region = new("", "Region", 3, listName: "regionsreal");
        }

        public override void UpdateDescription()
        {
            string location = region.Value != "Any Region" ? ChallengeTools.IGT.Translate(Region.GetRegionFullName(region.Value, ExpeditionData.slugcatPlayer)) : "";
            this.description = ChallengeTools.IGT.Translate("<action> [<current>/<amount>] <target_item> <shelter_type> shelter <location>")
                .Replace("<action>", anyShelter.Value ? ChallengeTools.IGT.Translate("Bring") : ChallengeTools.IGT.Translate("Hoard"))
                .Replace("<current>", ValueConverter.ConvertToString(current))
                .Replace("<amount>", ValueConverter.ConvertToString<int>(this.amount.Value))
                .Replace("<target_item>", common.Value ? ChallengeTools.IGT.Translate("common pearls") : ChallengeTools.IGT.Translate("colored pearls"))
                .Replace("<shelter_type>", anyShelter.Value ? ChallengeTools.IGT.Translate("to any") : ChallengeTools.IGT.Translate("in the same"))
                .Replace("<location>", location != "" ? ChallengeTools.IGT.Translate("in ") + location : "");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = anyShelter.Value ?
                new Phrase([[common.Value ? Icon.PEARL_HOARD_NORMAL : Icon.PEARL_HOARD_COLOR, new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.white, 90), new Icon("doubleshelter")]]) :
                new Phrase([[new Icon("ShelterMarker"), common.Value ? Icon.PEARL_HOARD_NORMAL : Icon.PEARL_HOARD_COLOR]]);
            int lastLine = 1;
            if (region.Value != "Any Region")
            {
                phrase.InsertWord(new Verse(region.Value), 1);
                lastLine = 2;
            }
            phrase.InsertWord(new Counter(current, amount.Value), lastLine);
            return phrase;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoPearlHoardChallenge c || c.common.Value != common.Value || c.region.Value != region.Value || c.anyShelter.Value != anyShelter.Value;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Putting pearls in shelters");
        }

        public override Challenge Generate()
        {
            bool flag = false;
            if ((ModManager.MSC && ExpeditionData.slugcatPlayer == MoreSlugcatsEnums.SlugcatStatsName.Saint) || (!ModManager.MSC && ExpeditionData.slugcatPlayer == SlugcatStats.Name.Yellow))
            {
                flag = true;
            }
            string[] array = ChallengeUtils.GetCorrectListForChallenge("regionsreal");
            bool spec = UnityEngine.Random.value < 0.5f;
            string region = spec ? "Any Region" : array[UnityEngine.Random.Range(0, array.Length)];
            return new BingoPearlHoardChallenge
            {
                common = new(flag, "Common Pearls", 0),
                amount = new(UnityEngine.Random.Range(1, 4), "Amount", 1),
                anyShelter = new(UnityEngine.Random.value < 0.5f, "Any Shelter", 2),
                region = new(region, "Region", 3, listName: "regions"),
            };
        }

        public override int Points()
        {
            return (this.common.Value ? 10 : 23) * this.amount.Value * (int)(this.hidden ? 2f : 1f);
        }

        public override bool CombatRequired()
        {
            return false;
        }

        // :slughollow:
        public override void Update()
        {
            base.Update();
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden || Custom.rainWorld.processManager.upcomingProcess != null)
                return;

            for (int i = 0; i < this.game.Players.Count; i++)
            {
                var player = this.game.Players[i];
                if (player?.realizedCreature?.room == null || !player.realizedCreature.room.abstractRoom.shelter)
                    continue;

                int num = 0;
                int num2 = 0;

                foreach (var obj in player.realizedCreature.room.updateList)
                {
                    if (obj is DataPearl p && ItemInLocation(p.abstractPhysicalObject))
                    {
                        string id = p.abstractPhysicalObject.ID.ToString();
                        bool isMisc = p.AbstractPearl.dataPearlType.value == DataPearl.AbstractDataPearl.DataPearlType.Misc.value
                                      || p.AbstractPearl.dataPearlType.value == DataPearl.AbstractDataPearl.DataPearlType.Misc2.value;
                        bool isColored = !isMisc || p is PebblesPearl;

                        if (anyShelter.Value)
                        {
                            if (!collected.Contains(id))
                            {
                                if ((isMisc && common.Value) || (isColored && !common.Value))
                                {
                                    collected.Add(id);
                                    current++;
                                    UpdateDescription();

                                    if (current >= amount.Value)
                                    {
                                        CompleteChallenge();
                                        return;
                                    }
                                    else
                                    {
                                        ChangeValue();
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (common.Value && isMisc)
                            {
                                num++;
                                if (num >= amount.Value)
                                {
                                    current = num;
                                    UpdateDescription();
                                    CompleteChallenge();
                                    return;
                                }
                            }
                            else if (!common.Value && isColored)
                            {
                                num2++;
                                if (num2 >= amount.Value)
                                {
                                    current = num2;
                                    UpdateDescription();
                                    CompleteChallenge();
                                    return;
                                }
                            }

                            UpdateDescription();
                        }
                    }
                }
            }
        }

        public bool ItemInLocation(AbstractPhysicalObject apo)
        {
            string location = region.Value != "Any Region" ? region.Value : "boowomp";
            AbstractRoom room = apo.Room;
            if (location.ToLowerInvariant() == region.Value.ToLowerInvariant())
            {
                return room.world.region.name.ToLowerInvariant() == location.ToLowerInvariant();
            }
            else return true;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoPearlHoardChallenge",
                "~",
                common.ToString(),
                "><",
                anyShelter.ToString(),
                "><",
                current.ToString(),
                "><",
                amount.ToString(),
                "><",
                region.ToString(),
                "><",
                completed ? "1" : "0",
                "><",
                revealed ? "1" : "0",
                "><",
                string.Join("cLtD", collected)
            });
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                common = SettingBoxFromString(array[0]) as SettingBox<bool>;
                anyShelter = SettingBoxFromString(array[1]) as SettingBox<bool>;
                current = int.Parse(array[2], NumberStyles.Any, CultureInfo.InvariantCulture);
                amount = SettingBoxFromString(array[3]) as SettingBox<int>;
                region = SettingBoxFromString(array[4]) as SettingBox<string>;
                completed = (array[5] == "1");
                revealed = (array[6] == "1");
                string[] arr = Regex.Split(array[7], "cLtD");
                collected = [.. arr];
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoPearlHoardChallenge FromString() encountered an error: " + ex.Message);
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

        public override List<object> Settings() => [common, amount, anyShelter, region];
    }
}
