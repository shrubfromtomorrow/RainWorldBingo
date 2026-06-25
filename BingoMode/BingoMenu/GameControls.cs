using BepInEx;
using BingoMode.BingoSteamworks;
using Expedition;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static BingoMode.BingoSteamworks.LobbySettings;

namespace BingoMode.BingoMenu
{
    internal class GameControls : PositionedMenuObject
    {
        // if you make changes, WIDTH and HEIGHT formulas *might* need to change to reflect that.
        private const float WIDTH = 2f * RESIZE_BUTTON_SIZE + 2f * MARGIN + UNLOCKS_BUTTON_WIDTH;
        private const float HEIGHT = NALL_READY_Y + TEXTBOX_HEIGHT;
        private const float MARGIN = 5f;

        private const float NALL_READY_Y = START_Y + HOLD_BUTTON_RADIUS * 2f + MARGIN;

        private const float START_Y = SHELTER_Y + TEXTBOX_HEIGHT + MARGIN;
        private const float HOLD_BUTTON_RADIUS = 157f / 2f; // not modifiable from here; radius of the biggest circle when focused
        private const float ALL_READY_FILL_TIME = 40f;
        private const float NALL_READY_FILL_TIME = 200f;

        private const float SHELTER_Y = UNLOCKS_Y + UNLOCKS_BUTTON_HEIGHT + MARGIN;
        private const float TEXTBOX_HEIGHT = 25f; // not modifiable from here

        private const float UNLOCKS_Y = COPY_PASTE_Y + COPY_PASTE_HEIGHT + MARGIN;
        private const float RESIZE_Y = UNLOCKS_Y + (UNLOCKS_BUTTON_HEIGHT - RESIZE_BUTTON_SIZE) / 2f;
        private const float RESIZE_BUTTON_SIZE = 40f;
        private const float UNLOCKS_BUTTON_WIDTH = 150f;
        private const float UNLOCKS_BUTTON_HEIGHT = 50f;

        private const float COPY_PASTE_Y = 0f;
        private const float COPY_PASTE_WDITH = 80f;
        private const float COPY_PASTE_HEIGHT = 20f;

        private MenuTabWrapper tabWrapper;
        private MenuLabel nallReady;
        private HoldButton startGame;
        private MenuLabel shelterLabel;
        private OpTextBox shelterSetting;
        private UIelementWrapper shelterSettingWrapper;
        private OpHoldButton unlocksButton;
        private UIelementWrapper unlockWrapper;
        private SymbolButton minusButton;
        private SymbolButton plusButton;
        private SimpleButton copyBoard;
        private SimpleButton pasteBoard;

        private float anchorX;
        public float AnchorX
        {
            get => anchorX;
            set
            {
                anchorX = value;
                Vector2 offset = new(Mathf.Lerp(0f, -WIDTH, anchorX), Mathf.Lerp(0f, -HEIGHT, anchorY));
                startGame.pos = offset + new Vector2(WIDTH / 2f, START_Y + HOLD_BUTTON_RADIUS);
                shelterLabel.pos = offset + new Vector2(RESIZE_BUTTON_SIZE + MARGIN, SHELTER_Y + TEXTBOX_HEIGHT / 2f);
                shelterSetting.pos = offset + new Vector2(RESIZE_BUTTON_SIZE + MARGIN + shelterLabel.label.textRect.width, SHELTER_Y);
                unlocksButton.pos = offset + new Vector2(RESIZE_BUTTON_SIZE + MARGIN, UNLOCKS_Y);
                minusButton.pos = offset + new Vector2(0f, RESIZE_Y);
                plusButton.pos = offset + new Vector2(RESIZE_BUTTON_SIZE + 2f * MARGIN + UNLOCKS_BUTTON_WIDTH, RESIZE_Y);
                copyBoard.pos = offset + new Vector2((WIDTH - MARGIN) / 2f - COPY_PASTE_WDITH, 0f);
                pasteBoard.pos = offset + new Vector2((WIDTH + MARGIN) / 2f, 0f);
            }
        }
        private float anchorY;
        public float AnchorY
        {
            get => anchorY;
            set
            {
                anchorY = value;
                Vector2 offset = new(Mathf.Lerp(0f, -WIDTH, anchorX), Mathf.Lerp(0f, -HEIGHT, anchorY));
                startGame.pos = offset + new Vector2(WIDTH / 2f, START_Y + HOLD_BUTTON_RADIUS);
                shelterLabel.pos = offset + new Vector2(RESIZE_BUTTON_SIZE + MARGIN, SHELTER_Y + TEXTBOX_HEIGHT / 2f);
                shelterSetting.pos = offset + new Vector2(RESIZE_BUTTON_SIZE + MARGIN + shelterLabel.label.textRect.width, SHELTER_Y);
                unlocksButton.pos = offset + new Vector2(RESIZE_BUTTON_SIZE + MARGIN, UNLOCKS_Y);
                minusButton.pos = offset + new Vector2(0f, RESIZE_Y);
                plusButton.pos = offset + new Vector2(RESIZE_BUTTON_SIZE + 2f * MARGIN + UNLOCKS_BUTTON_WIDTH, RESIZE_Y);
                copyBoard.pos = offset + new Vector2((WIDTH - MARGIN) / 2f - COPY_PASTE_WDITH, COPY_PASTE_Y);
                pasteBoard.pos = offset + new Vector2((WIDTH + MARGIN) / 2f, COPY_PASTE_Y);
            }
        }
        public bool HostPrivilege
        {
            set
            {
                shelterSetting.greyedOut = !value;
                shelterSetting.label.alpha = !value ? 0f : 1f;
                plusButton.buttonBehav.greyedOut = !value;
                minusButton.buttonBehav.greyedOut = !value;
                pasteBoard.buttonBehav.greyedOut = !value;
                copyBoard.buttonBehav.greyedOut = !value;
                startGame.signalText = value ? "STARTBINGO" : "GETREADY";
                startGame.menuLabel.text = value ? menu.Translate("BEGIN") : menu.Translate("I'M<LINE>READY").Replace("<LINE>", "\r\n");
            }
        }
        private bool _allReady = true;
        public bool AllReady
        {
            get => _allReady;
            set
            {
                _allReady = value;
                startGame.fillTime = value ? ALL_READY_FILL_TIME : NALL_READY_FILL_TIME;
                nallReady.label.alpha = 0f;
                nallReady.label.color = Color.white;
            }
        }

        public string Shelter
        {
            get => shelterSetting.value; set => shelterSetting.value = value;
        }

        public GameControls(Menu.Menu menu, MenuObject owner, Vector2 pos, float anchorX = 0f, float anchorY = 0f) : base(menu, owner, pos)
        {
            this.anchorX = anchorX;
            this.anchorY = anchorY;
            Vector2 offset = new(Mathf.Lerp(0f, -WIDTH, anchorX), Mathf.Lerp(0f, -HEIGHT, anchorY));

            tabWrapper = new MenuTabWrapper(menu, this);
            subObjects.Add(tabWrapper);

            nallReady = new(
                    menu,
                    this,
                    menu.Translate("Not all players are ready !"),
                    new(WIDTH / 2f, NALL_READY_Y),
                    Vector2.zero,
                    false);
            nallReady.label.alpha = 0f;
            subObjects.Add(nallReady);

            startGame = new(
                    menu,
                    this,
                    menu.Translate("BEGIN"),
                    "STARTBINGO",
                    offset + new Vector2(WIDTH / 2f, START_Y + HOLD_BUTTON_RADIUS),
                    ALL_READY_FILL_TIME);
            subObjects.Add(startGame);

            shelterLabel = new MenuLabel(
                    menu,
                    this,
                    menu.Translate("Shelter: "),
                    offset + new Vector2(RESIZE_BUTTON_SIZE + MARGIN, SHELTER_Y + TEXTBOX_HEIGHT / 2f),
                    Vector2.zero,
                    false);
            shelterLabel.label.alignment = FLabelAlignment.Left;
            subObjects.Add(shelterLabel);
            
            Configurable<string> shelterSettingConf = MenuModList.ModButton.RainWorldDummy.config.Bind("_ShelterSettingBingo", "_", (ConfigAcceptableBase)null);
            shelterSetting = new(
                    shelterSettingConf,
                    offset + new Vector2(RESIZE_BUTTON_SIZE + MARGIN + shelterLabel.label.textRect.width, SHELTER_Y),
                    UNLOCKS_BUTTON_WIDTH - shelterLabel.label.textRect.width)
            {
                alignment = FLabelAlignment.Center,
                description = menu.Translate("The shelter players start in. Please type in a valid shelter's room name (CASE SENSITIVE), or 'random'"),
                maxLength = 100,
            };
            shelterSetting.OnValueUpdate += ShelterSetting_OnValueUpdate;
            shelterSettingWrapper = new UIelementWrapper(tabWrapper, shelterSetting);
            shelterSetting.value = "random";

            unlocksButton = new(
                    offset + new Vector2(RESIZE_BUTTON_SIZE + MARGIN, UNLOCKS_Y),
                    new Vector2(UNLOCKS_BUTTON_WIDTH, UNLOCKS_BUTTON_HEIGHT),
                    menu.Translate("CONFIGURE<LINE>PERKS & BURDENS").Replace("<LINE>", "\r\n"),
                    20f)
            { description = " " };
            unlocksButton.OnPressDone += UnlocksButton_OnPressDone;
            unlockWrapper = new UIelementWrapper(tabWrapper, unlocksButton);

            Vector2 resizeButtonSize = new(RESIZE_BUTTON_SIZE, RESIZE_BUTTON_SIZE);
            minusButton = new(
                    menu,
                    this,
                    "minus",
                    "REMOVESIZE",
                    offset + new Vector2(0f, RESIZE_Y))
            { size = resizeButtonSize };
            minusButton.roundedRect.size = resizeButtonSize;
            subObjects.Add(minusButton);

            plusButton = new(
                    menu,
                    this,
                    "plus",
                    "ADDSIZE",
                    offset + new Vector2(RESIZE_BUTTON_SIZE + 2f * MARGIN + UNLOCKS_BUTTON_WIDTH, RESIZE_Y))
            { size = resizeButtonSize };
            plusButton.roundedRect.size = resizeButtonSize;
            subObjects.Add(plusButton);

            copyBoard = new(
                    menu,
                    this,
                    menu.Translate("Copy board"),
                    "COPYTOCLIPBOARD",
                    offset + new Vector2((WIDTH - MARGIN) / 2f - COPY_PASTE_WDITH, COPY_PASTE_Y),
                    new Vector2(COPY_PASTE_WDITH, COPY_PASTE_HEIGHT));
            subObjects.Add(copyBoard);

            pasteBoard = new(
                    menu,
                    this,
                    menu.Translate("Paste board"),
                    "PASTEFROMCLIPBOARD",
                    offset + new Vector2((WIDTH + MARGIN) / 2f, COPY_PASTE_Y),
                    new Vector2(COPY_PASTE_WDITH, COPY_PASTE_HEIGHT));
            subObjects.Add(pasteBoard);

        }

        public override void Singal(MenuObject sender, string message)
        {
            if (message == "GETREADY")
            {
                SteamMatchmaking.SetLobbyMemberData(SteamTest.CurrentLobby, "ready", "1");
                startGame.signalText = "GETUNREADY";
                startGame.menuLabel.text = menu.Translate("I'M NOT<LINE>READY").Replace("<LINE>", "\r\n");
                menu.PlaySound(SoundID.MENU_Start_New_Game);
            }

            if (message == "GETUNREADY")
            {
                SteamMatchmaking.SetLobbyMemberData(SteamTest.CurrentLobby, "ready", "0");
                startGame.signalText = "GETREADY";
                startGame.menuLabel.text = menu.Translate("I'M<LINE>READY").Replace("<LINE>", "\r\n");
                menu.PlaySound(SoundID.MENU_Start_New_Game);
            }

            if (message == "ADDSIZE")
            {
                if (BingoHooks.GlobalBoard.size < 9)
                {
                    BingoHooks.GlobalBoard.GenerateBoard(++BingoHooks.GlobalBoard.size, true);
                    menu.PlaySound(SoundID.MENU_Next_Slugcat);
                }
                return;
            }

            if (message == "REMOVESIZE")
            {
                if (BingoHooks.GlobalBoard.size > 1)
                {
                    BingoHooks.GlobalBoard.GenerateBoard(--BingoHooks.GlobalBoard.size, true);
                    menu.PlaySound(SoundID.MENU_Next_Slugcat);
                }
                return;
            }

            if (message == "COPYTOCLIPBOARD")
            {
                UniClipboard.SetText(BingoHooks.GlobalBoard.ToString());
                menu.PlaySound(SoundID.MENU_Next_Slugcat);
                return;
            }

            if (message == "PASTEFROMCLIPBOARD")
            {
                bool success = BingoHooks.GlobalBoard.FromString(UniClipboard.GetText());
                if (success) menu.PlaySound(SoundID.SS_AI_Give_The_Mark_Boom);
                else menu.PlaySound(SoundID.Snail_Pop);
                SteamTest.UpdateOnlineBingo();
                return;
            }

            base.Singal(sender, message);
        }

        public override void GrafUpdate(float timeStacker)
        {
            base.GrafUpdate(timeStacker);

            if (!_allReady)
            {
                nallReady.label.alpha = startGame.filled * 5f;
                nallReady.label.color = Color.Lerp(Color.white, Color.red, startGame.filled);
            }
        }

        public override void RemoveSprites()
        {
            base.RemoveSprites();

            unlocksButton.Unload();
            shelterSetting.Unload();
            shelterSetting.OnValueUpdate -= ShelterSetting_OnValueUpdate;
            tabWrapper.wrappers.Remove(unlocksButton);
            tabWrapper.wrappers.Remove(shelterSetting);
            tabWrapper.subObjects.Remove(unlockWrapper);
            tabWrapper.subObjects.Remove(shelterSettingWrapper);
            foreach (MenuObject obj in subObjects)
            {
                obj.RemoveSprites();
                RecursiveRemoveSelectables(obj);
            }
            subObjects.Clear();
        }

        private void UnlocksButton_OnPressDone(UIfocusable trigger)
        {
            UnlockDialog unlockDialog = new(menu.manager, (menu as ExpeditionMenu).challengeSelect);
            unlocksButton.greyedOut = true;
            unlocksButton.Reset();
            if (BingoData.MultiplayerGame)
            {
                bool isHost = SteamMatchmaking.GetLobbyOwner(SteamTest.CurrentLobby) == SteamTest.selfIdentity.GetSteamID();
                foreach (var perk in unlockDialog.perkButtons)
                {
                    perk.buttonBehav.greyedOut = perk.buttonBehav.greyedOut || BingoData.globalSettings.perks == AllowUnlocks.None || (BingoData.globalSettings.perks == AllowUnlocks.Inherited && !isHost);
                }
                foreach (var burden in unlockDialog.burdenButtons)
                {
                    burden.buttonBehav.greyedOut = burden.buttonBehav.greyedOut || BingoData.globalSettings.burdens == AllowUnlocks.None || (BingoData.globalSettings.burdens == AllowUnlocks.Inherited && !isHost);
                }
            }
            string[] bannedBurdens = ["bur-doomed"];
            string[] bannedPerks = ["unl-passage", "unl-karma"];
            foreach (var bur in unlockDialog.burdenButtons)
            {
                if (bannedBurdens.Contains(bur.signalText))
                {
                    bur.buttonBehav.greyedOut = true;
                    if (ExpeditionGame.activeUnlocks.Contains(bur.signalText)) unlockDialog.ToggleBurden(bur.signalText);
                }
            }
            foreach (var per in unlockDialog.perkButtons)
            {
                if (bannedPerks.Contains(per.signalText))
                {
                    per.buttonBehav.greyedOut = true;
                    if (ExpeditionGame.activeUnlocks.Contains(per.signalText)) unlockDialog.ToggleBurden(per.signalText);
                }
            }
            menu.manager.ShowDialog(unlockDialog);
        }

        public void UnlocksDialogClose()
        {
            unlocksButton.greyedOut = false;
            unlocksButton.Reset();
        }

        private void ShelterSetting_OnValueUpdate(UIconfig config, string value, string oldValue) =>
            BingoData.BingoDen = value.IsNullOrWhiteSpace() ? "random" : value;
    }
}
