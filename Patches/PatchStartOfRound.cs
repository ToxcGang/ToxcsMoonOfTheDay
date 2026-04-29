using System.Linq;
using HarmonyLib;

namespace ToxcsMoonOfTheDay.Patches;

[HarmonyPatch(typeof(StartOfRound))]
public class PatchStartOfRound
{
    [HarmonyPatch("ChangeLevel")]
    [HarmonyPrefix]
    public static void ResolveCustomRoute(StartOfRound __instance, ref int levelID)
    {
        Plugin.TryResolveCustomRoute(__instance, ref levelID);
    }

    [HarmonyPatch("StartGame")]
    [HarmonyPrefix]
    public static void SetSeed(StartOfRound __instance)
    {
        if (__instance.currentLevel == null) return;
        Plugin.SetSeedForActiveRoute(__instance);
    }

    [HarmonyPatch("Awake")]
    [HarmonyPostfix]
    public static void InsertMoons(StartOfRound __instance)
    {
        Plugin.Logger.LogDebug("Inserting moons in StartOfRound...");

        var startOfRound = StartOfRound.Instance;
        startOfRound.levels = Plugin.WithCustomMoons(startOfRound.levels);
    }
}
