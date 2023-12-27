using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Management.Instrumentation;
using System.Reflection.Emit;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.UIElements;
using Verse;
using Verse.AI.Group;
using Verse.Noise;
using static HarmonyLib.Code;
using static RimWorld.MechClusterSketch;
using CombatExtended;

namespace CE_FFTweak
{
    [StaticConstructorOnStartup]
    public static class Main
    {
        static Main()
        {
            var harmony = new Harmony("basics.ce_fftweak");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(ProjectileCE), "TryCollideWith", new Type[] { typeof(Thing) })]
    public class ProjectileCE_TryCollideWith
    {
        static bool Prefix(Thing thing, ref bool __result, ProjectileCE __instance)
        {
            if (thing != __instance.intendedTarget.Thing && __instance.launcher is Pawn && thing is Pawn)
            {
                var launcherPawn = __instance.launcher as Pawn;
                var thingPawn = thing as Pawn;
                //Log.Message("launcher: " + __instance.launcher.Label);
                //Log.Message("TryCollideWith " + thing.Label);
                //Log.Message("launcher position: " + thingPawn.Position);
                //Log.Message("pawn position: " + launcherPawn.Position);
                if (launcherPawn != null && thingPawn != null)
                {
                    if (!launcherPawn.HostileTo(thingPawn))
                    {
                        //var x_diff = Math.Abs(thingPawn.Position.x - thing.Position.x);
                        //var y_diff = Math.Abs(thingPawn.Position.y - thing.Position.y);
                        //var z_diff = Math.Abs(thingPawn.Position.z - thing.Position.z);
                        //if (x_diff + z_diff + y_diff < 3)
                        //{
                        //    Log.Message("Skipping friendly-fire due to pawns being close enough.");
                        //    return false;
                        //}
                        var x_diff = thingPawn.Position.x - thing.Position.x;
                        var z_diff = thingPawn.Position.z - thing.Position.z;
                        if (x_diff == 0 || z_diff == 0)
                        {
                            //Log.Message("Ignoring friendly-fire since these pawns must be leaning and it's dumb they'd shoot into a leaning ally.");
                            __result = false;
                            return false;
                        }
                    }
                }
            }
            return true;
        }
    }
}
