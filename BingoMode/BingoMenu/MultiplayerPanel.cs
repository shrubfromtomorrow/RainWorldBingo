using BingoMode.BingoRandomizer;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using Rewired.ControllerExtensions;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using UnityEngine;
using static BingoMode.BingoSteamworks.LobbySettings;

namespace BingoMode.BingoMenu
{
    internal class MultiplayerPanel : PositionedMenuObject
    {
        private const float MARGIN = 7f;
        private const float TEXT_BUTTON_HEIGHT = 25f; // only partially changes stuff, change not recommended
        private const float BIG_TEXT_HEIGHT = 30f; // not actually modifiable from here
        private const float SYMBOL_BUTTON_SIZE = 35f;
        private const float REFRESH_SEARCH_WIDTH = 60f;
        private const float FRIENDS_WIDTH = 110f;
        private const float DIVIDER_WIDTH = 2f;
        private const float HEADER_HEIGHT = 2f * MARGIN + SYMBOL_BUTTON_SIZE + DIVIDER_WIDTH;

        private const float LOBBY_HEIGHT = 20f;
        private const float LOBBY_SPACING = 5f;
        private const float PLAYER_HEIGHT = LOBBY_HEIGHT;
        private const float PLAYER_SPACING = LOBBY_SPACING;
        private const float SLIDER_WIDTH = 10f;
        private const float SLIDER_OFFSET = 15f; // not actually modifiable from here
        private const float SLIDER_DEAD_ZONE = 21f; // not actually modifiable from here
        private const float DIV_X_OFFSET = 2f;

        private bool inLobby = false;

        private RoundedRect background;

        private MenuTabWrapper tabWrapper;
        private UIelementWrapper nameFilterWrapper;
        private OpTextBox nameFilter;
        private SimpleButton refreshSearch;
        private SimpleButton friendsOnly;
        private SymbolButton createLobby;

        private MenuLabel lobbyName;
        private SymbolButton lobbySettings;

        private FSprite divider;
        private VerticalSlider slider;

        private List<LobbyInfo> foundLobbies = [];
        public List<PlayerInfo> lobbyPlayers = [];
        private List<FSprite> lobbyDividers = [];

        private bool _visible = true;
        
        public Vector2 size;
        public float sliderF;
        public bool Visible
        {
            get => _visible;
            set
            {
                if (value == _visible)
                    return;

                if (!value)
                {
                    RemoveLobbiesSprites();
                    RemovePlayersSprites();
                }

                _visible = value;
            }
        }
        public bool InLobby { get => inLobby; }

        public MultiplayerPanel(Menu.Menu menu, MenuObject owner, Vector2 pos, Vector2 size) : base(menu, owner, pos)
        {
            this.size = size;

            background = new RoundedRect(menu, this, Vector2.zero, size, true);
            subObjects.Add(background);

            tabWrapper = new(menu, this);
            subObjects.Add(tabWrapper);

            ConstructSearchHeader();

            divider = new("pixel")
            {
                scaleX = size.x,
                scaleY = DIVIDER_WIDTH,
                anchorX = 0f,
                anchorY = 0f,
                x = pos.x,
                y = pos.y + size.y - HEADER_HEIGHT
            };
            Container.AddChild(divider);

            slider = new(
                    menu,
                    this,
                    "",
                    new Vector2(size.x - MARGIN - SLIDER_OFFSET - SLIDER_WIDTH / 2f, MARGIN),
                    new Vector2(SLIDER_WIDTH, size.y - HEADER_HEIGHT - 2 * MARGIN - SLIDER_DEAD_ZONE),
                    BingoEnums.MultiplayerSlider,
                    true);
            sliderF = 1f;
            subObjects.Add(slider);
        }

        public override void GrafUpdate(float timeStacker)
        {
            base.GrafUpdate(timeStacker);

            divider.SetPosition(DrawPos(timeStacker) + new Vector2(0f, size.y - HEADER_HEIGHT));

            if (!inLobby)
                DrawDisplayedLobbies(timeStacker);
            else
                DrawPlayerInfo(timeStacker);
        }

        public override void Singal(MenuObject sender, string message)
        {
            if (message == "REFRESH_SEARCH")
            {
                RemoveLobbiesSprites();
                SteamTest.GetJoinableLobbies();
                return;
            }

            if (message == "TOGGLE_FRIENDSONLY")
            {
                SteamTest.CurrentFilters.friendsOnly = !SteamTest.CurrentFilters.friendsOnly;
                friendsOnly.menuLabel.text = SteamTest.CurrentFilters.friendsOnly ? menu.Translate("Friends only: Yes") : menu.Translate("Friends only: No");
                return;
            }

            if (message == "CREATE_LOBBY")
            {
                menu.manager.ShowDialog(new CreateLobbyDialog(menu.manager, this));
                return;
            }

            if (message == "CHANGE_SETTINGS")
            {
                menu.manager.ShowDialog(new CreateLobbyDialog(menu.manager, this, true, true));
                return;
            }

            if (message == "INFO_SETTINGS")
            {
                menu.manager.ShowDialog(new CreateLobbyDialog(menu.manager, this, true, false));
                return;
            }

            if (message.StartsWith("JOIN-"))
            {
                if (SteamTest.CurrentLobby != default)
                    return;

                (sender as SimpleButton).buttonBehav.greyedOut = true;

                if (ulong.TryParse(message.Split('-')[1], out ulong lobbyID))
                {
                    CSteamID lobby = new(lobbyID);
                    if (!SteamTest.JoinLobby(lobby, menu.manager, out string errMsg, out bool tryReconnect))
                    {
                        if (tryReconnect)
                            menu.manager.ShowDialog(new InfoDialog(menu.manager, errMsg, lobby));
                        else
                            menu.manager.ShowDialog(new InfoDialog(menu.manager, errMsg));
                    }
                }
                else
                {
                    Plugin.logger.LogError("FAILED TO PARSE LOBBY ULONG FROM " + message);
                    (sender as SimpleButton).buttonBehav.greyedOut = false;
                }
                return;
            }

            if (message.StartsWith("KICK-"))
            {
                ulong playerId = ulong.Parse(message.Split('-')[1], System.Globalization.NumberStyles.Any);
                SteamNetworkingIdentity kickedPlayer = new SteamNetworkingIdentity();
                kickedPlayer.SetSteamID64(playerId);
                InnerWorkings.SendMessage("@", kickedPlayer);
                return;
            }
            
            if (message == "FOCUS_DD")
            {
                foreach (PlayerInfo player in lobbyPlayers)
                {
                    if (player != sender)
                    {
                        player.DropDownEnabled = false;
                    }
                }
                return;
            }

            if (message == "UNFOCUS_DD")
            {
                foreach (PlayerInfo player in lobbyPlayers)
                {
                    if (player != sender)
                        player.DropDownEnabled = true;
                }
                return;
            }
            
            if (message.StartsWith("SWTEAM-"))
            {
                string[] data = message.Split('-');
                ulong playerId = ulong.Parse(data[1], System.Globalization.NumberStyles.Any);
                int playerTeam = int.Parse(data[2], System.Globalization.NumberStyles.Any);
                if (playerId == SteamTest.selfIdentity.GetSteamID64())
                {
                    SteamTest.team = playerTeam;
                    SteamMatchmaking.SetLobbyMemberData(SteamTest.CurrentLobby, "playerTeam", playerTeam.ToString());
                    return;
                }
                SteamNetworkingIdentity playerIdentity = new();
                playerIdentity.SetSteamID64(playerId);
                InnerWorkings.SendMessage("%" + playerTeam, playerIdentity);
                return;
            }

            base.Singal(sender, message);
        }

        public override void RemoveSprites()
        {
            base.RemoveSprites();
            foreach (MenuObject obj in subObjects)
            {
                obj.RemoveSprites();
                RecursiveRemoveSelectables(obj);
            }
            subObjects.Clear();
            divider.RemoveFromContainer();
            foreach (FSprite divider in lobbyDividers)
                divider.RemoveFromContainer();
        }

        public void SwitchToLobby(bool create)
        {
            if (inLobby)
                return;
            inLobby = true;
            if (BingoData.globalSettings.perks == AllowUnlocks.None)
                ExpeditionGame.activeUnlocks.RemoveAll(x => x.StartsWith("unl-"));
            if (BingoData.globalSettings.burdens == AllowUnlocks.None)
                ExpeditionGame.activeUnlocks.RemoveAll(x => x.StartsWith("bur-"));

            DestructSearchHeader();
            RemoveLobbiesSprites();

            ConstructLobbyHeader();
            CreateLobbyPlayers();
        }

        public void SwitchToSearch()
        {
            if (!inLobby)
                return;
            inLobby = false;
            DestructLobbyHeader();
            RemovePlayersSprites();

            ConstructSearchHeader();
            CreateDisplayedLobbies();
        }

        /// <summary>
        /// Refresh lobbies list with the given list of steam IDs.
        /// </summary>
        /// <param name="lobbies">A list of steam IDs from which to fetch lobby data.</param>
        public void AddLobbies(List<CSteamID> lobbies)
        {
            RemoveLobbiesSprites();
            Vector2 lobbySize = new(size.x - 3f * MARGIN - SLIDER_WIDTH, LOBBY_HEIGHT);
            foreach (var lobby in lobbies)
            {
                try
                {
                    LobbyData data = SteamTest.GetLobbyData(lobby);
                    foundLobbies.Add(new LobbyInfo(menu, this, Vector2.zero, lobbySize, data));
                }
                catch (Exception e)
                {
                    Plugin.logger.LogError("Failed to get lobby info from lobby " + lobby + ". Exception:\n" + e);
                }
            }
            CreateDisplayedLobbies();
        }

        public void UpdateLobbyHost(bool isHost)
        {
            lobbySettings.UpdateSymbol(isHost ? "settingscog" : "Menu_InfoI");
            lobbySettings.signalText = isHost ? "CHANGE_SETTINGS" : "INFO_SETTINGS";
        }

        public void ResetPlayerLobby()
        {
            RemovePlayersSprites();
            CreateLobbyPlayers();
        }

        private void ConstructSearchHeader()
        {
            float smallItemY = size.y - MARGIN - SYMBOL_BUTTON_SIZE + (SYMBOL_BUTTON_SIZE - TEXT_BUTTON_HEIGHT) / 2f;

            Configurable<string> nameFilterConf = MenuModList.ModButton.RainWorldDummy.config.Bind("_NameFilterBingo", "", (ConfigAcceptableBase)null);
            nameFilter = new(
                    nameFilterConf,
                    new Vector2(MARGIN, smallItemY),
                    size.x - 5 * MARGIN - REFRESH_SEARCH_WIDTH - FRIENDS_WIDTH - SYMBOL_BUTTON_SIZE)
            { allowSpace = true };
            nameFilter.OnValueUpdate += NameFilter_OnValueUpdate;
            nameFilterWrapper = new(tabWrapper, nameFilter);

            refreshSearch = new(
                    menu,
                    this,
                    menu.Translate("Refresh"),
                    "REFRESH_SEARCH",
                    new Vector2(size.x - 3f * MARGIN - REFRESH_SEARCH_WIDTH - FRIENDS_WIDTH - SYMBOL_BUTTON_SIZE, smallItemY),
                    new Vector2(REFRESH_SEARCH_WIDTH, TEXT_BUTTON_HEIGHT));
            subObjects.Add(refreshSearch);

            friendsOnly = new(
                    menu,
                    this,
                    menu.Translate("Friends only: No"),
                    "TOGGLE_FRIENDSONLY",
                    new Vector2(size.x - 2f * MARGIN - FRIENDS_WIDTH - SYMBOL_BUTTON_SIZE, smallItemY),
                    new Vector2(FRIENDS_WIDTH, TEXT_BUTTON_HEIGHT));
            subObjects.Add(friendsOnly);

            createLobby = new(
                    menu,
                    this,
                    "plus",
                    "CREATE_LOBBY",
                    new Vector2(size.x - MARGIN - SYMBOL_BUTTON_SIZE, size.y - MARGIN - SYMBOL_BUTTON_SIZE))
            { size = Vector2.one * SYMBOL_BUTTON_SIZE };
            createLobby.roundedRect.size = createLobby.size;
            createLobby.symbolSprite.scale = 0.9f;
            subObjects.Add(createLobby);

        }

        private void DestructSearchHeader()
        {
            nameFilter.OnValueUpdate -= NameFilter_OnValueUpdate;
            nameFilter.Unload();
            nameFilterWrapper.RemoveSprites();
            RecursiveRemoveSelectables(nameFilterWrapper);
            nameFilterWrapper = null;

            refreshSearch.RemoveSprites();
            RecursiveRemoveSelectables(refreshSearch);
            subObjects.Remove(refreshSearch);
            refreshSearch = null;

            friendsOnly.RemoveSprites();
            RecursiveRemoveSelectables(friendsOnly);
            subObjects.Remove(friendsOnly);
            friendsOnly = null;

            createLobby.RemoveSprites();
            RecursiveRemoveSelectables(createLobby);
            subObjects.Remove(createLobby);
            createLobby = null;
        }

        private void ConstructLobbyHeader()
        {
            lobbyName = new MenuLabel(
                    menu,
                    this,
                    Expedition.ChallengeTools.IGT.Translate("<hostName>'s lobby").Replace("<hostName>", SteamMatchmaking.GetLobbyData(SteamTest.CurrentLobby, "name")),
                    new Vector2(MARGIN, size.y - MARGIN - BIG_TEXT_HEIGHT),
                    new Vector2(size.x - 3f * MARGIN - SYMBOL_BUTTON_SIZE, BIG_TEXT_HEIGHT),
                    true);
            subObjects.Add(lobbyName);

            bool isHost = SteamMatchmaking.GetLobbyOwner(SteamTest.CurrentLobby) == SteamTest.selfIdentity.GetSteamID();
            lobbySettings = new(
                    menu,
                    this,
                    isHost ? "settingscog" : "Menu_InfoI",
                    isHost ? "CHANGE_SETTINGS" : "INFO_SETTINGS",
                    new Vector2(size.x - MARGIN - SYMBOL_BUTTON_SIZE, size.y - MARGIN - SYMBOL_BUTTON_SIZE))
            { size = Vector2.one * SYMBOL_BUTTON_SIZE };
            lobbySettings.roundedRect.size = lobbySettings.size;
            lobbySettings.symbolSprite.scale = 0.9f;
            subObjects.Add(lobbySettings);
        }

        private void DestructLobbyHeader()
        {
            lobbyName.RemoveSprites();
            RecursiveRemoveSelectables(lobbyName);
            subObjects.Remove(lobbyName);
            lobbyName = null;

            lobbySettings.RemoveSprites();
            RecursiveRemoveSelectables(lobbySettings);
            subObjects.Remove(lobbySettings);
            lobbySettings = null;
        }

        private void NameFilter_OnValueUpdate(UIconfig config, string value, string oldValue)
        {
            SteamTest.CurrentFilters.text = value;
            SteamTest.GetJoinableLobbies();
        }

        private void CreateDisplayedLobbies()
        {
            foreach (LobbyInfo lobby in foundLobbies)
                subObjects.Add(lobby);
            for (int i = 0; i < foundLobbies.Count - 1; i++)
            {
                FSprite divider = new("pixel")
                {
                    scaleX = size.x - 3f * MARGIN - SLIDER_WIDTH - 2f * DIV_X_OFFSET,
                    scaleY = 1f,
                    anchorX = 0f
                };
                lobbyDividers.Add(divider);
                Container.AddChild(divider);
            }
    }

        private void RemoveLobbiesSprites()
        {
            foreach (LobbyInfo lobby in foundLobbies)
            {
                lobby.RemoveSprites();
                subObjects.Remove(lobby);
            }
            foundLobbies.Clear();

            foreach (FSprite divider in lobbyDividers)
                divider.RemoveFromContainer();
            lobbyDividers.Clear();
        }

        private void CreateLobbyPlayers()
        {
            bool isHost = SteamMatchmaking.GetLobbyOwner(SteamTest.CurrentLobby) == SteamTest.selfIdentity.GetSteamID();
            Vector2 playerSize = new(size.x - 3f * MARGIN - SLIDER_WIDTH, PLAYER_HEIGHT);

            BingoData.MultiplayerGame = true;
            List<PlayerData> players = SteamTest.GetPlayersData();

            bool allReady = true;
            // Build the list backwards because z-indexing doesn't exist in these lands. yippee...
            players.Reverse();
            foreach (PlayerData player in players)
            {
                PlayerInfo info = new(menu, this, Vector2.zero, playerSize, isHost, player);
                lobbyPlayers.Insert(0, info);
                subObjects.Add(info);
                allReady &= player.ready || player.isHost;
            }

            if (lobbyPlayers.Count == 0)
                Plugin.logger.LogMessage("No people in the lobby");

            for (int i = 0; i < lobbyPlayers.Count - 1; i++)
            {
                FSprite divider = new("LinearGradient200")
                {
                    rotation = 90f,
                    anchorY = 0f,
                    scaleY = 1.5f
                };
                lobbyDividers.Add(divider);
                Container.AddChild(divider);
            }
            if (isHost)
                Singal(this, allReady ? "ALL_READY" : "NALL_READY");
        }

        private void RemovePlayersSprites()
        {
            foreach (PlayerInfo player in lobbyPlayers)
            {
                player.RemoveSprites();
                RecursiveRemoveSelectables(player);
                subObjects.Remove(player);
            }
            lobbyPlayers.Clear();

            foreach (FSprite divider in lobbyDividers)
                divider.RemoveFromContainer();
            lobbyDividers.Clear();
        }

        private void DrawDisplayedLobbies(float timeStacker)
        {
            // top and bottom require a slight offset, otherwise dividers land in-between pixels and disappear. Yes, that is stupid.
            float top = size.y - HEADER_HEIGHT - MARGIN - LOBBY_HEIGHT + 0.01f;
            float bottom = MARGIN - 0.01f;
            float middle = bottom + (top - bottom) / 2f;
            float list_height = (foundLobbies.Count - 1) * (LOBBY_HEIGHT + LOBBY_SPACING);
            float y = (list_height > top - bottom) ? Mathf.Lerp(list_height + bottom, top, sliderF) : top;

            foreach (LobbyInfo lobby in foundLobbies)
            {
                float overshoot = (y > middle) ? (y - top) / MARGIN : (bottom - y) / MARGIN;
                lobby.Alpha = Mathf.Lerp(1f, 0f, overshoot);
                lobby.pos = new Vector2(MARGIN, y);
                y -= LOBBY_HEIGHT + LOBBY_SPACING;
            }

            y = (list_height > top - bottom) ? Mathf.Lerp(list_height + bottom, top, sliderF) : top;
            foreach (FSprite divider in lobbyDividers)
            {
                float overshoot = (y > middle) ? (y - top) / MARGIN : (bottom - y + LOBBY_HEIGHT + LOBBY_SPACING) / MARGIN;
                divider.alpha = Mathf.Lerp(1f, 0f, overshoot);
                divider.SetPosition(DrawPos(timeStacker) + new Vector2(MARGIN + DIV_X_OFFSET, y - LOBBY_SPACING / 2f));
                y -= LOBBY_HEIGHT + LOBBY_SPACING;
            }
        }

        private void DrawPlayerInfo(float timeStacker)
        {
            // top and bottom require a slight offset, otherwise dividers land in-between pixels and disappear. Yes, that is stupid.
            float top = size.y - HEADER_HEIGHT - MARGIN - PLAYER_HEIGHT + 0.01f;
            float bottom = MARGIN - 0.01f;
            float middle = bottom + (top - bottom) / 2f;
            float list_height = (lobbyPlayers.Count - 1) * (PLAYER_HEIGHT + PLAYER_SPACING);
            float y = (list_height > top - bottom) ? Mathf.Lerp(list_height + bottom, top, sliderF) : top;
            foreach (PlayerInfo player in lobbyPlayers)
            {
                float overshoot = (y > middle) ? (y - top) / MARGIN : (bottom - y) / MARGIN;
                player.Alpha = Mathf.Lerp(1f, 0f, overshoot);
                player.pos = new Vector2(MARGIN, y);
                y -= PLAYER_HEIGHT + PLAYER_SPACING;
            }

            y = (list_height > top - bottom) ? Mathf.Lerp(list_height + bottom, top, sliderF) : top;
            foreach (FSprite divider in lobbyDividers)
            {
                float overshoot = (y > middle) ? (y - top) / MARGIN : (bottom - y + PLAYER_HEIGHT + PLAYER_SPACING) / MARGIN;
                divider.alpha = Mathf.Lerp(1f, 0f, overshoot);
                divider.SetPosition(DrawPos(timeStacker) + new Vector2(MARGIN + DIV_X_OFFSET, y - PLAYER_SPACING / 2f));
                y -= PLAYER_HEIGHT + PLAYER_SPACING;
            }
        }
    }
}
