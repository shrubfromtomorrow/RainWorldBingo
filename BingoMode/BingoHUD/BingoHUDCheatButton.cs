using BingoMode.BingoSteamworks;
using BingoMode.BingoChallenges;
using Expedition;
using RWCustom;
using UnityEngine;

namespace BingoMode.BingoHUD
{
    using BingoMenu;

    public class BingoHUDCheatButton
    {
        public BingoHUDMain.BingoInfo info;
        public bool fail;
        public float alpha;
        public int team;
        public float lastAlpha;
        public float angle;
        public float size;
        public Vector2 pos;
        public Vector2 lastPos;
        public Vector2 ogPos;
        public Vector2[] corners;
        public FSprite background;
        public FSprite tickSprite;
        public FSprite tickShadow;
        public FSprite[] border;
        public bool appear;
        public bool lastMouseOver;
        public bool mouseOver;
        public Color tickColor;

        public BingoHUDCheatButton(BingoHUDMain.BingoInfo info, int team, float angle)
        {
            this.info = info;
            this.angle = angle;
            this.team = team;
            size = 40f;
            FContainer container = info.hud.fContainers[1];

            background = new FSprite("pixel") { scale = size, color = BingoPage.TEAM_COLOR[team], alpha = 0.7f };
            container.AddChild(background);

            tickShadow = new FSprite("pixel") { color = new Color(0.02f, 0.02f, 0.02f), scale = 22f, alpha = 0.3f };
            //container.AddChild(tickShadow);

            tickSprite = new FSprite("Menu_Symbol_CheckBox");
            container.AddChild(tickSprite);

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

            alpha = 0f;
            ogPos = info.pos;
            pos = ogPos;
            lastPos = ogPos;

            corners = new Vector2[3];
            corners[0] = pos + new Vector2(-size / 2f, -size / 2f);
            corners[1] = pos + new Vector2(-size / 2f, size / 2f);
            corners[2] = pos + new Vector2(size / 2f, -size / 2f);
            border[0].SetPosition(corners[0]);
            border[1].SetPosition(corners[1]);
            border[2].SetPosition(corners[0]);

            tickColor = Color.green;
        }

        public void Update()
        {
            lastMouseOver = mouseOver;
            mouseOver = info.owner.mousePosition.x > pos.x - size / 2f && info.owner.mousePosition.y > pos.y - size / 2f
                    && info.owner.mousePosition.x < pos.x + size / 2f && info.owner.mousePosition.y < pos.y + size / 2f;

            lastAlpha = alpha;
            alpha = appear ? Mathf.Min(1f, alpha + 0.08f) : Mathf.Max(0f, alpha - 0.12f);

            lastPos = pos;
            pos = ogPos + Custom.DegToVec(angle) * info.size * 0.95f * Custom.LerpCircEaseOut(0f, 1f, alpha);

            if (alpha > 0.5f && mouseOver && info.owner.MouseLeftDown)
            {
                if (tickSprite.element.name == "Menu_Symbol_CheckBox")
                {
                    if (team != SteamTest.team && SteamTest.team != 8 && BingoData.IsCurrentSaveLockout())
                    {
                        (info.challenge as BingoChallenge).OnChallengeLockedOut(team);
                    }
                    else (info.challenge as BingoChallenge).OnChallengeCompleted(team);
                }
                else
                {
                    if ((info.challenge as BingoChallenge).ReverseChallenge())
                    {
                        (info.challenge as BingoChallenge).OnChallengeFailed(team);
                    }
                    else
                    {
                        (info.challenge as BingoChallenge).OnChallengeDepleted(team);
                    }
                }
                SteamFinal.BroadcastCurrentBoardState();
                info.beingCheatedBeepBoop = false;
                info.owner.cheatingRnAtThisMoment = false;
                info.owner.cantClickCounter = 10;
            }
        }

        public void Draw(float timeStacker)
        {
            // Position
            Vector2 drawPos = Vector2.Lerp(lastPos, pos, timeStacker);

            background.SetPosition(drawPos);
            tickSprite.SetPosition(drawPos);
            tickShadow.SetPosition(drawPos);

            corners[0] = drawPos + new Vector2(-size / 2f, -size / 2f);
            corners[1] = drawPos + new Vector2(-size / 2f, size / 2f);
            corners[2] = drawPos + new Vector2(size / 2f, -size / 2f);
            border[0].SetPosition(corners[0]);
            border[1].SetPosition(corners[1]);
            border[2].SetPosition(corners[0]);
            border[3].SetPosition(corners[2]);

            // Alpha
            float drawAlpha = Mathf.Lerp(lastAlpha, alpha, timeStacker) * (mouseOver ? 0.8f : 1f);
            background.alpha = drawAlpha * 0.7f;
            tickSprite.alpha = drawAlpha;
            tickShadow.alpha = drawAlpha * 0.3f;
            for (int i = 0; i < border.Length; i++)
            {
                border[i].alpha = drawAlpha;
            }
            bool cheetah = tickSprite.element.name == "Menu_Symbol_CheckBox";
            tickSprite.color = (mouseOver || (cheetah ? !(info.challenge as BingoChallenge).TeamsCompleted[team] : (info.challenge as BingoChallenge).TeamsCompleted[team])) ? tickColor : Color.white;
        }

        public void Remove()
        {
            background.RemoveFromContainer();
            tickShadow.RemoveFromContainer();
            tickSprite.RemoveFromContainer();
            for (int i = 0; i < border.Length; i++)
            {
                border[i].RemoveFromContainer();
            }
        }
    }
}
