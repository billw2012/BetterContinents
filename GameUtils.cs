using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BetterContinents
{
    public static class GameUtils
    {
        private static void RegenerateHeightmaps()
        {
            var sw = new Stopwatch();
            sw.Start();
            
            // Regenerate all heightmaps            
            foreach (var heightmap in Resources.FindObjectsOfTypeAll<Heightmap>())
            {
                heightmap.CancelInvoke();
                heightmap.Clear();
                heightmap.Regenerate();
            }
            BetterContinents.Log($"Regenerating heightmaps took {sw.ElapsedMilliseconds} ms");
        }

        private static Dictionary<ZDOID, ZDO> GetObjectsByID() =>
            (Dictionary<ZDOID, ZDO>) AccessTools.Field(typeof(ZDOMan), "m_objectsByID").GetValue(ZDOMan.instance);

        private static Dictionary<ZDO, float> zdos;
        private static Dictionary<ZNetView, float> zNetViews;
        private static Dictionary<LocationProxy, float> locations;
            
        public static void BeginHeightChanges()
        {
            // Get all the ZoneVegetation
            var allVegetation = new List<ZoneSystem.ZoneVegetation>();
            ZoneSystem.instance.GetVegetation((Heightmap.Biome) 0x7fffffff, allVegetation);

            // Get the prefabs of all Spawners
            var allSpawns = Object.FindObjectsOfType<SpawnSystem>()
                .SelectMany(s => s.m_spawners)
                .Select(s => s.m_prefab);

            // Convert the prefabs of Vegetation and Spawners into their hash ids, which is what the ZDOs save
            var prefabIds = allVegetation
                .Select(v => v.m_prefab)
                .Concat(allSpawns)
                .Select(p => p?.GetComponentInChildren<ZNetView>()?.GetPrefabName()?.GetStableHashCode())
                .Where(h => h != null)
                .Select(h => h.Value)
                .ToHashSet();
            
            // Stop and reset the heightmap generator first
            HeightmapBuilder.instance.Dispose();
            var _ = new HeightmapBuilder();

            // Collect all ZDOs of types we want to update in the world, we won't modify others
            zdos = GetObjectsByID().Values
                .Where(z => prefabIds.Contains(z.GetPrefab()))
                .ToDictionary(zdo => zdo, zdo =>
                {
                    var pos = zdo.GetPosition();
                    return pos.y - WorldGenerator.instance.GetHeight(pos.x, pos.z);
                });
            
            // locations = Resources.FindObjectsOfTypeAll<LocationProxy>().ToDictionary(tm => tm, tm =>
            // {
            //     var pos = tm.transform.position;
            //     var terrainHeight = WorldGenerator.instance.GetHeight(pos.x, pos.z);
            //     var height = pos.y - terrainHeight;
            //     BetterContinents.Log($"Found location {tm} at position {pos} terrainHeight {terrainHeight} height {height}");
            //     return height;
            // });
            
            // Get vegetation zNetViews only (or ones without zdos, which are just clutter)
            zNetViews = Resources.FindObjectsOfTypeAll<ZNetView>()
                .Where(z => z.GetZDO() == null || prefabIds.Contains(z.GetZDO().GetPrefab()))
                .ToDictionary(z => z, z =>
                {
                    var pos = z.transform.position;
                    if (Heightmap.GetHeight(pos, out float height))
                    {
                        return pos.y - height;
                    }
                    else
                    {
                        return pos.y - WorldGenerator.instance.GetHeight(pos.x, pos.z);    
                    }
                });
            
            BetterContinents.WorldGeneratorPatch.DisableCache();
        }

        public static void EndHeightChanges()
        {
            BetterContinents.WorldGeneratorPatch.EnableCache();
            
            // foreach (var kv in locations)
            // {
            //     var lp = kv.Key;
            //     var pos = lp.transform.position;
            //     var terrainHeight = WorldGenerator.instance.GetHeight(pos.x, pos.z);
            //     pos.y = terrainHeight + kv.Value;
            //     lp.transform.position = pos;
            //     BetterContinents.Log($"Updated location {lp} to position {pos} terrainHeight {terrainHeight}");
            //     foreach (var z in lp.GetComponentsInChildren<ZNetView>().Where(z => z.GetZDO() != null))
            //     {
            //         z.GetZDO().SetPosition(z.transform.position);
            //         zdos.Remove(z.GetZDO());
            //         BetterContinents.Log($"Updated location {lp} ZDO {z}");
            //         zNetViews.Remove(z);
            //     }
            // }
            
            // foreach (var obj in Resources.FindObjectsOfTypeAll<TerrainModifier>()
            //     .Select(t => t.GetComponent<ZNetView>())
            //     .Where(z => z != null && z.transform != null && z.GetZDO() != null))
            // {
            //     obj.transform.position = obj.GetZDO().GetPosition();
            // }
            
            // Do this AFTER we have updated the terrain effectors above.
            GameUtils.RegenerateHeightmaps();

            foreach (var kv in zNetViews)
            {
                var z = kv.Key;
                var pos = z.transform.position;
                if (Heightmap.GetHeight(pos, out float height))
                {
                    pos.y = height + kv.Value;
                }
                else
                {
                    pos.y = WorldGenerator.instance.GetHeight(pos.x, pos.z) + kv.Value;    
                }

                z.transform.position = pos;
                // Override the rough position from the zdos above with our more accurate one
                if (z.GetZDO() != null)
                {
                    z.GetZDO().SetPosition(pos);
                    zdos.Remove(z.GetZDO());
                }
            }
            
            // Lastly we will update any ZDOs we haven't already updated above
            foreach (var kv in zdos)
            {
                var zdo = kv.Key;
                var pos = zdo.GetPosition();
                pos.y = WorldGenerator.instance.GetHeight(pos.x, pos.z) + kv.Value;
                zdo.SetPosition(pos);
            }

            // Update the grass etc.
            AccessTools.Method(typeof(ClutterSystem), "ClearAll").Invoke(ClutterSystem.instance, new object[]{});
            // UpdateZNetViewHeights();
        }

        private static void UpdateZNetViewHeights()
        {
            var GetFallHeight = AccessTools.Method(typeof(StaticPhysics), "GetFallHeight");
            var PushUp = AccessTools.Method(typeof(StaticPhysics), "CheckFall");
            var m_nview_FI = AccessTools.Field(typeof(StaticPhysics), "m_nview");
            foreach (var obj in Resources.FindObjectsOfTypeAll<ZNetView>()
                .Where(z => z.transform != null))
            {
                // Prefer to use static physics
                var staticPhysics = obj.GetComponent<StaticPhysics>();
                if (staticPhysics != null && (staticPhysics.m_fall || staticPhysics.m_pushUp))
                {
                    if (staticPhysics.m_fall)
                    {
                        // Instant fall
                        //CheckFall.Invoke(obj, new object[]{});
                        float fallHeight = (float) GetFallHeight.Invoke(staticPhysics, new object[] { });
                        Vector3 position = staticPhysics.transform.position;
                        position.y = fallHeight;
                        staticPhysics.transform.position = position;
                        var m_nview = (ZNetView) m_nview_FI.GetValue(staticPhysics);
                        if (m_nview && m_nview.IsValid() && m_nview.IsOwner())
                        {
                            m_nview.GetZDO().SetPosition(staticPhysics.transform.position);
                        }
                    }

                    if (staticPhysics.m_pushUp)
                    {
                        PushUp.Invoke(staticPhysics, new object[] { });
                    }

                    // var pos = obj.transform.position;
                    // pos.y = WorldGenerator.instance.GetHeight(pos.x, pos.z);
                    // obj.transform.position = pos;
                }
                else if (staticPhysics == null && obj.GetZDO() != null)
                {
                    obj.transform.position = obj.GetZDO().GetPosition();
                }
            }
        }

        public static void FallAllObjects()
        {
            foreach (var zdo in GetObjectsByID().Values)
            {
                var pos = zdo.GetPosition();
                pos.y = WorldGenerator.instance.GetHeight(pos.x, pos.z);
                
                zdo.SetPosition(pos);
            }
            
            UpdateZNetViewHeights();
            
            // //var m_nview_FI = AccessTools.Field(typeof(ZNetView), "m_nview");
            // foreach (var obj in Resources.FindObjectsOfTypeAll<ZNetView>())
            // {
            //     //var m_nview = (ZNetView)m_nview_FI.GetValue(obj);
            //     //if (m_nview && m_nview.IsValid() && m_nview.IsOwner())
            //     //{
            //     if (obj.GetZDO() != null)
            //     {
            //         obj.transform.position = obj.GetZDO().GetPosition();
            //     }
            //     else
            //     {
            //         var staticPhysics = obj.GetComponent<StaticPhysics>();
            //         if (staticPhysics != null && (staticPhysics.m_fall || staticPhysics.m_pushUp))
            //         {
            //             var pos = obj.transform.position;
            //             pos.y = WorldGenerator.instance.GetHeight(pos.x, pos.z);
            //             obj.transform.position = pos;
            //         }
            //     }
            //     //}
            // }
        }
        
        //     var CheckFall = AccessTools.Method(typeof(StaticPhysics), "CheckFall");
        //     var GetFallHeight = AccessTools.Method(typeof(StaticPhysics), "GetFallHeight");
        //     var PushUp = AccessTools.Method(typeof(StaticPhysics), "CheckFall");
        //     var m_nview_FI = AccessTools.Field(typeof(StaticPhysics), "m_nview");
        //     foreach (var obj in Resources.FindObjectsOfTypeAll<StaticPhysics>())
        //     {
        //         if (obj.m_fall)
        //         {
        //             // Instant fall
        //             //CheckFall.Invoke(obj, new object[]{});
        //             float fallHeight = (float)GetFallHeight.Invoke(obj, new object[]{});
        //             Vector3 position = obj.transform.position;
        //             position.y = fallHeight;
        //             obj.transform.position = position;
        //             var m_nview = (ZNetView)m_nview_FI.GetValue(obj);
        //             if (m_nview && m_nview.IsValid() && m_nview.IsOwner())
        //             {
        //                 m_nview.GetZDO().SetPosition(obj.transform.position);
        //             }
        //         }
        //         if (obj.m_pushUp) PushUp.Invoke(obj, new object[]{});
        //     }
        //     
        //     ZDOMan.instance.
        // }
        
        public static Dictionary<Vector2i, ZoneSystem.LocationInstance> GetLocationInstances() =>
            (Dictionary<Vector2i, ZoneSystem.LocationInstance>) AccessTools.Field(typeof(ZoneSystem), "m_locationInstances").GetValue(ZoneSystem.instance);

        public static void ShowOnMap(params string[] list)
        {
            var locationInstances = GetLocationInstances();
            foreach (var lg in locationInstances.Values.GroupBy(l => l.m_location.m_prefabName))
            {
                if (list == null || list.Length == 0 || list.Any(f => lg.Key.ToLower().StartsWith(f)))
                {
                    BetterContinents.Log($"Marking {lg.Count()} {lg.Key} locations on map");
                    int idx = 0;
                    foreach (var li in lg)
                    {
                        Minimap.instance.AddPin(li.m_position, Minimap.PinType.Icon3,
                            $"{li.m_location.m_prefabName} {idx++}", false, false);
                    }
                }
            }
        }

        public static void HideOnMap(params string[] list)
        {
            var locationInstances = GetLocationInstances();
            var pins = (List<Minimap.PinData>) AccessTools.Field(typeof(Minimap), "m_pins").GetValue(Minimap.instance);
            foreach (var lg in locationInstances.Values.GroupBy(l => l.m_location.m_prefabName))
            {
                if (list == null || list.Length == 0 || list.Any(f => lg.Key.ToLower().StartsWith(f)))
                {
                    BetterContinents.Log($"Hiding {lg.Count()} {lg.Key} locations from the map");
                    int idx = 0;
                    foreach (var li in lg)
                    {
                        var name = $"{li.m_location.m_prefabName} {idx++}";
                        var pin = pins.FirstOrDefault(p => p.m_name == name && p.m_pos == li.m_position);
                        if (pin != null)
                        {
                            Minimap.instance.RemovePin(pins.FirstOrDefault());
                        }
                    }
                }
            }
        }
    }
}