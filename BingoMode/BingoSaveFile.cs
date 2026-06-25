using BingoMode.BingoSteamworks;
using BingoMode.BingoChallenges;
using Expedition;
using RWCustom;
using Steamworks;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode
{
    public static class BingoSaveFile
    {
        public static void Apply()
        {
            On.Expedition.ExpeditionCoreFile.ToString += ExpeditionCoreFile_ToString;
            On.Expedition.ExpeditionCoreFile.FromString += ExpeditionCoreFile_FromString;
            Directory.CreateDirectory(Application.persistentDataPath + Path.DirectorySeparatorChar.ToString() + "Bingo");
        }

        private static void ExpeditionCoreFile_FromString(On.Expedition.ExpeditionCoreFile.orig_FromString orig, ExpeditionCoreFile self, string saveString)
        {
            orig.Invoke(self, saveString);
            Load();
        }

        private static string ExpeditionCoreFile_ToString(On.Expedition.ExpeditionCoreFile.orig_ToString orig, ExpeditionCoreFile self)
        {
            Save();
            return orig.Invoke(self);
        }

        public static void Save()
        {
            if (Custom.rainWorld == null || ExpeditionData.allChallengeLists == null|| Custom.rainWorld.options == null || BingoData.BingoSaves == null) return;

            string text = "";
            for (int i = 0; i < BingoData.BingoSaves.Count; i++)
            {
                BingoData.BingoSaveData saveData = BingoData.BingoSaves.ElementAt(i).Value;
                if (saveData == null) continue;
                text += BingoData.BingoSaves.ElementAt(i).Key + "#" + saveData.size.ToString();
                if (SteamFinal.IsSaveMultiplayer(saveData))
                {
                    text +=
                    "#" +
                    saveData.team +
                    "#" +
                    saveData.hostID.GetSteamID64() +
                    "#" +
                    (saveData.isHost ? "1" : "0") +
                    "#" +
                    saveData.playerWhiteList +
                    "#" +
                    ((int)saveData.gamemode) +
                    "#" +
                    (saveData.showedWin ? "1" : "0") +
                    "#" +
                    (saveData.firstCycleSaved ? "1" : "0") +
                    "#" +
                    (saveData.passageUsed ? "1" : "0") +
                    "#" +
                    saveData.teamsInBingo +
                    "#" +
                    (saveData.songPlayed ? "1" : "0");
                }
                else
                {
                    text +=
                    "#" +
                    (saveData.showedWin ? "1" : "0") +
                    "#" +
                    saveData.team +
                    "#" +
                    (saveData.firstCycleSaved ? "1" : "0") +
                    "#" +
                    (saveData.passageUsed ? "1" : "0");
                }

                // Add teams string for all challenges at the end of this
                text += "#";
                List<string> teamStrings = [];
                SlugcatStats.Name scug = BingoData.BingoSaves.ElementAt(i).Key;
                if (!ExpeditionData.allChallengeLists.ContainsKey(scug))
                {
                    ExpeditionData.allChallengeLists[scug] = [];
                }
                for (int c = 0; c < ExpeditionData.allChallengeLists[scug].Count; c++)
                {
                    string teams = "000000000";
                    if (ExpeditionData.allChallengeLists[scug][c] is BingoChallenge b) 
                    {
                        teams = b.TeamsToString();
                    }
                    teamStrings.Add(teams);
                }
                text += string.Join("|", teamStrings);

                if (i < BingoData.BingoSaves.Count - 1)
                {
                    text += "<>";
                }
            }

            File.WriteAllText(Application.persistentDataPath + 
                Path.DirectorySeparatorChar.ToString() + 
                "Bingo" + 
                Path.DirectorySeparatorChar.ToString() + 
                "bingo" + 
                Mathf.Abs(Custom.rainWorld.options.saveSlot) + 
                ".txt", 
                text);
        }

        public static void Load()
        {
            if (Custom.rainWorld.options == null) return;

            string path = Application.persistentDataPath +
                Path.DirectorySeparatorChar.ToString() +
                "Bingo" +
                Path.DirectorySeparatorChar.ToString() +
                "bingo" +
                Mathf.Abs(Custom.rainWorld.options.saveSlot) +
                ".txt";

            if (!File.Exists(path)) return;

            BingoData.BingoSaves = [];
            string data = File.ReadAllText(path);
            if (string.IsNullOrEmpty(data)) return;

            string[] array = Regex.Split(data, "<>");
            if (array.Length == 0) return;
            for (int i = 0; i < array.Length; i++)
            {
                string[] array2 = array[i].Split('#');
                SlugcatStats.Name slug = new(array2[0]);
                int size = int.Parse(array2[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                try
                {
                    if (array2.Length > 7)
                    {
                        int team = int.Parse(array2[2], NumberStyles.Any, CultureInfo.InvariantCulture);
                        SteamNetworkingIdentity hostIdentity = new SteamNetworkingIdentity();
                        hostIdentity.SetSteamID64(ulong.Parse(array2[3], NumberStyles.Any, CultureInfo.InvariantCulture));
                        bool isHost = array2[4] == "1";
                        BingoData.BingoGameMode gamemode = (BingoData.BingoGameMode)int.Parse(array2[6], NumberStyles.Any);
                        bool showedWin = array2[7] == "1";
                        bool firstCycleSaved = array2[8] == "1";
                        bool passageUsed = array2[9] == "1";
                        string teamsInBingo = array2[10];
                        bool songPlayed = array2[11] == "1";

                        BingoData.BingoSaves.Add(slug, new(size, team, hostIdentity, isHost, array2[5], gamemode, showedWin, firstCycleSaved, passageUsed, teamsInBingo, songPlayed));
                    }
                    else
                    {
                        bool showedWin = false;
                        int team = SteamTest.team;
                        bool firstCycleSaved = false;
                        bool passageUsed = false;
                        showedWin = array2[2] == "1";
                        team = int.Parse(array2[3], NumberStyles.Any, CultureInfo.InvariantCulture);
                        firstCycleSaved = array2[4] == "1";
                        passageUsed = array2[5] == "1";

                        BingoData.BingoSaves.Add(slug, new(size, showedWin, team, firstCycleSaved, passageUsed));
                    }
                    string teamString = array2[array2.Length - 1];
                    string[] teams = teamString.Split('|');
                    int next = 0;
                    
                    for (int x = 0; x < size; x++)
                    {
                        for (int y = 0; y < size; y++)
                        {
                            (ExpeditionData.allChallengeLists[slug][next] as BingoChallenge).TeamsFromString(teams[next], BingoData.BingoSaves[slug].team);
                            next++;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Plugin.logger.LogError($"Failed to load save for {slug} - {array[i]} " + e);
                    BingoData.BingoSaves[new(array2[0])] = new(size, false, 0, false, false);
                }
            }
        }
    }
}
