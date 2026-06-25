using System;
using System.Reflection.Emit;
using BepInEx.Logging;
using Menu.Remix.MixedUI;
using Menu.Remix.MixedUI.ValueTypes;
using UnityEngine;


namespace BingoMode
{
    public class BingoModOptions : OptionInterface
    {
        public readonly Configurable<KeyCode> HUDKeybindKeyboard;
        public readonly Configurable<KeyCode> HUDKeybindC1;
        public readonly Configurable<KeyCode> ResetBind;
        public readonly Configurable<string> SinglePlayerTeam;
        public readonly Configurable<bool> FillIcons;
        public readonly Configurable<bool> UseMapInput;
        public readonly Configurable<bool> PlayMenuSong;
        public readonly Configurable<bool> PlayEndingSong;
        public readonly Configurable<bool> PlayDangerSong;
        public readonly Configurable<bool> DiscordRichPresence;
        public readonly Configurable<bool> OneToOneSpecBoard;
        public readonly Configurable<bool> Tutorials;

        public readonly Configurable<bool> DialCharged;
        public readonly Configurable<int> DialAmount;

        private UIelement[] optionse;
        private UIelement[] optionse1;
        private bool greyedOut;

        public BingoModOptions(Plugin plugin)
        {
            HUDKeybindKeyboard = config.Bind<KeyCode>("HUDKeybind", KeyCode.Space);
            HUDKeybindC1 = config.Bind<KeyCode>("HUDKeybindC1", KeyCode.Joystick1Button5);

            ResetBind = config.Bind<KeyCode>("Reset", KeyCode.Slash);

            SinglePlayerTeam = config.Bind<string>("SinglePlayerTeam", "Red");
            FillIcons = config.Bind<bool>("FillIcons", false);
            UseMapInput = config.Bind<bool>("UseMapInput", false);
            PlayMenuSong = config.Bind<bool>("PlayMenuSong", true);
            PlayEndingSong = config.Bind<bool>("PlayEndingSong", true);
            PlayDangerSong = config.Bind<bool>("PlayDangerSong", true);
            DiscordRichPresence = config.Bind<bool>("DiscordRichPresence", true);
            OneToOneSpecBoard = config.Bind<bool>("OneToOneSpecBoard", false);
            Tutorials = config.Bind<bool>("Tutorials", true);


            DialCharged = config.Bind<bool>("DialCharged", false);
            DialAmount = config.Bind<int>("DialAmount", 50, new ConfigurableInfo("", new ConfigAcceptableRange<int>(1, 100)));
        }

        private static float GetLabelLength(string text)
        {
            return text.Length * 6f + 10f;
        }

        public override void Initialize()
        {
            base.Initialize();

            OpTab tabMain = new OpTab(this, Translate("Main"));
            OpTab tabGameplay = new OpTab(this, Translate("Gameplay"));
            Tabs = new[] { tabMain, tabGameplay };

            // Calculate label length to get an even line of buttons
            float maxLabelWidth = 0;
            maxLabelWidth = Mathf.Max(maxLabelWidth, GetLabelLength(Translate("Play custom music in bingo menu:")));
            maxLabelWidth = Mathf.Max(maxLabelWidth, GetLabelLength(Translate("Play custom music when game ends:")));
            maxLabelWidth = Mathf.Max(maxLabelWidth, GetLabelLength(Translate("Play custom music when your team is losing:")));
            maxLabelWidth = Mathf.Max(maxLabelWidth, GetLabelLength(Translate("Singleplayer team color:")));
            maxLabelWidth = Mathf.Max(maxLabelWidth, GetLabelLength(Translate("Fill icon sprites:")));
            maxLabelWidth = Mathf.Max(maxLabelWidth, GetLabelLength(Translate("Discord Rich Presence:")));
            maxLabelWidth = Mathf.Max(maxLabelWidth, GetLabelLength(Translate("1:1 spectator board:")));

            // For keybinds
            float labelX = Math.Max(GetLabelLength(Translate("Open Bingo HUD keybind:")), GetLabelLength(Translate("Quick reset keybind:"))) + 10f;
            float posText = labelX + 140f + 10f;
            float text = posText + Mathf.Max(GetLabelLength(Translate("-  Keyboard")), GetLabelLength(Translate("-  Controller 1")));

            optionse = new UIelement[]
            {
                new OpLabel(10f, 560f, Translate("Bingo Config"), true),
                new OpLabel(10f, 512f, Translate("Open Bingo HUD keybind:")) {alignment = FLabelAlignment.Left, description = Translate("Which button opens/closes the Bingo grid in game")},
                new OpLabel(posText, 510f, Translate("-  Keyboard")) {alignment = FLabelAlignment.Left},
                new OpKeyBinder(HUDKeybindKeyboard, new Vector2(labelX, 505f), new Vector2(140f, 20f), false, OpKeyBinder.BindController.AnyController) {description = Translate("Which button opens/closes the Bingo grid in game")},

                new OpLabel(posText, 470f, Translate("-  Controller 1")) {alignment = FLabelAlignment.Left},
                new OpKeyBinder(HUDKeybindC1, new Vector2(labelX, 465f), new Vector2(140f, 20f), false, OpKeyBinder.BindController.Controller1),
   
                new OpLabel(10f, 432, Translate("Quick reset keybind:")) {alignment = FLabelAlignment.Left, description = Translate("Reset a Bingo/Expedition save instantly")},
                new OpKeyBinder(ResetBind, new Vector2(labelX, 425f), new Vector2(140f, 20f), false, OpKeyBinder.BindController.AnyController),

                new OpLabel(text, 512f, Translate("Use map<LINE>input instead:").Replace("<LINE>", "\n")) {alignment = FLabelAlignment.Left},
                new OpCheckBox(UseMapInput, text + 10f, 475f),

                new OpLabel(10f, 388f, Translate("Show tutorials:")) {alignment = FLabelAlignment.Left},
                new OpCheckBox(Tutorials, maxLabelWidth, 385f),

                new OpLabel(10f, 348f, Translate("Play custom music in bingo menu:")) {alignment = FLabelAlignment.Left},
                new OpCheckBox(PlayMenuSong, maxLabelWidth, 345f),
                
                new OpLabel(10f, 308f, Translate("Play custom music when game ends:")) {alignment = FLabelAlignment.Left},
                new OpCheckBox(PlayEndingSong, maxLabelWidth, 305f),
                
                new OpLabel(10f, 268f, Translate("Play custom music when your team is losing:")) {alignment = FLabelAlignment.Left},
                new OpCheckBox(PlayDangerSong, maxLabelWidth, 265f),

                new OpLabel(10f, 228f, Translate("1:1 spectator board:")) {alignment = FLabelAlignment.Left, description = Translate("Leave Bingo board at same scale as gameplay while in board viewer mode")},
                new OpCheckBox(OneToOneSpecBoard, maxLabelWidth, 225f),

                new OpLabel(10f, 188f, Translate("Fill icon sprites:")) {alignment = FLabelAlignment.Left, description = Translate("Fill the crosses and arrows on certain goals")},
                new OpCheckBox(FillIcons, maxLabelWidth, 185f),

                new OpLabel(10f, 148f, Translate("Discord Rich Presence:")) {alignment = FLabelAlignment.Left, description = Translate("Show Bingo as your Discord activity (restart to take effect)")},
                new OpCheckBox(DiscordRichPresence, maxLabelWidth, 145f),

                new OpLabel(10f, 108f, Translate("Singleplayer team color:")) {alignment = FLabelAlignment.Left, description = Translate("Which team's color to use in singleplayer")},
                new OpComboBox(SinglePlayerTeam, new Vector2(maxLabelWidth, 105f), 100f, new string[] {"Red", "Blue", "Green", "Orange", "Pink", "Cyan", "Black", "Hurricane" }) {description = Translate("Which team's color to use in singleplayer")},
            };
            tabMain.AddItems(optionse);

            optionse1 = new UIelement[]
            {
                new OpLabel(10f, 560f, Translate("Bingo Gameplay Config"), true),

                new OpLabel(10f, 512f, Translate("Start with the Dial Warp perk fully charged:")) {alignment = FLabelAlignment.Left},
                new OpCheckBox(DialCharged, GetLabelLength(Translate("Start with the Dial Warp perk fully charged:")), 509f),

                new OpLabel(10f, 472f, Translate("Ripple eggs required for dial warp:")) {alignment = FLabelAlignment.Left},
                new OpUpdown(DialAmount, new Vector2(GetLabelLength(Translate("Ripple eggs required for dial warp:")), 466f), 60f),
            };
            tabGameplay.AddItems(optionse1);
        }

        public override void Update()
        {
            base.Update();
            
            foreach (var item in Tabs[0].items)
            {
                if (item is OpCheckBox bock && bock.cfgEntry == UseMapInput)
                {
                    greyedOut = bock.GetValueBool();
                }

                if (item is OpKeyBinder g && !(g.cfgEntry == ResetBind))
                {
                    g.greyedOut = greyedOut;
                }
            }
        }
    }
}
