using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Random = System.Random;

namespace BetterContinents
{
    public class NoiseStackSettings
    {
        public class NoiseSettings
        {
            // Basic
            public FastNoiseLite.NoiseType NoiseType; // = FastNoiseLite.NoiseType.OpenSimplex2
            public float Frequency; // = 0.0005f
            
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
            
            public float? SmoothStepStart;
            public float? SmoothStepEnd;
            
            public float? Threshold;
            
            public float? RangeStart; 
            public float? RangeEnd;
            
            public float? Opacity;

            // Disabled, not sure how this applies...
            // public float? FillOpacity; // Applies to ColorBurn, LinearBurn, ColorDodge, LinearDodge, VividLight, LinearLight, HardMix, and Difference.
            public BlendOperations.BlendModeType BlendMode;

            public static NoiseSettings Default() =>
                new NoiseSettings
                {
                    NoiseType = FastNoiseLite.NoiseType.OpenSimplex2,
                    Frequency = 0.0005f,
                    FractalType = FastNoiseLite.FractalType.FBm,
                    FractalOctaves = 4,
                    FractalLacunarity = 2,
                    FractalGain = 0.5f,
                    FractalWeightedStrength = 0,
                };

            public void Serialize(ZPackage pkg)
            {
                void WriteOptionalSingle(float? value) => pkg.Write(value ?? float.NegativeInfinity);

                pkg.Write((int)NoiseType);
                pkg.Write(Frequency);
                
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
                WriteOptionalSingle(SmoothStepStart);
                WriteOptionalSingle(SmoothStepEnd);
                WriteOptionalSingle(Threshold);
                WriteOptionalSingle(RangeStart);
                WriteOptionalSingle(RangeEnd);
                
                WriteOptionalSingle(Opacity);
                pkg.Write((int)BlendMode);
            }
            
            public static NoiseSettings Deserialize(ZPackage pkg)
            {
                float? ReadOptionalSingle()
                {
                    float v = pkg.ReadSingle();
                    return float.IsNegativeInfinity(v) ? (float?)null : v;
                }
                
                // Don't use object initializer, although it executes in lexical order, it isn't explicit in the spec
                // ReSharper disable once UseObjectOrCollectionInitializer
                var settings = new NoiseSettings();
                
                settings.NoiseType = (FastNoiseLite.NoiseType) pkg.ReadInt();
                settings.Frequency = pkg.ReadSingle();
                
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
                settings.SmoothStepStart = ReadOptionalSingle();
                settings.SmoothStepEnd = ReadOptionalSingle();
                settings.Threshold = ReadOptionalSingle();
                settings.RangeStart = ReadOptionalSingle();
                settings.RangeEnd = ReadOptionalSingle();

                settings.Opacity = ReadOptionalSingle();
                settings.BlendMode = (BlendOperations.BlendModeType) pkg.ReadInt();

                return settings;
            }
            
            public FastNoiseLite CreateNoise()
            {
                var noise = new FastNoiseLite();
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
                output(
                    $"type {NoiseType}, freq {Frequency}, frac {FractalType}, octaves {FractalOctaves}, lac {FractalLacunarity}, gain {FractalGain}, w. str. {FractalWeightedStrength}, ping-ping str. {FractalPingPongStrength}");
                output($"cell. dist. fn. {CellularDistanceFunction}, cell. ret. type {CellularReturnType}, cell jitt. {CellularJitter}");
                output($"warp type {DomainWarpType}, warp amp {DomainWarpAmp}");
                output($"invert {Invert}");
                if (SmoothStepStart != null && SmoothStepEnd != null)
                {
                    output($"smooth step {SmoothStepStart} - {SmoothStepEnd}");
                }
                if (Threshold != null)
                {
                    output($"threshold {Threshold}");
                }
                if (RangeStart != null || RangeEnd != null)
                {
                    output($"range {RangeStart ?? 0} - {RangeEnd ?? 1}");
                }
                if (Opacity != null)
                {
                    output($"opacity {Opacity}");
                }
                output($"blend mode {BlendMode}");
            }
        }
        
        public class NoiseLayer
        {
            public NoiseSettings noiseSettings = NoiseSettings.Default();
            public NoiseSettings noiseWarpSettings;
            public NoiseSettings maskSettings;
            public NoiseSettings maskWarpSettings;

            public void Serialize(ZPackage pkg)
            {
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

        public NoiseSettings BaseLayer = NoiseSettings.Default();
        public List<NoiseLayer> Layers = new List<NoiseLayer>();

        public void SetNoiseLayerCount(int count)
        {
            while (Layers.Count > count && Layers.Count > 0)
            {
                Layers.RemoveAt(Layers.Count - 1);
            }
            while (Layers.Count < count)
            {
                Layers.Add(new NoiseLayer());
            }
        }

        public void Dump(Action<string> output)
        {
            BaseLayer.Dump(output);
            for (int i = 0; i < Layers.Count; i++)
            {
                output($"Layer {i + 1}:");
                Layers[i].Dump(output);
            }
        }

        public void Serialize(ZPackage pkg)
        {
            BaseLayer.Serialize(pkg);
            pkg.Write(Layers.Count);
            for (int i = 0; i < Layers.Count; i++)
            {
                Layers[i].Serialize(pkg);
            }
        }

        public static NoiseStackSettings Deserialize(ZPackage pkg)
        {
            var stack = new NoiseStackSettings();
            stack.BaseLayer = NoiseSettings.Deserialize(pkg);
            int noiseLayerCount = pkg.ReadInt();
            if (noiseLayerCount > 0)
            {
                for (int i = 0; i < noiseLayerCount; i++)
                {
                    stack.Layers.Add(NoiseLayer.Deserialize(pkg));
                }
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
        
        public WarpedNoise(NoiseStackSettings.NoiseSettings settings, NoiseStackSettings.NoiseSettings warpSettings = null)
        {
            this.settings = settings;
    
            noise = settings.CreateNoise();
            warp = warpSettings?.CreateNoise();
    
            min = float.MaxValue;
            max = float.MinValue;
    
            var st = new Stopwatch();
            st.Start();
            for (int i = 0; i < 10000; ++i)
            {
                float height = noise.GetNoise(UnityEngine.Random.Range(-BetterContinents.WorldSize, BetterContinents.WorldSize), UnityEngine.Random.Range(-BetterContinents.WorldSize, BetterContinents.WorldSize));
                min = Mathf.Min(min, height);
                max = Mathf.Max(max, height);
            }
            BetterContinents.Log($"{10000 / st.ElapsedMilliseconds} samples per ms, range [{min}, {max}]");
            add = -min;
            mul = 1 / (max - min);
        }
    
        public float GetBlendedNoise(float x, float y, float baseNoise) => BlendOperations.Blend(baseNoise, GetNoise(x, y), settings.BlendMode);
    
        public float GetNoise(float x, float y)
        {
            warp?.DomainWarp(ref x, ref y);
            
            float normalizedNoise = (noise.GetNoise(x, y) + add) * mul;
            
            if (settings.Invert)
            {
                normalizedNoise = 1 - normalizedNoise;
            }
            if (settings.SmoothStepStart != null && settings.SmoothStepEnd != null)
            {
                normalizedNoise = Mathf.SmoothStep(settings.SmoothStepStart.Value, settings.SmoothStepEnd.Value, normalizedNoise);
            }
            normalizedNoise = Mathf.Lerp(settings.RangeStart ?? 0, settings.RangeEnd ?? 1, normalizedNoise);
            if (settings.Threshold != null)
            {
                normalizedNoise = normalizedNoise < settings.Threshold ? 0f : 1f;
            }
            normalizedNoise *= (settings.Opacity ?? 1f);
            
            return normalizedNoise;
        }
    }
    
    public struct NoiseLayer
    {
        private WarpedNoise noise;
        private WarpedNoise? mask;
    
        public NoiseLayer(NoiseStackSettings.NoiseSettings layerSettings)
        {
            noise = new WarpedNoise(layerSettings);
            mask = null;
        }
        
        public NoiseLayer(NoiseStackSettings.NoiseLayer noiseLayerSettings)
        {
            noise = new WarpedNoise(noiseLayerSettings.noiseSettings, noiseLayerSettings.noiseWarpSettings);
            mask = noiseLayerSettings.maskSettings != null
                ? new WarpedNoise(noiseLayerSettings.maskSettings, noiseLayerSettings.maskWarpSettings) 
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
        public NoiseLayer baseLayer;
        public List<NoiseLayer> layers = new List<NoiseLayer>();

        public NoiseStack(NoiseStackSettings settings)
        {
            baseLayer = new NoiseLayer(settings.BaseLayer);
            foreach (var layerSettings in settings.Layers)
            {
                layers.Add(new NoiseLayer(layerSettings));
            }
        }

        public float Apply(float x, float y, float inValue = 0f)
        {
            float newValue = baseLayer.Apply(x, y, inValue);
            foreach (var layer in layers)
            {
                newValue = layer.Apply(x, y, newValue);
            }

            return newValue;
        }
    }
}