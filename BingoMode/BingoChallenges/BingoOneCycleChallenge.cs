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
        public int current;

        public void EndCycle()
        {
            if (!oneCycle.Value || completed || revealed || TeamsCompleted[SteamTest.team] || hidden || current == 0) return;
            Reset();
            UpdateDescription();
            ChangeValue();
        }
    }
}
