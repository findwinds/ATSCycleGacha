using System;
using System.Collections.Generic;
using System.Text;

namespace Forwindz.CycleBlessings
{
    public enum BlessingDimension
    {
        Orders,
        Resolve,
        Glade
    }

    /// <summary>
    /// Stored in ModSaveData.CurrentCycle, so ATS_API clears it automatically when the world cycle ends.
    /// Values are raw reputation amounts, not milestone counts. Negative reputation is never recorded.
    /// </summary>
    [Serializable]
    public class CycleState
    {
        public float ordersReputation;
        public float resolveReputation;
        public float gladeReputation;

        public int ordersBlessings;
        public int resolveBlessings;
        public int gladeBlessings;

        public List<string> blessingIds = new List<string>();

        public float GetReputation(BlessingDimension dimension)
        {
            switch (dimension)
            {
                case BlessingDimension.Orders: return ordersReputation;
                case BlessingDimension.Resolve: return resolveReputation;
                case BlessingDimension.Glade: return gladeReputation;
                default: return 0f;
            }
        }

        public float AddReputation(BlessingDimension dimension, float amount)
        {
            if (amount <= 0f)
            {
                return GetReputation(dimension);
            }

            switch (dimension)
            {
                case BlessingDimension.Orders:
                    ordersReputation += amount;
                    return ordersReputation;
                case BlessingDimension.Resolve:
                    resolveReputation += amount;
                    return resolveReputation;
                case BlessingDimension.Glade:
                    gladeReputation += amount;
                    return gladeReputation;
                default:
                    return 0f;
            }
        }

        public int GetBlessingCount(BlessingDimension dimension)
        {
            switch (dimension)
            {
                case BlessingDimension.Orders: return ordersBlessings;
                case BlessingDimension.Resolve: return resolveBlessings;
                case BlessingDimension.Glade: return gladeBlessings;
                default: return 0;
            }
        }

        public void IncrementBlessing(BlessingDimension dimension)
        {
            switch (dimension)
            {
                case BlessingDimension.Orders:
                    ordersBlessings++;
                    break;
                case BlessingDimension.Resolve:
                    resolveBlessings++;
                    break;
                case BlessingDimension.Glade:
                    gladeBlessings++;
                    break;
            }
        }

        public void Sanitize()
        {
            if (blessingIds == null)
            {
                blessingIds = new List<string>();
            }

            if (ordersReputation < 0f) ordersReputation = 0f;
            if (resolveReputation < 0f) resolveReputation = 0f;
            if (gladeReputation < 0f) gladeReputation = 0f;

            if (ordersBlessings < 0) ordersBlessings = 0;
            if (resolveBlessings < 0) resolveBlessings = 0;
            if (gladeBlessings < 0) gladeBlessings = 0;
        }

        public string Describe()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Orders=").Append(ordersReputation.ToString("0.##"));
            builder.Append(" Resolve=").Append(resolveReputation.ToString("0.##"));
            builder.Append(" Glade=").Append(gladeReputation.ToString("0.##"));
            builder.Append(" Blessings=").Append(blessingIds.Count);
            return builder.ToString();
        }
    }
}
