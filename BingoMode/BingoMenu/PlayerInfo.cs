using BingoMode.BingoSteamworks;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using System.Security.Principal;
using UnityEngine;

namespace BingoMode.BingoMenu
{
    public class PlayerInfo : PositionedMenuObject
    {
        private const float MARGIN = 5f;
        private const float READY_MARK_SIZE = 5f;
        private const float ALPHA_THRESHOLD = 0.01f;
        private const float SELECT_TEAM_WIDTH = 90f;
        private const float KICK_WIDTH = 40f;
        private const float DROPDOWN_SIZE = 300f; // can be arbitrarily large, it just needs to be bigger than the dropdown.

        private float _alpha = 1f;
        private PlayerData data;

        private FSprite readyMark;
        private MenuLabel nameLabel;
        private SimpleButton kick;
        private MenuTabWrapper tabWrapper;
        private UIelementWrapper selectTeamWrapper;
        private OpComboBox selectTeam;

        public Vector2 size;
        public float Alpha
        {
            get => _alpha;
            set
            {
                if (value == _alpha)
                    return;

                _alpha = value;

                readyMark.alpha = value;
                nameLabel.label.alpha = value;

                if (kick != null)
                {
                    for (int i = 9; i < 17; i++)
                        kick.roundedRect.sprites[i].alpha = Mathf.Lerp(0f, 1f, value);
                    kick.menuLabel.label.alpha = value;
                    kick.buttonBehav.greyedOut = value < ALPHA_THRESHOLD;
                }

                if (selectTeam != null)
                {
                    selectTeam.colorFill.a = value;
                    selectTeam.colorEdge.a = value;
                    DropDownEnabled = true;
                }
            }
        }
        public bool DropDownEnabled
        {
            get => selectTeam != null && !selectTeam.greyedOut;
            set
            {
                bool greyedOut = !value || _alpha < ALPHA_THRESHOLD;
                if (selectTeam == null || selectTeam.greyedOut == greyedOut)
                    return;

                // For some absurd reason, toggling selectTeam.greyedOut fires OnListClose.
                // Below is the fix I came up with. If for some reason you need to change this, god help you.
                if (greyedOut)
                    selectTeam.OnListClose -= UnfocusDropDown;
                else
                    selectTeam.OnListClose += UnfocusDropDown;
                selectTeam.greyedOut = greyedOut;
            }
        }
        public PlayerData Data { get => data; }

        public PlayerInfo(Menu.Menu menu, MenuObject owner, Vector2 pos, Vector2 size, bool controls, PlayerData data) : base(menu, owner, pos)
        {
            this.size = size;
            this.data = data;

            string nameAndTeam = $"{data.nickname} ({Expedition.ChallengeTools.IGT.Translate(BingoPage.TeamName[data.team])})";

            string markAtlas = data.isHost ? "TinyCrown" : data.ready ? "TinyCheck" : "TinyX";
            Color markColor = data.isHost ? Color.yellow : data.ready ? Color.green : Color.red;
            readyMark = new FSprite(markAtlas) { color = markColor };
            Container.AddChild(readyMark);

            nameLabel = new(menu, this, nameAndTeam, new Vector2(2f * MARGIN + READY_MARK_SIZE, size.y / 2f), Vector2.zero, false);
            nameLabel.label.color = BingoPage.TEAM_COLOR[data.team];
            nameLabel.label.alignment = FLabelAlignment.Left;
            nameLabel.label.anchorY = 0.5f;
            subObjects.Add(nameLabel);

            if (!controls)
                return;

            Configurable<string> conf = MenuModList.ModButton.RainWorldDummy.config.Bind("_PlayerInfoSelect", BingoPage.TeamName[data.team], (ConfigAcceptableBase)null);
            selectTeam = new(
                    conf,
                    new Vector2(size.x - MARGIN - SELECT_TEAM_WIDTH, DROPDOWN_SIZE), // DROPDOWN_SIZE part of hack described below
                    SELECT_TEAM_WIDTH,
                    ["Red", "Blue", "Green", "Orange", "Pink", "Cyan", "Black", "Hurricane", "Board view"]);
            selectTeam.OnValueChanged += SelectTeam_OnValueChanged;
            selectTeam.OnListOpen += FocusDropDown;
            selectTeam.OnListClose += UnfocusDropDown;
            // Hack to trick the ComboBox into thinking it has enough space to open downward.
            // This is stupid. I hate this, but it's the cleanest workaround I found.
            tabWrapper = new(menu, this) { pos = new Vector2(0f, -DROPDOWN_SIZE) };
            subObjects.Add(tabWrapper);
            selectTeamWrapper = new(tabWrapper, selectTeam);

            if (!data.isSelf)
            {
                kick = new SimpleButton(
                        menu,
                        this,
                        "Kick",
                        $"KICK-{data.identity.GetSteamID64()}",
                        new Vector2(size.x - 2f * MARGIN - SELECT_TEAM_WIDTH - KICK_WIDTH, 0f),
                        new Vector2(KICK_WIDTH, size.y));
                subObjects.Add(kick);
            }
        }

        public override void GrafUpdate(float timeStacker)
        {
            if (kick != null)
                kick.roundedRect.fillAlpha = Mathf.Lerp(0f, 0.3f, _alpha);

            base.GrafUpdate(timeStacker);
            
            readyMark.SetPosition(DrawPos(timeStacker) + new Vector2(MARGIN, size.y / 2f));
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
            readyMark.RemoveFromContainer();
        }

        private void SelectTeam_OnValueChanged(UIconfig config, string value, string oldValue)
        {
            data.team = BingoPage.TeamNumber[value];
            Singal(null, $"SWTEAM-{data.identity.GetSteamID64()}-{data.team}");
        }

        private void FocusDropDown(UIfocusable sender) => Singal(this, "FOCUS_DD");

        private void UnfocusDropDown(UIfocusable sender) => Singal(this, "UNFOCUS_DD");
    }
}
