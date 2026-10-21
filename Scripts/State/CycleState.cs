using System;
using System.Collections.Generic;
using System.Text;

namespace Forwindz.CycleBlessings
{
    [Serializable]
    public class WheelOption
    {
        public string effect;
        public string rarity;
    }

    /// <summary>
    /// Stored in ModSaveData.CurrentCycle, so ATS_API clears tickets, the wheel and all
    /// owned buffs automatically when the world cycle ends.
    /// </summary>
    [Serializable]
    public class CycleState
    {
        public int tickets;
        public bool wheelGenerated;
        public List<WheelOption> wheel = new List<WheelOption>();
        public List<string> effectNames = new List<string>();
        public int drawsSinceWheelGenerated;
        public int totalDraws;

        public void Sanitize()
        {
            if (wheel == null)
            {
                wheel = new List<WheelOption>();
            }

            if (effectNames == null)
            {
                effectNames = new List<string>();
            }

            for (int i = wheel.Count - 1; i >= 0; i--)
            {
                WheelOption option = wheel[i];
                if (option == null || string.IsNullOrEmpty(option.effect) || string.IsNullOrEmpty(option.rarity))
                {
                    wheel.RemoveAt(i);
                }
            }

            effectNames.RemoveAll(string.IsNullOrEmpty);

            if (tickets < 0) tickets = 0;
            if (drawsSinceWheelGenerated < 0) drawsSinceWheelGenerated = 0;
            if (totalDraws < 0) totalDraws = 0;
        }

        public string Describe()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Tickets=").Append(tickets);
            builder.Append(" Wheel=").Append(wheel.Count);
            builder.Append(" Buffs=").Append(effectNames.Count);
            builder.Append(" Draws=").Append(totalDraws);
            return builder.ToString();
        }
    }
}
