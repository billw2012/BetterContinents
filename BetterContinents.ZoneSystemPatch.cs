using HarmonyLib;
using UnityEngine;

namespace BetterContinents
{
    public partial class BetterContinents
    {
        // Changes to location type spawn placement (this is the functional part of the mod)
        [HarmonyPatch(typeof(ZoneSystem))]
        private class ZoneSystemPatch
        {
            private delegate void RegisterLocationDelegate(ZoneSystem instance, ZoneSystem.ZoneLocation location, Vector3 pos, bool generated);
            private static readonly RegisterLocationDelegate RegisterLocation = GetDelegate<RegisterLocationDelegate>(typeof(ZoneSystem), "RegisterLocation");

            // [HarmonyPostfix, HarmonyPatch(nameof(ZoneSystem.ValidateVegetation))]
            // private static void ValidateVegetationPostfix(ZoneSystem __instance)
            // {
            // }
            [HarmonyPrefix, HarmonyPatch(nameof(ZoneSystem.GenerateLocations), typeof(ZoneSystem.ZoneLocation))]
            private static bool GenerateLocationsPrefix(ZoneSystem __instance, ZoneSystem.ZoneLocation location)
            {
                var groupName = string.IsNullOrEmpty(location.m_group) ? "<unnamed>" : location.m_group;
                Log($"Generating location of group {groupName}, required {location.m_quantity}, unique {location.m_unique}, name {location.m_prefabName}");
                if (Settings.EnabledForThisWorld)
                {
                    if (Settings.UseSpawnmap)
                    {
                        // Place all locations specified by the spawn map, ignoring counts specified in the prefab
                        int placed = 0;
                        foreach (var normalizedPosition in Settings.GetAllSpawns(location.m_prefabName))
                        {
                            var worldPos = NormalizedToWorld(normalizedPosition);
                            var position = new Vector3(
                                worldPos.x,
                                WorldGenerator.instance.GetHeight(worldPos.x, worldPos.y),
                                worldPos.y
                            );
                            RegisterLocation(__instance, location, position, false);
                            Log($"Position of {location.m_prefabName} ({++placed}/{location.m_quantity}) overriden: set to {position}");
                        }

                        // The vanilla placement algorithm considers already placed zones, but we can early out here anyway if we place them all
                        // (this is required in the case of the StartTemple as we don't want to place it twice if OverrideStartPosition is specified) 
                        if (placed >= location.m_quantity)
                        {
                            return false;
                        }
                    }
                    
                    if (Settings.OverrideStartPosition && location.m_prefabName == "StartTemple")
                    {
                        var position = new Vector3(
                            Settings.StartPositionX,
                            WorldGenerator.instance.GetHeight(Settings.StartPositionX, Settings.StartPositionY),
                            Settings.StartPositionY
                        );
                        RegisterLocation(__instance, location, position, false);
                        Log($"Start position overriden: set to {position}");
                        return false;
                    }
                    
                    if (location.m_prefabName != "StartTemple" && ConfigDebugSkipDefaultLocationPlacement.Value)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }
}