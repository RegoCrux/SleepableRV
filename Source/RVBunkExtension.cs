using RimWorld;
using Verse;

namespace SleepableRV
{
    /// <summary>
    /// Shelter settings for an RV bunk, modeled after CampingTent's TentModExtension.
    /// The shelter effects only apply while the bunk is deployed inside the vehicle footprint;
    /// a bunk set up beside the RV behaves like an ordinary bedroll.
    /// </summary>
    public class RVBunkExtension : DefModExtension
    {
        public bool negateSleptOutside = true;
        public bool negateSleptOnGround = true;
        public bool negateWet = true;
        public bool negateTemperatureThoughts = true;

        /// <summary>Hediff applied to sleepers inside the RV (climate control).</summary>
        public HediffDef sleeperHediff;

        /// <summary>Memory gained on waking inside the RV.</summary>
        public ThoughtDef sleptThought;
    }
}
