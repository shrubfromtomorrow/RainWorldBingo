using BingoMode.BingoChallenges;
using Expedition;
using HUD;
using Menu.Remix.MixedUI;
using MoreSlugcats;
using RWCustom;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BingoMode.BingoHUD
{
    using BingoMenu;
    using BingoSteamworks;

    public class BingoHUDMain : HudPart
    {
        public static bool ReadyForLeave;
        public static bool Toggled;
        public static bool ForceTallyUp;

        public BingoBoard board;
        public Vector2 pos;
        public BingoInfo[,] grid;
        public Vector2 mousePosition;
        public Vector2 lastMousePosition;
        public bool mouseDown;
        public bool lastMouseDown;
        public bool MouseLeftDown => mouseDown && !lastMouseDown && cantClickCounter == 0;
        public bool mouseRightDown;
        public bool lastMouseRightDown;
        public bool MouseRightDown => mouseRightDown && !lastMouseRightDown && cantClickCounter == 0;
        public float alpha;
        public float lastAlpha;
        const int animationLength = 20;
        public int animation = 0;
        public List<BingoInfo> queue;
        public BingoHUDCursor cursor;
        public List<BingoHUDHint> hints;
        public bool mapOpen;
        public bool lastMapOpen;
        public bool cheatsEnabled;
        public bool cheatingRnAtThisMoment;
        public int cantClickCounter;
        public BingoHUDButton[] hudButtons;

        // Bingo complete business
        public enum BingoCompleteReason
        {
            Bingo,
            Tie,
            Majority,
            TallyUp,
            Blackout
        }

        public struct BingoCompleteInfo
        {
            public List<int> teams;
            public string subTitle;
            public string evenSubtlerTitle;
            public BingoCompleteReason reason;

            public BingoCompleteInfo(List<int> teams, string subTitle, string evenSubtlerTitle, BingoCompleteReason reason)
            {
                this.teams = teams;
                this.subTitle = subTitle;
                this.evenSubtlerTitle = evenSubtlerTitle;
                this.reason = reason;
            }
        }
        private FAtlasElement normalTitle;
        private FAtlasElement watcherTitle;
        public FSprite bingoCompleteTitle;
        public FLabel bingoCompleteInfo;
        public FLabel bingoCompleteInfoShadow;
        public float completeAlpha;
        public float lastCompleteAlpha;
        public bool addCompleteAlpha;
        public int completeAnimation = 0;
        public List<BingoInfo> completeQueue;
        public float textShake;
        public float sinCounter;
        public Color colorToFadeTo;
        public int fadeOutCounter;
        //public bool mouseOverCompleteText
        //{
        //    get
        //    {
        //        bool isHost = BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer) &&
        //            (BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID.GetSteamID64() == default ||
        //            BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID.GetSteamID64() == SteamTest.selfIdentity.GetSteamID64());
        //        return isHost && mousePosition.x > hud.rainWorld.screenSize.x * 0.5f - 130f && mousePosition.y > hud.rainWorld.screenSize.y * 0.9f - 80f 
        //                && mousePosition.x < hud.rainWorld.screenSize.x * 0.5f + 130f && mousePosition.y < hud.rainWorld.screenSize.y * 0.9f + 80f;
        //    }
        //}

        public BingoHUDMain(HUD.HUD hud) : base(hud)
        {
            normalTitle = Futile.atlasManager.GetElementWithName("bingotitle");
            watcherTitle = Futile.atlasManager.GetElementWithName("bingotitlewatcher");

            pos = new Vector2(20.2f, 725.2f);
            board = BingoHooks.GlobalBoard;
            queue = [];
            completeQueue = [];
            hints = []; 
            if (BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer))
            {
                cheatsEnabled = SteamTest.team == 8 && BingoData.BingoSaves[ExpeditionData.slugcatPlayer].isHost;
            }
            GenerateBingoGrid();
            if (hud.owner.GetOwnerType() == HUD.HUD.OwnerType.SleepScreen)
            {
                AddRevealedToQueue();
            }

            ChallengeHooks.revealInMemory = [];
            bingoCompleteTitle = new FSprite(normalTitle)
            {
                x = hud.rainWorld.screenSize.x * 0.5f,
                y = hud.rainWorld.screenSize.y * 0.92f,
                alpha = 0f
            };

            bingoCompleteInfo = new FLabel(Custom.GetDisplayFont(), "")
            {
                alignment = FLabelAlignment.Center,
                alpha = 0f,
                anchorY = 1f
            };
            bingoCompleteInfoShadow = new FLabel(Custom.GetDisplayFont(), "")
            {
                alignment = FLabelAlignment.Center,
                alpha = 0f,
                anchorY = 1f,
                color = new Color(0.05f, 0.05f, 0.05f)
            };
            hud.fContainers[1].AddChild(bingoCompleteTitle);
            hud.fContainers[1].AddChild(bingoCompleteInfoShadow);
            hud.fContainers[1].AddChild(bingoCompleteInfo);
            addCompleteAlpha = false;
            colorToFadeTo = Color.white;

            if (BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer) && !BingoData.BingoSaves[ExpeditionData.slugcatPlayer].showedWin)
            {
                BingoCompleteInfo? potentialEndGame = CheckWinLose();
                if (potentialEndGame.HasValue)
                {
                    DoComplete(potentialEndGame.Value);
                }
            }

            cursor = new BingoHUDCursor(hud.fContainers[1], new Vector2(-100f, -100f));

            hudButtons = new BingoHUDButton[2];

            hudButtons[0] = new BingoHUDButton(this, default, ChallengeTools.IGT.Translate("Tally up"), PressTallyUp) { active = BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer) && (BingoData.BingoSaves[ExpeditionData.slugcatPlayer].isHost || BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID.GetSteamID() == default) };
            hudButtons[1] = new BingoHUDButton(this, default, ChallengeTools.IGT.Translate("End Session"), PressExitGame) { active = BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer) && BingoData.BingoSaves[ExpeditionData.slugcatPlayer].isHost && BingoData.BingoSaves[ExpeditionData.slugcatPlayer].showedWin };
        }

        public void ShowWinText()
        {
            addCompleteAlpha = true;
            textShake = 1f;
            sinCounter = 0.5f;
            fadeOutCounter = 3000;
            if (BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer))
            {
                BingoData.BingoSaves[ExpeditionData.slugcatPlayer].showedWin = true;
                hudButtons[1].active = true;
                BingoSaveFile.Save();
            }
        }

        public void DoComplete(BingoCompleteInfo endGameInfo)
        {
            
            //if (!fromStart)
            //{
            completeAnimation = 150;
            switch (endGameInfo.reason)
            {
                case BingoCompleteReason.Majority:
                case BingoCompleteReason.Blackout:
                    for (int x = 0; x < grid.GetLength(0); x++)
                    {
                        for (int y = 0; y < grid.GetLength(0); y++)
                        {
                            if (grid[x, y].challenge is BingoChallenge bimbo && bimbo.TeamsCompleted[endGameInfo.teams[0]])
                            {
                                completeQueue.Add(grid[x, y]);
                                grid[x, y].teamResponsible = endGameInfo.teams[0];
                            }
                        }
                    }
                    bingoCompleteInfo.text = endGameInfo.subTitle.Replace("<team_name>", BingoPage.TeamName[endGameInfo.teams[0]]) + "\n" + endGameInfo.evenSubtlerTitle;
                    bingoCompleteInfo.color = BingoPage.TEAM_COLOR[endGameInfo.teams[0]];
                    break;

                case BingoCompleteReason.Bingo:
                    List<IntVector2> winCoords = [];
                    BingoHooks.GlobalBoard.CheckWin(endGameInfo.teams[0], winCoords);
                    foreach (var g in winCoords)
                    {
                        completeQueue.Add(grid[g.x, g.y]);
                        grid[g.x, g.y].teamResponsible = endGameInfo.teams[0];
                    }
                    bingoCompleteInfo.text = endGameInfo.subTitle.Replace("<team_name>", BingoPage.TeamName[endGameInfo.teams[0]]) + "\n" + endGameInfo.evenSubtlerTitle;
                    bingoCompleteInfo.color = BingoPage.TEAM_COLOR[endGameInfo.teams[0]];
                    break;
                case BingoCompleteReason.Tie:
                    for (int x = 0; x < grid.GetLength(0); x++)
                    {
                        for (int y = 0; y < grid.GetLength(0); y++)
                        {
                            foreach (var t in endGameInfo.teams)
                            {
                                if (grid[x, y].challenge is BingoChallenge bimbo && bimbo.TeamsCompleted[t])
                                {
                                    completeQueue.Add(grid[x, y]);
                                    grid[x, y].teamResponsible = t;
                                }
                            }
                        }
                    }
                    bingoCompleteInfo.text = endGameInfo.subTitle + "\n" + endGameInfo.evenSubtlerTitle;
                    bingoCompleteInfo.color = Menu.Menu.MenuColor(Menu.Menu.MenuColors.MediumGrey).rgb;
                    break;
                case BingoCompleteReason.TallyUp:
                    for (int x = 0; x < grid.GetLength(0); x++)
                    {
                        for (int y = 0; y < grid.GetLength(0); y++)
                        {
                            if (grid[x, y].challenge is BingoChallenge bimbo && bimbo.TeamsCompleted.Any(x => x == true))
                            {
                                completeQueue.Add(grid[x, y]);
                                grid[x, y].teamResponsible = 8;
                            }
                        }
                    }
                    bingoCompleteInfo.text = endGameInfo.subTitle + "\n" + endGameInfo.evenSubtlerTitle;
                    bingoCompleteInfo.color = BingoPage.TEAM_COLOR[SteamTest.team];
                    completeAnimation = 1;
                break;
            }
            bingoCompleteInfoShadow.text = bingoCompleteInfo.text;
            //}
            //else
            //{
            //    switch (endGameInfo.reason)
            //    {
            //        case BingoCompleteReason.Majority:
            //        case BingoCompleteReason.Bingo:
            //        case BingoCompleteReason.Blackout:
            //            bingoCompleteInfo.text = endGameInfo.subTitle.Replace("<team_name>", BingoPage.TeamName(endGameInfo.teams[0])) + "\n" + endGameInfo.evenSubtlerTitle;
            //            bingoCompleteInfo.color = BingoPage.TEAM_COLOR[endGameInfo.teams[0]];
            //            break;
            //        case BingoCompleteReason.Tie:
            //            bingoCompleteInfo.text = endGameInfo.subTitle + "\n" + endGameInfo.evenSubtlerTitle;
            //            bingoCompleteInfo.color = Menu.Menu.MenuColor(Menu.Menu.MenuColors.MediumGrey).rgb;
            //            break;
            //    }
            //}
            colorToFadeTo = bingoCompleteInfo.color;
        }

        public BingoCompleteInfo? CheckWinLose()
        {
            if (BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer) &&
                BingoData.BingoSaves[ExpeditionData.slugcatPlayer].showedWin) return null;

            string addText = ChallengeTools.IGT.Translate("Press the button in the bingo HUD or abandon the save to end the bingo session.");
            bool isMultiplayer = BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer) &&
                BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID.GetSteamID64() != default;
            bool doDangerMusic = Plugin.PluginInstance.BingoConfig.PlayDangerSong.Value;

            if (isMultiplayer)
            {
                bool songAlreadyPlayed = BingoData.BingoSaves[ExpeditionData.slugcatPlayer].songPlayed;
                if (BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID.GetSteamID64() != SteamTest.selfIdentity.GetSteamID64())
                {
                    addText = ChallengeTools.IGT.Translate("Exit the game and abandon the save or wait for host to end the session.");
                }
                else addText = ChallengeTools.IGT.Translate("Press the button in the bingo HUD to end the bingo session for everyone.");


                List<int> teamsInBingo = BingoData.TeamsStringToList(BingoData.BingoSaves[ExpeditionData.slugcatPlayer].teamsInBingo);

                switch (BingoData.BingoSaves[ExpeditionData.slugcatPlayer].gamemode)
                {
                    case BingoData.BingoGameMode.Bingo:
                        return GetBingoCompleteInfo(teamsInBingo, isMultiplayer, addText, songAlreadyPlayed, doDangerMusic);
                    case BingoData.BingoGameMode.Lockout:
                        return GetLockoutCompleteInfo(teamsInBingo, isMultiplayer, addText, songAlreadyPlayed, doDangerMusic, true);
                    case BingoData.BingoGameMode.LockoutNoTies:
                        return GetLockoutCompleteInfo(teamsInBingo, isMultiplayer, addText, songAlreadyPlayed, doDangerMusic, false);
                    case BingoData.BingoGameMode.Blackout:
                        return GetBlackoutCompleteInfo(teamsInBingo, isMultiplayer, addText, songAlreadyPlayed, doDangerMusic);
                }
            }
            else // Default to blackout for win
            {
                if (GetBlackoutCompleteInfo([SteamTest.team], isMultiplayer, addText, true, doDangerMusic) is BingoCompleteInfo info)
                {
                    return info;
                }
            }

            return null;
        }

        private BingoCompleteInfo? GetBingoCompleteInfo(List<int> teamsInBingo, bool isMultiplayer, string addText, bool songAlreadyPlayed, bool doDangerMusic)
        {
            foreach (int t in teamsInBingo)
            {
                if (BingoHooks.GlobalBoard.CheckWin(t))
                {
                    return new BingoCompleteInfo([t], isMultiplayer ? ChallengeTools.IGT.Translate("Team <team_name> won!") : ChallengeTools.IGT.Translate("You won!"), addText, BingoCompleteReason.Bingo);
                }

                if (!songAlreadyPlayed && doDangerMusic && t != SteamTest.team && Custom.rainWorld.processManager != null && Custom.rainWorld.processManager.musicPlayer != null && BingoHooks.GlobalBoard.CheckMaxTeamSquaresInLineForPlayingBingoThreatMusicYuh(t, false) == grid.GetLength(0) - 1)
                {
                    BingoHooks.RequestBingoSong(Custom.rainWorld.processManager.musicPlayer, ChallengeTools.IGT.Translate("Bingo - Scheming"));
                    BingoData.BingoSaves[ExpeditionData.slugcatPlayer].songPlayed = true;
                    BingoSaveFile.Save();
                }
            }

            return null;
        }

        private BingoCompleteInfo? GetLockoutCompleteInfo(List<int> teamsInBingo, bool isMultiplayer, string addText, bool songAlreadyPlayed, bool doDangerMusic, bool allowTies)
        {
            Dictionary<int, int> completedForTeam = [];
            Dictionary<int, int> emptyForTeam = [];
            int majorityMargin = Mathf.FloorToInt(Mathf.Pow(grid.GetLength(0), 2f) / 2f);

            Dictionary<int, bool> teamsLost = []; // Teams that cant do a bingo
            foreach (int t in teamsInBingo)
            {
                teamsLost.Add(t, false);
                if (!BingoHooks.GlobalBoard.CheckLose(t)) teamsLost[t] = true;
            }

            foreach (int t in teamsInBingo)
            {
                // Bingo wins
                if (BingoHooks.GlobalBoard.CheckWin(t))
                {
                    return new BingoCompleteInfo([t], isMultiplayer ? ChallengeTools.IGT.Translate("Team <team_name> won!") : ChallengeTools.IGT.Translate("You won!"), addText, BingoCompleteReason.Bingo);
                }

                completedForTeam.Add(t, 0);
                emptyForTeam.Add(t, 0);
                foreach (var ch in ExpeditionData.challengeList)
                {
                    BingoChallenge challenge = ch as BingoChallenge;
                    if (challenge.TeamsCompleted[t]) completedForTeam[t]++;
                    else if (allowTies)
                    {
                        bool addEmpty = true;
                        foreach (int o in teamsInBingo)
                        {
                            if (o != t && challenge.TeamsCompleted[o])
                            {
                                addEmpty = false;
                                break;
                            }
                        }
                        if (addEmpty) emptyForTeam[t]++;
                    }
                }
            }

            List<int> tieTeams = [];
            foreach (int t in teamsInBingo)
            {
                int otherLostTeams = 0;
                foreach (var kvp in teamsLost)
                {
                    if (kvp.Key != t && kvp.Value == true) otherLostTeams++;
                }
                if (otherLostTeams != teamsInBingo.Count - 1) continue;

                if (completedForTeam[t] <= majorityMargin) continue;

                bool canWin = true;
                if (allowTies)
                {
                    foreach (int o in teamsInBingo)
                    {
                        if (t == o) continue;

                        if (completedForTeam[t] == completedForTeam[o])
                        {
                            if (!tieTeams.Contains(t)) tieTeams.Add(t);
                            if (!tieTeams.Contains(o)) tieTeams.Add(o);

                        }
                        if (tieTeams.Count > 0) continue;

                        if (completedForTeam[o] + emptyForTeam[o] >= completedForTeam[t])
                        {
                            canWin = false;
                            break;
                        }
                    }
                    if (tieTeams.Count > 0) continue;
                }

                if (canWin)
                {
                    return new BingoCompleteInfo([t], ChallengeTools.IGT.Translate("Team <team_name> won!"), addText, BingoCompleteReason.Majority);
                }
            }

            if (allowTies && tieTeams.Count > 0)
            {
                return new BingoCompleteInfo(tieTeams, ChallengeTools.IGT.Translate("We have a tie!"), addText, BingoCompleteReason.Tie);
            }

            foreach (int t in teamsInBingo)
            {
                if (!songAlreadyPlayed && doDangerMusic && t != SteamTest.team && Custom.rainWorld.processManager != null && Custom.rainWorld.processManager.musicPlayer != null && (BingoHooks.GlobalBoard.CheckMaxTeamSquaresInLineForPlayingBingoThreatMusicYuh(t, true, teamsInBingo) == grid.GetLength(0) - 1 || completedForTeam[t] > majorityMargin - 3))
                {
                    BingoHooks.RequestBingoSong(Custom.rainWorld.processManager.musicPlayer, ChallengeTools.IGT.Translate("Bingo - Scheming"));
                    BingoData.BingoSaves[ExpeditionData.slugcatPlayer].songPlayed = true;
                    BingoSaveFile.Save();
                }
            }

            return null;
        }

        private BingoCompleteInfo? GetBlackoutCompleteInfo(List<int> teamsInBingo, bool isMultiplayer, string addText, bool songAlreadyPlayed, bool doDangerMusic)
        {
            foreach (int t in teamsInBingo)
            {
                bool won = true;
                int squareCount = 0;
                foreach (var ch in ExpeditionData.challengeList)
                {
                    BingoChallenge challenge = ch as BingoChallenge;
                    if (challenge.TeamsCompleted[t])
                    {
                        squareCount++;

                        if (!songAlreadyPlayed && doDangerMusic && t != SteamTest.team && Custom.rainWorld.processManager != null && Custom.rainWorld.processManager.musicPlayer != null && squareCount > (grid.GetLength(0) * grid.GetLength(0) - 2))
                        {
                            BingoHooks.RequestBingoSong(Custom.rainWorld.processManager.musicPlayer, ChallengeTools.IGT.Translate("Bingo - Scheming"));
                            BingoData.BingoSaves[ExpeditionData.slugcatPlayer].songPlayed = true;
                            BingoSaveFile.Save();
                        }
                        continue;
                    }

                    won = false;
                }

                if (won)
                {

                    return new BingoCompleteInfo([t], isMultiplayer ? ChallengeTools.IGT.Translate("Team <team_name> won!") : ChallengeTools.IGT.Translate("You won!"), addText, BingoCompleteReason.Blackout);
                }
            }

            return null;
        }


        public int CompletedChallengesForTeam(int team)
        {
            int all = 0;
            for (int j = 0; j < grid.GetLength(1); j++)
                for (int i = 0; i < grid.GetLength(0); i++)
                    if ((BingoHooks.GlobalBoard.challengeGrid[i, j] as BingoChallenge).TeamsCompleted[team])
                        all++;
            
            return all;
        }

        public void AddRevealedToQueue()
        {
            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    if (grid[i, j].challenge is BingoChallenge g && ChallengeHooks.revealInMemory.Contains(g))
                    {
                        g.revealed = true;
                        g.CompleteChallenge();
                        g.revealed = false;
                        queue.Add(grid[i, j]);
                        grid[i, j].teamResponsible = SteamTest.team;
                    }
                }
            }
            animation = 100;
        }

        public BingoCompleteInfo? TallyUp()
        {
            string addText = ChallengeTools.IGT.Translate("Number of challenges completed: ");
            bool isMultiplayer = BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer) &&
                BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID.GetSteamID64() != default;

            if (isMultiplayer)
            {
                addText = ChallengeTools.IGT.Translate("Number of challenges completed for each team: ");
                List<int> teamsInBingo = BingoData.TeamsStringToList(BingoData.BingoSaves[ExpeditionData.slugcatPlayer].teamsInBingo);
                Dictionary<int, int> completedForTeam = [];

                foreach (int t in teamsInBingo)
                {
                    completedForTeam.Add(t, 0);
                    foreach (var ch in ExpeditionData.challengeList)
                    {
                        BingoChallenge challenge = ch as BingoChallenge;
                        if (challenge.TeamsCompleted[t]) completedForTeam[t]++;
                    }
                }

                foreach (var kvp in completedForTeam)
                {
                    
                    addText += "\n" + BingoPage.TeamName[kvp.Key] + " - " + kvp.Value;
                }
            }
            else
            {
                int completedChallenges = 0;

                foreach (var ch in ExpeditionData.challengeList)
                {
                    BingoChallenge challenge = ch as BingoChallenge;
                    if (challenge.TeamsCompleted[SteamTest.team]) completedChallenges++;
                }

                addText += completedChallenges;
            }

            return new BingoCompleteInfo([SteamTest.team], ChallengeTools.IGT.Translate("Game concluded!").Replace("<LINE>", "\r\n"), addText, BingoCompleteReason.TallyUp);
        }

        public override void ClearSprites()
        {
            base.ClearSprites();

            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    grid[i, j].Clear();
                    if (grid[i, j].phrase != null) grid[i, j].phrase.ClearAll();
                }
            }
            bingoCompleteInfo.RemoveFromContainer();
            bingoCompleteTitle.RemoveFromContainer();
            bingoCompleteInfoShadow.RemoveFromContainer();
            queue.Clear();
            completeQueue.Clear();
            cursor.RemoveSprites();
            foreach (var butt in hudButtons)
            {
                butt.Remove();
            }

            foreach (var hint in hints)
            {
                hint.RemoveSprites();
            }
        }

        public override void Update()
        {
            base.Update();

            lastMousePosition = mousePosition;
            mousePosition = Futile.mousePosition;

            cantClickCounter = Mathf.Max(0, cantClickCounter - 1);

            lastMouseDown = mouseDown;
            mouseDown = Input.GetMouseButton(0);
            lastMouseRightDown = mouseRightDown;
            mouseRightDown = Input.GetMouseButton(1);

            if (Plugin.PluginInstance.BingoConfig.UseMapInput.Value)
            {
                if (hud.owner.GetOwnerType() == HUD.HUD.OwnerType.Player)
                {
                    Player p = hud.owner as Player;

                    if (p.input[0].mp && !p.input[1].mp)
                    {
                        Toggled = !Toggled;
                    }
                }
                else if (hud.owner.GetOwnerType() == HUD.HUD.OwnerType.SleepScreen || hud.owner.GetOwnerType() == HUD.HUD.OwnerType.DeathScreen)
                {
                    lastMapOpen = mapOpen;
                    mapOpen = hud.owner.MapInput.mp;

                    if (mapOpen && !lastMapOpen)
                    {
                        Toggled = !Toggled;
                    }
                }
            }

            if (queue.Count != 0)
            {
                animation--;
                if (animation <= 0)
                {
                    queue[0].StartAnim();
                    queue.RemoveAt(0);
                    animation = animationLength;
                    //if (queue.Count == 0 && completeQueue.Count == 0 && hud.owner is SleepAndDeathScreen scr) scr.forceWatchAnimation = false;
                    if (queue.Count == 0 && completeQueue.Count == 0 && !addCompleteAlpha)
                    {
                        BingoCompleteInfo? potentialEndGame = CheckWinLose();
                        if (potentialEndGame.HasValue)
                        {
                            DoComplete(potentialEndGame.Value);
                        }
                    }
                }
            }

            if (completeQueue.Count != 0)
            {
                completeAnimation--;
                if (completeAnimation <= 0)
                {
                    completeQueue[0].context = completeQueue.Count == 1 ? BingoInfo.AnimationContext.BingoLast : BingoInfo.AnimationContext.Bingo;
                    completeQueue[0].StartAnim();
                    completeQueue.RemoveAt(0);
                    completeAnimation = Mathf.Min(completeQueue.Count * 3, 30);
                }
            }

            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    grid[i, j].Update();
                }
            }

            //if (hud.owner.GetOwnerType() == HUD.HUD.OwnerType.Player)
            //{
            //    Player p = hud.owner as Player;
            //
            //    if (Input.GetKeyDown(Plugin.bingoConfig.HUDKeybind.Value))//(p.input[0].mp && !p.input[1].mp)
            //    {
            //        toggled = !toggled;
            //        Cursor.visible = toggled;
            //    }
            //}
            //else if (hud.owner.GetOwnerType() == HUD.HUD.OwnerType.SleepScreen || hud.owner.GetOwnerType() == HUD.HUD.OwnerType.DeathScreen)
            //{
            //    lastMapOpen = mapOpen;
            //    mapOpen = hud.owner.MapInput.mp;
            //
            //    if (mapOpen && !lastMapOpen)
            //    {
            //        toggled = !toggled;
            //    }
            //}
            lastAlpha = alpha;
            alpha = BingoData.SpectatorMode ? 1f : Mathf.Clamp01(alpha + 0.1f * (Toggled ? 1f : -1f));

            if (fadeOutCounter > 0)
            {
                fadeOutCounter--;
                if (fadeOutCounter == 0)
                {
                    addCompleteAlpha = false;
                }
            }

            lastCompleteAlpha = completeAlpha;
            completeAlpha = Mathf.Clamp01(completeAlpha + 0.15f * (addCompleteAlpha ? 1f : -0.1f));

            textShake = Mathf.Max(0f, textShake - 0.034f);

            sinCounter += 0.016f;
            if (sinCounter >= 1f)
            {
                sinCounter -= 1f;
            }

            cursor.Update();

            Vector2 center = BingoData.SpectatorMode ? new(hud.rainWorld.screenSize.x * 0.5f - 15f + 0.01f, hud.rainWorld.screenSize.y * 0.5f - 35f + 0.01f) : new(hud.rainWorld.screenSize.x * 0.16f + 0.01f, hud.rainWorld.screenSize.y * 0.715f + 0.01f);
            int activeButts = 0;
            foreach (var butt in hudButtons)
            {
                if (butt.active) activeButts++;
            }
            const float distanceToTravel = 90f;
            float initNegativePos = (Mathf.Max(0, activeButts - 1) / 2f) * -distanceToTravel;
            int activeButtIndex = 0;
            for (int i = 0; i < hudButtons.Length; i++)
            {
                BingoHUDButton butt = hudButtons[i];
                butt.Update();
                butt.alpha = !butt.active ? 0f : alpha;
                butt.lastAlpha = !butt.active ? 0f : lastAlpha;

                butt.pos = center + new Vector2(12.5f + (initNegativePos + activeButtIndex * distanceToTravel), -(BingoData.SpectatorMode ? 475f : 420f) * 0.625f);
                if (butt.active)
                {
                    activeButtIndex++;
                }
            }

            // Hints
            if (hud.owner is Player player && SteamTest.team != 8)
            {
                Room room = player.abstractCreature.world.game.cameras[0].room;
                if (room != null)
                {
                    for (int i = 0; i < room.abstractRoom.entities.Count; i++)
                    {
                        if (room.abstractRoom.entities[i] is AbstractPhysicalObject obj && 
                            obj.realizedObject != null && 
                            !hints.Any(x => x.followObject == obj.realizedObject))
                        {
                            var tradeded = ExpeditionData.challengeList.FirstOrDefault(x => x is BingoTradeTradedChallenge);
                            var traded = ExpeditionData.challengeList.FirstOrDefault(x => x is BingoTradeChallenge);
                            if (tradeded is BingoTradeTradedChallenge || traded is BingoTradeChallenge)
                            {
                                if (obj.type != AbstractPhysicalObject.AbstractObjectType.Creature && 
                                    tradeded is BingoTradeTradedChallenge tradeChallenge && 
                                    tradeChallenge.traderItems.Keys.Count > 0 && 
                                    tradeChallenge.traderItems.Keys.Contains(room.abstractRoom.entities[i].ID) &&
                                    obj.realizedObject.grabbedBy.Count == 0)
                                {
                                    BingoHUDHint hint = new BingoHUDHint(obj.realizedObject, room.abstractRoom.index, "scav_merchant", Color.white, new Vector2(0f, 30f), player.abstractCreature.world.game.cameras[0], "Hologram");
                                    hints.Add(hint);
                                    hud.fContainers[1].AddChild(hint.sprite);
                                    continue;
                                }
                                if (room.abstractRoom.scavengerTrader && obj is AbstractCreature crit && crit.state.alive && crit.creatureTemplate.type == CreatureTemplate.Type.Scavenger)
                                {
                                    if ((crit.abstractAI as ScavengerAbstractAI).squad != null && (crit.abstractAI as ScavengerAbstractAI).squad.missionType == ScavengerAbstractAI.ScavengerSquad.MissionID.Trade)
                                    {
                                        BingoHUDHint checkMark = new BingoHUDHint(obj.realizedObject, room.abstractRoom.index, "yesmerchant", Color.white, new Vector2(0f, 30f), player.abstractCreature.world.game.cameras[0]);
                                        hints.Add(checkMark);
                                        hud.fContainers[1].AddChild(checkMark.sprite);
                                        continue;
                                    }
                                    else
                                    {
                                        BingoHUDHint wrongMark = new BingoHUDHint(obj.realizedObject, room.abstractRoom.index, "nomerchant", Color.white, new Vector2(0f, 30f), player.abstractCreature.world.game.cameras[0]);
                                        hints.Add(wrongMark);
                                        hud.fContainers[1].AddChild(wrongMark.sprite);
                                        continue;
                                    }
                                }
                            }
                            if (obj.type == AbstractPhysicalObject.AbstractObjectType.DangleFruit)
                            {
                                Random.State state = Random.state;
                                Random.InitState(obj.ID.RandomSeed);
                                // 1/999
                                if (Random.value < 0.000999f)
                                {
                                    BingoHUDHint hint = new BingoHUDHint(obj.realizedObject, room.abstractRoom.index, "pipis", Color.white, new Vector2(-17f, -22f), player.abstractCreature.world.game.cameras[0]);
                                    hints.Add(hint);
                                    hud.fContainers[1].AddChild(hint.sprite);
                                    Random.state = state;
                                    continue;
                                }
                                Random.state = state;
                            }
                            if (obj.realizedObject is HalcyonPearl p &&
                                p.grabbedBy.Count == 0 && 
                                p.hoverPos == null &&
                                ExpeditionData.slugcatPlayer == MoreSlugcatsEnums.SlugcatStatsName.Saint)
                            {
                                BingoHUDHint hint = new BingoHUDHint(obj.realizedObject, room.abstractRoom.index, "musicSymbol", Color.white, new Vector2(0f, 30f), player.abstractCreature.world.game.cameras[0], "Hologram");
                                hints.Add(hint);
                                hud.fContainers[1].AddChild(hint.sprite);
                                continue;
                            }
                        }
                    }
                }
            }

            foreach (var hint in hints)
            {
                hint.Update();

                if (hint.requestRemove && hint.deathFade == 0f) hint.RemoveSprites();
            }
            hints.RemoveAll(x => x.requestRemove == true && x.deathFade == 0f);

            if (ForceTallyUp)
            {
                PressTallyUp();
                ForceTallyUp = false;
            }
        }

        public static void EndBingoSessionHost()
        {
            if (SteamFinal.ConnectedPlayers.Count > 0)
            {
                foreach (var player in SteamFinal.ConnectedPlayers)
                {   
                    InnerWorkings.SendMessage("x", player);
                }
            }
            //Custom.rainWorld.processManager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
            Custom.rainWorld.processManager.rainWorld.progression.WipeSaveState(ExpeditionData.slugcatPlayer);
        }

        public void GenerateBingoGrid()
        {
            grid = new BingoInfo[board.size, board.size];

            for (int j = 0; j < grid.GetLength(1); j++)
            {
                for (int i = 0; i < grid.GetLength(0); i++)
                {
                    float size = ((BingoData.SpectatorMode && !Plugin.PluginInstance.BingoConfig.OneToOneSpecBoard.Value) ? 475f : 420f) / board.size;
                    float topLeft = -size * board.size / 2f;
                    Vector2 center = BingoData.SpectatorMode ? new(hud.rainWorld.screenSize.x * 0.5f - 15f + 0.01f, hud.rainWorld.screenSize.y * 0.5f - 35f + 0.01f) : new(hud.rainWorld.screenSize.x * 0.16f + 0.01f, hud.rainWorld.screenSize.y * 0.715f + 0.01f);
                    grid[i, j] = new BingoInfo(hud, this,
                        center + new Vector2(topLeft + i * size + (i * size * 0.075f) + size / 2f, -topLeft - j * size - (j * size * 0.075f) - size / 2f), size, hud.fContainers[0], board.challengeGrid[i, j], i, j);
                }
            }
        }

        public override void Draw(float timeStacker)
        {
            base.Draw(timeStacker);


            float alfa = Mathf.Lerp(lastAlpha, alpha, timeStacker);
            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    float chooseAlpha = Mathf.Max(Mathf.Lerp(grid[i, j].lastOverwriteAlpha, grid[i, j].overwriteAlpha, timeStacker), alfa);
                    grid[i, j].alpha = Custom.LerpCircEaseOut(0f, 1f, chooseAlpha);
                    grid[i, j].Draw(timeStacker);
                }
            }

            float cAlfa = Custom.LerpCircEaseOut(0f, 1f, Mathf.Lerp(lastCompleteAlpha, completeAlpha, timeStacker));

            if (bingoCompleteTitle.element == watcherTitle && ExpeditionData.slugcatPlayer != Watcher.WatcherEnums.SlugcatStatsName.Watcher)
            {
                bingoCompleteTitle.element = normalTitle;
                bingoCompleteTitle.shader = Custom.rainWorld.Shaders["MenuText"];
            }
            if (bingoCompleteTitle.element == normalTitle && ExpeditionData.slugcatPlayer == Watcher.WatcherEnums.SlugcatStatsName.Watcher)
            {
                bingoCompleteTitle.element = watcherTitle;
                bingoCompleteTitle.shader = Custom.rainWorld.Shaders["Basic"];
            }

            bingoCompleteTitle.alpha = cAlfa;
            bingoCompleteInfo.alpha = cAlfa;
            bingoCompleteInfoShadow.alpha = cAlfa;

            Vector2 random = Custom.RNV() * textShake * 7f;
            bingoCompleteTitle.SetPosition(new Vector2(hud.rainWorld.screenSize.x * 0.5f, hud.rainWorld.screenSize.y * 0.93f) + Custom.RNV() * textShake * 8f);
            bingoCompleteInfo.SetPosition(new Vector2(hud.rainWorld.screenSize.x * 0.5f + 0.01f, hud.rainWorld.screenSize.y * 0.87f + 0.01f) + Custom.RNV() * textShake * 6f);
            bingoCompleteInfoShadow.SetPosition(bingoCompleteInfo.GetPosition() + new Vector2(1f, -1f));
            bingoCompleteInfo.color = Color.Lerp(Color.white, colorToFadeTo, Mathf.Abs(Mathf.Sin(sinCounter * Mathf.PI)));

            cursor.GrafUpdate(timeStacker);

            foreach (var butt in hudButtons)
            {
                butt.Draw(timeStacker);
            }

            if (hud.owner is Player player)
            {
                foreach (var hint in hints)
                {
                    hint.Draw(timeStacker, player.abstractCreature.world.game.cameras[0].pos);
                }
            }
        }

        public void PressExitGame()
        {
            ReadyForLeave = true;
            Custom.rainWorld.processManager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
        }

        public void PressTallyUp()
        {
            BingoCompleteInfo? potentialEndGame = TallyUp();
            if (potentialEndGame.HasValue)
            {
                DoComplete(potentialEndGame.Value);
            }

            if (BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer) && BingoData.BingoSaves[ExpeditionData.slugcatPlayer].isHost) SteamFinal.BroadcastTallyUpOrder();
        }

        public class BingoInfo
        {
            public enum AnimationContext
            {
                Complete,
                Lockout,
                AlmostComplete,
                Failure,
                ValueChange,
                Bingo,
                BingoLast
            }

            public HUD.HUD hud;
            public Vector2 pos;
            public FSprite sprite;
            public FLabel label;
            public float size;
            public Challenge challenge;
            public BingoHUDMain owner;
            public float alpha;
            public float overwriteAlpha;
            public float lastOverwriteAlpha;
            public int x;
            public int y;
            public Phrase phrase;
            public FContainer container;
            public FSprite[] border;
            public FSprite lockoutLock;
            public TriangleMesh[] teamColors;
            public Vector2[] corners;
            bool showBG;
            FSprite[] boxSprites;
            FLabel infoLabel;
            bool boxVisible;
            public bool lastMouseOver;
            public bool mouseOver;
            public float scale;
            public float goalScale;
            public float goalScaleSpeed;
            public List<TriangleMesh> visible;
            public int updateTextCounter;
            public float sinCounter;
            public BorderEffect effect;
            public AnimationContext context;
            public int teamResponsible;
            public float shakeLock;
            public SoundID contextSound;
            public Color baseBorderColor;
            public List<Color> borderColors;
            public int borderColorIndex1;
            public int borderColorIndex2;
            public bool beingCheatedBeepBoop;
            public bool lastBeingCheatedBeepBoop;
            public BingoHUDCheatButton[] cheatsHeeHee;

            public BingoInfo(HUD.HUD hud, BingoHUDMain owner, Vector2 pos, float size, FContainer container, Challenge challenge, int x, int y)
            {
                this.hud = hud;
                this.pos = pos;
                this.size = size;
                this.challenge = challenge;
                this.owner = owner;
                this.x = x;
                this.y = y;
                this.container = container;
                alpha = 1f;
                (challenge as BingoChallenge).ValueChanged += OnValueChanged;
                (challenge as BingoChallenge).ChallengeCompleted += ChallengeCompleted;
                (challenge as BingoChallenge).ChallengeFailed += OnChallengeFailed;
                (challenge as BingoChallenge).ChallengeLockedOut += BingoInfo_ChallengeLockedOut;
                (challenge as BingoChallenge).ChallengeAlmostComplete += BingoInfo_ChallengeAlmostComplete;
                (challenge as BingoChallenge).ChallengeDepleted += BingoInfo_ChallengeDepleted;
                showBG = true;
                scale = 1f;
                goalScale = 1f;
                goalScaleSpeed = 1f;

                sprite = new FSprite("pixel")
                {
                    scale = size,
                    color = new Color(0.01f, 0.01f, 0.01f),
                    alpha = 0.7f,
                    x = pos.x,
                    y = pos.y,
                    anchorX = 0.5f,
                    anchorY = 0.5f
                };
                label = new FLabel(Custom.GetFont(), "")
                {
                    x = pos.x,
                    y = pos.y,
                    anchorX = 0.5f,
                    anchorY = 0.5f
                };
                container.AddChild(sprite);

                float scaleFac = (size / 84f);

                teamColors = new TriangleMesh[8];
                TriangleMesh.Triangle[] tris = [
                    new(0, 1, 2),
                    new(1, 2, 3)
                    ];
                for (int i = 0; i < teamColors.Length; i++)
                {
                    teamColors[i] = new TriangleMesh("Futile_White", tris, false)
                    {
                        color = BingoPage.TEAM_COLOR[i],
                    };
                    container.AddChild(teamColors[i]);
                }

                border = new FSprite[4];
                for (int i = 0; i < border.Length; i++)
                {
                    border[i] = new FSprite("pixel")
                    {
                        scaleX = (i < 2) ? size : 2f,
                        anchorX = (i < 2) ? 0f : 0.5f,
                        scaleY = (i < 2) ? 2f : size,
                        anchorY = (i < 2) ? 0.5f : 0f
                    };
                    container.AddChild(border[i]);
                }
                corners = new Vector2[4];
                corners[0] = pos + new Vector2(-size / 2f, -size / 2f);
                corners[1] = pos + new Vector2(-size / 2f, size / 2f);
                corners[2] = pos + new Vector2(size / 2f, -size / 2f);
                corners[3] = pos + new Vector2(size / 2f, size / 2f);
                border[0].SetPosition(corners[0]);
                border[1].SetPosition(corners[1]);
                border[2].SetPosition(corners[0]);
                border[3].SetPosition(corners[2]);

                container.AddChild(label);

                boxSprites = new FSprite[5];
                int width = 400;
                int height = 75;
                infoLabel = new FLabel(Custom.GetFont(), challenge.description.WrapText(false, width - 20f))
                {
                    anchorX = 0.5f,
                    anchorY = 0.5f,
                    alignment = FLabelAlignment.Center
                };
                container.AddChild(infoLabel);
                boxSprites[0] = new FSprite("pixel", true)
                {
                    anchorX = 0,
                    anchorY = 0,
                    scaleX = width,
                    scaleY = height,
                    color = new Color(0.01f, 0.01f, 0.01f, 0.9f)
                };
                boxSprites[1] = new FSprite("pixel", true)
                {
                    anchorX = 0,
                    anchorY = 0,
                    scaleX = width + 1,
                };
                boxSprites[2] = new FSprite("pixel", true)
                {
                    anchorX = 0,
                    anchorY = 0,
                    scaleX = width,
                };
                boxSprites[3] = new FSprite("pixel", true)
                {
                    anchorX = 0,
                    anchorY = 0,
                    scaleY = height,
                };
                boxSprites[4] = new FSprite("pixel", true)
                {
                    anchorX = 0,
                    anchorY = 0,
                    scaleY = height,
                };
                for (int i = 0; i < boxSprites.Length; i++)
                {
                    container.AddChild(boxSprites[i]);
                    if (i > 0)
                    {
                        boxSprites[i].scaleX += 1f;
                        boxSprites[i].scaleY += 1f;
                        boxSprites[i].color = Color.white;
                        boxSprites[i].shader = hud.rainWorld.Shaders["MenuText"];
                    }
                }

                if (owner.cheatsEnabled)
                {
                    cheatsHeeHee = new BingoHUDCheatButton[8];
                    for (int i = 0; i < cheatsHeeHee.Length; i++)
                    {
                        float angle = 45f * i;
                        cheatsHeeHee[i] = new BingoHUDCheatButton(this, i, angle);
                    }
                }

                contextSound = SoundID.None;
                baseBorderColor = (challenge as BingoChallenge).TeamsFailed[SteamTest.team] ? Color.grey : Color.white;
                if (challenge.hidden) context = AnimationContext.Lockout;
                UpdateText();
                UpdateTeamColors();
            }

            private void BingoInfo_ChallengeDepleted(int tea)
            {
                context = AnimationContext.Failure;
                teamResponsible = tea;
                UpdateText();
                sinCounter = 0f;
            }

            private void BingoInfo_ChallengeAlmostComplete(int tea)
            {
                context = AnimationContext.AlmostComplete;
                teamResponsible = tea;
                UpdateText();
                sinCounter = 0f;
            }

            private void BingoInfo_ChallengeLockedOut(int tea)
            {
                context = AnimationContext.Lockout;
                teamResponsible = tea;
                UpdateText();
                sinCounter = 0f;
            }

            public void OnChallengeFailed(int tea)
            {
                context = AnimationContext.Failure;
                teamResponsible = tea;
                if (tea == SteamTest.team) baseBorderColor = Color.grey;
                UpdateText();
                sinCounter = 0f;
            }

            public void ChallengeCompleted(int tea)
            {
                context = AnimationContext.Complete;
                teamResponsible = tea;
                UpdateText();
                sinCounter = 0f;
            }

            public void OnValueChanged()
            {
                context = AnimationContext.ValueChange;
                UpdateText();
            }

            public void Clear()
            {
                (challenge as BingoChallenge).ValueChanged -= OnValueChanged;
                (challenge as BingoChallenge).ChallengeCompleted -= ChallengeCompleted;
                (challenge as BingoChallenge).ChallengeFailed -= OnChallengeFailed;
                (challenge as BingoChallenge).ChallengeLockedOut -= BingoInfo_ChallengeLockedOut;
                (challenge as BingoChallenge).ChallengeAlmostComplete -= BingoInfo_ChallengeAlmostComplete;
                sprite.RemoveFromContainer();
                label.RemoveFromContainer();
                foreach (var g in border)
                {
                    g.RemoveFromContainer();
                }
                foreach (var g in boxSprites)
                {
                    g.RemoveFromContainer();
                }
                foreach (var g in teamColors)
                {
                    g.RemoveFromContainer();
                }
                infoLabel.RemoveFromContainer();
                if (owner.cheatsEnabled)
                {
                    for (int i = 0; i < cheatsHeeHee.Length; i++)
                    {
                        cheatsHeeHee[i].Remove();
                    }
                }
            }

            public void UpdateTeamColors()
            {
                borderColors = [baseBorderColor];
                visible = [];
                bool g = false;
                bool g2 = false;
                for (int i = 0; i < teamColors.Length; i++)
                {
                    teamColors[i].isVisible = false;
                    if ((challenge as BingoChallenge).TeamsCompleted[i])
                    {
                        visible.Add(teamColors[i]);
                        g = true;
                        if (SteamTest.team == i)
                        {
                            g2 = true;
                        }
                        borderColors.Add(BingoPage.TEAM_COLOR[i]);
                    }
                }
                if (!g2 && challenge.revealed)
                {
                    borderColors = [baseBorderColor, BingoPage.TEAM_COLOR[SteamTest.team]];
                }
                if (g2)
                {
                    borderColors.Remove(BingoPage.TEAM_COLOR[SteamTest.team]);
                    borderColors.Insert(1, BingoPage.TEAM_COLOR[SteamTest.team]);
                }
                if (g && SteamTest.team == 8) borderColors.Remove(baseBorderColor);
                showBG = !g;
                borderColorIndex1 = 0;
                borderColorIndex2 = borderColors.Count > 1 ? 1 : 0;
                sinCounter = UnityEngine.Random.value;
            }

            public void ShiftBorderColors()
            {
                bool skipSecond = borderColorIndex1 == borderColorIndex2;

                borderColorIndex2 += 1;
                if (borderColorIndex2 >= borderColors.Count)
                {
                    borderColorIndex2 = 0;
                }

                if (skipSecond) return;
                borderColorIndex1 += 1;
                if (borderColorIndex1 >= borderColors.Count)
                {
                    borderColorIndex1 = 0;
                }
            }

            public void ChangeCheetah(bool cheetah)
            {
                for (int i = 0; i < cheatsHeeHee.Length; i++)
                {
                    cheatsHeeHee[i].tickSprite.SetElementByName(cheetah ? "Menu_Symbol_Clear_All" : "Menu_Symbol_CheckBox");
                    cheatsHeeHee[i].tickColor = cheetah ? Color.red : Color.green;
                }
            }

            public void Update()
            {
                lastMouseOver = mouseOver;
                mouseOver = owner.mousePosition.x > pos.x - size / 2f && owner.mousePosition.y > pos.y - size / 2f
                        && owner.mousePosition.x < pos.x + size / 2f && owner.mousePosition.y < pos.y + size / 2f;
                boxVisible = alpha > 0f && mouseOver && (!owner.cheatsEnabled || !owner.cheatingRnAtThisMoment);

                if (mouseOver && lastMouseOver != mouseOver)
                {
                    for (int i = 0; i < boxSprites.Length; i++)
                    {
                        boxSprites[i].MoveToFront();
                    }
                    infoLabel.MoveToFront();
                }

                lastBeingCheatedBeepBoop = beingCheatedBeepBoop;
                if (owner.cheatsEnabled && (!owner.cheatingRnAtThisMoment || beingCheatedBeepBoop) && alpha > 0f && mouseOver)
                {
                    if (owner.MouseLeftDown)
                    {
                        if (!beingCheatedBeepBoop) ChangeCheetah(false);
                        beingCheatedBeepBoop = !beingCheatedBeepBoop;
                        owner.cheatingRnAtThisMoment = beingCheatedBeepBoop;
                    }
                    else if (owner.MouseRightDown)
                    {
                        if (!beingCheatedBeepBoop) ChangeCheetah(true);
                        beingCheatedBeepBoop = !beingCheatedBeepBoop;
                        owner.cheatingRnAtThisMoment = beingCheatedBeepBoop;
                    }
                }

                //if (beingCheatedBeepBoop && lastBeingCheatedBeepBoop != beingCheatedBeepBoop)
                //{
                //    for (int i = 0; i < cheatsHeeHee.Length; i++)
                //    {
                //        cheatsHeeHee[i].MoveToFront();
                //    }
                //}

                bool doOverwriteAlpha = false;
                if (updateTextCounter > 0)
                {
                    if (updateTextCounter > 1) doOverwriteAlpha = true;
                    updateTextCounter -= 1;
                    if (updateTextCounter == 0)
                    {
                        Tick();
                    }
                }

                lastOverwriteAlpha = overwriteAlpha;
                if (doOverwriteAlpha)
                {
                    overwriteAlpha = Mathf.Min(overwriteAlpha + 0.04f, 1f);
                }
                else overwriteAlpha = Mathf.Max(overwriteAlpha - 0.02f, 0f);

                sinCounter += 0.032f;
                if (sinCounter >= 1f)
                {
                    ShiftBorderColors();
                    sinCounter -= 1f;
                }

                if (scale == (context == AnimationContext.Failure ? 0.85f : 1.135f))
                {
                    goalScale = 1f;
                    goalScaleSpeed = 0.6f;
                }
                scale = Custom.LerpAndTick(scale, goalScale, goalScaleSpeed, 0.001f);

                effect?.Update();
                if (effect != null && effect.alpha == 0f)
                {
                    effect.Remove();
                    effect = null;
                }

                shakeLock = Mathf.Max(shakeLock - 0.07f, 0f);

                if (owner.cheatsEnabled)
                {
                    for (int i = 0; i < cheatsHeeHee.Length; i++)
                    {
                        cheatsHeeHee[i].appear = beingCheatedBeepBoop;
                        cheatsHeeHee[i].Update();
                    }
                }
            }

            public void Draw(float timeStacker)
            {
                // Phrase biz
                sprite.alpha = showBG ? Mathf.Lerp(0f, 0.85f, alpha) : 0f;
                foreach (var g in border) g.alpha = alpha;
                label.alpha = alpha;

                for (int i = 0; i < teamColors.Length; i++)
                {
                    teamColors[i].alpha = Mathf.Lerp(0f, 0.4f, alpha);
                }

                if (phrase != null)
                {
                    phrase.SetAlpha(alpha);
                    phrase.centerPos = pos;
                    phrase.scale = size / 84f * scale;
                    if (shakeLock > 0f)
                    {
                        phrase.WordsFlat[0].display.rotation = Mathf.LerpAngle(-30f, 30f, UnityEngine.Random.value) * shakeLock;
                    }
                    phrase.Draw();
                }

                // Border
                for (int i = 0; i < border.Length; i++)
                {
                    border[i].scaleX = (i < 2) ? size * scale : 2f;
                    border[i].scaleY = (i < 2) ? 2f : size * scale;
                }
                sprite.scale = scale * size;
                corners[0] = pos + new Vector2(-size * scale / 2f, -size * scale / 2f);
                corners[1] = pos + new Vector2(-size * scale / 2f, size * scale / 2f);
                corners[2] = pos + new Vector2(size * scale / 2f, -size * scale / 2f);
                corners[3] = pos + new Vector2(size * scale / 2f, size * scale / 2f);
                border[0].SetPosition(corners[0]);
                border[1].SetPosition(corners[1]);
                border[2].SetPosition(corners[0]);
                border[3].SetPosition(corners[2]);

                Color flashColor = Color.Lerp(borderColors[borderColorIndex1], borderColors[borderColorIndex2], Mathf.Abs(Mathf.Sin(sinCounter * Mathf.PI * 0.5f))).CloneWithNewAlpha(1f);
                for (int i = 0; i < border.Length; i++)
                {
                    border[i].color = flashColor;
                }

                // Colors
                float dist = size / visible.Count * scale;
                float halfStep = dist * 0.3f;
                for (int i = 0; i < visible.Count; i++)
                {
                    visible[i].isVisible = true;

                    int isFirst = i == 0 ? 0 : 1;
                    int isLast = i == visible.Count - 1 ? 0 : 1;
                    visible[i].MoveVertice(0, corners[0] + new Vector2(dist * i - halfStep * isFirst, 0f));
                    visible[i].MoveVertice(1, corners[1] + new Vector2(dist * i + halfStep * isFirst, 0f));

                    visible[i].MoveVertice(2, corners[0] + new Vector2(dist * (i + 1) - halfStep * isLast, 0f));
                    visible[i].MoveVertice(3, corners[1] + new Vector2(dist * (i + 1) + halfStep * isLast, 0f));
                }

                // Thinj and binj (box)
                for (int i = 0; i < boxSprites.Length; i++)
                {
                    boxSprites[i].isVisible = boxVisible;
                }
                infoLabel.isVisible = boxVisible;
                float yStep = boxSprites[3].scaleY / 2f;
                boxSprites[0].SetPosition(pos + new Vector2(size / 2f + 10f, -yStep));
                boxSprites[1].SetPosition(pos + new Vector2(size / 2f + 10f, yStep - 1));
                boxSprites[2].SetPosition(pos + new Vector2(size / 2f + 10f, -yStep));
                boxSprites[3].SetPosition(pos + new Vector2(size / 2f + 10f, -yStep));
                boxSprites[4].SetPosition(pos + new Vector2(size / 2f + 10f + boxSprites[0].scaleX, -yStep));
                infoLabel.SetPosition(pos + new Vector2(size / 2f + 10f + boxSprites[0].scaleX / 2f, 0) + new Vector2(0.01f, 0.01f));

                effect?.Draw(timeStacker);

                if (owner.cheatsEnabled)
                {
                    for (int i = 0; i < cheatsHeeHee.Length; i++)
                    {
                        cheatsHeeHee[i].Draw(timeStacker);
                    }
                }
            }

            public string SplitString(string s)
            {
                string modified = "";
                int limit = 0;
                foreach (var c in s)
                {
                    limit += 6;
                    if (limit > size * 0.8f)
                    {
                        modified += "\n";
                        limit = 0;
                    }
                    modified += c;
                }
                return modified;
            }

            public void UpdateText()
            {
                if (phrase == null)
                {
                    updateTextCounter = 1;
                    return;
                }
                owner.queue.Add(this);
            }

            public void StartAnim()
            {
                goalScale = 0.85f;
                goalScaleSpeed = 0.0125f;
                updateTextCounter = 50;
                contextSound = SoundID.HUD_Karma_Reinforce_Bump;
                switch (context)
                {
                    case AnimationContext.AlmostComplete:
                        contextSound = MMFEnums.MMFSoundID.Tock;
                        break;
                    case AnimationContext.Failure:
                        goalScale = 1.135f;
                        goalScaleSpeed = 0.0125f;
                        updateTextCounter = 50;
                        break;
                    case AnimationContext.ValueChange:
                        contextSound = MMFEnums.MMFSoundID.Tick;
                        break;
                    case AnimationContext.Bingo:
                        goalScale = 0.85f;
                        goalScaleSpeed = 0.013f;
                        updateTextCounter = 80;
                        contextSound = SoundID.Moon_Wake_Up_Swarmer_Ping;
                        break;
                    case AnimationContext.BingoLast:
                        goalScale = 0.8f;
                        goalScaleSpeed = 0.01f;
                        updateTextCounter = 80;
                        contextSound = BingoEnums.BINGO_FINAL_BONG;
                        break;
                }
            }

            public void Tick()
            {
                if (phrase != null)
                {
                    phrase.ClearAll();
                }
                try
                {
                    phrase = context == AnimationContext.Lockout ? Phrase.LockPhrase() : (challenge as BingoChallenge).ConstructPhrase();
                }
                catch
                {
                    phrase = new Phrase([[new Icon("Sandbox_QuestionMark")]]);
                }
                if (context == AnimationContext.Lockout) shakeLock = 1f;
                if (phrase != null)
                {
                    phrase.AddAll(container);
                    phrase.centerPos = pos;
                    phrase.scale = size / 84f * scale;
                    phrase.Draw();
                }
                label.text = phrase == null ? SplitString(challenge.description) : "";
                infoLabel.text = challenge.description.WrapText(false, boxSprites[0].scaleX - 20f);
                if (challenge.revealed && !challenge.completed) infoLabel.text += ChallengeTools.IGT.Translate("<LINE>Save the game to Complete").Replace("<LINE>", "\r\n");
                if ((challenge as BingoChallenge).TeamsCompleted.Any(x => x == true))
                {
                    infoLabel.text += ChallengeTools.IGT.Translate("<LINE>Completed by: ").Replace("<LINE>", "\r\n");
                    for (int i = 0; i < (challenge as BingoChallenge).TeamsCompleted.Length; i++)
                    {
                        if ((challenge as BingoChallenge).TeamsCompleted[i]) infoLabel.text += BingoPage.TeamName[i] + ", ";
                    }
                    infoLabel.text = infoLabel.text.Substring(0, infoLabel.text.Length - 2); // Trim the last ", "
                }
                if ((challenge as BingoChallenge).TeamsFailed.Any(x => x == true))
                {
                    infoLabel.text += ChallengeTools.IGT.Translate("<LINE>Failed by: ").Replace("<LINE>", "\r\n");
                    for (int i = 0; i < (challenge as BingoChallenge).TeamsFailed.Length; i++)
                    {
                        if ((challenge as BingoChallenge).TeamsFailed[i]) infoLabel.text += BingoPage.TeamName[i] + ", ";
                    }
                    infoLabel.text = infoLabel.text.Substring(0, infoLabel.text.Length - 2); // Trim the last ", "
                }
                if (context == AnimationContext.BingoLast)
                {
                    owner.ShowWinText();
                    //if (owner.queue.Count == 0 && owner.completeQueue.Count == 0 && hud.owner is SleepAndDeathScreen scr) scr.forceWatchAnimation = false;
                }
                if (overwriteAlpha > 0f) // Actual visual thonk bonk when the challenge completes
                {
                    hud.PlaySound(contextSound);
                    bool chCompleted = teamResponsible == 8 || (challenge as BingoChallenge).TeamsCompleted[teamResponsible];
                    goalScale = 1.135f;
                    if (context == AnimationContext.Failure)
                    {
                        sinCounter = 0f;
                        goalScale = 0.85f;
                    }
                    goalScaleSpeed = 0.7f;
                    UpdateTeamColors();
                    sinCounter = 1f;

                    if (context == AnimationContext.Complete || context == AnimationContext.AlmostComplete || context == AnimationContext.Lockout || context == AnimationContext.Bingo || context == AnimationContext.BingoLast)
                    {
                        float randomVariation = Mathf.Lerp(0.9f, 1.1f, UnityEngine.Random.value);
                        effect = new BorderEffect(container, pos, teamResponsible == 8 ? Color.white : BingoPage.TEAM_COLOR[teamResponsible], size, (context == AnimationContext.BingoLast ? 4f : chCompleted ? 2.8f : 1.7f) * randomVariation);

                        if (owner.hud.owner is Player p && p.room != null)
                        {
                            for (int i = 0; i < p.room.game.cameras.Length; i++)
                            {
                                RoomCamera cam = p.room.game.cameras[i];
                                cam.ScreenMovement(pos, default, (context == AnimationContext.BingoLast ? 0.8f : chCompleted ? 0.5f : 0.1f) * randomVariation);

                                //for (int e = 0; e < Random.Range(20, 41); e++)
                                //{
                                //    p.room.AddObject(new CollectToken.TokenSpark(pos + cam.CamPos(cam.currentCameraPosition) + new Vector2(18f, 18f), Custom.RNV() * (30f + 10f * Random.value), BingoPage.TEAM_COLOR[teamResponsible], false));
                                //}
                                if (teamResponsible != 8 && (!(challenge as BingoChallenge).TeamsCompleted[SteamTest.team] || teamResponsible != SteamTest.team)) continue;
                                for (int e = 0; e < Random.Range(7, 15) + (context == AnimationContext.BingoLast ? 10 : 0); e++)
                                {
                                    p.room.AddObject(new Confetti(pos + cam.CamPos(cam.currentCameraPosition) + new Vector2(18f, 18f), Custom.RNV() * (15f + 10f * Random.value + (context == AnimationContext.BingoLast ? 8f : 0f)), BingoPage.TEAM_COLOR[SteamTest.team], teamResponsible == 8 ? Color.white : BingoPage.TEAM_COLOR[SteamTest.team]));
                                }
                            }
                        }

                        if (Plugin.PluginInstance.BingoConfig.PlayEndingSong.Value && context == AnimationContext.BingoLast && Custom.rainWorld.processManager != null && Custom.rainWorld.processManager.musicPlayer != null)
                        {
                            Custom.rainWorld.processManager.musicPlayer.FadeOutAllSongs(0f);
                            BingoHooks.RequestBingoSong(Custom.rainWorld.processManager.musicPlayer, "Bingo - Blithely Beached");
                        }
                    }
                }
            }

            public class BorderEffect
            {
                public FSprite[] border;
                public Vector2[] corners;
                public Vector2 pos;
                public float size;
                public float lastSize;
                public float alpha;
                public float lastAlpha;
                public float initSize;
                public float maxSizeIncrease;

                public BorderEffect(FContainer container, Vector2 pos, Color color, float initSize, float maxSizeIncrease)
                {
                    this.maxSizeIncrease = maxSizeIncrease;
                    this.initSize = initSize;
                    size = initSize;
                    lastSize = size;
                    alpha = 1f;
                    lastAlpha = alpha;
                    this.pos = pos;

                    border = new FSprite[4];
                    for (int i = 0; i < border.Length; i++)
                    {
                        border[i] = new FSprite("pixel")
                        {
                            scaleX = (i < 2) ? size : 2f,
                            anchorX = (i < 2) ? 0f : 0.5f,
                            scaleY = (i < 2) ? 2f : size,
                            anchorY = (i < 2) ? 0.5f : 0f,
                            color = color
                        };
                        container.AddChild(border[i]);
                    }
                    corners = new Vector2[4];
                    corners[0] = pos + new Vector2(-size / 2f, -size / 2f);
                    corners[1] = pos + new Vector2(-size / 2f, size / 2f);
                    corners[2] = pos + new Vector2(size / 2f, -size / 2f);
                    corners[3] = pos + new Vector2(size / 2f, size / 2f);
                    border[0].SetPosition(corners[0]);
                    border[1].SetPosition(corners[1]);
                    border[2].SetPosition(corners[0]);
                    border[3].SetPosition(corners[2]);
                }

                public void Update()
                {
                    lastAlpha = alpha;
                    alpha = Mathf.Max(0f, alpha - 0.025f);
                    lastSize = size;
                    size = Mathf.Lerp(size, initSize * maxSizeIncrease, 0.33f);
                }

                public void Draw(float timeStacker)
                {
                    float s = Mathf.Lerp(lastSize, size, timeStacker);
                    float a = Mathf.Lerp(lastAlpha, alpha, timeStacker);

                    corners[0] = pos + new Vector2(-s / 2f, -s / 2f);
                    corners[1] = pos + new Vector2(-s / 2f, s / 2f);
                    corners[2] = pos + new Vector2(s / 2f, -s / 2f);
                    corners[3] = pos + new Vector2(s / 2f, s / 2f);

                    for (int i = 0; i < 4; i++)
                    {
                        border[i].alpha = a;
                        border[i].scaleX = (i < 2) ? s : 2f;
                        border[i].scaleY = (i < 2) ? 2f : s;
                    }
                    border[0].SetPosition(corners[0]);
                    border[1].SetPosition(corners[1]);
                    border[2].SetPosition(corners[0]);
                    border[3].SetPosition(corners[2]);
                }

                public void Remove()
                {
                    for (int i = 0; i < 4; i++)
                    {
                        border[i].RemoveFromContainer();
                    }
                }
            }

            public class Confetti : PuffBallSkin
            {
                public Confetti(Vector2 pos, Vector2 vel, Color color, Color color2) : base(pos, vel, color, color2)
                {
                    this.lifeTime = Mathf.Lerp(300f, 500f, UnityEngine.Random.value);
                }

                public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
                {
                    sLeaser.sprites = new FSprite[1];
                    sLeaser.sprites[0] = new FSprite("Cicada" + Random.Range(2, 6).ToString() + "shield", true);
                    sLeaser.sprites[0].scaleX = ((Random.value < 0.5f) ? -1f : 1f) * Mathf.Lerp(0.5f, 1f, Random.value);
                    sLeaser.sprites[0].scaleY = ((Random.value < 0.5f) ? -1f : 1f) * 1.2f;
                    this.AddToContainer(sLeaser, rCam, null);
                }

                public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
                {
                    if (newContatiner == null)
                    {
                        newContatiner = rCam.ReturnFContainer("HUD2");
                    }
                    foreach (FSprite fsprite in sLeaser.sprites)
                    {
                        fsprite.RemoveFromContainer();
                        newContatiner.AddChild(fsprite);
                    }
                }

                public override void Update(bool eu)
                {
                    base.Update(eu);
                    if (this.room.GetTile(this.pos).Solid)
                    {
                        this.life += 0.04f;
                    }
                }
            }
        }
    }
}
