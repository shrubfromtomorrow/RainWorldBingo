using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu.Remix;
using MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using CreatureType = CreatureTemplate.Type;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoEatRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> foodType;
        public Randomizer<int> amountRequired;
        public Randomizer<bool> starve;

        public override Challenge Random()
        {
            BingoEatChallenge challenge = new();
            challenge.foodType.Value = foodType.Random();
            challenge.amountRequired.Value = amountRequired.Random();
            challenge.starve.Value = starve.Random();
            int index = Array.IndexOf(ChallengeUtils.GetCorrectListForChallenge("food"), challenge.foodType.Value);
            challenge.isCreature = index >= Array.IndexOf(ChallengeUtils.GetCorrectListForChallenge("food"), "VultureGrub");
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}foodType-{foodType.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}amountRequired-{amountRequired.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}starve-{starve.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "Eat").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            foodType = Randomizer<string>.InitDeserialize(dict["foodType"]);
            amountRequired = Randomizer<int>.InitDeserialize(dict["amountRequired"]);
            starve = Randomizer<bool>.InitDeserialize(dict["starve"]);
        }
    }

    public class BingoEatChallenge : BingoChallenge
    {
        public SettingBox<string> foodType;
        public SettingBox<int> amountRequired;
        public SettingBox<bool> starve;
        public int currentEated;
        public bool isCreature;

        public BingoEatChallenge()
        {
            foodType = new("", "Food type", 0, "food");
            amountRequired = new(0, "Amount", 1);
            starve = new(false, "While Starving", 2);
        }

        // Check customizer dialogue for updating iscreature
        public override void UpdateDescription()
        {
            if (ChallengeTools.creatureNames == null)
            {
                ChallengeTools.CreatureName(ref ChallengeTools.creatureNames);
            }
            description = ChallengeTools.IGT.Translate("Eat [<current>/<amount>] <food_type> <starved>")
                .Replace("<current>", ValueConverter.ConvertToString(currentEated))
                .Replace("<amount>", ValueConverter.ConvertToString(amountRequired.Value))
                .Replace("<food_type>", isCreature ? ChallengeTools.IGT.Translate(ChallengeTools.creatureNames[new CreatureType(foodType.Value).Index]) : ChallengeTools.ItemName(new(foodType.Value)))
                .Replace("<starved>", starve.Value ? ChallengeTools.IGT.Translate("while starving") : "");
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new(
                [[new Icon("foodSymbol"), Icon.FromEntityName(foodType.Value)],
                [new Counter(currentEated, amountRequired.Value)]]);
            if (starve.Value) phrase.InsertWord(new Icon("MartyrB"), 1);
            return phrase;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Eating specific food");
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoEatChallenge c || c.foodType.Value != foodType.Value;
        }

        public override Challenge Generate()
        {
            bool c = UnityEngine.Random.value < 0.5f;

            int critStart = Array.IndexOf(ChallengeUtils.GetCorrectListForChallenge("food"), "VultureGrub");
            int foodCount = ChallengeUtils.GetCorrectListForChallenge("food").Length;
            string randomFood;
            if (c)
            {
                randomFood = ChallengeUtils.GetCorrectListForChallenge("food")[UnityEngine.Random.Range(critStart, foodCount)];
            }
            else
            {
                randomFood = ChallengeUtils.GetCorrectListForChallenge("food")[UnityEngine.Random.Range(0, ChallengeUtils.GetCorrectListForChallenge("food").Length - (foodCount - critStart))];
            }

            return new BingoEatChallenge()
            {
                foodType = new(randomFood, "Food type", 0, listName: "food"),
                isCreature = c,
                starve = new(UnityEngine.Random.value < 0.1f, "While Starving", 2),
                amountRequired = new(UnityEngine.Random.Range(1, 7) * (isCreature && foodType.Value == "Fly" ? 2 : 1), "Amount", 1)
            };
        }

        public override bool CombatRequired()
        {
            return false;
        }
    
        public override int Points()
        {
            return 20;// Mathf.RoundToInt(6 * FoodDifficultyMultiplier()) * amountRequired.Value * (hidden ? 2 : 1);
        }
    
        //public float FoodDifficultyMultiplier()
        //{
        //    switch (foodType.Value)
        //    {
        //        case "DangleFruit": return 0.5f;
        //        case "SlimeMold": return 1.33f;
        //        case "GlowWeed": return 1.66f;
        //        case "DandelionPeach": return 1.33f;
        //        case "SmallNeedleWorm": return 1.5f;
        //        case "Fly": return 0.33f;
        //    }
        //
        //    return 1f;
        //}
    
        public void FoodEated(IPlayerEdible thisEdibleIsShit, Player playuh)
        {
            if (!completed && !TeamsCompleted[SteamTest.team] && !hidden && !revealed && thisEdibleIsShit is PhysicalObject p &&
                (isCreature ? (p.abstractPhysicalObject is AbstractCreature g && g.creatureTemplate.type.value == foodType.Value) : (p.abstractPhysicalObject.type.value == foodType.Value)) && (!starve.Value || playuh.Malnourished))
            {
                currentEated++;
                UpdateDescription();
                if (currentEated >= amountRequired.Value) CompleteChallenge();
                else ChangeValue();
            }
        }

        public override void Reset()
        {
            base.Reset();
            currentEated = 0;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoEatChallenge",
                "~",
                amountRequired.ToString(),
                "><",
                currentEated.ToString(),
                "><",
                isCreature ? "1" : "0",
                "><",
                foodType.ToString(),
                "><",
                starve.ToString(),
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
                amountRequired = SettingBoxFromString(array[0]) as SettingBox<int>;
                currentEated = int.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                isCreature = (array[2] == "1");
                foodType = SettingBoxFromString(array[3]) as SettingBox<string>;
                starve = SettingBoxFromString(array[4]) as SettingBox<bool>;
                completed = (array[5] == "1");
                revealed = (array[6] == "1");
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoEatChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return slugcat.value != "Spear";
        }

        public override void AddHooks()
        {
            On.Player.ObjectEaten += Player_ObjectEaten;
        }

        public override void RemoveHooks()
        {
            On.Player.ObjectEaten -= Player_ObjectEaten;
        }

        public override List<object> Settings() => [foodType, amountRequired, starve];
    }
}
