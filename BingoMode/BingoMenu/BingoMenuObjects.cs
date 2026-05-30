using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Menu;
using UnityEngine;
using RWCustom;
using Menu.Remix.MixedUI;

namespace BingoMode.BingoMenu
{
    public static class BingoMenuObjects
    {
        // I just want colors dawg
        public class BingoSymbolButton : SymbolButton
        {
            public BingoSymbolButton(Menu.Menu menu, MenuObject owner, string symbolName, string singalText, Vector2 pos) : base(menu, owner, symbolName, singalText, pos)
            {
            }

            public override void GrafUpdate(float timeStacker)
            {
                base.GrafUpdate(timeStacker);
                float num = 0.5f - 0.5f * Mathf.Sin(Mathf.Lerp(this.buttonBehav.lastSin, this.buttonBehav.sin, timeStacker) / 30f * 3.1415927f * 2f);
                num *= this.buttonBehav.sizeBump;
                HSLColor baseCol = this.rectColor != null ? this.rectColor.Value : Menu.Menu.MenuColor(Menu.Menu.MenuColors.MediumGrey);
                Color color = Custom.HSL2RGB(baseCol.hue, baseCol.saturation, baseCol.lightness);
                this.symbolSprite.color = (this.buttonBehav.greyedOut ? Menu.Menu.MenuRGB(Menu.Menu.MenuColors.VeryDarkGrey) : Color.Lerp(color, Menu.Menu.MenuRGB(Menu.Menu.MenuColors.VeryDarkGrey), num));
                this.symbolSprite.x = this.DrawX(timeStacker) + base.DrawSize(timeStacker).x / 2f;
                this.symbolSprite.y = this.DrawY(timeStacker) + base.DrawSize(timeStacker).y / 2f;
                for (int i = 0; i < 4; i++)
                {
                    this.roundedRect.sprites[this.roundedRect.SideSprite(i)].color = color;
                    this.roundedRect.sprites[this.roundedRect.CornerSprite(i)].color = color;
                }

            }
        }
    }
}
