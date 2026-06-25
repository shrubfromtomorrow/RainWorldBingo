using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoDontUseItemRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> item;

        public override Challenge Random()
        {
            BingoDontUseItemChallenge challenge = new();
            challenge.item.Value = item.Random();
            int index = Array.IndexOf(ChallengeUtils.GetCorrectListForChallenge("food"), challenge.item.Value);
            challenge.isFood = index >= 0;
            challenge.isCreature = index >= Array.IndexOf(ChallengeUtils.GetCorrectListForChallenge("food"), "VultureGrub");
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}item-{item.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "DontUseItem").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            item = Randomizer<string>.InitDeserialize(dict["item"]);
        }
    }

    //Using counts as either throwing an item, or holding it for more than 5 seconds
    public class BingoDontUseItemChallenge : BingoChallenge
    {
        public SettingBox<string> item;
        public bool isFood;
        public bool isCreature;

        public BingoDontUseItemChallenge()
        {
            item = new("", "Item type", 0, listName: "banitem");
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Never " + (isFood ? "eat" : "use") + " <item>")
                .Replace("<item>", isFood && isCreature ? ChallengeTools.IGT.Translate(ChallengeTools.creatureNames[new CreatureTemplate.Type(item.Value).Index]) : ChallengeTools.ItemName(new(item.Value)));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase([[new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "buttonCrossB" : "buttonCrossA", 1f, Color.red), Icon.FromEntityName(item.Value)]]);
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoDontUseItemChallenge c || (c.item.Value != item.Value);
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Avoiding items");
        }

        public override Challenge Generate()
        {
            bool edible = UnityEngine.Random.value < 0.5f;
            string type;
            bool c = false;
            if (edible)
            {
                type = ChallengeUtils.GetCorrectListForChallenge("food")[UnityEngine.Random.Range(0, ChallengeUtils.GetCorrectListForChallenge("food").Length)];
                c = ChallengeUtils.GetCorrectListForChallenge("food").IndexOf(type) >= Array.IndexOf(ChallengeUtils.GetCorrectListForChallenge("food"), "VultureGrub");
            }
            else type = ChallengeUtils.GetCorrectListForChallenge("banitem")[UnityEngine.Random.Range(Array.IndexOf(ChallengeUtils.GetCorrectListForChallenge("banitem"), "SmallCentipede") + 1, ChallengeUtils.GetCorrectListForChallenge("banitem").Length)];
            BingoDontUseItemChallenge ch = new BingoDontUseItemChallenge
            {
                item = new(type, "Item type", 0, listName: "banitem"),
                isFood = edible,
                isCreature = c
            };
            return ch;
        }

        public override bool RequireSave() => false;
        public override bool ReverseChallenge() => true;

        public void Used(AbstractPhysicalObject.AbstractObjectType used)
        {
            if (used.value == item.Value && !TeamsFailed[SteamTest.team] && completed)
            {
                FailChallenge(SteamTest.team);
            }
        }

        public void Eated(IPlayerEdible used)
        {
            if (TeamsFailed[SteamTest.team] || !completed) return;
            if (used is PhysicalObject p && (isCreature ? (p.abstractPhysicalObject is AbstractCreature g && g.creatureTemplate.type.value == item.Value) : (p.abstractPhysicalObject.type.value == item.Value)))
            {
                FailChallenge(SteamTest.team);
            }
        }

        public override void Update()
        {
            base.Update();

            if (isFood) return;
            for (int i = 0; i < BingoData.heldItemsTime.Length; i++)
            {
                if (i == (int)new AbstractPhysicalObject.AbstractObjectType(item.Value) && BingoData.heldItemsTime[i] > 200) Used(new(item.Value)); 
            }
            for (int i = 0; i < game.Players.Count; i++)
            {
                if (game.Players[i] != null && game.Players[i].realizedCreature is Player player && player.room != null)
                {
                    if (player.objectInStomach != null && player.objectInStomach.type.value == item.Value)
                    {
                        Used(new(item.Value));
                    }
                }
            }
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

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoDontUseItemChallenge",
                "~",
                item.ToString(),
                "><",
                isFood ? "1" : "0",
                "><",
                completed ? "1" : "0",
                "><",
                revealed ? "1" : "0",
                "><",
                isCreature ? "1" : "0",
            });
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                item = SettingBoxFromString(array[0]) as SettingBox<string>;
                isFood = (array[1] == "1");
                completed = (array[2] == "1");
                revealed = (array[3] == "1");
                isCreature = array[4] == "1";
                UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoDontUseItemChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.Player.ThrowObject += Player_ThrowObject;
            On.Player.GrabUpdate += Player_GrabUpdate;
            On.Player.ObjectEaten += Player_ObjectEaten2;
        }

        public override void RemoveHooks()
        {
            On.Player.ThrowObject -= Player_ThrowObject;
            On.Player.GrabUpdate -= Player_GrabUpdate;
            On.Player.ObjectEaten -= Player_ObjectEaten2;
        }

        public override List<object> Settings() => [item];
    }
}