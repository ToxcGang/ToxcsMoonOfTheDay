using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ToxcsMoonOfTheDay.Patches;

internal static class PatchTerminalHelpers
{
    public static void AddTerminalCommand(ref Terminal terminal, SelectableLevel moonToAdd)
    {
        Plugin.Logger.LogDebug($"Adding terminal command for {moonToAdd.PlanetName}...");

        var confirmKeyword = terminal.terminalNodes.allKeywords.First(keyword => keyword.name == "Confirm");
        var denyKeyword = terminal.terminalNodes.allKeywords.First(keyword => keyword.name == "Deny");
        var routeKeyword = terminal.terminalNodes.allKeywords.First(keyword => keyword.name == "Route");

        var cancelRouteNode = routeKeyword.compatibleNouns.First().result.terminalOptions.First(opt => opt.noun == denyKeyword).result;

        var moonKeyword = ScriptableObject.CreateInstance<TerminalKeyword>();
        moonKeyword.name = GetKWNameForMoon(moonToAdd);
        moonKeyword.word = GetKWWordforMoon(moonToAdd);
        moonKeyword.defaultVerb = routeKeyword;
        moonKeyword.compatibleNouns = Array.Empty<CompatibleNoun>();

        var travelNode = ScriptableObject.CreateInstance<TerminalNode>();
        travelNode.name = GetTravelNodeNameForMoon(moonToAdd);
        travelNode.displayText = "Routing autopilot to " + moonToAdd.PlanetName + ".\n\nPlease enjoy your flight.";
        travelNode.clearPreviousText = true;
        travelNode.buyRerouteToMoon = moonToAdd.levelID;

        var travelDecisionNode = ScriptableObject.CreateInstance<TerminalNode>();
        travelDecisionNode.name = GetKWNameForMoon(moonToAdd);
        travelDecisionNode.displayText = "The company has detected a rogue planet. It might not be available for long. Do you want to go there?\n\nIt is currently [currentPlanetTime] on this moon.\n\nPlease CONFIRM or DENY.\n\n";
        travelDecisionNode.clearPreviousText = true;
        travelDecisionNode.displayPlanetInfo = moonToAdd.levelID;
        travelDecisionNode.buyRerouteToMoon = -2;
        travelDecisionNode.overrideOptions = true;
        travelDecisionNode.terminalOptions = new[]
        {
            new CompatibleNoun(denyKeyword, cancelRouteNode),
            new CompatibleNoun(confirmKeyword, travelNode)
        };

        var allKeywords = terminal.terminalNodes.allKeywords.ToList();
        allKeywords.Add(moonKeyword);
        terminal.terminalNodes.allKeywords = allKeywords.ToArray();

        var compatibleNouns = routeKeyword.compatibleNouns.ToList();
        compatibleNouns.Add(new CompatibleNoun(moonKeyword, travelDecisionNode));
        routeKeyword.compatibleNouns = compatibleNouns.ToArray();

        terminal.terminalNodes.allKeywords.First(keyword => keyword.name == "Moons").specialKeywordResult
                .displayText += "* " + moonToAdd.PlanetName + " [planetTime]\n";

        Plugin.Logger.LogDebug($"Added terminal command for {moonToAdd.PlanetName}.");
    }

    public static string GetKWNameForMoon(SelectableLevel moon)
    {
        return "KW" + moon.PlanetName.ToLower().Replace(" ", "-");
    }

    public static string GetKWWordforMoon(SelectableLevel moon)
    {
        return moon.PlanetName.ToLower().Replace(" ", "-");
    }

    public static string GetTravelNodeNameForMoon(SelectableLevel moon)
    {
        return "Travel" + moon.PlanetName.ToLower().Replace(" ", "-");
    }
}

[HarmonyPatch(typeof(Terminal))]
public class PatchTerminal
{
    [HarmonyPatch("Awake")]
    [HarmonyPostfix]
    public static void InsertMoons(Terminal __instance)
    {
        Plugin.Logger.LogDebug("Inserting moons in Terminal...");

        __instance.moonsCatalogueList = Plugin.WithCustomMoons(__instance.moonsCatalogueList);
    }

    [HarmonyPatch("Awake")]
    [HarmonyPostfix]
    public static void AddTerminalCommands(Terminal __instance)
    {
        var dailyMoon = __instance.moonsCatalogueList.First(moon => moon.PlanetName == Plugin.DailyMoonName);
        var weeklyMoon = __instance.moonsCatalogueList.First(moon => moon.PlanetName == Plugin.WeeklyMoonName);

        if (__instance.terminalNodes.allKeywords.Any(keyword =>
                keyword.name == PatchTerminalHelpers.GetKWNameForMoon(dailyMoon)))
        {
            Plugin.Logger.LogDebug("Terminal nodes have already been modified.");
            return;
        }

        Plugin.Logger.LogDebug("Modifying terminal nodes...");

        PatchTerminalHelpers.AddTerminalCommand(ref __instance, dailyMoon);
        PatchTerminalHelpers.AddTerminalCommand(ref __instance, weeklyMoon);

        __instance.terminalNodes.allKeywords.First(keyword => keyword.name == "Moons").specialKeywordResult
            .displayText += "\n";
    }
}
