using UnityEngine;

namespace BetterContinents
{
    internal class ImageMapFloat : ImageMapBase
    {
        private float[] Map;

        public ImageMapFloat(string filePath) : base(filePath) { }

        public ImageMapFloat(string filePath, byte[] sourceData) : base(filePath, sourceData) { }

        protected override bool LoadTextureToMap(Texture2D tex)
        {
            var pixels = tex.GetPixels();
            Map = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Map[i] = pixels[i].r;
            }
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