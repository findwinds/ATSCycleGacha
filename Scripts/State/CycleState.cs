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
    /// Reputation values are raw amounts; effectNames holds the internal EffectModel names granted
    /// by milestones. One entry per grant, so duplicates stack naturally.
    /// </summary>
    [Serializable]
    public class CycleState
    {
        public float ordersReputation;
        public float resolveReputation;
        public float gladeReputation;

        public int ordersGrants;
        public int resolveGrants;
        public int gladeGrants;

        public List<string> effectNames = new List<string>();

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

        public int GetGrantCount(BlessingDimension dimension)
        {
            switch (dimension)
            {
                case BlessingDimension.Orders: return ordersGrants;
                case BlessingDimension.Resolve: return resolveGrants;
                case BlessingDimension.Glade: return gladeGrants;
                default: return 0;
            }
        }

        public void IncrementGrant(BlessingDimension dimension)
        {
            switch (dimension)
            {
                case BlessingDimension.Orders:
                    ordersGrants++;
                    break;
                case BlessingDimension.Resolve:
                    resolveGrants++;
                    break;
                case BlessingDimension.Glade:
                    gladeGrants++;
                    break;
            }
        }

        public void Sanitize()
        {
            if (effectNames == null)
            {
                effectNames = new List<string>();
            }

            if (ordersReputation < 0f) ordersReputation = 0f;
            if (resolveReputation < 0f) resolveReputation = 0f;
            if (gladeReputation < 0f) gladeReputation = 0f;

            if (ordersGrants < 0) ordersGrants = 0;
            if (resolveGrants < 0) resolveGrants = 0;
            if (gladeGrants < 0) gladeGrants = 0;
        }

        public string Describe()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Orders=").Append(ordersReputation.ToString("0.##"));
            builder.Append(" Resolve=").Append(resolveReputation.ToString("0.##"));
            builder.Append(" Glade=").Append(gladeReputation.ToString("0.##"));
            builder.Append(" Buffs=").Append(effectNames.Count);
            return builder.ToString();
        }
    }
}
