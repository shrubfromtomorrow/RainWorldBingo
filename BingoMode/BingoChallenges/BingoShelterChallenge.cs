using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoShelterChallenge : BingoChallenge
    {
        public SettingBox<string> region;
        public SettingBox<bool> unique;
        public SettingBox<bool> differentRegions;
        public SettingBox<int> amount;
        public int current;
        public List<string> shelters = [];

        public BingoShelterChallenge()
        {
            region = new("", "Region", 0, listName: ChallengeListConstants.ShelterRegions);
            unique = new(false, "Unique shelters", 1);
            differentRegions = new(false, "Different regions", 2);
            amount = new(0, "Amount", 3);
        }

        public override void UpdateDescription()
        {
            description = ChallengeTools.IGT.Translate("Rest [<current>/<amount>] times <unique> shelters <region>")
                .Replace("<current>", current.ToString())
                .Replace("<amount>", amount.Value.ToString())
                .Replace("<unique>", "in" + (differentRegions.Value ? "" : unique.Value ? " unique" : ""))
                .Replace("<region>", differentRegions.Value ? ChallengeTools.IGT.Translate("in different regions") : region.Value == "Any Region" ? "" : ChallengeTools.IGT.Translate("in ") + ChallengeTools.IGT.Translate(Region.GetRegionFullName(region.Value, BingoData.slugcatPlayer)));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            if (differentRegions.Value)
            {
                return new Phrase(
                    [[new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.green, 90), new Icon("ShelterMarker"), new Icon("TravellerA")],
                    [new Counter(current, amount.Value)]]);
            }
            else
            {
                if (region.Value != "Any Region")
                {
                    return new Phrase(
                        [[new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.green, 90), new Icon(unique.Value ? "ShelterMarker" : "doubleshelter")],
                        [new Verse(region.Value)],
                        [new Counter(current, amount.Value)]]);
                }
                else
                {
                    return new Phrase(
                        [[new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.green, 90), new Icon(unique.Value ? "ShelterMarker" : "doubleshelter")],
                        [new Counter(current, amount.Value)]]);
                }
            }
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoShelterChallenge c || c.differentRegions.Value != differentRegions.Value;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Resting in shelters");
        }

        public override Challenge Generate()
        {
            List<string> regiones = ChallengeUtils.GetCorrectListForChallenge(ChallengeListConstants.ShelterRegions, true).ToList();
            string regionn = regiones[UnityEngine.Random.Range(0, regiones.Count)];
            bool u = UnityEngine.Random.value < 0.5f;
            bool d = UnityEngine.Random.value < 0.5f;
            int count = 0;
            Plugin.logger.LogInfo($"Region: {regionn} Different: {d} Unique: {u}");
            if (d || !u || regionn == "Any Region")
            {
                count = UnityEngine.Random.Range(1, 8);
            }
            else
            {
                count = UnityEngine.Random.Range(1, ChallengeUtils.RegionShelterCount[ExpeditionData.slugcatPlayer.value][regionn]);
            }

            return new BingoShelterChallenge
            {
                region = new(regionn, "Region", 0, listName: ChallengeListConstants.ShelterRegions),
                unique = new(u, "Unique shelters", 1),
                differentRegions = new(d, "Different regions", 2),
                amount = new(count, "Amount", 3)
            };
        }

        public void Rest(string shelter)
        {
            if (completed || revealed || hidden || TeamsCompleted[SteamTest.team]) return;
            string r = Regex.Split(shelter, "_")[0].ToUpperInvariant();
            string[] regions = shelters.Select(x => x.Split('_')[0]).ToArray();

            // I know this logic is long and unruly but I like to make these cases as explicit as possible
            if (differentRegions.Value)
            {
                if (!regions.Contains(r))
                {
                    shelters.Add(shelter);
                    Progress();
                }
            }
            else
            {
                if (region.Value != "Any Region")
                {
                    if (r == region.Value)
                    {
                        if (unique.Value && !shelters.Contains(shelter))
                        {
                            shelters.Add(shelter);
                            Progress();
                        }
                        if (!unique.Value) Progress();
                    }
                }
                else
                {
                    if (unique.Value && !shelters.Contains(shelter))
                    {
                        shelters.Add(shelter);
                        Progress();
                    }
                    if (!unique.Value) Progress();
                }
            }
        }

        private void Progress()
        {
            current++;
            UpdateDescription();

            if (current >= amount.Value) CompleteChallenge();
            else ChangeValue();
        }

        public override int Points()
        {
            return 10;
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override void Reset()
        {
            base.Reset();
            current = 0;
            shelters = [];
        }

        public override bool ValidForThisBingoSlugcat(SlugName slugcat, BingoData.BingoModifier modifier)
        {
            return true;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoShelterChallenge",
                "~",
                region.ToString(),
                "><",
                unique.ToString(),
                "><",
                differentRegions.ToString(),
                "><",
                current.ToString(),
                "><",
                amount.ToString(),
                "><",
                string.Join("|", shelters),
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
                region = SettingBoxFromString(array[0]) as SettingBox<string>;
                unique = SettingBoxFromString(array[1]) as SettingBox<bool>;
                differentRegions = SettingBoxFromString(array[2]) as SettingBox<bool>;
                current = int.Parse(array[3], System.Globalization.NumberStyles.Any);
                amount = SettingBoxFromString(array[4]) as SettingBox<int>;
                shelters = [.. array[5].Split('|')];
                completed = (array[6] == "1");
                revealed = (array[7] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoShelterChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.ShelterDoor.Close += ShelterDoor_Close1;
        }

        public override void RemoveHooks()
        {
            On.ShelterDoor.Close -= ShelterDoor_Close1;
        }

        public override List<object> Settings() => [region, unique, differentRegions, amount];
    }
}