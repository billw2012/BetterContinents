using System;
using System.Diagnostics;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using UnityEngine;

namespace BetterContinents
{
    // Loads and stores source image file in original format.
    // Derived types will define the final type of the image pixels (the "map"), and
    // how to access them
    internal abstract class ImageMapBase
    {
        public string FilePath;

        public byte[] SourceData;

        public int Size;

        public ImageMapBase(string filePath)
        {
            this.FilePath = filePath;
        }

        public ImageMapBase(string filePath, byte[] sourceData) : this(filePath)
        {
            this.SourceData = sourceData;
        }

        public bool LoadSourceImage()
        {
            try
            {
                SourceData = File.ReadAllBytes(FilePath);
                return true;
            }
            catch (Exception ex)
            {
                BetterContinents.LogError($"Cannot load image {FilePath}: {ex.Message}");
                return false;
            }
        }

        protected static Color32 Convert(Rgba32 rgba) => new Color32(rgba.R, rgba.G, rgba.B, rgba.A);
        
        protected virtual Image LoadImage(byte[] data) => Image.Load<Rgba32>(Configuration.Default, SourceData);

        protected abstract bool LoadTextureToMap(Image image);

        public bool CreateMap()
        {
            try
            {
                var sw = new Stopwatch();
                sw.Start();
                
                // Cast disambiguates to the correct return type for some reason
                using(var image = LoadImage(SourceData))
                {
                    if (!ValidateDimensions(image.Width, image.Height))
                    {
                        return false;
                    }
                    Size = image.Width;

                    image.Mutate(x => x.Flip(FlipMode.Vertical));

                    BetterContinents.Log($"Time to load {FilePath}: {sw.ElapsedMilliseconds} ms");
                    
                    return this.LoadTextureToMap(image);
                }
            }
            catch (Exception ex)
            {
                BetterContinents.LogError($"Cannot load texture {FilePath}: {ex.Message}");
                return false;
            }
        }

        protected bool ValidateDimensions(int width, int height)
        {
            if (width != height)
            {
                BetterContinents.LogError(
                    $"Cannot use texture {FilePath}: its width ({width}) does not match its height ({height})");
                return false;
            }

            bool IsPowerOfTwo(int x) => (x & (x - 1)) == 0;
            if (!IsPowerOfTwo(width))
            {
                BetterContinents.LogError(
                    $"Cannot use texture {FilePath}: it is not a power of two size (e.g. 256, 512, 1024, 2048)");
                return false;
            }

            if (width > 4096)
            {
                BetterContinents.LogError(
                    $"Cannot use texture {FilePath}: it is too big ({width}x{height}), keep the size to less or equal to 4096x4096");
                return false;
            }

            return true;
        }
    }
}