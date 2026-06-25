using BingoMode.BingoSteamworks;
using Menu;
using Menu.Remix.MixedUI;
using RWCustom;
using Steamworks;
using UnityEngine;

namespace BingoMode.BingoMenu
{
    public class InfoDialog : Dialog
    {
        float num;
        SimpleButton closeButton;
        SimpleButton confirmButton;
        FLabel infoText;
        int wait; // they dont love you like i love you
        CSteamID? lobbyID;

        // Extremely hardcoded info thing hi
        public InfoDialog(ProcessManager manager, string message, CSteamID lobbyID) : this(manager, message)
        {
            this.lobbyID = lobbyID;
            float yTop = 578f;

            confirmButton = new SimpleButton(this, pages[0], Translate("JOIN"), "CONFIRMJOIN", new Vector2(683f - num / 2f + 50f, yTop - 305f), new Vector2(num, 35f));
            confirmButton.buttonBehav.greyedOut = true;
            pages[0].subObjects.Add(confirmButton);

            float num2 = LabelTest.GetWidth(Translate("CANCEL")) + 10f;
            if (num2 > num)
            {
                num = num2;
            }
            closeButton.menuLabel.text = Translate("CANCEL");
            closeButton.pos = new Vector2(683f - num / 2f - 50f, yTop - 305f);
            closeButton.size = new Vector2(num, 35f);
        }

        public InfoDialog(ProcessManager manager, string message) : base(manager)
        {
            num = 85f;
            float num2 = LabelTest.GetWidth(message == "Trying to reconnect to the host." ? Translate("CANCEL") : Translate("CLOSE"), false) + 10f;
            if (num2 > num)
            {
                num = num2;
            }
            darkSprite.alpha = 0.95f;

            float yTop = 578f;

            closeButton = new SimpleButton(this, pages[0], message == "Trying to reconnect to the host." ? Translate("CANCEL") : Translate("CLOSE"), message == "Trying to reconnect to the host." ? "STOPRECONNECT" : message == "Cannot reconnect to host." ? "QUITGAEM" : "CLOSE", new Vector2(683f - num / 2f, yTop - 305f), new Vector2(num, 35f));
            closeButton.buttonBehav.greyedOut = true;
            pages[0].subObjects.Add(closeButton);
            wait = 80;
            infoText = new FLabel(Custom.GetFont(), message)
            {
                anchorX = 0.5f,
                anchorY = 0.5f,
                alignment = FLabelAlignment.Center
            };
            infoText.SetPosition(new Vector2(683.01f, yTop - 185.01f));

            container.AddChild(infoText);
            if (message == "Trying to reconnect to the host.")
            {
                SteamFinal.TryToReconnect = true;
                SteamFinal.ReconnectTimer = 0;
            }
        }

        public override void Update()
        {
            base.Update();

            wait = Mathf.Max(0, wait - 1);
            closeButton.buttonBehav.greyedOut = wait != 0;
            if (confirmButton != null) confirmButton.buttonBehav.greyedOut = wait != 0;

            if ((infoText.text == "Trying to reconnect to the host." || infoText.text == "Cannot reconnect to host.") && manager.currentMainLoop is RainWorldGame game)
            {
                game.paused = true;
            }
        }

        public override void Singal(MenuObject sender, string message)
        {
            base.Singal(sender, message);
            switch (message)
            {
                case "CLOSE":
                    manager.StopSideProcess(this);
                    break;
                case "QUITGAEM":
                    Custom.rainWorld.processManager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
                    manager.StopSideProcess(this);
                    SteamFinal.TryToReconnect = false;
                    break;
                case "STOPRECONNECT":
                    SteamFinal.TryToReconnect = false;
                    Custom.rainWorld.processManager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
                    manager.StopSideProcess(this);
                    break;
                case "CONFIRMJOIN":
                    var call = SteamMatchmaking.JoinLobby(lobbyID.Value);
                    SteamTest.lobbyEntered.Set(call, SteamTest.OnLobbyEntered);
                    manager.StopSideProcess(this);
                    break;
            }
        }
    }
}
