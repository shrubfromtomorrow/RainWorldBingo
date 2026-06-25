using BingoMode.BingoChallenges;
using Expedition;
using Watcher;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using RWCustom;
using Steamworks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BingoMode.BingoMenu
{
    using BingoSteamworks;
    using System;
    using static BingoMode.BingoSteamworks.LobbySettings;

    public class BingoPage : PositionedMenuObject
    {
        public ExpeditionMenu expMenu;

        #region Title
        private const float TITLE_MARGIN = 36f;
        private const float TITLE_WIDTH = 164f; // source : atlases/bingomode.txt
        private const float TITLE_HEIGHT = 52f; // source : atlases/bingomode.txt
        private const float BACK_BUTTON_SIZE = 45f;
        private const float TITLE_SPRITE_ALIGN = 5f;

        private FAtlasElement normalTitle;
        private FAtlasElement watcherTitle;

        private MenuLabel nowPlaying;
        private MenuLabel tutorial;

        private FSprite title;
        private SymbolButton back;
        #endregion

        public BingoBoard board;
        public BingoGrid grid;
        private BoardControls boardControls;
        private GameControls gameControls;
        public string Shelter
        {
            get => gameControls.Shelter; set => gameControls.Shelter = value;
        }

        public SymbolButton eggButton;

        #region Multiplayer
        private const float MULTIPLAYER_PANEL_WIDTH = 380f;
        private const float MULTIPLAYER_PANEL_HEIGHT = 600f;

        public SimpleButton multiplayerButton;
        private MultiplayerPanel multiplayerPanel;
        public float multiplayerSlideIn;
        public float multiplayerSlideStep;
        public bool InLobby { get => multiplayerPanel.InLobby; }
        #endregion

        #region Randomizer
        private const float RANDOMIZER_PANEL_WIDTH = 190f;
        private const float RANDOMIZER_PANEL_HEIGHT = 300f;

        private SimpleButton randomizerButton;
        private RandomizerPanel randomizerPanel;
        private float randomizerSlideIn;
        private float randomizerSlideStep;
        #endregion

        public static readonly float desaturara = 0.1f;
        public static readonly Color[] TEAM_COLOR =
        {
            Custom.Saturate(new Color(0.9019608f, 0.05490196f, 0.05490196f), desaturara), // Red
            Custom.Saturate(new Color(0f, 0.5f, 1f), desaturara), // Blue
            Custom.Saturate(new Color(0.2f, 1f, 0f), desaturara), // Green
            Custom.Saturate(new Color(1f, 0.6f, 0f), desaturara), // Orange
            Custom.Saturate(new Color(1f, 0f, 1f), desaturara), // Pink
            Custom.Saturate(new Color(0f, 0.9098039f, 0.9019608f), desaturara), // Cyan
            Custom.Saturate(new Color(0.36862746f, 0.36862746f, 0.43529412f), desaturara), // Black
            Custom.Saturate(new Color(0.3f, 0f, 1f), desaturara), // Hurricane
            Custom.Saturate(Color.grey, desaturara), // Spectator
        };
        public static readonly string[] TeamName =
        [
            "Red",
            "Blue",
            "Green",
            "Orange",
            "Pink",
            "Cyan",
            "Black",
            "Hurricane",
            "Board view",
        ];
        public static readonly Dictionary<string, int> TeamNumber = new()
        {
            { "Red", 0 },
            { "Blue", 1 },
            { "Green", 2 },
            { "Orange", 3 },
            { "Pink", 4 },
            { "Cyan", 5 },
            { "Black", 6 },
            { "Hurricane", 7 },
            { "Board view", 8 },
        };

        public BingoPage(Menu.Menu menu, MenuObject owner, Vector2 pos) : base(menu, owner, pos)
        {
            expMenu = menu as ExpeditionMenu;
            board = BingoHooks.GlobalBoard;
            BingoData.BingoMode = false;
            BingoData.TeamsInBingo = [0];
            Vector2 topCenter = new(menu.manager.rainWorld.screenSize.x / 2f, menu.manager.rainWorld.screenSize.y - TITLE_MARGIN);

            normalTitle = Futile.atlasManager.GetElementWithName("bingotitle");
            watcherTitle = Futile.atlasManager.GetElementWithName("bingotitlewatcher");

            nowPlaying = new MenuLabel(menu, owner, expMenu.characterSelect.nowPlaying.label.text, new Vector2(683f, 40f), default(Vector2), true, null);
            nowPlaying.label.color = new Color(0.5f, 0.5f, 0.5f);
            nowPlaying.label.shader = menu.manager.rainWorld.Shaders["MenuTextCustom"];
            subObjects.Add(nowPlaying);

            tutorial = new MenuLabel(menu, owner, Plugin.PluginInstance.BingoConfig.Tutorials.Value ? menu.Translate("Click a square to customize it!") : "", topCenter - new Vector2(0, 100f), default(Vector2), true, null);
            tutorial.label.color = new Color(0.85f, 0.85f, 0.85f);
            tutorial.label.shader = menu.manager.rainWorld.Shaders["MenuTextCustom"];
            subObjects.Add(tutorial);

            title = new(normalTitle)
            {
                anchorX = 0.5f,
                anchorY = 1f,
                x = topCenter.x,
                y = topCenter.y,
                shader = menu.manager.rainWorld.Shaders["MenuText"]
            };
            Container.AddChild(title);

            back = new(
                    menu,
                    this,
                    "Big_Menu_Arrow",
                    "GOBACK",
                    topCenter + new Vector2(TITLE_WIDTH / 2f + TITLE_MARGIN, -TITLE_HEIGHT + TITLE_SPRITE_ALIGN))
            { size = Vector2.one * BACK_BUTTON_SIZE };
            back.symbolSprite.rotation = 90f;
            back.roundedRect.size = Vector2.one * BACK_BUTTON_SIZE;
            subObjects.Add(back);

            boardControls = new(
                    menu,
                    this,
                    topCenter + new Vector2(-TITLE_WIDTH / 2f - TITLE_MARGIN, -TITLE_HEIGHT / 2f),
                    1f,
                    0.5f);
            subObjects.Add(boardControls);

            gameControls = new(
                    menu,
                    this,
                    new Vector2(menu.manager.rainWorld.screenSize.x * 0.79f - 45f, 60f));
            subObjects.Add(gameControls);

            multiplayerButton = new SimpleButton(
                    menu,
                    this,
                    expMenu.Translate("Multiplayer"),
                    "SWITCH_MULTIPLAYER",
                    expMenu.exitButton.pos + new Vector2(0f, -40f),
                    new Vector2(140f, 30f));
            subObjects.Add(multiplayerButton);
            multiplayerPanel = new(menu, this, Vector2.zero, new Vector2(MULTIPLAYER_PANEL_WIDTH, MULTIPLAYER_PANEL_HEIGHT));
            subObjects.Add(multiplayerPanel);

            randomizerButton = new(
                    menu,
                    this,
                    expMenu.Translate("Profiles"),
                    "SWITCH_RANDOMIZATION",
                    expMenu.manualButton.pos + new Vector2(0, -40f), expMenu.manualButton.size);
            subObjects.Add(randomizerButton);
            randomizerPanel = new(menu, this, Vector2.zero, new Vector2(RANDOMIZER_PANEL_WIDTH, RANDOMIZER_PANEL_HEIGHT));
            subObjects.Add(randomizerPanel);

            if (ExpeditionData.ints.Sum() >= 8)
            {
                eggButton = new SymbolButton(menu, this, "GuidanceSlugcat", "EGGBUTTON", new Vector2(1313f, 11f));
                eggButton.roundedRect.size = new Vector2(40f, 40f);
                eggButton.size = eggButton.roundedRect.size;
                subObjects.Add(eggButton);
            }
        }

        public void UpdateLobbyHost(bool isHost)
        {
            gameControls.HostPrivilege = isHost;
            boardControls.HostPrivilege = isHost;
            grid.Switch(!isHost);

            multiplayerPanel.UpdateLobbyHost(isHost);
        }

        public void Switch(bool toInLobby, bool create) // (nintendo reference
        {
            if (InLobby == toInLobby)
                return;

            if (toInLobby)
                multiplayerPanel.SwitchToLobby(create);
            else
                multiplayerPanel.SwitchToSearch();
            if (toInLobby)
            {
                if (BingoData.globalSettings.perks == AllowUnlocks.None)
                    ExpeditionGame.activeUnlocks.RemoveAll(x => x.StartsWith("unl-"));
                if (BingoData.globalSettings.burdens == AllowUnlocks.None)
                    ExpeditionGame.activeUnlocks.RemoveAll(x => x.StartsWith("bur-"));

                expMenu.exitButton.buttonBehav.greyedOut = true;
                back.buttonBehav.greyedOut = true;
                gameControls.HostPrivilege = create;
                boardControls.HostPrivilege = create;
                expMenu.manualButton.buttonBehav.greyedOut = true;
                multiplayerButton.menuLabel.text = expMenu.Translate("Leave Lobby");
                multiplayerButton.signalText = "LEAVE_LOBBY";
                grid.Switch(!create);
                return;
            }

            ExpeditionGame.activeUnlocks.RemoveAll(x => x.StartsWith("unl-"));
            ExpeditionGame.activeUnlocks.RemoveAll(x => x.StartsWith("bur-"));

            expMenu.exitButton.buttonBehav.greyedOut = false;
            back.buttonBehav.greyedOut = false;
            gameControls.HostPrivilege = true;
            boardControls.HostPrivilege = true;
            expMenu.manualButton.buttonBehav.greyedOut = false;
            multiplayerButton.menuLabel.text = expMenu.Translate("Multiplayer");
            multiplayerButton.signalText = "SWITCH_MULTIPLAYER";
            grid.Switch(false);
        }

        public void UnlocksDialogClose() => gameControls.UnlocksDialogClose();

        public static string ExpeditionRandomStartsUnlocked(RainWorld rainWorld, SlugcatStats.Name slug)
        {
            Dictionary<string, int> dictionary = new Dictionary<string, int>();
            Dictionary<string, List<string>> dictionary2 = new Dictionary<string, List<string>>();
            List<string> list2 = SlugcatStats.SlugcatStoryRegions(slug);
            if (File.Exists(AssetManager.ResolveFilePath("bingorandomstarts.txt")))
            {
                string[] array = File.ReadAllLines(AssetManager.ResolveFilePath("bingorandomstarts.txt"));
                for (int i = 0; i < array.Length; i++)
                {
                    if (!array[i].StartsWith("//") && array[i].Length > 0)
                    {
                        string text = Regex.Split(array[i], "_")[0];
                        if (!(ExpeditionGame.lastRandomRegion == text))
                        {
                            if (!dictionary2.ContainsKey(text))
                            {
                                dictionary2.Add(text, new List<string>());
                            }
                            if (list2.Contains(text))
                            {
                                dictionary2[text].Add(array[i]);
                            }
                            else if (ModManager.MSC && (slug == SlugcatStats.Name.White || slug == SlugcatStats.Name.Yellow))
                            {
                                if (text == "OE")
                                {
                                    dictionary2[text].Add(array[i]);
                                }
                                if (text == "LC")
                                {
                                    dictionary2[text].Add(array[i]);
                                }
                                if (text == "MS" && array[i] != "MS_S07")
                                {
                                    dictionary2[text].Add(array[i]);
                                }
                            }
                            if (dictionary2[text].Contains(array[i]) && !dictionary.ContainsKey(text))
                            {
                                dictionary.Add(text, ExpeditionGame.GetRegionWeight(text));
                            }
                        }
                    }
                }
                System.Random random = new System.Random();
                int maxValue = dictionary.Values.Sum();
                int randomIndex = random.Next(0, maxValue);
                string key = dictionary.First(delegate (KeyValuePair<string, int> x)
                {
                    randomIndex -= x.Value;
                    return randomIndex < 0;
                }).Key;
                ExpeditionGame.lastRandomRegion = key;
                int num = (from list in dictionary2.Values
                           select list.Count).Sum();
                string text2 = dictionary2[key].ElementAt(UnityEngine.Random.Range(0, dictionary2[key].Count - 1));
                ExpLog.Log(string.Format("{0} | {1} valid regions for {2} with {3} possible dens", new object[]
                {
            text2,
            dictionary.Keys.Count,
            slug.value,
            num
                }));
                return text2;
            }
            return slug == WatcherEnums.SlugcatStatsName.Watcher ? "WSKB_S06" : "SU_S01";
        }

        public override void GrafUpdate(float timeStacker)
        {
            base.GrafUpdate(timeStacker);

            nowPlaying.text = expMenu.characterSelect.nowPlaying.label.text;

            if (title.element == watcherTitle && ExpeditionData.slugcatPlayer != Watcher.WatcherEnums.SlugcatStatsName.Watcher)
            {
                title.element = normalTitle;
                title.shader = Custom.rainWorld.Shaders["MenuText"];
            }
            if (title.element == normalTitle && ExpeditionData.slugcatPlayer == Watcher.WatcherEnums.SlugcatStatsName.Watcher)
            {
                title.element = watcherTitle;
                title.shader = Custom.rainWorld.Shaders["Basic"];
            }

            title.SetPosition(DrawPos(timeStacker) + new Vector2(menu.manager.rainWorld.screenSize.x / 2f, menu.manager.rainWorld.screenSize.y - TITLE_MARGIN));

            if (eggButton != null && expMenu.challengeSelect != null)
            {
                int num = ExpeditionGame.ExIndex(ExpeditionData.slugcatPlayer);
                if (num > -1)
                {
                    eggButton.symbolSprite.color = ((ExpeditionData.ints[num] == 2) ? new HSLColor(Mathf.Sin(expMenu.challengeSelect.colorCounter / 20f), 1f, 0.75f).rgb : new Color(0.3f, 0.3f, 0.3f));
                }
            }


            MultiplayerSlide(timeStacker);
            RandomizerSlide(timeStacker);
        }

        public override void Singal(MenuObject sender, string message)
        {
            base.Singal(sender, message);

            if (message == "GOBACK")
            {
                multiplayerSlideStep = -1f;
                randomizerSlideStep = -1f;
                expMenu.manualButton.signalText = "MANUAL";
                expMenu.manualButton.menuLabel.text = expMenu.Translate("MANUAL");
                expMenu.UpdatePage(1);
                expMenu.MovePage(new Vector2(-1500f, 0f));
                if (Plugin.PluginInstance.BingoConfig.PlayMenuSong.Value && expMenu.manager.musicPlayer != null) expMenu.manager.musicPlayer.FadeOutAllSongs(50f);
                return;
            }

            if (message == "STARTBINGO")
            {
                if (menu.manager.dialog != null) menu.manager.StopSideProcess(menu.manager.dialog);

                if (SteamTest.team == 8)
                {
                    BingoData.TeamsInBingo = [];
                    SpectatorHooks.Hook();
                }
                else
                    BingoData.TeamsInBingo = [SteamTest.team];

                List<PlayerData> players = SteamTest.GetPlayersData();
                foreach (PlayerData player in players)
                    if (!BingoData.TeamsInBingo.Contains(player.team) && player.team != 8)
                        BingoData.TeamsInBingo.Add(player.team);

                if (ModManager.JollyCoop && ModManager.CoopAvailable)
                {
                    for (int i = 1; i < menu.manager.rainWorld.options.JollyPlayerCount; i++)
                    {
                        menu.manager.rainWorld.RequestPlayerSignIn(i, null);
                    }
                    for (int j = menu.manager.rainWorld.options.JollyPlayerCount; j < 4; j++)
                    {
                        menu.manager.rainWorld.DeactivatePlayer(j);
                    }
                }
                menu.manager.arenaSitting = null;
                menu.manager.rainWorld.progression.currentSaveState = null;
                menu.manager.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat = ExpeditionData.slugcatPlayer;
                menu.manager.rainWorld.progression.WipeSaveState(ExpeditionData.slugcatPlayer);

                BingoData.InitializeBingo();
                BingoData.RedoTokens();

                List<string> bannedRegions = [];
                foreach (var ch in ExpeditionData.challengeList)
                {
                    if (ch is BingoNoRegionChallenge r) bannedRegions.Add(r.region.Value);
                    if (ch is BingoAllRegionsExceptChallenge g) bannedRegions.Add(g.region.Value);
                    if (ch is BingoEnterRegionChallenge b) bannedRegions.Add(b.region.Value);
                    if (ch is BingoEnterRegionFromChallenge a) bannedRegions.Add(a.to.Value);

                    // watcher touches this
                    if (ch is WatcherBingoEnterRegionChallenge c) bannedRegions.Add(c.region.Value);
                    if (ch is WatcherBingoNoRegionChallenge d) bannedRegions.Add(d.region.Value);
                    if (ch is WatcherBingoAllRegionsExceptChallenge e) bannedRegions.Add(e.region.Value);
                }
                if (BingoData.BingoDen.ToLowerInvariant() == "random")
                {
                    int tries = 0;
                reset:
                    ExpeditionData.startingDen = ExpeditionRandomStartsUnlocked(menu.manager.rainWorld, ExpeditionData.slugcatPlayer);
                    BingoData.BingoDen = ExpeditionData.startingDen;

                    if (bannedRegions.Count > 0)
                    {
                        foreach (var banned in bannedRegions)
                        {
                            
                            if (bannedRegions.Count == ChallengeUtils.GetCorrectListForChallenge("regionsreal", true).Length)
                            {
                                BingoData.BingoDen = "SU_S01";
                                ExpeditionData.startingDen = "SU_S01";
                            }
                            else if (ExpeditionData.startingDen.Substring(0, ExpeditionData.slugcatPlayer == Watcher.WatcherEnums.SlugcatStatsName.Watcher ? 4 : 2).ToLowerInvariant() == banned.ToLowerInvariant())
                            {
                                tries++;
                                goto reset;
                            }
                            if (banned == null || banned == "") continue;
                        }
                    }
                }
                else ExpeditionData.startingDen = BingoData.BingoDen;

                if (SteamTest.team == 8)
                {
                    ExpeditionData.startingDen = "SU_S01";
                }

                foreach (var kvp in menu.manager.rainWorld.progression.mapDiscoveryTextures)
                {
                    menu.manager.rainWorld.progression.mapDiscoveryTextures[kvp.Key] = null;
                }

                ExpeditionGame.PrepareExpedition();
                ExpeditionData.AddExpeditionRequirements(ExpeditionData.slugcatPlayer, false);
                ExpeditionData.earnedPassages = 1;
                bool isHost = false;
                SteamFinal.SendUpKeepCounter = SteamFinal.PlayerUpkeepTime;
                SteamFinal.HostUpkeep = SteamFinal.MaxHostUpKeepTime;
                SteamFinal.ReconnectTimer = SteamFinal.TryToReconnectTime;
                SteamFinal.UpkeepCounter = SteamFinal.MaxUpkeepCounter;
                if (BingoData.MultiplayerGame)
                {
                    string connectedPlayers = "";

                    SteamNetworkingIdentity hostIdentity = new SteamNetworkingIdentity();
                    hostIdentity.SetSteamID(SteamMatchmaking.GetLobbyOwner(SteamTest.CurrentLobby));
                    isHost = hostIdentity.GetSteamID() == SteamTest.selfIdentity.GetSteamID();

                    if (isHost)
                    {
                        SteamFinal.ConnectedPlayers.Clear();
                        SteamFinal.ReceivedPlayerUpKeep = [];
                        foreach (var player in players)
                        {
                            if (player.identity.GetSteamID64() == SteamTest.selfIdentity.GetSteamID64())
                                continue;
                            connectedPlayers += "bPlR" + player.identity.GetSteamID64();
                            SteamFinal.ConnectedPlayers.Add(player.identity);
                            SteamFinal.ReceivedPlayerUpKeep[player.identity.GetSteamID64()] = false;
                            SteamFinal.SendUpKeepCounter = SteamFinal.PlayerUpkeepTime;
                        }
                        if (connectedPlayers.StartsWith("bPlR"))
                            connectedPlayers = connectedPlayers.Substring(4);
                    }
                    else if (!isHost)
                    {
                        SteamFinal.ReceivedHostUpKeep = true;
                        SteamFinal.HostUpkeep = SteamFinal.MaxHostUpKeepTime;
                        InnerWorkings.SendMessage("C" + SteamTest.selfIdentity.GetSteamID64(), hostIdentity);
                    }

                    BingoData.BingoSaves[ExpeditionData.slugcatPlayer] = new(BingoHooks.GlobalBoard.size, SteamTest.team, hostIdentity, isHost, connectedPlayers, BingoData.globalSettings.gamemode, false, false, false, BingoData.TeamsListToString(BingoData.TeamsInBingo), false);
                    BingoData.RandomStartingSeed = int.Parse(SteamMatchmaking.GetLobbyData(SteamTest.CurrentLobby, "randomSeed"), System.Globalization.NumberStyles.Any);
                }
                else
                {
                    int newTeam = TeamNumber[Plugin.PluginInstance.BingoConfig.SinglePlayerTeam.Value];

                    BingoData.BingoSaves[ExpeditionData.slugcatPlayer] = new(BingoHooks.GlobalBoard.size, false, newTeam, false, false);
                    SteamTest.team = newTeam;
                }
                Expedition.Expedition.coreFile.Save(false);
                menu.manager.menuSetup.startGameCondition = ProcessManager.MenuSetup.StoryGameInitCondition.New;
                menu.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.Game, 0.1f);
                menu.manager.rainWorld.progression.WipeSaveState(ExpeditionData.slugcatPlayer);
                menu.PlaySound(SoundID.MENU_Start_New_Game);
                if (BingoData.MultiplayerGame && isHost)
                {
                    SteamMatchmaking.SetLobbyData(SteamTest.CurrentLobby, "startGame", BingoData.BingoDen);
                    SteamMatchmaking.SetLobbyJoinable(SteamTest.CurrentLobby, false);
                }
                return;
            }

            if (message == "LEAVE_LOBBY")
            {
                SteamTest.LeaveLobby();
                SteamTest.GetJoinableLobbies();
                gameControls.AllReady = true;
                return;
            }

            if (message == "SWITCH_MULTIPLAYER")
            {
                if (multiplayerSlideStep == 0f)
                    multiplayerSlideStep = 1f;
                else
                    multiplayerSlideStep = -multiplayerSlideStep;
                float ff = multiplayerSlideStep == 1f ? 1f : 0f;
                if (multiplayerSlideStep == 1f)
                    SteamTest.GetJoinableLobbies();
                return;
            }

            if (message == "ALL_READY")
            {
                gameControls.AllReady = true;
                return;
            }

            if (message == "NALL_READY")
            {
                gameControls.AllReady = false;
                return;
            }

            if (message == "EGGBUTTON")
            {
                menu.PlaySound(SoundID.MENU_Player_Join_Game);
                if (ExpeditionGame.ExIndex(ExpeditionData.slugcatPlayer) > -1)
                {
                    if (ExpeditionData.ints[ExpeditionGame.ExIndex(ExpeditionData.slugcatPlayer)] == 1)
                    {
                        ExpeditionData.ints[ExpeditionGame.ExIndex(ExpeditionData.slugcatPlayer)] = 2;
                        return;
                    }
                    ExpeditionData.ints[ExpeditionGame.ExIndex(ExpeditionData.slugcatPlayer)] = 1;
                }
                return;
            }

            if (message == "SWITCH_RANDOMIZATION")
            {
                if (randomizerSlideStep == 0f) randomizerSlideStep = 1f;
                else randomizerSlideStep = -randomizerSlideStep;
                float ff = randomizerSlideStep == 1f ? 1f : 0f;
                return;
            }

            if (sender is BingoButton)
            {
                if (tutorial.text != "")
                {
                    tutorial.text = "";
                }
            }
        }

        public override void RemoveSprites()
        {
            base.RemoveSprites();

            title.RemoveFromContainer();
            foreach (MenuObject obj in subObjects)
            {
                obj.RemoveSprites();
                RecursiveRemoveSelectables(obj);
            }
            subObjects.Clear();
        }

        public void ResetPlayerLobby() => multiplayerPanel.ResetPlayerLobby();

        private void MultiplayerSlide(float timeStacker)
        {
            const float DIST_TO_EDGE = MULTIPLAYER_PANEL_WIDTH + 50f;
            const float OFFSET_TO_BUTTON_X = -25f; // left of button to left of panel
            const float OFFSET_TO_BUTTON_Y = -25f; // bottom of button to top of panel
            multiplayerSlideIn = Mathf.Clamp01(multiplayerSlideIn + multiplayerSlideStep * 0.05f);
            multiplayerPanel.pos =
                    multiplayerButton.pos
                    + new Vector2(OFFSET_TO_BUTTON_X, OFFSET_TO_BUTTON_Y - MULTIPLAYER_PANEL_HEIGHT)
                    + Vector2.left * (1f - Custom.LerpExpEaseInOut(0f, 1f, multiplayerSlideIn)) * DIST_TO_EDGE;
            multiplayerPanel.Visible = multiplayerSlideIn >= 0.01f;
        }

        private void RandomizerSlide(float timeStacker)
        {
            const float DIST_TO_EDGE = RANDOMIZER_PANEL_WIDTH + 50f;
            const float OFFSET_TO_BUTTON_X = 25f; // right of button to right of panel
            const float OFFSET_TO_BUTTON_Y = -25f; // bottom of button to top of panel
            randomizerSlideIn = Mathf.Clamp01(randomizerSlideIn + randomizerSlideStep * 0.05f);
            randomizerPanel.pos =
                    randomizerButton.pos
                    + new Vector2(randomizerButton.size.x + OFFSET_TO_BUTTON_X - RANDOMIZER_PANEL_WIDTH, OFFSET_TO_BUTTON_Y - RANDOMIZER_PANEL_HEIGHT)
                    + Vector2.right * (1f - Custom.LerpExpEaseInOut(0f, 1f, randomizerSlideIn)) * DIST_TO_EDGE;
            randomizerPanel.Visible = randomizerSlideIn >= 0.01f;
        }

        public void AddLobbies(List<CSteamID> lobbies) => multiplayerPanel.AddLobbies(lobbies);

        public void SliderSetValue(Slider slider, float f)
        {
            if (slider.ID == BingoEnums.MultiplayerSlider)
                multiplayerPanel.sliderF = f;
            else if (slider.ID == BingoEnums.RandomizerSlider)
                randomizerPanel.sliderF = f;
        }

        public float ValueOfSlider(Slider slider)
        {
            if (slider.ID == BingoEnums.MultiplayerSlider)
                return multiplayerPanel.sliderF;
            if (slider.ID == BingoEnums.RandomizerSlider)
                return randomizerPanel.sliderF;
            return 0f;
        }
    }
}
