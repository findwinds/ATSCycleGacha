using System;
using System.Collections.Generic;
using System.Text;

namespace Forwindz.CycleBlessings
{
    [Serializable]
    public class WheelOption
    {
        public string rewardId;
        public string rarity;
    }

    /// <summary>
    /// Stored in ModSaveData.CurrentCycle, so ATS_API clears tickets, the wheel, draw charge
    /// and all owned rewards automatically when the world cycle ends.
    /// </summary>
    [Serializable]
    public class CycleState
    {
        public int tickets;
        public bool wheelGenerated;
        public List<WheelOption> wheel = new List<WheelOption>();
        public List<string> rewardIds = new List<string>();
        public int drawsSinceWheelGenerated;
        public int totalDraws;

        /// <summary>0-9. Reaching 10 grants one free draw.</summary>
        public int drawCharge;
        public int freeDraws;

        public void Sanitize()
        {
            if (wheel == null)
            {
                wheel = new List<WheelOption>();
            }

            if (rewardIds == null)
            {
                rewardIds = new List<string>();
            }

            for (int i = wheel.Count - 1; i >= 0; i--)
            {
                WheelOption option = wheel[i];
                if (option == null || string.IsNullOrEmpty(option.rewardId) || string.IsNullOrEmpty(option.rarity))
                {
                    wheel.RemoveAt(i);
                }
            }

            rewardIds.RemoveAll(string.IsNullOrEmpty);

            if (tickets < 0) tickets = 0;
            if (drawsSinceWheelGenerated < 0) drawsSinceWheelGenerated = 0;
            if (totalDraws < 0) totalDraws = 0;
            if (drawCharge < 0) drawCharge = 0;
            if (freeDraws < 0) freeDraws = 0;

            while (drawCharge >= 10)
            {
                drawCharge -= 10;
                freeDraws++;
            }
        }

        public string Describe()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Tickets=").Append(tickets);
            builder.Append(" Wheel=").Append(wheel.Count);
            builder.Append(" Rewards=").Append(rewardIds.Count);
            builder.Append(" Draws=").Append(totalDraws);
            builder.Append(" Charge=").Append(drawCharge);
            builder.Append(" Free=").Append(freeDraws);
            return builder.ToString();
        }
    }
}
