using System.Collections.Generic;
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

            public static int TagLocationZDOs = 0;
            public static int TagVegetationZDOs = 0;
            
            [HarmonyPrefix, HarmonyPatch(nameof(ZoneSystem.SpawnLocation))]
            private static void SpawnLocationPrefix()
            {
                ++TagLocationZDOs;
            }
            [HarmonyFinalizer, HarmonyPatch(nameof(ZoneSystem.SpawnLocation))]
            private static void SpawnLocationFinalizer()
            {
	            --TagLocationZDOs;
            }
                        
            [HarmonyPrefix, HarmonyPatch(nameof(ZoneSystem.PlaceVegetation))]
            private static void PlaceVegetationPrefix()
            {
	            ++TagVegetationZDOs;
            }
            [HarmonyFinalizer, HarmonyPatch(nameof(ZoneSystem.PlaceVegetation))]
            private static void PlaceVegetationFinalizer()
            {
	            --TagVegetationZDOs;
            }
            
   //          [HarmonyPrefix, HarmonyPatch(nameof(ZoneSystem.SpawnLocation))]
   //          private static bool SpawnLocation(ZoneSystem __instance, ZoneSystem.ZoneLocation location, int seed, Vector3 pos, Quaternion rot, ZoneSystem.SpawnMode mode, List<GameObject> spawnedGhostObjects, ref GameObject __result)
			// {
			// 	// Only enable this behaviour if BC is enabled.
			// 	SpawningLocation = Settings.EnabledForThisWorld;
   //
			// 	Vector3 position = location.m_prefab.transform.position;
			// 	Quaternion lhs = Quaternion.Inverse(location.m_prefab.transform.rotation);
			// 	UnityEngine.Random.InitState(seed);
			// 	if (mode == ZoneSystem.SpawnMode.Full || mode == ZoneSystem.SpawnMode.Ghost)
			// 	{
			// 		foreach (ZNetView znetView in location.m_netViews)
			// 		{
			// 			znetView.gameObject.SetActive(true);
			// 		}
			// 		foreach (RandomSpawn randomSpawn in location.m_randomSpawns)
			// 		{
			// 			randomSpawn.Randomize();
			// 		}
			// 		WearNTear.m_randomInitialDamage = location.m_location.m_applyRandomDamage;
			// 		foreach (ZNetView znetView2 in location.m_netViews)
			// 		{
			// 			if (znetView2.gameObject.activeSelf)
			// 			{
			// 				Vector3 point = znetView2.gameObject.transform.position - position;
			// 				Vector3 position2 = pos + rot * point;
			// 				Quaternion rhs = lhs * znetView2.gameObject.transform.rotation;
			// 				Quaternion rotation = rot * rhs;
			// 				if (mode == ZoneSystem.SpawnMode.Ghost)
			// 				{
			// 					ZNetView.StartGhostInit();
			// 				}
			// 				GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(znetView2.gameObject, position2, rotation);
			// 				gameObject.GetComponent<ZNetView>().GetZDO().SetPGWVersion(__instance.m_pgwVersion);
			// 				DungeonGenerator component = gameObject.GetComponent<DungeonGenerator>();
			// 				if (component)
			// 				{
			// 					component.Generate(mode);
			// 				}
			// 				if (mode == ZoneSystem.SpawnMode.Ghost)
			// 				{
			// 					spawnedGhostObjects.Add(gameObject);
			// 					ZNetView.FinishGhostInit();
			// 				}
			// 			}
			// 		}
			// 		WearNTear.m_randomInitialDamage = false;
			// 		__instance.CreateLocationProxy(location, seed, pos, rot, mode, spawnedGhostObjects);
			// 		SnapToGround.SnappAll();
			// 		__result = null;
			// 		return false;
			// 	}
			// 	foreach (RandomSpawn randomSpawn2 in location.m_randomSpawns)
			// 	{
			// 		randomSpawn2.Randomize();
			// 	}
			// 	foreach (ZNetView znetView3 in location.m_netViews)
			// 	{
			// 		znetView3.gameObject.SetActive(false);
			// 	}
			// 	__result = UnityEngine.Object.Instantiate<GameObject>(location.m_prefab, pos, rot);
			// 	__result.SetActive(true);
			// 	SnapToGround.SnappAll();
			// 	return false;
			// }
            

        }
        
        [HarmonyPatch(typeof(CreatureSpawner))]
        private class CreatureSpawnerPatch
        {
	        public static int TagSpawnZDOs = 0;

	        [HarmonyPrefix, HarmonyPatch(nameof(CreatureSpawner.Spawn))]
	        private static void SpawnPrefix()
	        {
		        ++TagSpawnZDOs;
	        }
	        [HarmonyFinalizer, HarmonyPatch(nameof(CreatureSpawner.Spawn))]
	        private static void SpawnFinalizer()
	        {
		        --TagSpawnZDOs;
	        }
        }

        [HarmonyPatch(typeof(ZDOMan))]
        private class ZDOManPatch
        {
            [HarmonyPostfix, HarmonyPatch(nameof(ZDOMan.CreateNewZDO), typeof(ZDOID), typeof(Vector3))]
            private static void CreateNewZDOPostfix(ref ZDO __result)
            {
	            if (BetterContinents.Settings.EnabledForThisWorld && ZoneSystemPatch.TagLocationZDOs > 0)
	            {
		            __result.Set("bc_loc", 1);
	            }
	            if (BetterContinents.Settings.EnabledForThisWorld && ZoneSystemPatch.TagVegetationZDOs > 0)
	            {
		            __result.Set("bc_veg", 1);
	            }
	            if (BetterContinents.Settings.EnabledForThisWorld && CreatureSpawnerPatch.TagSpawnZDOs > 0)
	            {
		            __result.Set("bc_spawn", 1);
	            }
            }
        }
    }
}