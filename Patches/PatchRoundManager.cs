using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using Random = System.Random;

#pragma warning disable Harmony003

namespace ToxcsMoonOfTheDay.Patches;

public static class PatchRoundManagerHelpers
{
    public static Vector3 SeededInsideUnitSphere(ref Random random)
    {
        Vector3 point;
        
        do
        {
            point = new Vector3((float)(random.NextDouble() * 2 - 1),
                                (float)(random.NextDouble() * 2 - 1),
                                (float)(random.NextDouble() * 2 - 1));
        } while (point.sqrMagnitude > 1f);
        
        return point;
    }
}

[HarmonyPatch(typeof(RoundManager))]
public class PatchRoundManager
{
    [HarmonyPatch("GetRandomNavMeshPositionInRadiusSpherical")]
    [HarmonyPrefix]
    public static bool GetRandomNavMeshPositionInRadiusSpherical(ref RoundManager __instance, ref Vector3 __result,
        Vector3 pos, float radius = 10f, NavMeshHit navHit = default)
    {
        var targetPosition = PatchRoundManagerHelpers.SeededInsideUnitSphere(ref __instance.LevelRandom) * radius + pos;
        __result = NavMesh.SamplePosition(targetPosition, out var hit, radius, -1) ? hit.position : targetPosition;
        return false;
    }

    [HarmonyPatch("GetRandomNavMeshPositionInRadius")]
    [HarmonyPrefix]
    public static bool GetRandomNavMeshPositionInRadius(ref RoundManager __instance, ref Vector3 __result, Vector3 pos,
        float radius = 10f, NavMeshHit navHit = default)
    {
        var originalY = pos[1];
        var targetPosition = PatchRoundManagerHelpers.SeededInsideUnitSphere(ref __instance.LevelRandom) * radius + pos;
        targetPosition[1] = originalY;
        if (NavMesh.SamplePosition(targetPosition, out var hit, radius, -1))
        {
            __result = hit.position;
            return false;
        }

        Debug.Log("Unable to get random nav mesh position in radius! Returning old pos");
        __result = targetPosition;
        return false;
    }
}
