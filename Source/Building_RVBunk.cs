using System.Text;
using RimWorld;
using Vehicles;
using Verse;

namespace SleepableRV
{
    /// <summary>
    /// A real bed spawned by a parked RV. Pawns claim it, sleep in it and use it for lovin'
    /// exactly like any other bed. When it sits under the vehicle it is not drawn and its
    /// sleepers are hidden, so they appear to be inside the RV.
    /// </summary>
    public class Building_RVBunk : Building_Bed
    {
        private VehiclePawn vehicle;
        private bool insideVehicle;

        public VehiclePawn Vehicle => vehicle;

        public bool InsideVehicle => insideVehicle;

        public RVBunkExtension Extension => def.GetModExtension<RVBunkExtension>();

        public void Attach(VehiclePawn vehicle, bool insideVehicle)
        {
            this.vehicle = vehicle;
            this.insideVehicle = insideVehicle;
        }

        public override void Print(SectionLayer layer)
        {
            if (insideVehicle)
                return;
            base.Print(layer);
        }

        public override void TickRare()
        {
            base.TickRare();
            if (!insideVehicle)
                return;

            HediffDef hediffDef = Extension?.sleeperHediff;
            if (hediffDef == null)
                return;

            foreach (Pawn pawn in CurOccupants)
            {
                if (pawn.health?.hediffSet != null && !pawn.health.hediffSet.HasHediff(hediffDef))
                    pawn.health.AddHediff(hediffDef);
            }
        }

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder(base.GetInspectString());
            if (vehicle != null)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                string key = insideVehicle ? "SleepableRV_BunkInside" : "SleepableRV_BunkBeside";
                sb.Append(key.Translate(vehicle.LabelShortCap));
            }
            return sb.ToString();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref vehicle, "vehicle");
            Scribe_Values.Look(ref insideVehicle, "insideVehicle");
        }
    }
}
