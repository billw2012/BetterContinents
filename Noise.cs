using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace BetterContinents
{
    public class NoiseStackSettings
    {
        public class NoiseSettings
        {
            // Add new properties at the end, and comment where new versions start
            public const int LatestVersion = 1;
            
            public static readonly FastNoiseLite.FractalType[] WarpFractalTypes = {
                FastNoiseLite.FractalType.None,
                FastNoiseLite.FractalType.DomainWarpIndependent,
                FastNoiseLite.FractalType.DomainWarpProgressive,
            };
            
            public static readonly FastNoiseLite.FractalType[] NonFractalTypes = {
                FastNoiseLite.FractalType.None,
                FastNoiseLite.FractalType.FBm,
                FastNoiseLite.FractalType.Ridged,
                FastNoiseLite.FractalType.PingPong,
            };
            
            // Version 1
            public int Version = LatestVersion;

            // Basic
            public FastNoiseLite.NoiseType NoiseType; // = FastNoiseLite.NoiseType.OpenSimplex2
            public float Frequency; // = 0.0005f
            public float Aspect; // = 1
            
            // Fractal settings
            public FastNoiseLite.FractalType FractalType; // = FastNoiseLite.FractalType.FBm
            public int FractalOctaves; // = 4
            public float FractalLacunarity; // = 2
            public float FractalGain; // = 0.5f
            public float FractalWeightedStrength; // = 0
            public float FractalPingPongStrength; // = 2

            // Cellular Noise Type specific
            public FastNoiseLite.CellularDistanceFunction CellularDistanceFunction;
            public FastNoiseLite.CellularReturnType CellularReturnType;
            public float CellularJitter;
            
            // Warp specific
            public FastNoiseLite.DomainWarpType DomainWarpType;
            public float DomainWarpAmp;
            
            // Filters
            public bool Invert;

            public bool UseSmoothThreshold;
            public float SmoothThresholdStart;
            public float SmoothThresholdEnd = 1;
            
            public bool UseThreshold;
            public float Threshold;
            
            public bool UseRange;
            public float RangeStart; 
            public float RangeEnd = 1;
            
            public bool UseOpacity;
            public float Opacity = 1;

            // Disabled, not sure how this applies...
            // public float? FillOpacity; // Applies to ColorBurn, LinearBurn, ColorDodge, LinearDodge, VividLight, LinearLight, HardMix, and Difference.
            public BlendOperations.BlendModeType BlendMode = BlendOperations.BlendModeType.Normal;

            public static NoiseSettings Default() =>
                new ()
                {
                    NoiseType = FastNoiseLite.NoiseType.OpenSimplex2,
                    Frequency = 0.0005f,
                    Aspect = 1,
                    FractalType = FastNoiseLite.FractalType.FBm,
                    FractalOctaves = 7,
                    FractalLacunarity = 2,
                    FractalGain = 0.5f,
                    FractalWeightedStrength = 0,
                };
            
            public static NoiseSettings Ridged() =>
                new ()
                {
                    NoiseType = FastNoiseLite.NoiseType.OpenSimplex2,
                    Frequency = 0.0005f,
                    Aspect = 1,
                    FractalType = FastNoiseLite.FractalType.Ridged,
                    FractalOctaves = 7,
                    FractalLacunarity = 2,
                    FractalGain = 0.5f,
                    FractalWeightedStrength = 0,
                };

            public static NoiseSettings DefaultWarp() =>
                new ()
                {
                    NoiseType = FastNoiseLite.NoiseType.OpenSimplex2,
                    Frequency = 0.0005f,
                    Aspect = 1,
                    FractalType = FastNoiseLite.FractalType.DomainWarpIndependent,
                    FractalOctaves = 1,
                    FractalLacunarity = 2,
                    FractalGain = 0.5f,
                    FractalWeightedStrength = 0,
                    DomainWarpAmp = 4000,
                    DomainWarpType = FastNoiseLite.DomainWarpType.OpenSimplex2,
                };

            public void Serialize(ZPackage pkg)
            {
                pkg.Write(Version);
                
                pkg.Write((int)NoiseType);
                pkg.Write(Frequency);
                pkg.Write(Aspect);
                
                pkg.Write((int)FractalType);
                pkg.Write(FractalOctaves);
                pkg.Write(FractalLacunarity);
                pkg.Write(FractalGain);
                pkg.Write(FractalWeightedStrength);
                pkg.Write(FractalPingPongStrength);
                
                pkg.Write((int)CellularDistanceFunction);
                pkg.Write((int)CellularReturnType);
                pkg.Write(CellularJitter);
                
                pkg.Write((int)DomainWarpType);
                pkg.Write(DomainWarpAmp);
                
                pkg.Write(Invert);
                
                pkg.Write(UseSmoothThreshold);
                pkg.Write(SmoothThresholdStart);
                pkg.Write(SmoothThresholdEnd);
                
                pkg.Write(UseThreshold);
                pkg.Write(Threshold);
                
                pkg.Write(UseRange);
                pkg.Write(RangeStart);
                pkg.Write(RangeEnd);
                
                pkg.Write(UseOpacity);
                pkg.Write(Opacity);
                
                pkg.Write((int)BlendMode);
            }
            
            public static NoiseSettings Deserialize(ZPackage pkg)
            {
                // Don't use object initializer, although it executes in lexical order, it isn't explicit in the spec
                // ReSharper disable once UseObjectOrCollectionInitializer
                var settings = new NoiseSettings();
                
                settings.Version = pkg.ReadInt();
                
                settings.NoiseType = (FastNoiseLite.NoiseType) pkg.ReadInt();
                settings.Frequency = pkg.ReadSingle();
                settings.Aspect = pkg.ReadSingle();
                
                settings.FractalType = (FastNoiseLite.FractalType) pkg.ReadInt();
                settings.FractalOctaves = pkg.ReadInt();
                settings.FractalLacunarity = pkg.ReadSingle();
                settings.FractalGain = pkg.ReadSingle();
                settings.FractalWeightedStrength = pkg.ReadSingle();
                settings.FractalPingPongStrength = pkg.ReadSingle();

                settings.CellularDistanceFunction = (FastNoiseLite.CellularDistanceFunction) pkg.ReadInt();
                settings.CellularReturnType = (FastNoiseLite.CellularReturnType) pkg.ReadInt();
                settings.CellularJitter = pkg.ReadSingle();

                settings.DomainWarpType = (FastNoiseLite.DomainWarpType) pkg.ReadInt();
                settings.DomainWarpAmp = pkg.ReadSingle();

                settings.Invert = pkg.ReadBool();
                
                settings.UseSmoothThreshold = pkg.ReadBool();
                settings.SmoothThresholdStart = pkg.ReadSingle();
                settings.SmoothThresholdEnd = pkg.ReadSingle();
                
                settings.UseThreshold = pkg.ReadBool();
                settings.Threshold = pkg.ReadSingle();
                
                settings.UseRange = pkg.ReadBool();
                settings.RangeStart = pkg.ReadSingle();
                settings.RangeEnd = pkg.ReadSingle();
                
                settings.UseOpacity = pkg.ReadBool();
                settings.Opacity = pkg.ReadSingle();
                
                settings.BlendMode = (BlendOperations.BlendModeType) pkg.ReadInt();

                return settings;
            }
            
            public FastNoiseLite CreateNoise(int seed)
            {
                var noise = new FastNoiseLite(seed);

                noise.SetNoiseType(NoiseType);
                noise.SetFrequency(Frequency / BetterContinents.Settings.GlobalScale);
                noise.SetFractalType(FractalType);
                noise.SetFractalOctaves(FractalOctaves);
                noise.SetFractalLacunarity(FractalLacunarity);
                noise.SetFractalGain(FractalGain);
                noise.SetFractalWeightedStrength(FractalWeightedStrength);
                noise.SetFractalPingPongStrength(FractalPingPongStrength);
                
                noise.SetCellularDistanceFunction(CellularDistanceFunction);
                noise.SetCellularReturnType(CellularReturnType);
                noise.SetCellularJitter(CellularJitter);
                
                noise.SetDomainWarpType(DomainWarpType);
                noise.SetDomainWarpAmp(DomainWarpAmp);
                return noise;
            }

            public void Dump(Action<string> output)
            {
                output($"version {Version}");
                output(
                    $"type {NoiseType}, freq {Frequency}, aspect {Aspect}, frac {FractalType}, octaves {FractalOctaves}, lac {FractalLacunarity}, gain {FractalGain}, w. str. {FractalWeightedStrength}, ping-ping str. {FractalPingPongStrength}");
                output($"cell. dist. fn. {CellularDistanceFunction}, cell. ret. type {CellularReturnType}, cell jitt. {CellularJitter}");
                output($"warp type {DomainWarpType}, warp amp {DomainWarpAmp}");
                output($"invert {Invert}");
                if (UseSmoothThreshold)
                {
                    output($"smooth step {SmoothThresholdStart} - {SmoothThresholdEnd}");
                }
                if (UseThreshold)
                {
                    output($"threshold {Threshold}");
                }
                if (UseRange)
                {
                    output($"range {RangeStart} - {RangeEnd}");
                }
                if (UseOpacity)
                {
                    output($"opacity {Opacity}");
                }
                output($"blend mode {BlendMode}");
            }

            public void CopyFrom(NoiseSettings from)
            {
                Version = from.Version;
                
                NoiseType = from.NoiseType; 
                Frequency = from.Frequency;
                Aspect = from.Aspect; 
                
                FractalType = from.FractalType; 
                FractalOctaves = from.FractalOctaves; 
                FractalLacunarity = from.FractalLacunarity; 
                FractalGain = from.FractalGain; 
                FractalWeightedStrength = from.FractalWeightedStrength; 
                FractalPingPongStrength = from.FractalPingPongStrength; 
                
                CellularDistanceFunction = from.CellularDistanceFunction;
                CellularReturnType = from.CellularReturnType;
                CellularJitter = from.CellularJitter;
                
                DomainWarpType = from.DomainWarpType;
                DomainWarpAmp = from.DomainWarpAmp;

                Invert = from.Invert;

                UseSmoothThreshold = from.UseSmoothThreshold;
                SmoothThresholdStart = from.SmoothThresholdStart;
                SmoothThresholdEnd = from.SmoothThresholdEnd;

                UseThreshold = from.UseThreshold;
                Threshold = from.Threshold;

                UseRange = from.UseRange;
                RangeStart = from.RangeStart; 
                RangeEnd = from.RangeEnd;

                UseOpacity = from.UseOpacity;
                Opacity = from.Opacity;
                
                BlendMode = from.BlendMode;
            }
        }
        
        public class NoiseLayer
        {
            // Add new properties at the end, and comment where new versions start
            public const int LatestVersion = 1;

            public int Version = LatestVersion;
            public NoiseSettings noiseSettings = NoiseSettings.Default();
            public NoiseSettings noiseWarpSettings;
            public NoiseSettings maskSettings;
            public NoiseSettings maskWarpSettings;

            public void Serialize(ZPackage pkg)
            {
                pkg.Write(Version);
                noiseSettings.Serialize(pkg);
                pkg.Write(noiseWarpSettings != null);
                noiseWarpSettings?.Serialize(pkg);
                pkg.Write(maskSettings != null);
                maskSettings?.Serialize(pkg);
                pkg.Write(maskWarpSettings != null);
                maskWarpSettings?.Serialize(pkg);
            }

            public static NoiseLayer Deserialize(ZPackage pkg)
            {
                // ReSharper disable once UseObjectOrCollectionInitializer
                var noiseLayer = new NoiseLayer();
                noiseLayer.Version = pkg.ReadInt();
                noiseLayer.noiseSettings = NoiseSettings.Deserialize(pkg);
                if(pkg.ReadBool())
                {
                    noiseLayer.noiseWarpSettings = NoiseSettings.Deserialize(pkg);
                }   
                if(pkg.ReadBool())
                {
                    noiseLayer.maskSettings = NoiseSettings.Deserialize(pkg);
                }   
                if(pkg.ReadBool())
                {
                    noiseLayer.maskWarpSettings = NoiseSettings.Deserialize(pkg);
                }  
                return noiseLayer;
            }

            public void Dump(Action<string> output)
            {
                output($"Version {Version}");
                output($"Noise Settings:");
                noiseSettings.Dump(str => output($"    {str}"));
                if (noiseWarpSettings != null)
                {
                    output($"Noise Warp Settings:");
                    noiseWarpSettings.Dump(str => output($"    {str}"));
                }
                if (maskSettings != null)
                {
                    output($"Mask Settings:");
                    maskSettings.Dump(str => output($"    {str}"));
                }
                if (maskWarpSettings != null)
                {
                    output($"Mask Warp Settings:");
                    maskWarpSettings.Dump(str => output($"    {str}"));
                }
            }
        }

        public readonly List<NoiseLayer> NoiseLayers = new List<NoiseLayer>();

        public static NoiseStackSettings Default()
        {
            var val = new NoiseStackSettings();
            val.NoiseLayers.Add(new NoiseLayer());
            return val;
        }

        public void AddNoiseLayer() => SetNoiseLayerCount(NoiseLayers.Count + 1);
        public void RemoveNoiseLayer()
        {
            if (NoiseLayers.Count > 1)
            {
                NoiseLayers.RemoveAt(NoiseLayers.Count - 1);
            }
        }

        public void SetNoiseLayerCount(int count)
        {
            count = Mathf.Max(1, count);
            while (NoiseLayers.Count > count && NoiseLayers.Count > 0)
            {
                NoiseLayers.RemoveAt(NoiseLayers.Count - 1);
            }
            while (NoiseLayers.Count < count)
            {
                NoiseLayers.Add(new NoiseLayer());
            }
        }

        public void Dump(Action<string> output)
        {
            for (int i = 0; i < NoiseLayers.Count; i++)
            {
                output($"Layer {i + 1}:");
                NoiseLayers[i].Dump(output);
            }
        }

        public void Serialize(ZPackage pkg)
        {
            pkg.Write(NoiseLayers.Count);
            for (int i = 0; i < NoiseLayers.Count; i++)
            {
                NoiseLayers[i].Serialize(pkg);
            }
        }

        public static NoiseStackSettings Deserialize(ZPackage pkg)
        {
            var stack = new NoiseStackSettings();
            int noiseLayerCount = pkg.ReadInt();
            for (int i = 0; i < noiseLayerCount; i++)
            {
                stack.NoiseLayers.Add(NoiseLayer.Deserialize(pkg));
            }
            return stack;
        }
    }
    
    public struct WarpedNoise
    {
        private FastNoiseLite noise;
        private FastNoiseLite warp;
        
        private float min;
        private float max;
        private float add;
        private float mul;
        private NoiseStackSettings.NoiseSettings settings;
    
        public static readonly WarpedNoise Empty = default(WarpedNoise);
        
        public bool IsValid => noise != null;
        
        public WarpedNoise(int seed, NoiseStackSettings.NoiseSettings settings, NoiseStackSettings.NoiseSettings warpSettings = null)
        {
            this.settings = settings;
    
            noise = settings.CreateNoise(seed);
            warp = warpSettings?.CreateNoise(seed + 127);
    
            min = float.MaxValue;
            max = float.MinValue;
    
            var st = new Stopwatch();
            st.Start();
            const int RangeCheckSize = 100;
            for (int y = 0; y < RangeCheckSize; ++y)
            {
                float yp = 2f * (y / (float) RangeCheckSize - 0.5f) * BetterContinents.WorldSize;
                for (int x = 0; x < RangeCheckSize; ++x)
                {
                    float xp = 2f * (x / (float) RangeCheckSize - 0.5f) * BetterContinents.WorldSize;
                    float height = noise.GetNoise(xp, yp);
                    min = Mathf.Min(min, height);
                    max = Mathf.Max(max, height);
                }
            }
            BetterContinents.Log($"{RangeCheckSize * RangeCheckSize / st.ElapsedMilliseconds} samples per ms, range [{min}, {max}]");
            add = -min;
            mul = 1 / (max - min);
        }
    
        public float GetBlendedNoise(float x, float y, float baseNoise) => BlendOperations.Blend(baseNoise, GetNoise(x, y), settings.BlendMode);
    
        public float GetNoise(float x, float y)
        {
            y *= settings.Aspect;
            
            warp?.DomainWarp(ref x, ref y);
            
            float normalizedNoise = (noise.GetNoise(x, y) + add) * mul;
            
            if (settings.Invert)
            {
                normalizedNoise = 1 - normalizedNoise;
            }
            if (settings.UseSmoothThreshold)
            {
                //normalizedNoise = Mathf.SmoothStep(settings.SmoothStepStart.Value, settings.SmoothStepEnd.Value, normalizedNoise);
                normalizedNoise = Mathf.InverseLerp(settings.SmoothThresholdStart, settings.SmoothThresholdEnd, normalizedNoise);
            }
            normalizedNoise =settings.UseRange ? Mathf.Lerp(settings.RangeStart, settings.RangeEnd, normalizedNoise) : normalizedNoise;
            if (settings.UseThreshold)
            {
                normalizedNoise = normalizedNoise < settings.Threshold ? 0f : 1f;
            }
            normalizedNoise *= settings.UseOpacity ? settings.Opacity : 1f;
            
            return normalizedNoise;
        }
    }
    
    public struct NoiseLayer
    {
        public WarpedNoise noise;
        public WarpedNoise? mask;
    
        public NoiseLayer(int seed, NoiseStackSettings.NoiseSettings layerSettings)
        {
            noise = new WarpedNoise(seed, layerSettings);
            mask = null;
        }
        
        public NoiseLayer(int seed, NoiseStackSettings.NoiseLayer noiseLayerSettings)
        {
            noise = new WarpedNoise(seed, noiseLayerSettings.noiseSettings, noiseLayerSettings.noiseWarpSettings);
            mask = noiseLayerSettings.maskSettings != null
                ? new WarpedNoise(seed * 107, noiseLayerSettings.maskSettings, noiseLayerSettings.maskWarpSettings) 
                : (WarpedNoise?) null;
        }
        
        public float Apply(float wx, float wy, float inValue)
        {
            float layerValue = noise.GetBlendedNoise(wx, wy, inValue);
            return mask != null 
                ? Mathf.Lerp(inValue, layerValue, mask.Value.GetNoise(wx, wy))
                : layerValue;
        }
    }
    
    public class NoiseStack
    {
        public List<NoiseLayer> layers = new List<NoiseLayer>();

        public NoiseStack(int seed, NoiseStackSettings settings)
        {
            for (var layerIndex = 0; layerIndex < settings.NoiseLayers.Count; layerIndex++)
            {
                var layerSettings = settings.NoiseLayers[layerIndex];
                layers.Add(new NoiseLayer(seed * 31 * (layerIndex + 3), layerSettings));
            }
        }

        public float Apply(float x, float y, float inValue = 0f)
        {
            float newValue = inValue;
            foreach (var layer in layers)
            {
                newValue = layer.Apply(x, y, newValue);
            }

            return newValue;
        }
    }
}