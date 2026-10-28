using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace SleepableRV
{
    public class CompProperties_RVBunks : CompProperties
    {
        /// <summary>One bed def per bunk the RV deploys when parked.</summary>
        public List<ThingDef> bunks = new List<ThingDef>();

        /// <summary>Hediff given to pawns riding inside the vehicle (climate control).</summary>
        public HediffDef passengerHediff;

        /// <summary>How far from the vehicle a bunk may be set up when it doesn't fit underneath.</summary>
        public int besideRadius = 3;

        public int checkIntervalTicks = 120;

        public CompProperties_RVBunks()
        {
            compClass = typeof(CompRVBunks);
        }
    }

    /// <summary>
    /// Spawns real beds (<see cref="Building_RVBunk"/>) while the vehicle is parked and removes them
    /// as soon as it is drafted or starts moving. Bunks go under the vehicle footprint when they fit,
    /// otherwise beside it. Bunk owners are remembered so the same pawns reclaim them on the next stop.
    /// </summary>
    public class CompRVBunks : ThingComp
    {
        private List<Building_RVBunk> deployed = new List<Building_RVBunk>();
        private List<BunkOwners> rememberedOwners = new List<BunkOwners>();
        private bool deployEnabled = true;

        public CompProperties_RVBunks Props => (CompProperties_RVBunks)props;

        public VehiclePawn Vehicle => parent as VehiclePawn;

        private bool AnyDeployed => deployed.Any(b => b != null && b.Spawned);

        private bool ParkedForCamp
        {
            get
            {
                VehiclePawn vehicle = Vehicle;
                return vehicle != null && vehicle.Spawned && vehicle.Faction == Faction.OfPlayer
                    && !vehicle.Drafted && !vehicle.vehiclePather.Moving;
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            VehiclePawn vehicle = Vehicle;
            if (vehicle == null || !vehicle.Spawned)
                return;

            // Pack up immediately when the vehicle is about to drive, so no bed is left under a moving vehicle.
            bool driving = vehicle.Drafted || vehicle.vehiclePather.Moving;
            if (driving && AnyDeployed)
                PackUp();

            if (!parent.IsHashIntervalTick(Props.checkIntervalTicks))
                return;

            ApplyPassengerHediff(vehicle);
            if (driving)
                return;

            if (deployEnabled && ParkedForCamp)
                Deploy(vehicle);
            else if (!deployEnabled && AnyDeployed)
                PackUp();
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            PackUp();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            PackUp();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            if (parent.Faction != Faction.OfPlayer || Props.bunks.NullOrEmpty())
                yield break;

            yield return new Command_Toggle
            {
                defaultLabel = "SleepableRV_DeployBunks".Translate(),
                defaultDesc = "SleepableRV_DeployBunksDesc".Translate(),
                icon = Props.bunks[0].uiIcon,
                isActive = () => deployEnabled,
                toggleAction = () => deployEnabled = !deployEnabled
            };
        }

        public override string CompInspectStringExtra()
        {
            if (Props.bunks.NullOrEmpty() || parent.Faction != Faction.OfPlayer)
                return null;

            int count = deployed.Count(b => b != null && b.Spawned);
            if (count > 0)
                return "SleepableRV_BunksDeployed".Translate(count, Props.bunks.Count);
            return "SleepableRV_BunksPacked".Translate();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref deployEnabled, "deployEnabled", true);
            Scribe_Collections.Look(ref deployed, "deployedBunks", LookMode.Reference);
            Scribe_Collections.Look(ref rememberedOwners, "rememberedBunkOwners", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                deployed ??= new List<Building_RVBunk>();
                rememberedOwners ??= new List<BunkOwners>();
            }
        }

        private void Deploy(VehiclePawn vehicle)
        {
            while (deployed.Count < Props.bunks.Count)
                deployed.Add(null);

            Map map = vehicle.Map;
            for (int i = 0; i < Props.bunks.Count; i++)
            {
                if (deployed[i] != null && deployed[i].Spawned)
                    continue;

                ThingDef bunkDef = Props.bunks[i];
                if (!TryFindBunkPlacement(vehicle, bunkDef, out IntVec3 center, out bool inside))
                    continue;

                Building_RVBunk bunk = (Building_RVBunk)ThingMaker.MakeThing(bunkDef);
                bunk.SetFaction(vehicle.Faction);
                bunk.Attach(vehicle, inside);
                GenSpawn.Spawn(bunk, center, map, vehicle.Rotation, WipeMode.Vanish);
                deployed[i] = bunk;
                RestoreOwners(bunk, i);
            }
        }

        private void PackUp()
        {
            for (int i = 0; i < deployed.Count; i++)
            {
                Building_RVBunk bunk = deployed[i];
                if (bunk == null || bunk.Destroyed)
                    continue;

                RememberOwners(bunk, i);
                Map map = bunk.Map;
                if (map != null)
                    SendSleepersOut(bunk, map);
                bunk.Destroy(DestroyMode.Vanish);
            }
            deployed.Clear();
        }

        private void RememberOwners(Building_RVBunk bunk, int index)
        {
            while (rememberedOwners.Count <= index)
                rememberedOwners.Add(new BunkOwners());

            List<Pawn> owners = bunk.OwnersForReading.ToList();
            rememberedOwners[index].owners = owners;

            // Unclaim quietly; destroying a bed with owners spams "lost bed assignment" messages.
            foreach (Pawn owner in owners)
                owner.ownership?.UnclaimBed();
        }

        private void RestoreOwners(Building_RVBunk bunk, int index)
        {
            if (index >= rememberedOwners.Count)
                return;

            foreach (Pawn pawn in rememberedOwners[index].owners)
            {
                // Only reclaim for pawns that haven't moved on to another bed in the meantime.
                if (pawn == null || pawn.Dead || pawn.ownership == null || pawn.ownership.OwnedBed != null)
                    continue;
                if (pawn.Faction != bunk.Faction || !bunk.AnyUnownedSleepingSlot)
                    continue;
                bunk.CompAssignableToPawn.TryAssignPawn(pawn);
            }
            rememberedOwners[index].owners.Clear();
        }

        /// <summary>Wakes anyone in the bunk and walks them out of the vehicle footprint before it drives off.</summary>
        private void SendSleepersOut(Building_RVBunk bunk, Map map)
        {
            CellRect vehicleRect = parent.OccupiedRect();
            foreach (Pawn sleeper in bunk.CurOccupants.ToList())
            {
                if (sleeper.jobs == null)
                    continue;

                if (bunk.InsideVehicle && CellFinder.TryFindRandomCellNear(sleeper.Position, map, 4,
                    c => c.Standable(map) && !vehicleRect.Contains(c), out IntVec3 exit))
                {
                    sleeper.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, exit), JobCondition.InterruptForced);
                }
                else
                {
                    sleeper.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
        }

        private bool TryFindBunkPlacement(VehiclePawn vehicle, ThingDef bunkDef, out IntVec3 center, out bool inside)
        {
            Map map = vehicle.Map;
            Rot4 rot = vehicle.Rotation;
            CellRect vehicleRect = vehicle.OccupiedRect();

            // Prefer the back of the vehicle: the camper shell / rear bedroom.
            IntVec3 rear = vehicle.Position - rot.FacingCell * (Mathf.Max(vehicleRect.Width, vehicleRect.Height) / 2);
            foreach (IntVec3 cell in vehicleRect.Cells.OrderBy(c => c.DistanceToSquared(rear)))
            {
                CellRect bunkRect = GenAdj.OccupiedRect(cell, rot, bunkDef.size);
                if (bunkRect.Cells.All(vehicleRect.Contains) && CanPlaceBunk(bunkRect, map, mustBeStandable: false))
                {
                    center = cell;
                    inside = true;
                    return true;
                }
            }

            CellRect searchRect = vehicleRect.ExpandedBy(Props.besideRadius).ClipInsideMap(map);
            foreach (IntVec3 cell in searchRect.Cells.OrderBy(c => c.DistanceToSquared(vehicle.Position)))
            {
                CellRect bunkRect = GenAdj.OccupiedRect(cell, rot, bunkDef.size);
                if (!bunkRect.Overlaps(vehicleRect) && CanPlaceBunk(bunkRect, map, mustBeStandable: true))
                {
                    center = cell;
                    inside = false;
                    return true;
                }
            }

            center = IntVec3.Invalid;
            inside = false;
            return false;
        }

        private static bool CanPlaceBunk(CellRect rect, Map map, bool mustBeStandable)
        {
            foreach (IntVec3 cell in rect)
            {
                if (!cell.InBounds(map) || !cell.Walkable(map))
                    return false;
                // Under the vehicle the cell is pass-through only (like a door or a normal bed), which is fine.
                if (mustBeStandable && !cell.Standable(map))
                    return false;
                if (cell.GetEdifice(map) != null)
                    return false;
                foreach (Thing thing in cell.GetThingList(map))
                {
                    if (thing.def.IsBlueprint || thing.def.IsFrame || thing.def.category == ThingCategory.Building)
                        return false;
                }
            }
            return true;
        }

        private void ApplyPassengerHediff(VehiclePawn vehicle)
        {
            HediffDef hediffDef = Props.passengerHediff;
            if (hediffDef == null || vehicle.AllPawnsAboard.NullOrEmpty())
                return;

            foreach (Pawn pawn in vehicle.AllPawnsAboard)
            {
                if (pawn.health?.hediffSet != null && !pawn.health.hediffSet.HasHediff(hediffDef))
                    pawn.health.AddHediff(hediffDef);
            }
        }
    }

    /// <summary>Owners of one bunk slot, kept while the bunks are packed away.</summary>
    public class BunkOwners : IExposable
    {
        public List<Pawn> owners = new List<Pawn>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref owners, "owners", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                owners ??= new List<Pawn>();
                owners.RemoveAll(p => p == null);
            }
        }
    }
}
