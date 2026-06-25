using BingoMode.BingoSteamworks;
using BingoMode.BingoChallenges;
using Expedition;
using Menu;
using RWCustom;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode
{
    using BingoMenu;
    using Rewired.ControllerExtensions;
    using BingoMode.BingoRandomizer;

    public class BingoBoard
    {
        public ExpeditionCoreFile core;
        public Challenge[,] challengeGrid; // The challenges will be treated as coordinates on a grid for convenience
        public List<IntVector2> currentWinLine; // A list of grid coordinates
        public int size;
        public List<Challenge> recreateList;

        public Challenge switch1;
        public IntVector2 switch1Pos;
        public Challenge switch2;
        public IntVector2 switch2Pos;

        public BingoBoard()
        {
            size = 5;
            currentWinLine = [];
            recreateList = [];
        }

        public void GenerateBoard(int size, bool changeSize = false)
        {
            BingoRandomizationProfile.Reset();
            Challenge[,] ghostGrid = new Challenge[size, size];
            BingoData.FillPossibleTokens(ExpeditionData.slugcatPlayer);
            ExpeditionData.ClearActiveChallengeList();
            if (changeSize)
                ghostGrid = challengeGrid;

            challengeGrid = new Challenge[size, size];

            if (UnityEngine.Random.value < 0.0005 && ExpeditionData.slugcatPlayer == SlugcatStats.Name.Red)
            {
                BingoHooks.GlobalBoard.FromString(BingoData.normalBingoBoard);
            }
            else
            {
                for (int j = 0; j < size; j++)
                {
                    for (int i = 0; i < size; i++)
                    {
                        if (changeSize && !(i + 1 > ghostGrid.GetLength(0) || j + 1 > ghostGrid.GetLength(1)) && ghostGrid[i, j] != null)
                        {
                            challengeGrid[i, j] = ghostGrid[i, j];
                            if (!ExpeditionData.challengeList.Contains(challengeGrid[i, j]))
                                ExpeditionData.challengeList.Add(challengeGrid[i, j]);
                            continue;
                        }
                        if (challengeGrid[i, j] != null)
                            continue;
                        challengeGrid[i, j] = RandomBingoChallenge(x: i, y: j);
                    }
                }
            }
            SteamTest.UpdateOnlineBingo();
            UpdateChallenges();
        }

        public void ShuffleBoard()
        {
            int rows = challengeGrid.GetLength(0);
            int cols = challengeGrid.GetLength(1);

            List<Challenge> flatList = [];
            for (int j = 0; j < cols; j++)
                for (int i = 0; i < rows; i++)
                    flatList.Add(challengeGrid[i, j]);

            for (int i = 0; i < flatList.Count; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, flatList.Count);
                (flatList[randomIndex], flatList[i]) = (flatList[i], flatList[randomIndex]);
            }

            ExpeditionData.ClearActiveChallengeList();
            int index = 0;
            for (int j = 0; j < cols; j++)
            {
                for (int i = 0; i < rows; i++)
                {
                    Challenge shuffledChallenge = flatList[index++];
                    challengeGrid[i, j] = shuffledChallenge;

                    if (!ExpeditionData.challengeList.Contains(shuffledChallenge))
                        ExpeditionData.challengeList.Add(shuffledChallenge);
                }
            }

            SteamTest.UpdateOnlineBingo();
            UpdateChallenges();
        }

        public void SwitchChals(Challenge chal, int x, int y)
        {
            if (switch1 == null)
            {
                switch1 = chal;
                switch1Pos = new IntVector2(x, y);
                return;
            }
            else if (switch2 == null)
            {
                if (chal == switch1)
                {
                    switch1 = null;
                    switch2 = null;
                    return;
                }

                switch2 = chal;
                switch2Pos = new IntVector2(x, y);

                challengeGrid[switch1Pos.x, switch1Pos.y] = switch2;
                challengeGrid[switch2Pos.x, switch2Pos.y] = switch1;

                ExpeditionData.ClearActiveChallengeList();
                int rows = challengeGrid.GetLength(0);
                int cols = challengeGrid.GetLength(1);
                for (int j = 0; j < cols; j++)
                {
                    for (int i = 0; i < rows; i++)
                    {
                        Challenge c = challengeGrid[i, j];
                        if (c != null && !ExpeditionData.challengeList.Contains(c))
                            ExpeditionData.challengeList.Add(c);
                    }
                }

                SteamTest.UpdateOnlineBingo();
                UpdateChallenges();

                switch1 = null;
                switch2 = null;
            }
        }

        public void UpdateChallenges()
        {
            switch1 = null;
            switch2 = null;
            foreach (Challenge c in ExpeditionData.challengeList)
                c.UpdateDescription();

            ExpeditionMenu self = BingoData.globalMenu;
            if (self != null && BingoHooks.bingoPage.TryGetValue(self, out var page) && page.grid != null)
            {
                if (page.grid != null)
                {
                    page.grid.RemoveSprites();
                    page.RemoveSubObject(page.grid);
                    page.grid = null;
                }
                page.grid = new BingoGrid(self, page, new(self.manager.rainWorld.screenSize.x / 2f, self.manager.rainWorld.screenSize.y / 2f), 500f);
                page.subObjects.Add(page.grid);
                if (SteamTest.CurrentLobby != default && SteamMatchmaking.GetLobbyOwner(SteamTest.CurrentLobby).m_SteamID != SteamTest.selfIdentity.GetSteamID64())
                {
                    page.grid.Switch(true);
                }
            }
        }

        public bool CheckLose(int t)
        {
            bool won = false;
            currentWinLine = [];
            bool lockout = BingoData.IsCurrentSaveLockout();

            // Vertical lines
            for (int i = 0; i < size; i++)
            {
                bool line = true;
                for (int j = 0; j < size; j++)
                {
                    var ch = challengeGrid[i, j];
                    if ((ch as BingoChallenge).TeamsFailed[t])
                        line = false;
                    if (line && lockout && (ch as BingoChallenge).TeamsCompleted.Any(x => x == true) && !(ch as BingoChallenge).TeamsCompleted[t])
                        line = false;
                    if (line)
                        currentWinLine.Add(new IntVector2(i, j));
                }
                won = line;
                if (won)
                    break;
                else
                    currentWinLine.Clear();
            }

            // Horizontal lines
            if (!won)
            {
                for (int j = 0; j < size; j++)
                {
                    bool line = true;
                    for (int i = 0; i < size; i++)
                    {
                        var ch = challengeGrid[i, j];
                        if ((ch as BingoChallenge).TeamsFailed[t])
                            line = false;
                        if (line && lockout && (ch as BingoChallenge).TeamsCompleted.Any(x => x == true) && !(ch as BingoChallenge).TeamsCompleted[t])
                            line = false;
                        if (line)
                            currentWinLine.Add(new IntVector2(i, j));
                    }
                    won = line;
                    if (won)
                        break;
                    else
                        currentWinLine.Clear();
                }
            }

            // Diagonal line 1
            if (!won)
            {
                bool line = true;
                for (int i = 0; i < size; i++)
                {
                    var ch = challengeGrid[i, i];
                    if ((ch as BingoChallenge).TeamsFailed[t])
                        line = false;
                    if (line && lockout && (ch as BingoChallenge).TeamsCompleted.Any(x => x == true) && !(ch as BingoChallenge).TeamsCompleted[t])
                        line = false;
                    if (line)
                        currentWinLine.Add(new IntVector2(i, i));
                }
                won = line;
                if (!won)
                    currentWinLine.Clear();
            }

            // Diagonal line 2
            if (!won)
            {
                bool line = true;
                for (int i = 0; i < size; i++)
                {
                    var ch = challengeGrid[size - 1 - i, i];
                    if ((ch as BingoChallenge).TeamsFailed[t])
                        line = false;
                    if (line && lockout && (ch as BingoChallenge).TeamsCompleted.Any(x => x == true) && !(ch as BingoChallenge).TeamsCompleted[t])
                        line = false;
                    if (line)
                        currentWinLine.Add(new IntVector2(size - 1 - i, i));
                }
                won = line;
                if (!won)
                    currentWinLine.Clear();
            }

            currentWinLine = [];
            return won;
        }

        public bool CheckWin(int t, List<IntVector2> overrideArray = null) // Checks whether a team won or cant win
        {
            bool won = false;
            currentWinLine = [];
            bool lockout = BingoData.IsCurrentSaveLockout();

            // Vertical lines
            for (int i = 0; i < size; i++)
            {
                bool line = true;
                for (int j = 0; j < size; j++)
                {
                    var ch = challengeGrid[i, j];
                    line &= (ch as BingoChallenge).TeamsCompleted[t];
                    if (line)
                        currentWinLine.Add(new IntVector2(i, j));
                }
                won = line;
                if (won)
                    break;
                else
                    currentWinLine.Clear();
            }

            // Horizontal lines
            if (!won)
            {
                for (int j = 0; j < size; j++)
                {
                    bool line = true;
                    for (int i = 0; i < size; i++)
                    {
                        var ch = challengeGrid[i, j];
                        line &= (ch as BingoChallenge).TeamsCompleted[t];
                        if (line) currentWinLine.Add(new IntVector2(i, j));
                    }
                    won = line;
                    if (won)
                        break;
                    else
                        currentWinLine.Clear();
                }
            }

            // Diagonal line 1
            if (!won)
            {
                bool line = true;
                for (int i = 0; i < size; i++)
                {
                    var ch = challengeGrid[i, i];
                    line &= (ch as BingoChallenge).TeamsCompleted[t];
                    if (line) currentWinLine.Add(new IntVector2(i, i));
                }
                won = line;
                if (!won)
                    currentWinLine.Clear();
            }

            // Diagonal line 2
            if (!won)
            {
                bool line = true;
                for (int i = 0; i < size; i++)
                {
                    var ch = challengeGrid[size - 1 - i, i];
                    line &= (ch as BingoChallenge).TeamsCompleted[t];
                    if (line) currentWinLine.Add(new IntVector2(size - 1 - i, i));
                }
                won = line;
                if (!won)
                    currentWinLine.Clear();
            }

            if (overrideArray != null)
                foreach (var coord in currentWinLine)
                    overrideArray.Add(coord);
            currentWinLine = [];
            return won;
        }

        public int CheckMaxTeamSquaresInLineForPlayingBingoThreatMusicYuh(int t, bool lockout, List<int> teamsInBingo = null) // wonderful name (wonderfuler name)
        {
            int squares = 0;

            // Vertical lines
            for (int i = 0; i < size; i++)
            {
                int tempSquaresV = 0;
                bool fuckThisShitImOut = false;
                for (int j = 0; j < size; j++)
                {
                    var ch = challengeGrid[i, j] as BingoChallenge;
                    if (ch.TeamsCompleted[t]) tempSquaresV++;
                    else if (lockout)
                    {
                        foreach (int o in teamsInBingo)
                        {
                            if (o != t && ch.TeamsCompleted[o])
                            {
                                fuckThisShitImOut = true;
                            }
                            if (fuckThisShitImOut) break;
                        }
                        if (fuckThisShitImOut) break;
                    }
                }

                if (fuckThisShitImOut) break;
                if (tempSquaresV > squares) squares = tempSquaresV;
            }

            // Horizontal lines
            for (int j = 0; j < size; j++)
            {
                int tempSquaresH = 0;
                bool fuckThisShitImOut = false;
                for (int i = 0; i < size; i++)
                {
                    var ch = challengeGrid[i, j] as BingoChallenge;
                    if (ch.TeamsCompleted[t]) tempSquaresH++;
                    else if (lockout)
                    {
                        foreach (int o in teamsInBingo)
                        {
                            if (o != t && ch.TeamsCompleted[o])
                            {
                                fuckThisShitImOut = true;
                            }
                            if (fuckThisShitImOut) break;
                        }
                        if (fuckThisShitImOut) break;
                    }
                }

                if (fuckThisShitImOut) break;
                if (tempSquaresH > squares) squares = tempSquaresH;
            }

            // Diagonal line 1
            int tempSquaresD1 = 0;
            for (int i = 0; i < size; i++)
            {
                var ch = challengeGrid[i, i] as BingoChallenge;
                if (ch.TeamsCompleted[t]) tempSquaresD1++;
                else if (lockout)
                {
                    foreach (int o in teamsInBingo)
                    {
                        if (o != t && ch.TeamsCompleted[o])
                        {
                            goto theOutInQuestion;
                        }
                    }
                }
            }
            if (tempSquaresD1 > squares) squares = tempSquaresD1;

            theOutInQuestion:
            // Diagonal line 2
            int tempSquaresD2 = 0;
            for (int i = 0; i < size; i++)
            {
                var ch = challengeGrid[size - 1 - i, i] as BingoChallenge;
                if (ch.TeamsCompleted[t]) tempSquaresD2++;
                else if (lockout)
                {
                    foreach (int o in teamsInBingo)
                    {
                        if (o != t && ch.TeamsCompleted[o])
                        {
                            return squares;
                        }
                    }
                }
            }
            if (tempSquaresD2 > squares) squares = tempSquaresD2;

            return squares;
        }

        public Challenge RandomBingoChallenge(Challenge type = null, bool ignore = false, int x = 1, int y = -1)
        {
            if (BingoRandomizationProfile.IsLoaded)
                try
                {
                    return BingoRandomizationProfile.GetChallenge();
                } catch (Exception)
                {
                    Plugin.logger.LogMessage("Error getting challenge from randomizer, resorting to default generation.");
                }

            if (BingoData.availableBingoChallenges == null)
            {
                ChallengeOrganizer.SetupChallengeTypes();
            }

            List<Challenge> list = [];
            list.AddRange(BingoData.GetAdequateChallengeList(ExpeditionData.slugcatPlayer));

            if (!BingoData.bannedChallenges.ContainsKey(ExpeditionData.slugcatPlayer)) BingoData.LoadAllBannedChallengeLists(ExpeditionData.slugcatPlayer);

            list.RemoveAll(x => (type == null || x.GetType() != type.GetType()) && BingoData.bannedChallenges[ExpeditionData.slugcatPlayer].Contains(x.GetType().Name));
            if (type != null) list.RemoveAll(x => x.GetType() != type.GetType());
            int tries = 0;
        resette:
            tries++;
            Challenge ch = list.Count == 0 || tries > 500 ? new BingoKillChallenge() : list[UnityEngine.Random.Range(0, list.Count)];
            try
            {
                ch = ch.Generate();
            }
            catch (Exception e)
            {
                Plugin.logger.LogError("Failed to generate random challenge of type: " + ch.GetType());
                Plugin.logger.LogError(e);
                goto resette;
            }

            if (list.Count > 0 && ExpeditionData.challengeList.Count > 0 && type == null && !ignore)
            {
                for (int i = 0; i < ExpeditionData.challengeList.Count; i++)
                {
                    if (!ExpeditionData.challengeList[i].Duplicable(ch))
                    {
                        list.Remove(ch);
                        ch = null;
                        goto resette;
                    }
                }
            }

            if (x != -1 && y != -1 && (ch as BingoChallenge).ReverseChallenge() && ReverseCollisionCheck(x, y))
            {
                list.Remove(ch);
                goto resette;
            }
            if (ch == null)
                ch = (Activator.CreateInstance(BingoData.availableBingoChallenges.Find((Challenge c) => c.GetType().Name == "BingoKillChallenge").GetType()) as Challenge).Generate();
            if (!ExpeditionData.challengeList.Contains(ch) && !ignore)
                ExpeditionData.challengeList.Add(ch);
            return ch;
        }

        public bool ReverseCollisionCheck(int x, int y)
        {
            // Horizontal check
            for (int i = 0; i < size; i++)
            {
                if (challengeGrid[i, y] != null && (challengeGrid[i, y] as BingoChallenge).ReverseChallenge())
                    return true;
            }
            // Vertical check
            for (int i = 0; i < size; i++)
            {
                if (challengeGrid[x, i] != null && (challengeGrid[x, i] as BingoChallenge).ReverseChallenge())
                    return true;
            }
            // Diagonal 1 check
            if (x == y)
            {
                for (int i = 0; i < size; i++)
                {
                    if (challengeGrid[i, i] != null && (challengeGrid[i, i] as BingoChallenge).ReverseChallenge())
                        return true;
                }
            }
            // Diagonal 2 check
            if (size - 1 - y == x)
            {
                for (int i = 0; i < size; i++)
                {
                    if (challengeGrid[size - 1 - i, i] != null && (challengeGrid[size - 1 - i, i] as BingoChallenge).ReverseChallenge())
                        return true;
                }
            }

            return false;
        }

        public void RecreateFromList()
        {
            
             
            if (recreateList != null && Mathf.RoundToInt(Mathf.Sqrt(recreateList.Count)) == size)
            {
                 
                challengeGrid = new Challenge[size, size];
                int next = 0;
                for (int j = 0; j < size; j++)
                    for (int i = 0; i < size; i++)
                        challengeGrid[i, j] = recreateList[next++];
                
                SteamTest.UpdateOnlineBingo();
                UpdateChallenges();
            }
            recreateList = [];
        }

        public void SetChallenge(int x, int y, Challenge newChallenge, int index)
        {
            try
            {
                int g1 = index == -1 ? ExpeditionData.challengeList.IndexOf(challengeGrid[x, y]) : index;

                ExpeditionData.challengeList.Remove(challengeGrid[x, y]);
                challengeGrid[x, y] = newChallenge;
                ExpeditionData.challengeList.Insert(g1, challengeGrid[x, y]);
                SteamTest.UpdateOnlineBingo();
            }
            catch (Exception e)
            {
                Plugin.logger.LogError("Invalid bingo board coordinates or challenge null :( " + e);
            }
        }

        public override string ToString()
        {
            string text = ExpeditionData.slugcatPlayer.value + ";" + BingoData.BingoDen + ";" + string.Join("bChG", ExpeditionData.challengeList);
            return text;
        }
        
        public bool FromString(string text)
        {
            bool success = true;
            if (string.IsNullOrEmpty(text) || !text.Contains("bChG") || !text.Contains(';')) { success = false; return success; }
            string slug = text.Substring(0, text.IndexOf(';'));
            text = text.Substring(text.IndexOf(";") + 1);
            if (slug.ToLowerInvariant() != ExpeditionData.slugcatPlayer.value.ToLowerInvariant())
            {
                if (BingoData.globalMenu != null)
                    BingoData.globalMenu.manager.ShowDialog(new InfoDialog(
                            BingoData.globalMenu.manager,
                            BingoData.globalMenu.Translate("Slugcat mismatch!<LINE><LINE>").Replace("<LINE>", "\r\n") +
                            BingoData.globalMenu.Translate($"Selected slugcat: {ExpeditionData.slugcatPlayer.value}<LINE>").Replace("<LINE>", "\r\n") +
                            BingoData.globalMenu.Translate($"Provided Slugcat: {slug}<LINE><LINE>").Replace("<LINE>", "\r\n") +
                            BingoData.globalMenu.Translate($"Please paste a board from the same slugcat that's currently selected.")));
                success = false;
                return success;
            }

            string shelter = "random";
            if (text.IndexOf(';') >= 0)
            {
                shelter = text.Substring(0, text.IndexOf(';'));
                text = text.Substring(text.IndexOf(";") + 1);
            }
            ExpeditionMenu self = BingoData.globalMenu;
            if (self != null && BingoHooks.bingoPage.TryGetValue(self, out var page))
            {
                page.Shelter = shelter;
            }

            string last = ToString();
            try
            {
                if (ExpeditionData.allChallengeLists.ContainsKey(ExpeditionData.slugcatPlayer) && ExpeditionData.allChallengeLists[ExpeditionData.slugcatPlayer] != null) ExpeditionData.allChallengeLists[ExpeditionData.slugcatPlayer].Clear();
                string[] challenges = Regex.Split(text, "bChG");
                size = Mathf.RoundToInt(Mathf.Sqrt(challenges.Length));
                int next = 0;
                challengeGrid = new Challenge[size, size];
                for (int j = 0; j < size; j++)
                {
                    for (int i = 0; i < size; i++)
                    {
                        try
                        {
                            string[] array11 = Regex.Split(challenges[next], "~");
                            string type = array11[0];
                            string text2 = array11[1];
                            Challenge challenge = (Challenge)Activator.CreateInstance(BingoData.availableBingoChallenges.Find((Challenge c) => c.GetType().Name == type).GetType());
                            challenge.FromString(text2);
                            ExpLog.Log(challenge.description);
                            if (!ExpeditionData.allChallengeLists.ContainsKey(ExpeditionData.slugcatPlayer))
                            {
                                ExpeditionData.allChallengeLists.Add(ExpeditionData.slugcatPlayer, new List<Challenge>());
                            }
                            ExpeditionData.allChallengeLists[ExpeditionData.slugcatPlayer].Add(challenge);
                            challengeGrid[i, j] = challenge;
                        }
                        catch (Exception ex)
                        {
                            Plugin.logger.LogError("Problem recreating challenge \"" + challenges[next] + "\" in bingoboard.fromstring: " + ex.Message);

                            // Eat 999999 levis
                            string[] eatLevi = Regex.Split("BingoEatChallenge~System.Int32|999999|Amount|3|NULL><0><1><System.String|BigEel|Food type|0|food><System.Boolean|false|While Starving|2|NULL><0><0", "~");
                            string type = eatLevi[0];
                            string details = eatLevi[1];
                            Challenge fill = (Challenge)Activator.CreateInstance(BingoData.availableBingoChallenges.Find((Challenge c) => c.GetType().Name == type).GetType());
                            fill.FromString(details);

                            challengeGrid[i, j] = fill;
                            ExpeditionData.challengeList.Add(fill);
                            success = false;
                            Plugin.logger.LogInfo("Replacing with: " + challengeGrid[i, j]);
                        }
                        next++;
                    }
                }
                UpdateChallenges();
                return success;
            }
            catch
            {
                success = false;
                FromString(last);
                return success;
            }
        }

        public void CompleteChallengeAt(int x, int y)
        {
            challengeGrid[x, y].CompleteChallenge();
        }

        public Challenge GetChallenge(int x, int y)
        {
            if (x < challengeGrid.GetLength(0) && y < challengeGrid.GetLength(1))
                return challengeGrid[x, y];
            return null;
        }

        public string GetBingoState()
        {
            string state = "";
            for (int j = 0; j < challengeGrid.GetLength(1); j++)
                for (int i = 0; i < challengeGrid.GetLength(0); i++)
                    state += "<>" + (challengeGrid[i, j] as BingoChallenge).TeamsToString();
            if (state != "") state = state.Substring(2);
            return state;
        }

        public void InterpretBingoState(string state)
        {
            if (challengeGrid == null)
            {
                Plugin.logger.LogError("CHALLENGE GRID IS NULL!! Returning");
                return;
            }

            string[] challenges = Regex.Split(state, "<>");

            int next = 0;
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    if (challengeGrid[i, j] == null)
                    {
                        next++;
                        continue;
                    }
                    BingoChallenge ch = challengeGrid[i, j] as BingoChallenge;
                    string currentTeamsString = ch.TeamsToString();
                    string newTeamsString = challenges[next++];

                    // All the switch statements to make it 100% clear, obviously can be shortened down
                    if (currentTeamsString != newTeamsString)
                    {
                        for (int k = 0; k < currentTeamsString.Length; k++)
                        {
                            if (currentTeamsString[k] != newTeamsString[k])
                            {
                                switch (newTeamsString[k])
                                {
                                    case '0':
                                        switch (currentTeamsString[k])
                                        {
                                            case '0':
                                                // Do nothing
                                                break;
                                            case '1':
                                                ch.OnChallengeDepleted(k);
                                                break;
                                            case '2':
                                                // Do nothing
                                                break;
                                        }
                                        break;
                                    case '1':
                                        if (BingoData.IsCurrentSaveLockout())
                                        {
                                            // If its the same team
                                            if (SteamTest.team == k || SteamTest.team == 8 || ch.ReverseChallenge())
                                            {
                                                switch (currentTeamsString[k])
                                                {
                                                    case '0':
                                                        ch.OnChallengeCompleted(k);
                                                        break;
                                                    case '1':
                                                        // Do nothing
                                                        break;
                                                    case '2':
                                                        // Do nothing
                                                        break;
                                                }
                                            }
                                            else
                                            {
                                                switch (currentTeamsString[k])
                                                {
                                                    case '0':
                                                        ch.OnChallengeLockedOut(k);
                                                        break;
                                                    case '1':
                                                        // Do nothing
                                                        break;
                                                    case '2':
                                                        ch.OnChallengeLockedOut(k);
                                                        break;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            switch (currentTeamsString[k])
                                            {
                                                case '0':
                                                    ch.OnChallengeCompleted(k);
                                                    break;
                                                case '1':
                                                    // Do nothing
                                                    break;
                                                case '2':
                                                    // Do nothing
                                                    break;
                                            }
                                        }
                                        break;
                                    case '2':
                                        switch (currentTeamsString[k])
                                        {
                                            case '0':
                                                ch.OnChallengeFailed(k);
                                                break;
                                            case '1':
                                                ch.OnChallengeFailed(k);
                                                break;
                                            case '2':
                                                // Do nothing
                                                break;
                                        }
                                        break;
                                }

                                // This was the code before the switch hell
                                //if (currentTeamsString[k] == '1')
                                //{
                                //    //if (SteamTest.team != 8 && 
                                //    //    k != SteamTest.team &&
                                //    //    !ch.ReverseChallenge() && 
                                //    //    BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer) && 
                                //    //    BingoData.BingoSaves[ExpeditionData.slugcatPlayer].lockout)
                                //    //{
                                //    //    ch.OnChallengeLockedOut(k);
                                //    //}
                                //    ch.OnChallengeCompleted(k);
                                //}
                                //else
                                //{
                                //    ch.OnChallengeFailed(k);
                                //}
                            }
                        }
                    }
                }
            }
        }
    }
}
