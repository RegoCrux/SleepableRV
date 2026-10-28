using RimWorld;
using Vehicles;
using Verse;

namespace SleepableRV
{
    public class HediffCompProperties_RVClimate : HediffCompProperties
    {
        public int checkIntervalTicks = 60;

        public HediffCompProperties_RVClimate()
        {
            compClass = typeof(HediffComp_RVClimate);
        }
    }

    /// <summary>
    /// Removes the RV climate hediff once the pawn is neither sleeping in an RV bunk inside a vehicle
    /// nor riding inside an RV.
    /// </summary>
    public class HediffComp_RVClimate : HediffComp
    {
        private bool shouldRemove;

        public HediffCompProperties_RVClimate Props => (HediffCompProperties_RVClimate)props;

        public override bool CompShouldRemove => shouldRemove;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (Pawn.IsHashIntervalTick(Props.checkIntervalTicks, delta))
                shouldRemove = !IsSheltered(Pawn);
        }

        public static bool IsSheltered(Pawn pawn)
        {
            if (pawn.CurrentBed() is Building_RVBunk { InsideVehicle: true })
                return true;
            return pawn.ParentHolder is VehicleRoleHandler handler && handler.vehicle?.GetComp<CompRVBunks>() != null;
        }
    }
}
