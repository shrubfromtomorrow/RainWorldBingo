using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu.Remix;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using CreatureType = CreatureTemplate.Type;

namespace BingoMode.BingoChallenges
{
    // Copied from vanilla game and modified
    using static ChallengeHooks;

    public class BingoPinRandomizer : ChallengeRandomizer
    {
        public Randomizer<int> amount;
        public Randomizer<string> region;
        public Randomizer<string> crit;

        public override Challenge Random()
        {
            BingoPinChallenge challenge = new();
            challenge.amount.Value = amount.Random();
            challenge.region.Value = region.Random();
            challenge.crit.Value = crit.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}region-{region.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}crit-{crit.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "Pin").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
            region = Randomizer<string>.InitDeserialize(dict["region"]);
            crit = Randomizer<string>.InitDeserialize(dict["crit"]);
        }
    }

    public class BingoPinChallenge : BingoChallenge
    {
        public int current;
        public SettingBox<int> amount;
        public List<Creature> pinList = [];
        public List<Spear> spearList = [];
        public SettingBox<string> region;
        public List<string> pinRegions = [];
        public SettingBox<string> crit;

        public BingoPinChallenge()
        {
            amount = new(0, "Amount", 0);
            crit = new("", "Creature Type", 1, listName: "creatures");
            region = new("", "Region", 2, listName: "regions");
        }

        public override void UpdateDescription()
        {
            description = ChallengeTools.IGT.Translate("Pin [<current_pin>/<pin_amount>] <crit> to walls or floors<region>")
                .Replace("<current_pin>", current.ToString())
                .Replace("<pin_amount>", amount.Value.ToString())
                .Replace("<crit>", crit.Value != "Any Creature" ? ChallengeTools.creatureNames[new CreatureType(crit.Value).Index] : ChallengeTools.IGT.Translate("creatures"))
                .Replace("<region>", region.Value != "" ? region.Value == "Any Region" ? ChallengeTools.IGT.Translate(" in different regions") : ChallengeTools.IGT.Translate(" in ") + ChallengeTools.IGT.Translate(Region.GetRegionFullName(region.Value, ExpeditionData.slugcatPlayer)) : "");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new(
                [[new Icon("pin_creature")]]);
            if (crit.Value != "Any Creature") phrase.InsertWord(Icon.FromEntityName(crit.Value));
            if (region.Value == "Any Region") phrase.InsertWord(new Icon("TravellerA"), 1);
            else phrase.InsertWord(new Verse(region.Value), 1);

            phrase.InsertWord(new Counter(current, amount.Value), 2);
            return phrase;
        }

        public override int Points()
        {
            return 20;
        }

        public override Challenge Generate()
        {
            string r = "";
            int tries = 0;
            List<string> regions = [];
        shitGoBack:
            string c = Random.value < 0.3f ? ChallengeUtils.GetCorrectListForChallenge("pin")[0] : ChallengeUtils.GetCorrectListForChallenge("pin")[Random.Range(1, ChallengeUtils.GetCorrectListForChallenge("pin").Length)];

            if (BingoData.pinnableCreatureRegions == null || tries > 10)
            {
                regions = ChallengeUtils.GetCorrectListForChallenge("regionsreal", true).ToList();
            }
            else
            {
                if (!BingoData.pinnableCreatureRegions.ContainsKey(c))
                {
                    tries += 1;
                    goto shitGoBack;
                };
                regions = BingoData.pinnableCreatureRegions[c].Where(x => x.StartsWith(ExpeditionData.slugcatPlayer.value)).ToList();
            }
            float radom = Random.value; // Radom mentioned
            if (radom < 0.7f && regions.Count > 0) r = regions[Random.Range(0, regions.Count)];
            else r = "Any Region";
            if (r.IndexOf('_') != -1) r = r.Split('_')[1];
    
            return new BingoPinChallenge
            {
                amount = new(Mathf.Max(1, Mathf.FloorToInt(Random.Range(1, 4) / (r == "Any Region" ? 2f : 1f))), "Amount", 0),
                crit = new(c, "Creature Type", 1, listName: "creatures"),
                region = new(r, "Region", 2, listName: "regions"),
            };
        }

        public override void Update()
        {
            base.Update();
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden || game.Players == null) return;
            for (int i = 0; i < this.game.Players.Count; i++)
            {
                if (this.game.Players[i] != null && this.game.Players[i].realizedCreature != null && this.game.Players[i].realizedCreature.room != null)
                {
                    for (int j = 0; j < this.game.Players[i].realizedCreature.room.updateList.Count; j++)
                    {
                        if (this.game.Players[i].realizedCreature.room.updateList[j] is Spear && (this.game.Players[i].realizedCreature.room.updateList[j] as Spear).thrownBy != null && (this.game.Players[i].realizedCreature.room.updateList[j] as Spear).thrownBy is Player && !this.spearList.Contains(this.game.Players[i].realizedCreature.room.updateList[j] as Spear))
                        {
                            this.spearList.Add(this.game.Players[i].realizedCreature.room.updateList[j] as Spear);
                        }
                    }
                }
            }
            for (int k = 0; k < this.spearList.Count; k++)
            {
                if (spearList[k] == null || spearList[k].room == null || spearList[k].room.world == null || spearList[k].room.world.region == null) continue;
                if ((this.spearList[k].thrownBy != null && !(this.spearList[k].thrownBy is Player)) || this.spearList[k] == null)
                {
                    this.spearList.Remove(this.spearList[k]);
                    break;
                }
                string rr = spearList[k].room.world.region.name;
                if ((region.Value == "Any Region" || rr == region.Value) && !pinRegions.Contains(rr) && this.spearList[k].stuckInObject != null && this.spearList[k].stuckInObject is Creature c && (crit.Value == "Any Creature" || c.Template.type.value == crit.Value) && this.spearList[k].stuckInWall != null && !this.pinList.Contains(c))
                {
                    this.pinList.Add(c);
                    this.current++;
                    if (region.Value == "Any Region") pinRegions.Add(rr);
                    this.UpdateDescription();
                    if (current != amount.Value) ChangeValue();
                    this.spearList.Remove(this.spearList[k]);
                    break;
                }
            }
            if (this.current >= this.amount.Value)
            {
                this.CompleteChallenge();
            }
        }
    
        public override bool CombatRequired()
        {
            return true;
        }
    
        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoPinChallenge;
        }
    
        public override void Reset()
        {
            current = 0;
            pinList?.Clear();
            pinList = [];
            spearList?.Clear();
            spearList = [];
            base.Reset();
        }
    
        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return true;
        }
    
        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Pinning creatures");
        }
    
        public override string ToString()
        {
            return string.Concat(
            [
                "BingoPinChallenge",
                "~",
                ValueConverter.ConvertToString(current),
                "><",
                amount.ToString(),
                "><",
                crit.ToString(),
                "><",
                string.Join("|", pinRegions),
                "><",
                region.ToString(),
                "><",
                completed ? "1" : "0",
                "><",
                revealed ? "1" : "0",
            ]);
        }
    
        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                current = int.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                amount = SettingBoxFromString(array[1]) as SettingBox<int>;
                crit = SettingBoxFromString(array[2]) as SettingBox<string>;
                pinRegions = [];
                pinRegions = [.. array[3].Split('|')];
                region = SettingBoxFromString(array[4]) as SettingBox<string>;
                completed = (array[5] == "1");
                revealed = (array[6] == "1");
                pinList = [];
                spearList = [];
                UpdateDescription();
            }
            catch (System.Exception ex)
            {
                ExpLog.Log("ERROR: BingoPinChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }
    
        public override void AddHooks()
        {
        }
    
        public override void RemoveHooks()
        {
        }
    
        public override List<object> Settings() => [region, crit, amount];
    }
}