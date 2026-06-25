using BingoMode.BingoChallenges;
using Expedition;
using Menu;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Watcher;
using RWCustom;
using static BingoMode.BingoMenu.BingoMenuObjects;

namespace BingoMode.BingoMenu
{
    public class BoardControls : PositionedMenuObject
    {
        private const float WIDTH = 3f * BUTTON_SIZE + 2f * MARGIN;
        private const float HEIGHT = BUTTON_SIZE;
        private const float MARGIN = 5f;
        private const float BUTTON_SIZE = 30f;
        private const float BIG_BUTTON_SIZE = 70f;

        private SymbolButton filter;
        private SymbolButton randomize;
        private SymbolButton shuffle;
        public BingoSymbolButton watcherMode;

        private float anchorX;
        public float AnchorX
        {
            get => anchorX;
            set
            {
                anchorX = value;
                Vector2 offset = new(Mathf.Lerp(0f, -WIDTH, anchorX), Mathf.Lerp(0f, -HEIGHT, anchorY));
                filter.pos = offset;
                randomize.pos = offset + new Vector2(BUTTON_SIZE + MARGIN, 0f);
                shuffle.pos = offset + new Vector2(2f * BUTTON_SIZE + 2f * MARGIN, 0f);
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
                filter.pos = offset;
                randomize.pos = offset + new Vector2(BUTTON_SIZE + MARGIN, 0f);
                shuffle.pos = offset + new Vector2(2f * BUTTON_SIZE + 2f * MARGIN, 0f);
            }
        }
        public bool HostPrivilege
        {
            set
            {
                filter.buttonBehav.greyedOut = !value;
                randomize.buttonBehav.greyedOut = !value;
                shuffle.buttonBehav.greyedOut = !value;
                if (ModManager.Watcher)
                {
                    watcherMode.buttonBehav.greyedOut = !value;
                }
            }
        }

        public BoardControls(Menu.Menu menu, MenuObject owner, Vector2 pos, float anchorX = 0f, float anchorY = 0f) : base(menu, owner, pos)
        {
            this.anchorX = anchorX;
            this.anchorY = anchorY;
            Vector2 offset = new(Mathf.Lerp(0f, -WIDTH, anchorX), Mathf.Lerp(0f, -HEIGHT, anchorY));
            Vector2 size = Vector2.one * BUTTON_SIZE;
            Vector2 bigSize = Vector2.one * BIG_BUTTON_SIZE;

            filter = new(
                    menu,
                    this,
                    "filter",
                    "FILTER",
                    offset)
            { size = size };
            filter.symbolSprite.scale = 0.8f;
            filter.roundedRect.size = size;
            subObjects.Add(filter);

            randomize = new(
                    menu,
                    this,
                    "Sandbox_Randomize",
                    "RANDOMIZE",
                    offset + new Vector2(BUTTON_SIZE + MARGIN, 0f))
            { size = size };
            randomize.roundedRect.size = size;
            subObjects.Add(randomize);

            shuffle = new(
                    menu,
                    this,
                    "Menu_Symbol_Shuffle",
                    "SHUFFLE",
                    offset + new Vector2(2f * BUTTON_SIZE + 2f * MARGIN, 0f))
            { size = size };
            shuffle.roundedRect.size = size;
            subObjects.Add(shuffle);

            if (ModManager.Watcher)
            {
                watcherMode = new(
                    menu,
                    this,
                    "ripple5.0",
                    "WATCHERMODE",
                    offset - new Vector2(7.5f * BUTTON_SIZE + 7.5f * MARGIN, 1.2f * BUTTON_SIZE))
                { size = bigSize };
                watcherMode.roundedRect.size = bigSize;
                subObjects.Add(watcherMode);
            }
        }

        public override void Singal(MenuObject sender, string message)
        {
            if (message == "RANDOMIZE")
            {
                BingoHooks.GlobalBoard.GenerateBoard(BingoHooks.GlobalBoard.size, false);
                menu.PlaySound(SoundID.MENU_Next_Slugcat);
                return;
            }

            if (message == "SHUFFLE")
            {
                BingoHooks.GlobalBoard.ShuffleBoard();
                menu.PlaySound(SoundID.MENU_Next_Slugcat);
                return;
            }

            if (message == "FILTER")
            {
                menu.manager.ShowDialog(new FilterDialog(menu.manager));
                menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
                return;
            }

            base.Singal(sender, message);

            // majority of behavior is the same as ExpeditionMenu_Singal hook watcherbingohooks.cs and WatcherModeUIUpdate in BingoPage.cs
            if (message == "WATCHERMODE")
            {
                BingoHooks.GlobalBoard.GenerateBoard(BingoHooks.GlobalBoard.size, false);
                return;
            }
        }
    }
}
