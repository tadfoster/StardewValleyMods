using Microsoft.Xna.Framework;
using StardewModdingAPI;
using System.Collections.Generic;

namespace DayLimiter
{
    public class ModConfig
    {
        public bool ModEnabled { get; set; } = false;
        public bool ExitToTitle { get; set; } = false;
        public int DayLimitCount { get; set; } = 3;
        public DateTime? TakeBreakUntilTime { get; set; } = null;
    }
}