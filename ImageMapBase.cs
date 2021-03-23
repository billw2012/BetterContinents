using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

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

        public bool CreateMap()
        {
            var tex = new Texture2D(2, 2);
            try
            {
                try
                {
                    tex.LoadImage(SourceData);
                }
                catch (Exception ex)
                {
                    BetterContinents.LogError($"Cannot load texture {FilePath}: {ex.Message}");
                    return false;
                }

                if (tex.width != tex.height)
                {
                    BetterContinents.LogError(
                        $"Cannot use texture {FilePath}: its width ({tex.width}) does not match its height ({tex.height})");
                    return false;
                }

                bool IsPowerOfTwo(int x) => (x & (x - 1)) == 0;
                if (!IsPowerOfTwo(tex.width))
                {
                    BetterContinents.LogError(
                        $"Cannot use texture {FilePath}: it is not a power of two size (e.g. 256, 512, 1024, 2048)");
                    return false;
                }

                if (tex.width > 4096)
                {
                    BetterContinents.LogError(
                        $"Cannot use texture {FilePath}: it is too big ({tex.width}x{tex.height}), keep the size to less or equal to 4096x4096");
                    return false;
                }
                
                Size = tex.width;

                return this.LoadTextureToMap(tex);
            }
            finally
            {
                Object.Destroy(tex);
            }
        }

        protected abstract bool LoadTextureToMap(Texture2D tex);
    }
}