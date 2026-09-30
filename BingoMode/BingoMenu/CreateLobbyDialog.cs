using BingoMode.BingoSteamworks;
using Expedition;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using RWCustom;
using Steamworks;
using System;
using UnityEngine;

namespace BingoMode.BingoMenu
{
    public class CreateLobbyDialog : Dialog, CheckBox.IOwnCheckBox
    {
        private float leftAnchor;
        private bool opening;
        private bool closing;
        private float uAlpha;
        private float currentAlpha;
        private float lastAlpha;
        private float targetAlpha;
        private float num;
        private FSprite pageTitle;
        private SimpleButton closeButton;
        private SimpleButton createButton;
        private CheckBox[] gamemode;
        private CheckBox friendsOnly;
        private CheckBox hostMods;
        private CheckBox[] perks;
        private CheckBox[] burdens;
        private FLabel[] labels;
        private FSprite[] dividers;
        private bool inLobby;
        private PositionedMenuObject owner;
        private MenuTabWrapper menuTabWrapper;
        private ConfigurableBase maxPlayersConf;
        private OpUpdown maxPlayers;
        private UIelementWrapper maxPlayersWrapper;

        public CreateLobbyDialog(ProcessManager manager, PositionedMenuObject owner, bool inLobby = false, bool host = false) : base(manager)
        {
            this.inLobby = inLobby;
            float[] screenOffsets = Custom.GetScreenOffsets();
            leftAnchor = screenOffsets[0];
            Vector2 outOfBounds = new Vector2(10000f, 10000f);
            this.owner = owner;

            pageTitle = new FSprite(inLobby ? "lobbysettings" : "createlobby", true);
            pageTitle.SetAnchor(0.5f, 0.5f);
            pageTitle.x = 683f;
            pageTitle.y = 715f;
            pageTitle.shader = manager.rainWorld.Shaders["MenuText"];
            pages[0].Container.AddChild(pageTitle);

            num = 85f;
            float num2 = LabelTest.GetWidth(Translate("CLOSE"), false) + 10f;
            if (num2 > num)
            {
                num = num2;
            }
            closeButton = new SimpleButton(this, pages[0], Translate("CLOSE"), "CLOSE", outOfBounds, new Vector2(num, 35f));
            pages[0].subObjects.Add(closeButton);
            if (!inLobby)
            {
                createButton = new SimpleButton(this, pages[0], Translate("CREATE"), "CREATE", outOfBounds, new Vector2(num, 35f));
                pages[0].subObjects.Add(createButton);
            }

            friendsOnly = new CheckBox(this, pages[0], this, outOfBounds, 0f, Translate("Friends only: "), "FRIENDS");
            friendsOnly.label.label.alignment = FLabelAlignment.Right;
            friendsOnly.buttonBehav.greyedOut = inLobby && !host;
            pages[0].subObjects.Add(friendsOnly);
            hostMods = new CheckBox(this, pages[0], this, outOfBounds, 0f, Translate("Require host's mods: "), "HOSTMODS");
            hostMods.label.label.alignment = FLabelAlignment.Right;
            hostMods.buttonBehav.greyedOut = inLobby && !host;
            pages[0].subObjects.Add(hostMods);

            menuTabWrapper = new MenuTabWrapper(this, pages[0]);
            pages[0].subObjects.Add(menuTabWrapper);
            maxPlayersConf = MenuModList.ModButton.RainWorldDummy.config.Bind<int>("_LobbyMaxPlayers", 4, new ConfigAcceptableRange<int>(1, 64));
            maxPlayers = new OpUpdown(true, maxPlayersConf, outOfBounds, 50f);
            maxPlayers.OnValueChanged += MaxPlayers_OnValueChanged;
            maxPlayersWrapper = new UIelementWrapper(menuTabWrapper, maxPlayers);
            maxPlayers.greyedOut = inLobby && !host;
            if (inLobby) maxPlayers.valueInt = SteamMatchmaking.GetLobbyMemberLimit(SteamTest.CurrentLobby);

            perks = new CheckBox[3];
            burdens = new CheckBox[3];
            gamemode = new CheckBox[4];
            string[] gamemodes = { Translate("Bingo "),
                                Translate(" Lockout <LINE> (ties)    ").Replace("<LINE>", "\n"),
                                Translate("  Lockout  <LINE> (no ties)  ").Replace("<LINE>", "\n"),
                                Translate("Blackout ").Replace("<LINE>", "\n") };
            string[] texts = { Translate("Allowed "), Translate("Disabled "), Translate("Host decides ") };
            for (int i = 0; i < 3; i++)
            {
                perks[i] = new CheckBox(this, pages[0], this, outOfBounds, 0f, texts[i], "PERJ" + i.ToString());
                perks[i].label.label.alignment = FLabelAlignment.Right;
                pages[0].subObjects.Add(perks[i]);

                burdens[i] = new CheckBox(this, pages[0], this, outOfBounds, 0f, texts[i], "BURJ" + i.ToString());
                burdens[i].label.label.alignment = FLabelAlignment.Right;
                pages[0].subObjects.Add(burdens[i]);

                perks[i].buttonBehav.greyedOut = inLobby && !host;
                burdens[i].buttonBehav.greyedOut = inLobby && !host;
            }
            for (int i = 0; i < 4; i++)
            {
                gamemode[i] = new CheckBox(this, pages[0], this, outOfBounds, 0f, gamemodes[i], "GAMJ" + i.ToString());
                gamemode[i].label.label.alignment = FLabelAlignment.Right;
                pages[0].subObjects.Add(gamemode[i]);
                gamemode[i].buttonBehav.greyedOut = inLobby && !host;
            }

            labels = new FLabel[4];
            for (int i = 0; i < 2; i++)
            {
                labels[i] = new FLabel(Custom.GetFont(), i == 0 ? Translate("Perks:") : Translate("Burdens:")) { anchorX = 0.5f, anchorY = 0.5f, shader = manager.rainWorld.Shaders["MenuText"] };
                pages[0].Container.AddChild(labels[i]);
            }
            labels[2] = new FLabel(Custom.GetFont(), Translate("Max Players: ")) { anchorX = 1f, anchorY = 0.5f, color = MenuColor(MenuColors.MediumGrey).rgb };
            pages[0].Container.AddChild(labels[2]);
            labels[3] = new FLabel(Custom.GetFont(), Translate("Game mode:")) { anchorX = 0.5f, anchorY = 0.5f, shader = manager.rainWorld.Shaders["MenuText"] };
            pages[0].Container.AddChild(labels[3]);

            dividers = new FSprite[2];
            for (int i = 0; i < 2; i++)
            {
                dividers[i] = new FSprite("pixel")
                {
                    scaleY = 2,
                    scaleX = 400,
                };
                pages[0].Container.AddChild(dividers[i]);
            }

            targetAlpha = 1f;
            opening = true;
        }

        private void MaxPlayers_OnValueChanged(UIconfig config, string value, string oldValue)
        {
            if (!inLobby) return;
            int players = maxPlayers.GetValueInt();
            SteamMatchmaking.SetLobbyMemberLimit(SteamTest.CurrentLobby, players);
        }

        public override void Update()
        {
            base.Update();
            lastAlpha = currentAlpha;
            currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, 0.2f);
            if (opening && pages[0].pos.y <= 0.01f)
            {
                opening = false;
            }
            if (closing && Math.Abs(currentAlpha - targetAlpha) < 0.09f)
            {
                pageTitle.RemoveFromContainer();
                foreach (var lable in labels)
                {
                    lable.RemoveFromContainer();
                }
                for (int i = 0; i < 2; i++)
                {
                    dividers[i].RemoveFromContainer();
                }
                menuTabWrapper.wrappers.Remove(maxPlayers);
                menuTabWrapper.subObjects.Remove(maxPlayersWrapper);
                manager.StopSideProcess(this);
                closing = false;
            }
            closeButton.buttonBehav.greyedOut = opening;
            if (!inLobby) createButton.buttonBehav.greyedOut = opening;
        }

        public override void GrafUpdate(float timeStacker)
        {
            base.GrafUpdate(timeStacker);

            if (opening || closing)
            {
                uAlpha = Mathf.Pow(Mathf.Max(0f, Mathf.Lerp(lastAlpha, currentAlpha, timeStacker)), 1.5f);
                darkSprite.alpha = uAlpha * 0.95f;
            }
            pages[0].pos.y = Mathf.Lerp(manager.rainWorld.options.ScreenSize.y + 100f, 0.01f, (uAlpha < 0.999f) ? uAlpha : 1f);

            Vector2 pagePos = Vector2.Lerp(pages[0].lastPos, pages[0].pos, timeStacker);

            pageTitle.SetPosition(new Vector2(683f - leftAnchor, 685f) + pagePos);

            float xPos = 670f;
            float yTop = 528f;

            friendsOnly.pos = new Vector2(xPos, yTop + 30f);
            hostMods.pos = new Vector2(xPos, yTop);
            maxPlayers.pos = new Vector2(xPos, yTop - 35f);

            // I'm sorry nacu it was my constitutional duty to center ts

            string HostDecidesText = Translate("Host decides ");
            string BingoText = Translate("Bingo ");
            // In other languages, can need 2 words to describe this mode
            string BlackoutText = Translate("Blackout ").Replace("<LINE>", "\n");

            for (int i = 0; i < 3; i++)
            {
                perks[i].pos = new Vector2(xPos - 120f + 126f * i, yTop - 150f);
                burdens[i].pos = new Vector2(xPos - 120f + 126f * i, yTop - 200f);
                if (perks[i].displayText == HostDecidesText)
                {
                    perks[i].pos.x += 20f;
                    burdens[i].pos.x += 20f;
                }
            }

            for (int i = 0; i < 4; i++)
            {
                gamemode[i].pos = new Vector2(xPos - 188f + 140f * i, yTop - 100f);
                if (gamemode[i].displayText == BingoText)
                {
                    gamemode[i].pos.x += 17f;
                }
                else if (gamemode[i].displayText == BlackoutText)
                {
                    gamemode[i].pos.x -= 44f;
                }
            }


            labels[0].SetPosition(pagePos + new Vector2(xPos, yTop - 115f));
            labels[1].SetPosition(pagePos + new Vector2(xPos, yTop - 165f)); 
            labels[2].SetPosition(pagePos + new Vector2(xPos, yTop - 19f));
            labels[3].SetPosition(pagePos + new Vector2(xPos, yTop - 65f));


            for (int i = 0; i < 2; i++)
            {
                dividers[i].alpha = darkSprite.alpha;
            }
            dividers[0].SetPosition(pagePos + new Vector2(xPos, yTop - 48f));
            dividers[1].SetPosition(pagePos + new Vector2(xPos, yTop - 215f));

            float xxx = inLobby ? 683f - num / 2f : 683f - num - 10f;
            closeButton.pos = new Vector2(xxx, yTop - 265f);
            if (!inLobby) createButton.pos = new Vector2(683f + 10f, yTop - 265f);
        }

        public override void Singal(MenuObject sender, string message)
        {
            base.Singal(sender, message);
            switch (message)
            {
                case "CLOSE":
                    closing = true;
                    targetAlpha = 0f;
                    if (inLobby) SteamTest.UpdateOnlineSettings();
                    break;
                case "CREATE":
                    SteamTest.CreateLobby(maxPlayers.valueInt);
                    closing = true;
                    targetAlpha = 0f;
                    break;
            }
        }

        public bool GetChecked(CheckBox box)
        {
            if (box.IDString == null) return false;
            if (box.IDString.StartsWith("PERJ"))
            {
                int i = int.Parse(box.IDString.Substring(4), System.Globalization.NumberStyles.Any);
                switch (i)
                {
                    case 0:
                        return BingoData.globalSettings.perks == LobbySettings.AllowUnlocks.Any;
                    case 1:
                        return BingoData.globalSettings.perks == LobbySettings.AllowUnlocks.None;
                    case 2:
                        return BingoData.globalSettings.perks == LobbySettings.AllowUnlocks.Inherited;
                }
                return false;
            }
            if (box.IDString.StartsWith("BURJ"))
            {
                int i = int.Parse(box.IDString.Substring(4), System.Globalization.NumberStyles.Any);
                switch (i)
                {
                    case 0:
                        return BingoData.globalSettings.burdens == LobbySettings.AllowUnlocks.Any;
                    case 1:
                        return BingoData.globalSettings.burdens == LobbySettings.AllowUnlocks.None;
                    case 2:
                        return BingoData.globalSettings.burdens == LobbySettings.AllowUnlocks.Inherited;
                }
                return false;
            }
            if (box.IDString.StartsWith("GAMJ"))
            {
                int i = int.Parse(box.IDString.Substring(4), System.Globalization.NumberStyles.Any);
                switch (i)
                {
                    case 0:
                        return BingoData.globalSettings.gamemode == BingoData.BingoGameMode.Bingo;
                    case 1:
                        return BingoData.globalSettings.gamemode == BingoData.BingoGameMode.Lockout;
                    case 2:
                        return BingoData.globalSettings.gamemode == BingoData.BingoGameMode.LockoutNoTies;
                    case 3:
                        return BingoData.globalSettings.gamemode == BingoData.BingoGameMode.Blackout;
                }
                return false;
            }
            switch (box.IDString)
            {
                case "FRIENDS":
                    return BingoData.globalSettings.friendsOnly;
                case "HOSTMODS":
                    return BingoData.globalSettings.hostMods;
            }
            return false;
        }

        public void SetChecked(CheckBox box, bool c)
        {
            if (box.IDString == null) return;
            if (box.IDString.StartsWith("PERJ"))
            {
                int i = int.Parse(box.IDString.Substring(4), System.Globalization.NumberStyles.Any);
                switch (i)
                {
                    case 0:
                        BingoData.globalSettings.perks = LobbySettings.AllowUnlocks.Any;
                        break;
                    case 1:
                        BingoData.globalSettings.perks = LobbySettings.AllowUnlocks.None;
                        break;
                    case 2:
                        BingoData.globalSettings.perks = LobbySettings.AllowUnlocks.Inherited;
                        break;
                }
            }
            if (box.IDString.StartsWith("BURJ"))
            {
                int i = int.Parse(box.IDString.Substring(4), System.Globalization.NumberStyles.Any);
                switch (i)
                {
                    case 0:
                        BingoData.globalSettings.burdens = LobbySettings.AllowUnlocks.Any;
                        break;
                    case 1:
                        BingoData.globalSettings.burdens = LobbySettings.AllowUnlocks.None;
                        break;
                    case 2:
                        BingoData.globalSettings.burdens = LobbySettings.AllowUnlocks.Inherited;
                        break;
                }
            }
            if (box.IDString.StartsWith("GAMJ"))
            {
                int i = int.Parse(box.IDString.Substring(4), System.Globalization.NumberStyles.Any);
                switch (i)
                {
                    case 0:
                        BingoData.globalSettings.gamemode = BingoData.BingoGameMode.Bingo;
                        break;
                    case 1:
                        BingoData.globalSettings.gamemode = BingoData.BingoGameMode.Lockout;
                        break;
                    case 2:
                        BingoData.globalSettings.gamemode = BingoData.BingoGameMode.LockoutNoTies;
                        break;
                    case 3:
                        BingoData.globalSettings.gamemode = BingoData.BingoGameMode.Blackout;
                        break;
                }
            }
            switch (box.IDString)
            {
                case "FRIENDS":
                    BingoData.globalSettings.friendsOnly = c;
                    break;
                case "HOSTMODS":
                    BingoData.globalSettings.hostMods = c;
                    break;
            }
        }
    }
}
