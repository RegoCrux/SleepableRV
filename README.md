# Sleepable RVs

A RimWorld 1.6 mod that adds two recreational vehicles for [Vehicle Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3014915404). When an RV is parked, it sets up real beds that colonists claim, sleep in and use for lovin' on their own, just like any other bed.

**Only dependency:** Vehicle Framework (it brings Harmony with it).

## Vehicles

| | Truck camper | Motorhome |
|---|---|---|
| Inspired by | Pickup with a slide-in camper (e.g. Palomino 1250) | Class A motorhome |
| Footprint | 2 × 5 | 2 × 7 |
| Seats | Driver, passenger, 2 in the camper | Driver, co-pilot, 4 in the lounge |
| Beds when parked | 1 double (sleeps 2) | 2 doubles (sleeps 4) |
| World map | Good off-road (0.85), good in snow, decent on roads | Fast on roads (0.25), poor off-road (2.0), slow in hills and snow |
| Research | Truck campers (Machining + Complex furniture) | Motorhomes (Truck campers + Electricity) |
| Cost | 320 steel, 8 components, 60 cloth, 40 wood | 550 steel, 14 components, 100 cloth, 80 wood |

Both vehicles are built from the Vehicles build tab and run on chemfuel.

## How the beds work

- **Parked** (not drafted, not moving, player-owned): the RV spawns its beds (`SleepableRV_DoubleBunk`). They go under the vehicle footprint, rear first. A bed that doesn't fit there is laid out beside the vehicle (up to 3 cells away).
- **Driving**: as soon as the RV is drafted or moves, the beds are packed away. Anyone asleep is woken and walks out. Owners are remembered and get the same bed back at the next stop, unless they've claimed another bed in the meantime.
- **Gizmo**: "Set up beds when parked" turns this off for a vehicle.
- **Beds inside the RV** (CampingTent-style shelter):
  - The bed itself isn't drawn, and neither are its sleepers, so they look like they're inside.
  - No *slept outside* or *slept on ground* thought, and rain doesn't make sleepers soaking wet.
  - Sleepers get the *RV climate control* hediff (comfortable temperature range −25 °C / +15 °C). *Slept in cold/heat* is judged against that range.
  - Sleepers get the *slept in the RV* mood bonus (+3).
- **Beds beside the RV** are plain bedrolls: no *slept on ground* thought, but no other shelter.
- **Riding inside**: pawns aboard also get the climate hediff. VF's per-seat comfort still applies.
- To assign owners to a bed under the vehicle, click the RV again to cycle the selection to the bed.

## Layout

```
SleepableRV/
├── About/About.xml
├── Assemblies/SleepableRV.dll          # build output
├── Defs/
│   ├── SleepableRV_Base.xml            # research, abstract vehicle bases
│   ├── SleepableRV_Shelter.xml         # bed, climate hediff, thought
│   └── VehicleDefs/
│       ├── SleepableRV_TruckCamper.xml # vehicle + blueprint
│       └── SleepableRV_Motorhome.xml
├── Languages/English/Keyed/SleepableRV.xml
├── Source/                             # C# (net48)
│   ├── CompRVBunks.cs                  # vehicle comp: deploy/pack beds, owner memory, passenger hediff
│   ├── Building_RVBunk.cs              # the bed (Building_Bed subclass)
│   ├── RVBunkExtension.cs              # shelter settings (like CampingTent's TentModExtension)
│   ├── HediffComp_RVClimate.cs         # removes the climate hediff once out of the RV
│   └── HarmonyPatches.cs               # bed thoughts, rain, hiding sleepers under the vehicle
└── Textures/Things/Vehicles/SleepableRV/<Vehicle>/   # _north/_east/_south + "m" masks
```

## Building

Requires the .NET SDK. Paths default to a standard Steam install and can be overridden:

```
dotnet build Source/SleepableRV.csproj -c Release
dotnet build Source/SleepableRV.csproj -c Release -p:SteamAppsDir="D:\SteamLibrary\steamapps"
```

The project compiles against `RimWorldWin64_Data\Managed`, Vehicle Framework (workshop 3014915404) and Harmony (workshop 2009463077), and writes `Assemblies\SleepableRV.dll`.

## Textures

The current textures are generated placeholders (128 px per cell, north = front up; east is north rotated; west is mirrored from east by VF). In the `m` mask images, red/green/blue select VF paint colours 1/2/3. Replace them with real art under the same names.

## Known gaps / ideas

- No engine sound: VF ships none and the mod doesn't depend on VVE. Add an `.ogg` and a `SoundDef` to `soundSustainersOnEvent` if wanted.
- Caravan beds on the world map aren't added yet. VF's seat comfort and caravan rest still apply.
- Gas, toxic fallout and similar weather effects are not blocked for sleepers inside, only rain.
