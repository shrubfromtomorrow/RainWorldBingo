using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu.Remix;
using On.Watcher;
using UnityEngine;
using CreatureType = CreatureTemplate.Type;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoDamageRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> weapon;
        public Randomizer<string> victim;
        public Randomizer<int> amount;
        public Randomizer<bool> inOneCycle;
        public Randomizer<string> region;

        public override Challenge Random()
        {
            BingoDamageChallenge challenge = new();
            challenge.weapon.Value = weapon.Random();
            challenge.victim.Value = victim.Random();
            challenge.amount.Value = amount.Random();
            challenge.oneCycle.Value = inOneCycle.Random();
            challenge.region.Value = region.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}weapon-{weapon.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}victim-{victim.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}inOneCycle-{inOneCycle.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}region-{region.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "Damage").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            weapon = Randomizer<string>.InitDeserialize(dict["weapon"]);
            victim = Randomizer<string>.InitDeserialize(dict["victim"]);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
            inOneCycle = Randomizer<bool>.InitDeserialize(dict["inOneCycle"]);
            region = Randomizer<string>.InitDeserialize(dict["region"]);
        }
    }

    public class BingoDamageChallenge : BingoOneCycleChallenge
    {
        public SettingBox<string> weapon;
        public SettingBox<string> victim;
        public SettingBox<int> amount;
        public SettingBox<string> region;
        public List<string> frogsThrown;
        public int current;

        public BingoDamageChallenge()
        {
            weapon = new("", "Weapon", 0, listName: "weapons");
            victim = new("", "Creature Type", 1, listName: "creatures");
            amount = new(0, "Amount", 2);
            oneCycle = new(false, "In One Cycle", 3);
            region = new("", "Region", 4, listName: "regions");
            frogsThrown = [];
        }

        public override void UpdateDescription()
        {
            if (ChallengeTools.creatureNames == null && victim != null)
            {
                ChallengeTools.CreatureName(ref ChallengeTools.creatureNames);
            }
            string location = region.Value != "Any Region" ? ChallengeTools.IGT.Translate(Region.GetRegionFullName(region.Value, ExpeditionData.slugcatPlayer)) : "";
            this.description = ChallengeTools.IGT.Translate("Hit <crit> with <weapon> [<current>/<amount>] times<location><onecycle>")
                .Replace("<crit>", victim.Value == "Any Creature" ? ChallengeTools.IGT.Translate("creatures") : ChallengeTools.creatureNames[new CreatureType(victim.Value, false).Index])
                .Replace("<location>", location != "" ? ChallengeTools.IGT.Translate(" in ") + location : "")
                .Replace("<weapon>", ChallengeTools.ItemName(new(weapon.Value)))
                .Replace("<current>", ValueConverter.ConvertToString(current))
                .Replace("<amount>", ValueConverter.ConvertToString(amount.Value))
                .Replace("<onecycle>", oneCycle.Value ? ChallengeTools.IGT.Translate(" in one cycle") : "");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new([[new Icon("bingoimpact")]]);
            if (weapon.Value != "Any Weapon") phrase.InsertWord(Icon.FromEntityName(weapon.Value), 0, 0);
            if (victim.Value != "Any Creature") phrase.InsertWord(Icon.FromEntityName(victim.Value));

            int lastLine = 1;
            if (region.Value != "Any Region")
            {
                phrase.InsertWord(new Verse(region.Value), 1);
                lastLine = 2;
            }

            phrase.InsertWord(new Counter(current, amount.Value), lastLine);
            if (oneCycle.Value) phrase.InsertWord(new Icon("cycle_limit"), lastLine);
            return phrase;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoDamageChallenge c || (c.weapon.Value != weapon.Value && c.victim.Value != victim.Value);
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Hitting creatures with items");
        }

        public override Challenge Generate()
        {
            List<ChallengeTools.ExpeditionCreature> randoe = ChallengeTools.creatureSpawns[ExpeditionData.slugcatPlayer.value];
            bool oneCycle = UnityEngine.Random.value < 0.33f;

            string wep = ChallengeUtils.GetCorrectListForChallenge("weapons")[UnityEngine.Random.Range(1, ChallengeUtils.GetCorrectListForChallenge("weapons").Length)];

            string crit;
            if (UnityEngine.Random.value < 0.25f)
            {
                crit = "Any Creature";
                if (wep == "Any Weapon") wep = ChallengeUtils.GetCorrectListForChallenge("weapons")[UnityEngine.Random.Range(1, ChallengeUtils.GetCorrectListForChallenge("weapons").Length)];
            }
            else
            {
                List<ChallengeTools.ExpeditionCreature> valid = randoe.Where(x => x.creature.value != "Frog").ToList();

                if (valid.Count > 0) crit = valid[UnityEngine.Random.Range(0, valid.Count)].creature.value;
                else crit = "Frog";
            }
            int amound = UnityEngine.Random.Range(1, 5);

            return new BingoDamageChallenge
            {
                weapon = new(wep, "Weapon", 0, listName: "weapons"),
                victim = new(crit, "Creature Type", 1, listName: "creatures"),
                amount = new(amound, "Amount", 2),
                oneCycle = new(oneCycle, "In One Cycle", 3),
                region = new("Any Region", "Region", 4, listName: "regions"),
                frogsThrown = []
            };
        }

        public bool CritInLocation(Creature crit)
        {
            //                room.Value != "" ? room.Value : 
            string location = region.Value != "Any Region" ? region.Value : "boowomp";
            AbstractRoom room = crit.room.abstractRoom;
            /*if (location == room.Value)
            {
                return rom.name == location;
            }
            else*/
            if (location.ToLowerInvariant() == region.Value.ToLowerInvariant())
            {
                return room.world.region.name.ToLowerInvariant() == location.ToLowerInvariant();
            }
            else return true;
        }

        public void Hit(string weaponn, Creature victimm)
        {
            // watcher touches this
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden || !CritInLocation(victimm) || (victim.Value == "Any Creature" && (victimm.Template.smallCreature && !(ModManager.Watcher && victimm.Template.type == Watcher.WatcherEnums.CreatureTemplateType.FireSprite)))) return;

            bool glug = false;
            bool weaponCheck = false;
            if (victimm.Template.type.value.ToLowerInvariant() == victim.Value.ToLowerInvariant()) glug = true;
            if (victim.Value == "Any Creature" && victimm is not Player) glug = true;
            if (weaponn.ToLowerInvariant() == weapon.Value.ToLowerInvariant()) weaponCheck = true;
            if (weapon.Value == "Any Weapon") weaponCheck = true;

            if (weaponCheck && glug)
            {
                current++;
                UpdateDescription();
                if (current >= amount.Value) CompleteChallenge();
                else ChangeValue();
            }
        }

        public override int Points()
        {
            return 20;
        }

        public override bool CombatRequired()
        {
            return true;
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return true;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoDamageChallenge",
                "~",
                weapon.ToString(),
                "><",
                victim.ToString(),
                "><",
                current.ToString(),
                "><",
                amount.ToString(),
                "><",
                oneCycle.ToString(),
                "><",
                region.ToString(),
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
                weapon = SettingBoxFromString(array[0]) as SettingBox<string>;
                victim = SettingBoxFromString(array[1]) as SettingBox<string>;
                amount = SettingBoxFromString(array[3]) as SettingBox<int>;
                oneCycle = SettingBoxFromString(array[4]) as SettingBox<bool>;
                region = SettingBoxFromString(array[5]) as SettingBox<string>;
                completed = (array[6] == "1");
                revealed = (array[7] == "1");
                current = (oneCycle.Value && !completed) ? 0 : int.Parse(array[2], NumberStyles.Any, CultureInfo.InvariantCulture);
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoDamageChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void Reset()
        {
            base.Reset();
            current = 0;
            frogsThrown?.Clear();
            frogsThrown = [];
        }

        public override void AddHooks()
        {
            On.Watcher.Frog.Thrown += Frog_Thrown;
            On.Watcher.Frog.Jump += Frog_Jump;
            On.Watcher.Frog.Attach += Frog_Attach;
        }

        public override void RemoveHooks()
        {
            On.Watcher.Frog.Thrown -= Frog_Thrown;
            On.Watcher.Frog.Jump -= Frog_Jump;
            On.Watcher.Frog.Attach -= Frog_Attach;
        }

        public override List<object> Settings() => [weapon, amount, region, victim, oneCycle];
    }
}