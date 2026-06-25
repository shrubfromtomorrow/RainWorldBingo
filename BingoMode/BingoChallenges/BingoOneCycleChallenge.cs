using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BingoMode.BingoSteamworks;

namespace BingoMode.BingoChallenges
{
    using static ChallengeHooks;
    public abstract class BingoOneCycleChallenge : BingoChallenge
    {
        public SettingBox<bool> oneCycle;

        public void EndCycle()
        {
            Plugin.logger.LogInfo("Made it to end cycle");
            if (revealed || completed) return;
            if (oneCycle.Value)
            {
                Reset();
                UpdateDescription();
                ChangeValue();
            }
            return;
        }
    }
}
