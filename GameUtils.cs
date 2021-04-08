using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

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

        private static void RegenerateDistantLod()
        {
            foreach (var lod in Object.FindObjectsOfType<TerrainLod>())
            {
                lod.m_needRebuild = true;
                lod.m_hmap.m_buildData = null;
            }
        }

        private static Dictionary<ZDOID, ZDO> GetObjectsByID() => ZDOMan.instance.m_objectsByID;
        
        public static void BeginHeightChanges()
        {
            // Stop and reset the heightmap generator first
            HeightmapBuilder.instance.Dispose();
            var _ = new HeightmapBuilder();
        }

        public static void EndHeightChanges()
        { 
            Refresh();
        }

        public static void Refresh()
        {
            DespawnAll();
            
            ClutterSystem.instance.ClearAll();
            DeleteAllTaggedZDOs();
            ResetLocationInstances();
            RegenerateDistantLod();
            
            FastMinimapRegen();
        }

        public static void RegenerateLocations()
        {
            DespawnAll();
            
            ClutterSystem.instance.ClearAll();
            DeleteLocationZDOs();
            ResetLocationInstances();

            ZoneSystem.instance.GenerateLocations();
            ResetLocPins();
        }

        private static int MinimapOrigTextureSize = 0;
        private static float MinimapOrigPixelSize = 0;

        public static void SetMinimapDownscalingPower(int pwr)
            => MinimapDownscaling = (int) Mathf.Pow(2, Mathf.Clamp(pwr, 0, 3));

        private static int MinimapDownscaling = 4;

        public static void FastMinimapRegen()
        {
            if (MinimapOrigTextureSize == 0 
                || Minimap.instance.m_textureSize != MinimapOrigTextureSize / MinimapDownscaling)
            {
                if(MinimapOrigTextureSize == 0)
                {
                    MinimapOrigTextureSize = Minimap.instance.m_textureSize;
                    MinimapOrigPixelSize = Minimap.instance.m_pixelSize;
                }
                Minimap.instance.m_textureSize = MinimapOrigTextureSize / MinimapDownscaling;
                Minimap.instance.m_pixelSize = MinimapOrigPixelSize * MinimapDownscaling;
                Minimap.instance.m_mapTexture = new Texture2D(Minimap.instance.m_textureSize, Minimap.instance.m_textureSize, TextureFormat.RGBA32, false);
                Minimap.instance.m_mapTexture.wrapMode = TextureWrapMode.Clamp;
                Minimap.instance.m_forestMaskTexture = new Texture2D(Minimap.instance.m_textureSize, Minimap.instance.m_textureSize, TextureFormat.RGBA32, false);
                Minimap.instance.m_forestMaskTexture.wrapMode = TextureWrapMode.Clamp;
                Minimap.instance.m_heightTexture = new Texture2D(Minimap.instance.m_textureSize, Minimap.instance.m_textureSize, TextureFormat.RFloat, false);
                Minimap.instance.m_heightTexture.wrapMode = TextureWrapMode.Clamp;
                Minimap.instance.m_fogTexture = new Texture2D(Minimap.instance.m_textureSize, Minimap.instance.m_textureSize, TextureFormat.RGBA32, false);
                Minimap.instance.m_fogTexture.wrapMode = TextureWrapMode.Clamp;
                Minimap.instance.m_explored = new bool[Minimap.instance.m_textureSize * Minimap.instance.m_textureSize];
                Minimap.instance.m_mapImageLarge.material = Object.Instantiate<Material>(Minimap.instance.m_mapImageLarge.material);
                Minimap.instance.m_mapImageSmall.material = Object.Instantiate<Material>(Minimap.instance.m_mapImageSmall.material);
                Minimap.instance.m_mapImageLarge.material.SetTexture("_MainTex", Minimap.instance.m_mapTexture);
                Minimap.instance.m_mapImageLarge.material.SetTexture("_MaskTex", Minimap.instance.m_forestMaskTexture);
                Minimap.instance.m_mapImageLarge.material.SetTexture("_HeightTex", Minimap.instance.m_heightTexture);
                Minimap.instance.m_mapImageLarge.material.SetTexture("_FogTex", Minimap.instance.m_fogTexture);
                Minimap.instance.m_mapImageSmall.material.SetTexture("_MainTex", Minimap.instance.m_mapTexture);
                Minimap.instance.m_mapImageSmall.material.SetTexture("_MaskTex", Minimap.instance.m_forestMaskTexture);
                Minimap.instance.m_mapImageSmall.material.SetTexture("_HeightTex", Minimap.instance.m_heightTexture);
                Minimap.instance.m_mapImageSmall.material.SetTexture("_FogTex", Minimap.instance.m_fogTexture);
            }
            Minimap.instance.ForceRegen();
            Minimap.instance.ExploreAll();
        }

        public static string GetScreenShotDir() => Path.Combine(Utils.GetSaveDataPath(), "BetterContinents", WorldGenerator.instance.m_world.m_name);

        public static void SaveMinimap(int size)
        {
            BetterContinents.instance.StartCoroutine(SaveMinimapImpl(size));
        }

        private static GameObject CreateQuad(float width, float height, float z, Material material)
        {
            var gameObject = new GameObject();
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;//new Material(Shader.Find("Standard"));

            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();

            Mesh mesh = new Mesh();

            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-width / 2, -height / 2, z),
                new Vector3(width / 2, -height / 2, z),
                new Vector3(-width / 2, height / 2, z),
                new Vector3(width / 2, height / 2, z)
            };
            mesh.vertices = vertices;

            int[] tris = new int[6]
            {
                // lower left triangle
                0, 2, 1,
                // upper right triangle
                2, 3, 1
            };
            mesh.triangles = tris;

            Vector3[] normals = new Vector3[4]
            {
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward
            };
            mesh.normals = normals;

            Vector2[] uv = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            mesh.uv = uv;

            meshFilter.mesh = mesh;

            return gameObject;
        }
        
        private static IEnumerator SaveMinimapImpl(int size)
        {
            bool wasLarge = Minimap.instance.m_largeRoot.activeSelf;
            if (!wasLarge)
            {
                Minimap.instance.SetMapMode(Minimap.MapMode.Large);
                Minimap.instance.CenterMap(Vector3.zero);
            }

            var mapPanelObject = CreateQuad(100, 100, 10, Minimap.instance.m_mapImageLarge.material);

            mapPanelObject.layer = 19;

            var renderTexture = new RenderTexture(size, size, 24);
            var cameraObject = new GameObject();
            cameraObject.layer = 19;
            var camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = renderTexture;
            camera.orthographic = true;
            camera.rect = new Rect(0, 0, renderTexture.width, renderTexture.height); 
            camera.nearClipPlane = 0;
            camera.farClipPlane = 100;
            camera.orthographicSize = 50;
            camera.cullingMask = 1 << 19;
            camera.Render();
            
            yield return new WaitForEndOfFrame();

            RenderTexture.active = renderTexture;
            var tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            var filename = DateTime.Now.ToString("yyyy-dd-M-HH-mm-ss") + ".png";
            var path = Path.Combine(GetScreenShotDir(), filename);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Console.instance.Print($"Screenshot of minimap saved to {path}");
            
            File.WriteAllBytes(path, ImageConversion.EncodeToPNG(tex));

            Object.Destroy(mapPanelObject);
            Object.Destroy(cameraObject);
            Object.Destroy(renderTexture);
            Object.Destroy(tex);
            
            if (!wasLarge)
            {
                Minimap.instance.SetMapMode(Minimap.MapMode.Small);
            }
        }

        private static void ResetLocPins()
        {
            Minimap.instance.UpdateLocationPins(1000);
        }
        
        private static void DeleteTaggedZDOs(params string[] tags)
        {
            foreach (var zdo in GetObjectsByID().Values
                .Where(z => tags.Any(t => z.GetInt(t) == 1))
                .ToList()
            )
            {
                ZDOMan.instance.HandleDestroyedZDO(zdo.m_uid);
            }
        }

        private static void DeleteAllTaggedZDOs() => DeleteTaggedZDOs("bc_loc", "bc_veg", "bc_spawn");
        private static void DeleteLocationZDOs() => DeleteTaggedZDOs("bc_loc", "bc_spawn");

        private static void ResetLocationInstances()
        {
            // For each location recreate its instance with the correct height, marking it as unplaced (as we deleted it above we hope!)
            ZoneSystem.instance.m_locationInstances =
                ZoneSystem.instance.m_locationInstances.ToDictionary(kv => kv.Key,
                    kv => new ZoneSystem.LocationInstance
                    {
                        m_location = kv.Value.m_location,
                        m_placed = false,
                        m_position = new Vector3(
                            kv.Value.m_position.x,
                            WorldGenerator.instance.GetHeight(kv.Value.m_position.x, kv.Value.m_position.z),
                            kv.Value.m_position.z
                        )
                    });
            ResetLocPins();
        }

        // [HarmonyPatch(typeof(Player))]
        // private class PlayerPatch
        // {
        //     [HarmonyPrefix, HarmonyPatch(nameof(Player.OnDestroy))]
        //     private static void PlayerOnDestroyPrefix(Player __instance)
        //     {
        //         BetterContinents.Log($"Butwhhhy?");
        //     }
        // }

        // [HarmonyPatch(typeof(Object))]
        // private class ObjectPatch
        // {
        //     private static IEnumerable<GameObject> All(GameObject obj)
        //     {
        //         yield return obj;
        //         var parent = obj.transform.parent;
        //         while (parent != null)
        //         {
        //             yield return parent.gameObject;
        //             parent = parent.parent;
        //         }
        //     }
        //     
        //     [HarmonyPrefix, HarmonyPatch(nameof(Object.Destroy), typeof(Object))]
        //     private static void DestroyPrefix(Object obj)
        //     {
        //         if (obj != null)
        //         {
        //             BetterContinents.Log($"Destroying {obj.name}");
        //             if(Player.m_localPlayer != null && All(Player.m_localPlayer.gameObject).Contains(obj))
        //             {
        //                 BetterContinents.Log($"Butwhhhy?");
        //             }
        //         }
        //     }
        //     
        //     [HarmonyPrefix, HarmonyPatch(nameof(Object.Destroy), typeof(Object), typeof(float))]
        //     private static void Destroy2Prefix(Object obj)
        //     {
        //         if (obj != null)
        //         {
        //             BetterContinents.Log($"Destroying {obj.name}");
        //             if (Player.m_localPlayer != null && All(Player.m_localPlayer.gameObject).Contains(obj))
        //             {
        //                 BetterContinents.Log($"Butwhhhy?");
        //             }
        //         }
        //     }
        // }
        
        public static void DespawnAll()
        {
            ZNetScene.instance.RemoveObjects(new List<ZDO>
            {
                Player.m_localPlayer.m_nview.m_zdo
            }, new List<ZDO>());

            foreach (var kv in ZoneSystem.instance.m_zones)
            {
                Object.Destroy(kv.Value.m_root);
            }

            ZoneSystem.instance.m_zones.Clear();
            ZoneSystem.instance.m_generatedZones.Clear();
        }

        public static void ResetAll()
        {
            DespawnAll();

            var playerZDO = Player.m_localPlayer.m_nview.m_zdo;

            // Clear all the ZDOs except the player
            var zdoToDestroy = GetObjectsByID().Values
                .Where(z => z != playerZDO)
                .ToList();

            foreach (var zdo in zdoToDestroy)
            {
                ZDOMan.instance.HandleDestroyedZDO(zdo.m_uid);
            }
            
            ZDOMan.instance.ResetSectorArray();
            
            ZDOMan.instance.AddToSector(playerZDO, playerZDO.m_sector);
            
            ResetLocationInstances();
        }
        
        /*
         * How to update locations:
         * Prefix and postfix SpawnLocation, capture all created ZDOs in-between, add location tag to them all (ZDO.Set("location_tag", 0)).
         * To properly fix-up all locations:
         *  1. de-spawn all active zones
         *  2. delete all ZDOs with location tags (can delete all creatures as well)
         *  3. fixup heights of all LocationInstances
         *  4. allow spawning again
         */
        
        // This isn't correct yet, we don't want to fall ALL ZNetViews, only very specific ones (check above)
        // private static void UpdateZNetViewHeights()
        // {
        //     var GetFallHeight = AccessTools.Method(typeof(StaticPhysics), "GetFallHeight");
        //     var PushUp = AccessTools.Method(typeof(StaticPhysics), "CheckFall");
        //     var m_nview_FI = AccessTools.Field(typeof(StaticPhysics), "m_nview");
        //     foreach (var obj in Resources.FindObjectsOfTypeAll<ZNetView>()
        //         .Where(z => z.transform != null))
        //     {
        //         // Prefer to use static physics
        //         var staticPhysics = obj.GetComponent<StaticPhysics>();
        //         if (staticPhysics != null && (staticPhysics.m_fall || staticPhysics.m_pushUp))
        //         {
        //             if (staticPhysics.m_fall)
        //             {
        //                 // Instant fall
        //                 //CheckFall.Invoke(obj, new object[]{});
        //                 float fallHeight = (float) GetFallHeight.Invoke(staticPhysics, new object[] { });
        //                 Vector3 position = staticPhysics.transform.position;
        //                 position.y = fallHeight;
        //                 staticPhysics.transform.position = position;
        //                 var m_nview = (ZNetView) m_nview_FI.GetValue(staticPhysics);
        //                 if (m_nview && m_nview.IsValid() && m_nview.IsOwner())
        //                 {
        //                     m_nview.GetZDO().SetPosition(staticPhysics.transform.position);
        //                 }
        //             }
        //
        //             if (staticPhysics.m_pushUp)
        //             {
        //                 PushUp.Invoke(staticPhysics, new object[] { });
        //             }
        //
        //             // var pos = obj.transform.position;
        //             // pos.y = WorldGenerator.instance.GetHeight(pos.x, pos.z);
        //             // obj.transform.position = pos;
        //         }
        //         else if (staticPhysics == null && obj.GetZDO() != null)
        //         {
        //             obj.transform.position = obj.GetZDO().GetPosition();
        //         }
        //     }
        // }

        // public static void FallAllObjects()
        // {
        //     foreach (var zdo in GetObjectsByID().Values)
        //     {
        //         var pos = zdo.GetPosition();
        //         pos.y = WorldGenerator.instance.GetHeight(pos.x, pos.z);
        //         
        //         zdo.SetPosition(pos);
        //     }
        //     
        //     UpdateZNetViewHeights();
        //     
        //     // //var m_nview_FI = AccessTools.Field(typeof(ZNetView), "m_nview");
        //     // foreach (var obj in Resources.FindObjectsOfTypeAll<ZNetView>())
        //     // {
        //     //     //var m_nview = (ZNetView)m_nview_FI.GetValue(obj);
        //     //     //if (m_nview && m_nview.IsValid() && m_nview.IsOwner())
        //     //     //{
        //     //     if (obj.GetZDO() != null)
        //     //     {
        //     //         obj.transform.position = obj.GetZDO().GetPosition();
        //     //     }
        //     //     else
        //     //     {
        //     //         var staticPhysics = obj.GetComponent<StaticPhysics>();
        //     //         if (staticPhysics != null && (staticPhysics.m_fall || staticPhysics.m_pushUp))
        //     //         {
        //     //             var pos = obj.transform.position;
        //     //             pos.y = WorldGenerator.instance.GetHeight(pos.x, pos.z);
        //     //             obj.transform.position = pos;
        //     //         }
        //     //     }
        //     //     //}
        //     // }
        // }
        
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
                if (list == null || list.Length == 0 || list.Any(f => lg.Key.ToLower().StartsWith(f.ToLower())))
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
            var pins = Minimap.instance.m_pins;
            if (list == null || list.Length == 0)
            {
                foreach (var pin in pins.ToList())
                {
                    Minimap.instance.RemovePin(pin);
                }
            }
            else
            {
                var locationInstances = GetLocationInstances();
                foreach (var lg in locationInstances.Values.GroupBy(l => l.m_location.m_prefabName))
                {
                    if (list.Any(f => lg.Key.ToLower().StartsWith(f.ToLower())))
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

        public static void SimpleParallelFor(int taskCount, int from, int to, Action<int> action)
        {
            var tasks = new Task[taskCount];
            int perTaskCount = (to - @from) / taskCount;
            for (int i = 0, f = @from; i < taskCount; i++, f += perTaskCount)
            {
                int taskFrom = f;
                int taskTo = Mathf.Min(to, f + perTaskCount);
                tasks[i] = Task.Run(() =>
                {
                    for (int j = taskFrom; j < taskTo; j++)
                    {
                        action(j);
                    }
                });
            }
            Task.WaitAll(tasks);
        }
    }
}