using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Random = System.Random;

namespace ToxcsMoonOfTheDay;


public static class PluginInfo
{
    public const string PLUGIN_GUID = "Toxc.MoonOfTheDay";
    public const string PLUGIN_NAME = "ToxcsMoonOfTheDay";
    public const string PLUGIN_VERSION = "1.0.0";
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string DailyMoonName = "Daily Moon";
    public const string WeeklyMoonName = "Weekly Moon";
    internal new static ManualLogSource Logger;

    private Harmony _harmony;
    private bool _isPatched;
    private static readonly Dictionary<int, CustomRoute> CustomRoutes = new();
    private static readonly Dictionary<SelectableLevel, WeatherState> OriginalWeatherStates = new();
    private static SeedMode _activeSeedMode = SeedMode.None;
    public static Plugin Instance { get; private set; }

    private enum SeedMode
    {
        None,
        Daily,
        Weekly
    }

    private readonly struct CustomRoute
    {
        public CustomRoute(int sourceIndex, LevelWeatherType weatherType, SeedMode seedMode)
        {
            SourceIndex = sourceIndex;
            WeatherType = weatherType;
            SeedMode = seedMode;
        }

        public int SourceIndex { get; }
        public LevelWeatherType WeatherType { get; }
        public SeedMode SeedMode { get; }
    }

    private readonly struct WeatherState
    {
        public WeatherState(bool overrideWeather, LevelWeatherType overrideWeatherType, LevelWeatherType currentWeather,
            RandomWeatherWithVariables[] randomWeathers)
        {
            OverrideWeather = overrideWeather;
            OverrideWeatherType = overrideWeatherType;
            CurrentWeather = currentWeather;
            RandomWeathers = randomWeathers;
        }

        public bool OverrideWeather { get; }
        public LevelWeatherType OverrideWeatherType { get; }
        public LevelWeatherType CurrentWeather { get; }
        public RandomWeatherWithVariables[] RandomWeathers { get; }
    }

    private void Awake()
    {
        
        Instance = this;

        
        Logger = base.Logger;

        
        PatchAll();

        
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} (v{PluginInfo.PLUGIN_VERSION}) is loaded!");
    }

    public void PatchAll()
    {
        if (_isPatched)
        {
            Logger.LogWarning("Already patched!");
            return;
        }

        Logger.LogDebug("Patching...");

        _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        _harmony.PatchAll();
        _isPatched = true;

        Logger.LogDebug("Patched!");
    }

    public void UnpatchAll()
    {
        if (!_isPatched)
        {
            Logger.LogWarning("Not patched!");
            return;
        }

        Logger.LogDebug("Unpatching...");

        _harmony.UnpatchSelf();
        _isPatched = false;

        Logger.LogDebug("Unpatched!");
    }

    
    
    
    
    public static int GetDailySeed()
    {
        return (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalDays;
    }

    
    
    
    
    public static int GetWeeklySeed()
    {
        var day = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalDays;
        return (int)Math.Floor((float)day / 7);
    }

    public static bool IsCustomMoon(SelectableLevel moon)
    {
        return moon.PlanetName is DailyMoonName or WeeklyMoonName;
    }

    private static SelectableLevel GetCustomMoon(SelectableLevel[] moons, int levelID, bool isDailyElseWeekly = true)
    {
        var random = new Random(isDailyElseWeekly ? GetDailySeed() : GetWeeklySeed());

        var moonsWithoutCompany = moons.Where(moon => moon.levelID != 3 && !IsCustomMoon(moon)).ToArray();
        var sourceMoon = moonsWithoutCompany.OrderBy(m => m.levelID).ToArray()[random.Next(0, moonsWithoutCompany.Length)];
        var sourceIndex = Array.IndexOf(moons, sourceMoon);

        var moon = Instantiate(sourceMoon);

        moon.name = isDailyElseWeekly ? DailyMoonName : WeeklyMoonName;
        moon.PlanetName = isDailyElseWeekly ? DailyMoonName : WeeklyMoonName;
        moon.LevelDescription = "This moon looks familiar...";
        moon.riskLevel = "???";
        moon.levelID = levelID;

        
        moon.randomWeathers = Array.Empty<RandomWeatherWithVariables>();
        moon.overrideWeather = true;

        var weatherOverride = Enum.GetValues(typeof(LevelWeatherType)).Cast<LevelWeatherType>().ToArray()[
            random.Next(0, Enum.GetValues(typeof(LevelWeatherType)).Length)];

        moon.overrideWeatherType = weatherOverride;
        moon.currentWeather = weatherOverride;
        CustomRoutes[levelID] = new CustomRoute(sourceIndex, weatherOverride, isDailyElseWeekly ? SeedMode.Daily : SeedMode.Weekly);

        Logger.LogDebug($"{moon.PlanetName} route index {levelID} maps to {sourceMoon.PlanetName} at index {sourceIndex} with {weatherOverride} weather.");

        return moon;
    }

    public static SelectableLevel[] WithCustomMoons(SelectableLevel[] moons)
    {
        var baseMoons = moons.Where(moon => !IsCustomMoon(moon)).ToList();
        var dailyMoon = GetCustomMoon(baseMoons.ToArray(), baseMoons.Count);
        var weeklyMoon = GetCustomMoon(baseMoons.ToArray(), baseMoons.Count + 1, false);

        baseMoons.Add(dailyMoon);
        baseMoons.Add(weeklyMoon);

        Logger.LogDebug($"Daily moon level index: {dailyMoon.levelID}; weekly moon level index: {weeklyMoon.levelID}");

        return baseMoons.ToArray();
    }

    public static bool TryResolveCustomRoute(StartOfRound startOfRound, ref int levelID)
    {
        RestoreWeatherState(startOfRound);

        if (!CustomRoutes.TryGetValue(levelID, out var route))
        {
            _activeSeedMode = SeedMode.None;
            return false;
        }

        _activeSeedMode = route.SeedMode;
        levelID = route.SourceIndex;

        if (route.SourceIndex < 0 || route.SourceIndex >= startOfRound.levels.Length)
        {
            Logger.LogError($"Custom route resolved to invalid level index {route.SourceIndex}.");
            return false;
        }

        ApplyWeatherOverride(startOfRound.levels[route.SourceIndex], route.WeatherType);
        Logger.LogDebug($"Resolved custom route to {startOfRound.levels[route.SourceIndex].PlanetName} at index {route.SourceIndex}.");
        return true;
    }

    public static void SetSeedForActiveRoute(StartOfRound startOfRound)
    {
        switch (_activeSeedMode)
        {
            case SeedMode.Daily:
                Logger.LogDebug($"Setting daily seed for {startOfRound.currentLevel.PlanetName}...");
                startOfRound.overrideRandomSeed = true;
                startOfRound.overrideSeedNumber = GetDailySeed();
                break;
            case SeedMode.Weekly:
                Logger.LogDebug($"Setting weekly seed for {startOfRound.currentLevel.PlanetName}...");
                startOfRound.overrideRandomSeed = true;
                startOfRound.overrideSeedNumber = GetWeeklySeed();
                break;
            default:
                if (startOfRound.currentLevel != null)
                {
                    Logger.LogDebug($"Not setting seed for {startOfRound.currentLevel.PlanetName}...");
                }

                startOfRound.overrideRandomSeed = false;
                break;
        }
    }

    private static void ApplyWeatherOverride(SelectableLevel level, LevelWeatherType weatherType)
    {
        if (!OriginalWeatherStates.ContainsKey(level))
        {
            OriginalWeatherStates[level] = new WeatherState(level.overrideWeather, level.overrideWeatherType,
                level.currentWeather, level.randomWeathers);
        }

        level.randomWeathers = Array.Empty<RandomWeatherWithVariables>();
        level.overrideWeather = true;
        level.overrideWeatherType = weatherType;
        level.currentWeather = weatherType;
    }

    private static void RestoreWeatherState(StartOfRound startOfRound)
    {
        foreach (var pair in OriginalWeatherStates.ToArray())
        {
            if (!startOfRound.levels.Contains(pair.Key))
            {
                OriginalWeatherStates.Remove(pair.Key);
                continue;
            }

            pair.Key.overrideWeather = pair.Value.OverrideWeather;
            pair.Key.overrideWeatherType = pair.Value.OverrideWeatherType;
            pair.Key.currentWeather = pair.Value.CurrentWeather;
            pair.Key.randomWeathers = pair.Value.RandomWeathers;
            OriginalWeatherStates.Remove(pair.Key);
        }
    }
}
