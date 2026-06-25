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
using MoreSlugcats;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoBombTollRandomizer : ChallengeRandomizer
    {
        public Randomizer<bool> specific;
        public Randomizer<int> amount;
        public Randomizer<bool> pass;
        public Randomizer<string> roomName;

        public override Challenge Random()
        {
            BingoBombTollChallenge challenge = new();
            challenge.specific.Value = specific.Random();
            challenge.amount.Value = amount.Random();
            challenge.pass.Value = pass.Random();
            challenge.roomName.Value = roomName.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}specific-{specific.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}pass-{pass.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}roomName-{roomName.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "BombToll").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            specific = Randomizer<bool>.InitDeserialize(dict["specific"]);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
            pass = Randomizer<bool>.InitDeserialize(dict["pass"]);
            roomName = Randomizer<string>.InitDeserialize(dict["roomName"]);
        }
    }

    public class BingoBombTollChallenge : BingoChallenge
    {
        // First bool is the side when the bomb was thrown, the second bool is whether or not this toll has been properly dealt with
        public Dictionary<string, bool[]> bombed = [];
        public SettingBox<bool> pass;
        public SettingBox<string> roomName;
        public SettingBox<bool> specific;
        public SettingBox<int> amount;
        public int current;

        public BingoBombTollChallenge()
        {
            specific = new(false, "Specific toll", 0);
            amount = new(0, "Amount", 1);
            pass = new(false, "Pass the Toll", 2);
            roomName = new("", "Scavenger Toll", 3, listName: "tolls");
            bombed = [];
        }

        public override void UpdateDescription()
        {
            string action = specific.Value ? ChallengeTools.IGT.Translate("a grenade") : ChallengeTools.IGT.Translate("grenades");
            string tollPart;
            string passPart = pass.Value ? (specific.Value ? ChallengeTools.IGT.Translate(" and then pass it") : ChallengeTools.IGT.Translate(" and then pass them")) : "";

            if (specific.Value)
            {
                string region = Regex.Split(roomName.Value, "_")[0];
                string regionName = ChallengeTools.IGT.Translate(Region.GetRegionFullName(region, ExpeditionData.slugcatPlayer));

                if (roomName.Value == "GW_C05")
                {
                    regionName += ChallengeTools.IGT.Translate(" surface");
                }
                else if (roomName.Value == "GW_C11")
                {
                    regionName += ChallengeTools.IGT.Translate(" underground");
                }

                tollPart = ChallengeTools.IGT.Translate("the <toll> toll").Replace("<toll>", regionName);
            }
            else
            {
                tollPart = ChallengeTools.IGT.Translate("[<current>/<amount>] unique tolls").Replace("<current>", ValueConverter.ConvertToString(current)).Replace("<amount>", ValueConverter.ConvertToString(amount.Value));
            }

            description = ChallengeTools.IGT.Translate("Throw <action> at <toll><pass>").Replace("<action>", action).Replace("<toll>", tollPart).Replace("<pass>", passPart);
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new(
                [[Icon.FromEntityName("ScavengerBomb"), Icon.SCAV_TOLL],
                [specific.Value ? new Verse(roomName.Value.ToUpperInvariant()) : new Counter(current, amount.Value)]]);
            if (pass.Value) phrase.InsertWord(new Icon(Plugin.PluginInstance.BingoConfig.FillIcons.Value ? "keyShiftB" : "keyShiftA", 1f, Color.white, 90));
            return phrase;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoBombTollChallenge c;
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Throwing grenades at scavenger tolls");
        }

        public override Challenge Generate()
        {
            string toll = ChallengeUtils.GetCorrectListForChallenge("tolls")[UnityEngine.Random.Range(0, ChallengeUtils.GetCorrectListForChallenge("tolls").Length)];

            return new BingoBombTollChallenge
            {
                specific = new(UnityEngine.Random.value < 0.5f, "Specific toll", 0),
                amount = new(UnityEngine.Random.Range(1, 3), "Amount", 1),
                pass = new(UnityEngine.Random.value < 0.5f, "Pass the Toll", 2),
                roomName = new(toll, "Scavenger Toll", 3, listName: "tolls")
            };
        }
        public override void Update()
        {
            base.Update();

            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden) return;
            if (this.game?.cameras[0]?.room?.shelterDoor != null && this.game.cameras[0].room.shelterDoor.IsClosing)
            {
                var keysToRemove = new List<string>();

                foreach (var kvp in bombed)
                {
                    bool[] value = kvp.Value;

                    // Only save passed tolls
                    if (value.Length > 1 && !value[1])
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    bombed.Remove(key);
                }
                return;
            }
            if (this.game.cameras[0].room != null)
            {
                AbstractRoom room = this.game.cameras[0].room.abstractRoom;
                string roomUpper = room.name.ToUpperInvariant();
                if (room.scavengerOutpost && bombed.ContainsKey(roomUpper) && !bombed[roomUpper][1])
                {
                    foreach (UpdatableAndDeletable obj in room.realizedRoom.updateList)
                    {
                        if (obj is ScavengerOutpost o)
                        {
                            for (int j = 0; j < o.playerTrackers.Count; j++)
                            {
                                if (bombed[roomUpper][0] != o.playerTrackers[j].PlayerOnOtherSide)
                                {
                                    Pass(roomUpper, o.playerTrackers[j].PlayerOnOtherSide);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void Boom(string room, bool side)
        {
            string roomUpper = room.ToUpperInvariant();
            if (!completed && !revealed && !TeamsCompleted[SteamTest.team] && !hidden)
            {
                if (!pass.Value)
                {
                    if (specific.Value)
                    {
                        if (roomName.Value == roomUpper)
                        {
                            bombed[roomUpper] = new bool[2];
                            bombed[roomUpper][0] = side;
                            bombed[roomUpper][1] = true;
                            CompleteChallenge();
                            return;
                        }
                    }
                    else
                    {
                        if (!bombed.ContainsKey(roomUpper))
                        {
                            current++;
                            UpdateDescription();
                            if (current >= amount.Value)
                            {
                                CompleteChallenge();
                            }
                            else
                            {
                                ChangeValue();
                            }
                        }
                    }
                }
                if (!bombed.ContainsKey(roomUpper) || !bombed[roomUpper][1])
                {
                    bombed[roomUpper] = new bool[2];
                    bombed[roomUpper][0] = side;
                    bombed[roomUpper][1] = false;
                }
            }
        }

        public void Pass(string room, bool side)
        {
            string roomUpper = room.ToUpperInvariant();
            if (!completed && !revealed && !hidden && !TeamsCompleted[SteamTest.team] && pass.Value)
            {
                if (specific.Value)
                {
                    if (roomName.Value == roomUpper.ToUpperInvariant())
                    {
                        bombed[roomUpper][1] = true;
                        CompleteChallenge();
                    }
                }
                else
                {
                    bombed[roomUpper][1] = true;
                    current++;
                    UpdateDescription();
                    if (current >= amount.Value)
                    {
                        CompleteChallenge();
                    }
                    else
                    {
                        ChangeValue();
                    }
                }
            }
        }

        public override void Reset()
        {
            base.Reset();
            bombed?.Clear();
            bombed = [];
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

        public string BombedTollsToString()
        {
            if (bombed.Count == 0)
                return "empty";

            List<string> entries = new();

            foreach (var kvp in bombed)
            {
                bool[] value = kvp.Value;

                // Only save passed tolls
                if (value.Length > 1 && value[1])
                {
                    string boolArray = string.Join(",", value.Select(b => b ? "1" : "0"));
                    entries.Add($"{kvp.Key}|{boolArray}");
                }
            }

            if (entries.Count == 0)
                return "empty";

            return string.Join("%", entries);
        }

        public Dictionary<string, bool[]> BombedTollsFromString(string input)
        {
            Dictionary<string, bool[]> result = new();

            if (input == "empty") return result;

            string[] entries = input.Split('%');

            foreach (string entry in entries)
            {
                string[] parts = entry.Split('|');

                string key = parts[0];

                bool[] array = parts[1].Split(',').Select(s => s == "1").ToArray();

                result[key] = array;
            }
            return result;
        }


        public override string ToString()
        {
            return string.Concat(
            [
                "BingoBombTollChallenge",
                "~",
                specific.ToString(),
                "><",
                roomName.ToString(),
                "><",
                pass.ToString(),
                "><",
                current.ToString(),
                "><",
                amount.ToString(),
                "><",
                BombedTollsToString(),
                "><",
                completed ? "1" : "0",
                "><",
                revealed ? "1" : "0"
            ]);
        }



        public override void FromString(string args)
        {
            try
            {
                string[] array = Regex.Split(args, "><");
                specific = SettingBoxFromString(array[0]) as SettingBox<bool>;
                roomName = SettingBoxFromString(array[1]) as SettingBox<string>;
                pass = SettingBoxFromString(array[2]) as SettingBox<bool>;
                current = int.Parse(array[3], NumberStyles.Any, CultureInfo.InvariantCulture);
                amount = SettingBoxFromString(array[4]) as SettingBox<int>;
                bombed = BombedTollsFromString(array[5]);
                completed = (array[6] == "1");
                revealed = (array[7] == "1");
            }
            catch (Exception ex)
            {
                ExpLog.Log("ERROR: BingoBombTollChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override void AddHooks()
        {
            On.ScavengerBomb.Explode += ScavengerBomb_Explode;
        }

        public override void RemoveHooks()
        {
            On.ScavengerBomb.Explode -= ScavengerBomb_Explode;
        }

        public override List<object> Settings() => [specific, pass, amount, roomName];
    }
}