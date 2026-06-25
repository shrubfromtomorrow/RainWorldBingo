using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Rewired.ControllerExtensions;
using UnityEngine;
using Expedition;
using BingoMode.BingoSteamworks;
using BingoMode.BingoMenu;

namespace BingoMode.DiscordSDK
{
    internal static class BingoRichPresence
    {
        public static Discord.Discord discord;
        public static ActivityManager activityManager;
        public static bool discordStarted;
        private static readonly float updateDiscordInterval = 1f / 4;
        private static float timeSinceLastDiscordUpdate = 0f;
        public static long bingoRPTimestamp;
        public const string IMAGEID = "watcherbingosquarethumb";

        public static void Hook()
        {
            On.Player.Update += Player_Update;
            On.Menu.MainMenu.Update += MainMenu_Update;
            On.Menu.MainMenu.ctor += MainMenu_ctor;
            On.Menu.ExpeditionMenu.ctor += ExpeditionMenu_ctor;
            On.Menu.ExpeditionMenu.Update += ExpeditionMenu_Update;
        }

        public static void InitDiscord()
        {
            try
            {
                discord = new Discord.Discord(1456186644699807887, (ulong)CreateFlags.NoRequireDiscord);
                discordStarted = discord != null;
                if (discordStarted)
                {
                    discord.SetLogHook(LogLevel.Info, (level, message) =>
                        Plugin.logger.LogInfo($"[Bingo Discord RP {level}] {message}"));
                    activityManager = discord.GetActivityManager();
                }
            }
            catch { discordStarted = false; }
        }

        public static void DiscordCallback()
        {
            try { discord.RunCallbacks(); }
            catch { discordStarted = false; }
        }

        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);

            timeSinceLastDiscordUpdate += Time.deltaTime;

            if (timeSinceLastDiscordUpdate < updateDiscordInterval || !Plugin.PluginInstance.BingoConfig.DiscordRichPresence.Value) return;
            if (!discordStarted)
            {
                InitDiscord();
                timeSinceLastDiscordUpdate = 0f;
                return;
            }

            DiscordCallback();

            var activity = new Activity
            {
                Timestamps = { Start = bingoRPTimestamp }
            };

            UpdateBingoActivity(self, ref activity);

            activityManager.UpdateActivity(activity, result =>
            {
                if (result != Result.Ok) Plugin.logger.LogInfo("Bingo Discord RP update failed: " + result);
            });
            timeSinceLastDiscordUpdate = 0f;
        }

        private static void MainMenu_ctor(On.Menu.MainMenu.orig_ctor orig, Menu.MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
        {
            orig(self, manager, showRegionSpecificBkg);
            bingoRPTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private static void MainMenu_Update(On.Menu.MainMenu.orig_Update orig, Menu.MainMenu self)
        {
            orig(self);

            timeSinceLastDiscordUpdate += Time.deltaTime;
            if (timeSinceLastDiscordUpdate < updateDiscordInterval || !Plugin.PluginInstance.BingoConfig.DiscordRichPresence.Value) return;
            if (!discordStarted)
            {
                InitDiscord();
                timeSinceLastDiscordUpdate = 0f;
                return;
            }

            DiscordCallback();
            activityManager.UpdateActivity(new Activity
            {
                State = "Finding Bingo menu...",
                Timestamps = { Start = bingoRPTimestamp },
                Assets = { LargeImage = IMAGEID, LargeText = "Bingo" }
            }, _ => { });
            timeSinceLastDiscordUpdate = 0f;
        }

        private static void ExpeditionMenu_ctor(On.Menu.ExpeditionMenu.orig_ctor orig, Menu.ExpeditionMenu self, ProcessManager manager)
        {
            orig(self, manager);
            bingoRPTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private static void ExpeditionMenu_Update(On.Menu.ExpeditionMenu.orig_Update orig, Menu.ExpeditionMenu self)
        {
            orig(self);

            timeSinceLastDiscordUpdate += Time.deltaTime;
            if (timeSinceLastDiscordUpdate < updateDiscordInterval || !Plugin.PluginInstance.BingoConfig.DiscordRichPresence.Value) return;
            if (!discordStarted)
            {
                InitDiscord();
                timeSinceLastDiscordUpdate = 0f;
                return;
            }

            DiscordCallback();

            string state = "";
            string details = "";
            
            if (self.currentPage == 4)
            {
                state = "Creating board...";
            }
            else
            {
                state = "Picking slugcat...";
            }
            // This is true in lobbies but not in game
            if (BingoData.MultiplayerGame)
            {
                details = $"The {SlugcatStats.getSlugcatName(ExpeditionData.slugcatPlayer)} | With friends!";
            }
            else
            {
                details = $"The {SlugcatStats.getSlugcatName(ExpeditionData.slugcatPlayer)} | Solo";
            }

            activityManager.UpdateActivity(new Activity
            {
                State = state,
                Details = self.currentPage == 4 ? details : null,
                Timestamps = { Start = bingoRPTimestamp },
                Assets = { LargeImage = IMAGEID, LargeText = "Bingo" }
            }, _ => { });
            timeSinceLastDiscordUpdate = 0f;
        }

        private static void UpdateBingoActivity(Player player, ref Activity discordActivity)
        {
            if (!BingoData.BingoMode && player.abstractCreature?.world?.game?.session is not null)
            {
                string sesh = "";
                if (player.abstractCreature?.world?.game?.session is StoryGameSession story)
                {
                    sesh = "The " + SlugcatStats.getSlugcatName(story.saveStateNumber);
                }
                else
                {
                    sesh = "Arena";
                }
                discordActivity.Details = $"Not Bingoing";
                discordActivity.State = $"Playing: {sesh}";
                discordActivity.Assets.LargeImage = IMAGEID;
                discordActivity.Assets.LargeText = "Bingo";
            }
            else
            {
                // connectedplayers is more than 0 in game but not in lobbies
                discordActivity.Details = $"The {SlugcatStats.getSlugcatName(player.slugcatStats.name)} | " +$"{(SteamFinal.GetHost().GetSteamID64() == default ? "Solo" : "Multiplayer")}";
                discordActivity.State = GetBingoDetails(player);
                discordActivity.Assets.LargeImage = IMAGEID;
                discordActivity.Assets.LargeText = "Bingo";
            }
        }

        private static string GetBingoDetails(Player player)
        {
            int team = SteamTest.team;
            int size = BingoHooks.GlobalBoard.size;
            string gameMode = "";

            int squares = 0;
            foreach (BingoChallenges.BingoChallenge square in BingoHooks.GlobalBoard.challengeGrid)
            {
                if (square.TeamsCompleted[team])
                {
                    squares++;
                }
            }
            if (BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer)) {
                gameMode = BingoData.BingoSaves[ExpeditionData.slugcatPlayer].gamemode.ToString();
                if (gameMode == "LockoutNoTies") gameMode = "Lockout";
            }
            return $"{size}x{size} {gameMode} | \n" +
                $"{squares}/{size*size} | Team {BingoPage.TeamName[team]}";
        }

    }
}
