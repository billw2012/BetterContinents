using System;
using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterContinents
{
    internal class ImageMapFloat : ImageMapBase
    {
        private float[] Map;

        public ImageMapFloat(string filePath) : base(filePath) { }

        public ImageMapFloat(string filePath, byte[] sourceData) : base(filePath, sourceData) { }

        protected override Image LoadImage(byte[] data) => Image.Load<L16>(Configuration.Default, SourceData);

        public bool CreateMapLegacy()
        {
            var tex = new Texture2D(2, 2);
            try
            {
                var sw = new Stopwatch();
                sw.Start();

                tex.LoadImage(SourceData);
                // Cast disambiguates to the correct return type for some reason
                if (!ValidateDimensions(tex.width, tex.height))
                {
                    return false;
                }
                Size = tex.width;

                BetterContinents.Log($"Time to load {FilePath}: {sw.ElapsedMilliseconds} ms");

                var pixels = tex.GetPixels();
                Map = new float[pixels.Length];
                for (int i = 0; i < pixels.Length; i++)
                {
                    Map[i] = pixels[i].r;
                }
                return true;
            }
            catch (Exception ex)
            {
                BetterContinents.LogError($"Cannot load texture {FilePath}: {ex.Message}");
                return false;
            }
            finally
            {
                Object.Destroy(tex);
            }
        }
        
        protected override bool LoadTextureToMap(Image image)
        {
            var sw = new Stopwatch();
            sw.Start();
            var typedImage = (Image<L16>) image;
            Map = new float[typedImage.Width * typedImage.Height];
            for (int y = 0; y < typedImage.Height; y++)
            {
                var pixelRowSpan = typedImage.GetPixelRowSpan(y);
                for (int x = 0; x < typedImage.Width; x++)
                {
                    Map[y * typedImage.Width + x] = pixelRowSpan[x].ToVector4().X; // / (float)ushort.MaxValue;
                }
            }
            
            BetterContinents.Log($"Time to process {FilePath}: {sw.ElapsedMilliseconds} ms");
            
            return true;
        }

        public float GetValue(float x, float y)
        {
            float xa = x * (this.Size - 1);
            float ya = y * (this.Size - 1);

            int xi = Mathf.FloorToInt(xa);
            int yi = Mathf.FloorToInt(ya);

            float xd = xa - xi;
            float yd = ya - yi;

            int x0 = Mathf.Clamp(xi, 0, this.Size - 1);
            int x1 = Mathf.Clamp(xi + 1, 0, this.Size - 1);
            int y0 = Mathf.Clamp(yi, 0, this.Size - 1);
            int y1 = Mathf.Clamp(yi + 1, 0, this.Size - 1);

            float p00 = this.Map[y0 * this.Size + x0];
            float p10 = this.Map[y0 * this.Size + x1];
            float p01 = this.Map[y1 * this.Size + x0];
            float p11 = this.Map[y1 * this.Size + x1];

            return Mathf.Lerp(
                Mathf.Lerp(p00, p10, xd),
                Mathf.Lerp(p01, p11, xd),
                yd
            );
        }
    }
}