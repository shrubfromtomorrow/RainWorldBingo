using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu.Remix;
using MoreSlugcats;
using RWCustom;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;

    public class BingoEchoRandomizer : ChallengeRandomizer
    {
        public Randomizer<string> ghost;
        public Randomizer<bool> starve;
        public Randomizer<bool> specific;
        public Randomizer<int> amount;

        public override Challenge Random()
        {
            BingoEchoChallenge challenge = new();
            challenge.ghost.Value = ghost.Random();
            challenge.starve.Value = starve.Random();
            challenge.specific.Value = specific.Random();
            challenge.amount.Value = amount.Random();
            return challenge;
        }

        public override StringBuilder Serialize(string indent)
        {
            string surindent = indent + INDENT_INCREMENT;
            StringBuilder serializedContent = new();
            serializedContent.AppendLine($"{surindent}ghost-{ghost.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}starve-{starve.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}specific-{specific.Serialize(surindent)}");
            serializedContent.AppendLine($"{surindent}amount-{amount.Serialize(surindent)}");
            return base.Serialize(indent).Replace("__Type__", "Echo").Replace("__Content__", serializedContent.ToString());
        }

        public override void Deserialize(string serialized)
        {
            Dictionary<string, string> dict = ToDict(serialized);
            ghost = Randomizer<string>.InitDeserialize(dict["ghost"]);
            starve = Randomizer<bool>.InitDeserialize(dict["starve"]);
            specific = Randomizer<bool>.InitDeserialize(dict["specific"]);
            amount = Randomizer<int>.InitDeserialize(dict["amount"]);
        }
    }

    // Literally copied from base game, to add the starving thing easily, and to customize which echoes appear
    public class BingoEchoChallenge : BingoChallenge
    {
        public SettingBox<string> ghost; //GhostWorldPresence.GhostID
        public SettingBox<bool> starve;
        public SettingBox<bool> specific;
        public int current;
        public SettingBox<int> amount;
        public List<string> visited = [];

        public BingoEchoChallenge()
        {
            specific = new(false, "Specific Echo", 0);
            ghost = new("", "Region", 1, listName: ChallengeListConstants.Echoes);
            amount = new(0, "Amount", 2);
            starve = new(false, "While Starving", 3);
        }

        public override void UpdateDescription()
        {
            this.description = specific.Value ?
                ChallengeTools.IGT.Translate("Visit the <echo_location> Echo" + (starve.Value ? " while starving" : ""))
                .Replace("<echo_location>", ChallengeTools.IGT.Translate(Region.GetRegionFullName(ghost.Value, BingoData.slugcatPlayer)))
                :
                ChallengeTools.IGT.Translate("Visit <amount> Echoes" + (starve.Value ? " while starving" : ""))
                .Replace("<amount>", specific.Value ? "1" : ValueConverter.ConvertToString(amount.Value));
            base.UpdateDescription();
        }

        public override Phrase ConstructPhrase()
        {
            Phrase phrase = new([[new Icon("echo_icon"), specific.Value ? new Verse(ghost.Value) : new Counter(current, amount.Value)]]);
            if (starve.Value) phrase.InsertWord(new Icon("MartyrB"), 1);
            return phrase;
        }

        public void SeeGhost(string spectre)
        {
            if (completed || revealed || TeamsCompleted[SteamTest.team] || hidden) return;
            if (specific.Value)
            {
                if (spectre != ghost.Value) return;
                UpdateDescription();
                CompleteChallenge();
            }
            else
            {
                if (visited.Contains(spectre)) return;
                current++;
                visited.Add(spectre);
                UpdateDescription();
                if (current >= amount.Value) CompleteChallenge();
                else ChangeValue();
            }
        }

        public override void Update()
        {
            base.Update();
            if (Custom.rainWorld.processManager.upcomingProcess != null) return;
            for (int i = 0; i < game.Players.Count; i++)
            {
                if (game.Players[i] != null && game.Players[i].realizedCreature is Player player && player.room != null && (!starve.Value || player.Malnourished))
                {
                    for (int j = 0; j < player.room.updateList.Count; j++)
                    {
                        if (player.room.updateList[j] is Ghost echo && game.Players[i].world.worldGhost != null && (echo.fadeOut > 0f || echo.hasRequestedShutDown))
                        {
                            SeeGhost(game.Players[i].world.worldGhost.ghostID.value);
                            return;
                        }
                    }
                }
            }
        }

        public override int Points()
        {
            return 20;
        }

        public override Challenge Generate()
        {
            return new BingoEchoChallenge
            {
                specific = new SettingBox<bool>(Random.value < 0.5f, "Specific Echo", 0),
                ghost = new(ChallengeUtils.GetCorrectListForChallenge(ChallengeListConstants.Echoes)[Random.Range(0, ChallengeUtils.GetCorrectListForChallenge(ChallengeListConstants.Echoes).Length)], "Region", 1, listName: ChallengeListConstants.Echoes),
                amount = new(Random.Range(1, 5), "Amount", 2),
                starve = new(Random.value < 0.1f, "While Starving", 3)
            };
        }

        public override bool RequireSave() => false;

        public override bool CombatRequired()
        {
            return false;
        }

        public override bool Duplicable(Challenge challenge)
        {
            return challenge is not BingoEchoChallenge c || (c.ghost.Value != ghost.Value && c.specific.Value != specific.Value);
        }

        public override string ChallengeName()
        {
            return ChallengeTools.IGT.Translate("Visiting echoes");
        }

        public override void Reset()
        {
            base.Reset();
            visited?.Clear();
            visited = [];
            current = 0;
        }

        public override string ToString()
        {
            return string.Concat(
            [
                "BingoEchoChallenge",
                "~",
                specific.ToString(),
                "><",
                ghost.ToString(),
                "><",
                starve.ToString(),
                "><",
                current.ToString(),
                "><",
                amount.ToString(),
                "><",
                completed ? "1" : "0",
                "><",
                revealed ? "1" : "0",
                "><",
                string.Join("|", visited),
            ]);
        }

        public override void FromString(string args)
        {
            try
            {
                var fields = ChallengeUtilsDeserializer.Parse(ChallengeNameConstants.Echo, args);

                specific = SettingBoxFromString(fields["Specific"]) as SettingBox<bool>;
                ghost = SettingBoxFromString(fields["Ghost"]) as SettingBox<string>;
                starve = SettingBoxFromString(fields["Starve"]) as SettingBox<bool>;
                current = int.Parse(fields["Current"], NumberStyles.Any, CultureInfo.InvariantCulture);
                amount = SettingBoxFromString(fields["Amount"]) as SettingBox<int>;
                completed = fields["Completed"] == "1";
                revealed = fields["Revealed"] == "1";
                visited = [.. fields["Visited"].Split('|')];
                UpdateDescription();
            }
            catch (System.Exception ex)
            {
                ExpLog.Log("ERROR: BingoEchoChallenge FromString() encountered an error: " + ex.Message);
                throw ex;
            }
        }

        public override bool ValidForThisBingoSlugcat(SlugName slugcat, BingoData.BingoModifier modifier)
        {
            return modifier == BingoData.BingoModifier.Normal && slugcat != SlugNameWatcher.Watcher;
        }

        public override void AddHooks()
        {
            On.Ghost.StartConversation += Ghost_StartConversation;
        }

        public override void RemoveHooks()
        {

            On.Ghost.StartConversation -= Ghost_StartConversation;
        }

        public override List<object> Settings() => [ghost, specific, amount, starve];
    }
}
