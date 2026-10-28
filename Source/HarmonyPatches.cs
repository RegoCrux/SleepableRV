using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SleepableRV
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            new Harmony("SleepableRV").PatchAll();
        }
    }

    /// <summary>
    /// Mirrors CampingTent: sleeping in an RV bunk doesn't count as sleeping outside or on the ground,
    /// and the RV's climate control decides whether the pawn slept cold or hot.
    /// </summary>
    [HarmonyPatch(typeof(Toils_LayDown), "ApplyBedThoughts")]
    public static class Patch_Toils_LayDown_ApplyBedThoughts
    {
        public static void Postfix(Pawn actor, Building_Bed bed)
        {
            if (!(bed is Building_RVBunk bunk) || actor.needs?.mood == null)
                return;

            MemoryThoughtHandler memories = actor.needs.mood.thoughts.memories;
            RVBunkExtension ext = bunk.Extension;
            if (ext == null)
                return;

            if (ext.negateSleptOnGround)
                memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptOnGround);

            if (!bunk.InsideVehicle)
                return;

            if (ext.negateSleptOutside)
                memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptOutside);

            if (ext.negateTemperatureThoughts)
            {
                // Vanilla checks the race's base comfort range; use the pawn's actual range, which
                // includes the RV climate hediff.
                float temperature = actor.AmbientTemperature;
                if (temperature >= actor.GetStatValue(StatDefOf.ComfyTemperatureMin))
                    memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInCold);
                if (temperature <= actor.GetStatValue(StatDefOf.ComfyTemperatureMax))
                    memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInHeat);
            }

            if (ext.sleptThought != null)
                memories.TryGainMemory(ext.sleptThought);
        }
    }

    /// <summary>The RV roof keeps sleepers dry even though the cell under the vehicle is unroofed.</summary>
    [HarmonyPatch(typeof(Pawn_MindState), nameof(Pawn_MindState.MindStateTickInterval))]
    public static class Patch_Pawn_MindState_MindStateTickInterval
    {
        public static void Postfix(Pawn_MindState __instance, int delta)
        {
            Pawn pawn = __instance.pawn;
            if (pawn == null || !pawn.Spawned || pawn.needs?.mood == null || !pawn.IsHashIntervalTick(120, delta))
                return;
            if (!(pawn.CurrentBed() is Building_RVBunk { InsideVehicle: true } bunk) || bunk.Extension?.negateWet != true)
                return;

            ThoughtDef weatherThought = pawn.Map.weatherManager.CurWeatherLerped.weatherThought;
            if (weatherThought != null)
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(weatherThought);
        }
    }

    /// <summary>Pawns sleeping in a bunk under the vehicle are inside it, so don't draw them on top of it.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DynamicDrawPhaseAt))]
    public static class Patch_Pawn_DynamicDrawPhaseAt
    {
        public static bool Prefix(Pawn __instance)
        {
            return !(__instance.CurrentBed() is Building_RVBunk { InsideVehicle: true });
        }
    }
}
