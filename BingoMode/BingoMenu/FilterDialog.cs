using BingoMode.BingoSteamworks;
using Expedition;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BingoMode.BingoMenu
{
    public class FilterDialog : Dialog
    {
        private readonly int[] maxItems = [13, 5];
        private float leftAnchor;
        private bool opening;
        private bool closing;
        private float uAlpha;
        private float currentAlpha;
        private float lastAlpha;
        private float targetAlpha;
        private float sliderF;
        private float num;
        private SimpleButton closeButton;
        private FSprite[] dividers;
        private FLabel description;
        private List<Challenge> testList;
        private TypeButton[] testLabels;
        private VerticalSlider slider;
        private MenuTab tab;
        private MenuTabWrapper wrapper;
        private float scrollWheelVelocity;

        public FilterDialog(ProcessManager manager) : base(manager)
        {
            float[] screenOffsets = Custom.GetScreenOffsets();
            leftAnchor = screenOffsets[0];
            Vector2 outOfBounds = new Vector2(10000, 10000);

            description = new FLabel(Custom.GetFont(), Translate("Click on a challenge type to block it from generating"));
            description.shader = manager.rainWorld.Shaders["MenuText"];
            pages[0].Container.AddChild(description);

            num = 85f;
            float num2 = LabelTest.GetWidth(Translate("CLOSE"), false) + 10f;
            if (num2 > num)
            {
                num = num2;
            }
            closeButton = new SimpleButton(this, pages[0], Translate("CLOSE"), "CLOSE", outOfBounds, new Vector2(num, 35f));
            pages[0].subObjects.Add(closeButton);

            dividers = new FSprite[2];
            for (int i = 0; i < dividers.Length; i++)
            {
                dividers[i] = new FSprite("pixel")
                {
                    scaleY = 2,
                    scaleX = 400,
                };
                pages[0].Container.AddChild(dividers[i]);
            }

            opening = true;
            targetAlpha = 1f;

            slider = new VerticalSlider(this, pages[0], "", new Vector2(847f - leftAnchor, 294f), new Vector2(30f, 210f), BingoEnums.CustomizerSlider, true) { floatValue = 1f };
            foreach (var line in slider.lineSprites)
            {
                line.alpha = 0f;
            }
            slider.subtleSliderNob.outerCircle.alpha = 0f;
            pages[0].subObjects.Add(slider);

            testList = [.. BingoData.GetAdequateChallengeList(ExpeditionData.slugcatPlayer)];
            testLabels = new TypeButton[testList.Count];
            for (int i = 0; i < testList.Count; i++)
            {
                testLabels[i] = new TypeButton(this, pages[0], new Vector2(360f, 20f), testList[i]) { lastPos = new Vector2(360f, 20f)};
                pages[0].subObjects.Add(testLabels[i]);
            }
            testLabels = testLabels.ToList().OrderBy(x => x.text.text).ToArray();
            wrapper = new MenuTabWrapper(this, pages[0]);
            pages[0].subObjects.Add(wrapper);

            tab = new MenuTab();
            pages[0].Container.AddChild(tab._container);
            tab._Activate();
            tab._Update();
            tab._GrafUpdate(1f);
        }

        public override void SliderSetValue(Slider slider, float f)
        {
            if (slider.ID == BingoEnums.CustomizerSlider)
            {
                sliderF = f;
            }
        }

        public override float ValueOfSlider(Slider slider)
        {
            if (slider.ID == BingoEnums.CustomizerSlider)
            {
                return sliderF;
            }
            return 0f;
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
                description.RemoveFromContainer();
                for (int i = 0; i < dividers.Length; i++)
                {
                    dividers[i].RemoveFromContainer();
                }
                manager.StopSideProcess(this);
                closing = false;
                tab._Unload();
                testList.Clear();
            }
            closeButton.buttonBehav.greyedOut = opening;

            tab._Update();

            if (manager.menuesMouseMode && mouseScrollWheelMovement != 0)
            {
                scrollWheelVelocity = Mathf.Sign(mouseScrollWheelMovement);
            }
            scrollWheelVelocity *= 0.8f;
            if (scrollWheelVelocity != 0f)
            {
                SliderSetValue(slider, Mathf.Clamp01(ValueOfSlider(slider) - scrollWheelVelocity * 0.07f));
            }
            if (Mathf.Abs(scrollWheelVelocity) < 0.02f) scrollWheelVelocity = 0f;
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
            for (int i = 0; i < dividers.Length; i++)
            {
                dividers[i].alpha = darkSprite.alpha;
            }
            foreach (var line in slider.lineSprites)
            {
                line.alpha = uAlpha;
            }
            slider.subtleSliderNob.outerCircle.alpha = uAlpha;
            Vector2 pagePos = Vector2.Lerp(pages[0].lastPos, pages[0].pos, timeStacker);
            dividers[0].SetPosition(new Vector2(683f - leftAnchor, 534f) + pagePos);
            dividers[1].SetPosition(new Vector2(683f - leftAnchor, 284f) + pagePos);

            description.SetPosition(new Vector2(683f - leftAnchor, 553f) + pagePos);
            description.alpha = darkSprite.alpha;

            closeButton.pos = new Vector2(683f - num / 2f, 220f);

            Vector2 origPos = new Vector2(483f - leftAnchor, 514f) + pagePos;
            float dif = 210f / maxItems[0];
            float sliderDif = dif * (testLabels.Length - maxItems[0] + 1);
            for (int i = 0; i < testLabels.Length; i++)
            {
                testLabels[i].pos = origPos - new Vector2(0f, dif * i - sliderDif * (1f - sliderF));
                testLabels[i].maxAlpha = Mathf.InverseLerp(284f - pagePos.y, 294f - pagePos.y, testLabels[i].pos.y) - Mathf.InverseLerp(519f - pagePos.y, 529f - pagePos.y, testLabels[i].pos.y);
                testLabels[i].buttonBehav.greyedOut = testLabels[i].maxAlpha < 0.2f;
            }

            tab._GrafUpdate(timeStacker);
        }

        public override void Singal(MenuObject sender, string message)
        {
            base.Singal(sender, message);
            switch (message)
            {
                case "CLOSE":
                    closing = true;
                    targetAlpha = 0f;
                    BingoData.SaveChallengeBlacklistFor(ExpeditionData.slugcatPlayer);
                    break;
            }
        }
        public void BlacklistChallenge(Challenge ch)
        {
            if (ch == null) return;
            string className = ch.GetType().Name;
            if (!BingoData.bannedChallenges[ExpeditionData.slugcatPlayer].Contains(className))
            {
                BingoData.bannedChallenges[ExpeditionData.slugcatPlayer].Add(className);
            } 
            else
            {
                BingoData.bannedChallenges[ExpeditionData.slugcatPlayer].Remove(className);
            }
        }

        public class TypeButton : ButtonTemplate
        {
            public Challenge ch;
            public FLabel text;
            public float maxAlpha;
            public string baseText;
            private bool currentlyBanned;

            public TypeButton(Menu.Menu menu, MenuObject owner, Vector2 size, Challenge ch) : base(menu, owner, Vector2.zero, size)
            {
                baseText = ch.ChallengeName();
                text = new FLabel(Custom.GetFont(), baseText);
                text.SetAnchor(new Vector2(0.5f, 0f));
                text.scale = 1f;
                owner.Container.AddChild(text);
                this.ch = ch;
            }

            public override void Update()
            {
                base.Update();
                currentlyBanned = BingoData.bannedChallenges[ExpeditionData.slugcatPlayer].Contains(ch.GetType().Name);

                if (currentlyBanned)
                {
                    text.text = "x " + baseText + " x";
                }
                else text.text = baseText;
            }

            public override void GrafUpdate(float timeStacker)
            {
                base.GrafUpdate(timeStacker);

                text.SetPosition(Vector2.Lerp(lastPos, pos, timeStacker) + new Vector2(200f, 0f));
                if (MouseOver)
                {
                    text.alpha = Mathf.Clamp01(maxAlpha - 0.5f * Mathf.Abs(Mathf.Sin(Mathf.Lerp(buttonBehav.lastSin, buttonBehav.sin, timeStacker) / 30f * Mathf.PI)));
                }
                else text.alpha = maxAlpha;
                
                if (currentlyBanned)
                {
                    text.alpha *= 0.5f;
                }
            }

            public override void RemoveSprites()
            {
                base.RemoveSprites();
                text.RemoveFromContainer();
            }

            public override void Clicked()
            {
                base.Clicked();
                menu.PlaySound(currentlyBanned ? SoundID.MENU_Add_Level : SoundID.MENU_Remove_Level);
                (menu as FilterDialog).BlacklistChallenge(ch);
            }
        }
    }
}
