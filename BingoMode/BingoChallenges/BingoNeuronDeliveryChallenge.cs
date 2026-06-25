using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu.Remix;
using MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoNeuronDeliveryRandomizer : ChallengeRandomizer
    {
        public Randomizer<int> neurons;

        public override Challenge Random()
        {
            BingoNeuronDeliveryChallenge challenge = new();
            challenge.neurons.Value = neurons.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}neurons-{neurons.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "NeuronDelivery").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            neurons = Randomizer<int>.InitDeserialize(dict["neurons"]);
        }
    }

    public class BingoNeuronDeliveryChallenge : BingoChallenge
    {
        public SettingBox<int> neurons;
        public int delivered;

        public BingoNeuronDeliveryChallenge()
        {
            neurons = new(0, "Amount of Neurons", 0);
        }

        public override bool ValidForThisSlugcat(SlugcatStats.Name slugcat)
        {
            return !ModManager.MSC || (!(slugcat == MoreSlugcatsEnums.SlugcatStatsName.Spear) && !(slugcat == MoreSlugcatsEnums.SlugcatStatsName.Saint) && !(slugcat == MoreSlugcatsEnums.SlugcatStatsName.Artificer));
        }

        public override void UpdateDescription()
        {
            this.description = ChallengeTools.IGT.Translate("Deliver <progress> neurons to Looks to the Moon").Replace("<progress>", string.Concat(new string[]
            {
                "[",
                this.delivered.ToString(),
                "/",
                this.neurons.Value.ToString(),
                "]"
            }));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            return new Phrase(
                [[new Icon("Symbol_Neuron"), new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.white, 90), Icon.MOON],
                [new Counter(delivered, neurons.Value)]]);
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Delivering neurons");
        }

        public override void Update()
        {
            base.Update();
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden) return;
            if (this.game != null && this.game.rainWorld.progression.currentSaveState != null)
            {
                if (this.game.rainWorld.progression.currentSaveState.miscWorldSaveData.SLOracleState.totNeuronsGiven > this.delivered)
                {
                    this.delivered = this.game.rainWorld.progression.currentSaveState.miscWorldSaveData.SLOracleState.totNeuronsGiven;
                    this.UpdateDescription();
                    ChangeValue();
                }
                if (!this.completed && this.delivered >= this.neurons.Value)
                {
                    this.CompleteChallenge();
                }
            }
        }

        public override void Reset()
        {
            delivered = 0;
            base.Reset();
        }

        public override bool CombatRequired()
        {
            return false;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return !(challenge is BingoNeuronDeliveryChallenge);
        }

        public override Challenge Generate()
        {
            return new BingoNeuronDeliveryChallenge
            {
                neurons = new(UnityEngine.Random.Range(1, 3), "Amount of Neurons", 0)
            };
        }

        public override int Points()
        {
            return 70 * this.neurons.Value * (int)(this.hidden ? 2f : 1f);
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "BingoNeuronDeliveryChallenge",
                "~",
                neurons.ToString(),
                "><",
                ValueConverter.ConvertToString<int>(this.delivered),
                "><",
                this.completed ? "1" : "0",
                "><",
                this.revealed ? "1" : "0"
            });
        }

        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                this.neurons = SettingBoxFromString(array[0]) as SettingBox<int>;
                this.delivered = int.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                this.completed = (array[2] == "1");
                this.revealed = (array[3] == "1");
                this.UpdateDescription();
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoNeuronDeliveryChallenge FromString() encountered an error: " + ex.Message);
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

        public override List<object> Settings() => [neurons];
    }
}
