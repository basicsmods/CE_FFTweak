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
            var harmony = new Harmony("basics.cefftweak");
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

    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), "CanHitCellFromCellIgnoringRange", new Type[] { typeof(Vector3), typeof(IntVec3), typeof(Thing) })]
    public class Verb_LaunchProjectileCE_CanHitCellFromCellIgnoringRange
    {
        private static bool intersectWithEpsilon(Bounds baseBounds, Vector3 baseOrigin, Vector3 baseDirection, Vector3 directionEpsilon)
        {
            Ray EpsilonRay = new Ray(baseOrigin, baseDirection + directionEpsilon);
            //Log.Message("EpsilonRay.origin " + EpsilonRay.origin);
            //Log.Message("EpsilonRay.direction " + EpsilonRay.direction);
            return baseBounds.IntersectRay(EpsilonRay);
        }

        static void Postfix(Vector3 shotSource, IntVec3 targetLoc, Thing targetThing, ref bool __result, Verb_LaunchProjectileCE __instance)
        {
            // There are still some complicated edge cases which allow for friendly-fire to happen so we have the
            // ProjectileCE_TryCollideWith patch to handle the edge cases to just override the
            // friendly-fire that would have come from that edge case being let through.
            if (__result == false) { return; }

            if (__instance.verbProps.requireLineOfSight)
            {
                if (__instance.ShooterPawn != null)
                {
                    // Calculate shot vector
                    Vector3 targetPos;
                    if (targetThing != null)
                    {
                        float shotHeight = shotSource.y;
                        __instance.AdjustShotHeight(__instance.caster, targetThing, ref shotHeight);
                        shotSource.y = shotHeight;
                        Vector3 targDrawPos = targetThing.DrawPos;
                        targetPos = new Vector3(targDrawPos.x, new CollisionVertical(targetThing).Max, targDrawPos.z);
                        var targPawn = targetThing as Pawn;
                        if (targPawn != null)
                        {
                            targetPos += targPawn.Drawer.leaner.LeanOffset * 0.6f;
                        }
                    }
                    else
                    {
                        targetPos = targetLoc.ToVector3Shifted();
                    }
                    Ray shotLine = new Ray(shotSource, (targetPos - shotSource));
                    // Maybe keep a cache of leaning pawns that you refresh only once every 10 ticks or something
                    // so that we're not looping through every pawn on the map so frequently.
                    //Log.Message("Checking shot by " + ShooterPawn.Name?.ToStringFull);
                    //Log.Message("origin " + shotLine.origin);
                    //Log.Message("direction " + shotLine.direction);
                    //Log.ResetMessageCount();
                    //int curTick = Find.TickManager.TicksGame;
                    foreach (Pawn pawn in __instance.ShooterPawn.Map.mapPawns.AllPawnsSpawned)
                    {
                        // There seems to be an issue where when the front pawn goes to un-lean (and so the back pawn can shoot)
                        // the back pawn is able to get a shot off before the front pawn finishes their un-lean animation and
                        // they are vulnerable during this time.
                        // Can potentially work around this by maintaining the last 10 ticks worth of lean locations for a pawn
                        // and then deciding whether we can fire based on whether both the current tick and 10 ticks ago are safe.
                        //Log.Message("Looking at " + pawn.Name);
                        if (__instance.ShooterPawn != pawn && !__instance.ShooterPawn.Faction.HostileTo(pawn.Faction) && pawn.Drawer?.leaner != null && !pawn.Drawer.leaner.LeanOffset.Equals(Vector3.zero))
                        {
                            Bounds bounds = CE_Utility.GetBoundsFor(pawn);
                            //bounds.size = bounds.size * 1.9f;
                            //bounds.center = bounds.center + pawn.Drawer.leaner.LeanOffset;
                            //Log.Message("bounds.center: " + bounds.center);
                            //Log.Message("bounds.size: " + bounds.size);
                            // For some reason, sometimes the origin y value is 0.9 which ends up
                            // letting the shooter take the shot when they actually shouldn't.
                            // Clamp it to 0.7 which is a reasonable height for this check.
                            Vector3 NewOrigin = shotLine.origin;
                            if (NewOrigin.y > 0.7f)
                            {
                                NewOrigin.y = 0.7f;
                            }
                            if (intersectWithEpsilon(bounds, NewOrigin, shotLine.direction, new Vector3(0f, 0f, 0f)) ||
                                intersectWithEpsilon(bounds, NewOrigin, shotLine.direction, new Vector3(0.1f, 0f, 0f)) ||
                                intersectWithEpsilon(bounds, NewOrigin, shotLine.direction, new Vector3(-0.1f, 0f, 0f)) ||
                                intersectWithEpsilon(bounds, NewOrigin, shotLine.direction, new Vector3(0f, 0f, 0.1f)) ||
                                intersectWithEpsilon(bounds, NewOrigin, shotLine.direction, new Vector3(0f, 0f, -0.1f)) ||
                                intersectWithEpsilon(bounds, NewOrigin, shotLine.direction, new Vector3(0.2f, 0f, 0f)) ||
                                intersectWithEpsilon(bounds, NewOrigin, shotLine.direction, new Vector3(-0.2f, 0f, 0f)) ||
                                intersectWithEpsilon(bounds, NewOrigin, shotLine.direction, new Vector3(0f, 0f, 0.2f)) ||
                                intersectWithEpsilon(bounds, NewOrigin, shotLine.direction, new Vector3(0f, 0f, -0.2f)))
                            {
                                //Log.Message("Pawn " + pawn.Name + " in the way of pawn " + ShooterPawn.Name);
                                __result = false;
                                return;
                            }
                        }
                    }
                }
            }
            return;
        }
    }
}
