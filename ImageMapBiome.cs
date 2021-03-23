using System.Linq;
using UnityEngine;

namespace BetterContinents
{
    internal class ImageMapBiome : ImageMapBase
    {
        private Heightmap.Biome[] Map;

        public ImageMapBiome(string filePath) : base(filePath) { }

        public ImageMapBiome(string filePath, byte[] sourceData) : base(filePath, sourceData) { }

        private struct ColorBiome
        {
            public Color32 color;
            public Heightmap.Biome biome;


            public ColorBiome(Color32 color, Heightmap.Biome biome)
            {
                this.color = color;
                this.biome = biome;
            }
        }
            
        private static readonly ColorBiome[] BiomeColorMapping = new ColorBiome[]
        {
            /*
                    Ocean #0000FF 
                    Meadows #00FF00
                    Black Forest #007F00
                    Swamp #7F7F00
                    Mountains #FFFFFF
                    Plains #FFFF00
                    Mistlands #7F7F7F
                    Deep North #00FFFF
                    Ash Lands #FF0000
                */
            new ColorBiome(new Color32(0, 0, 255, 255), Heightmap.Biome.Ocean),
            new ColorBiome(new Color32(0, 255, 0, 255), Heightmap.Biome.Meadows),
            new ColorBiome(new Color32(0, 127, 0, 255), Heightmap.Biome.BlackForest),
            new ColorBiome(new Color32(127, 127, 0, 255), Heightmap.Biome.Swamp),
            new ColorBiome(new Color32(255, 255, 255, 255), Heightmap.Biome.Mountain),
            new ColorBiome(new Color32(255, 255, 0, 255), Heightmap.Biome.Plains),
            new ColorBiome(new Color32(127, 127, 127, 255), Heightmap.Biome.Mistlands),
            new ColorBiome(new Color32(0, 255, 255, 255), Heightmap.Biome.DeepNorth),
            new ColorBiome(new Color32(255, 0, 0, 255), Heightmap.Biome.AshLands),
        };

        protected override bool LoadTextureToMap(Texture2D tex)
        {
            float ColorDistance(Color a, Color b) =>
                Vector3.Distance(new Vector3(a.r, a.g, a.b), new Vector3(b.r, b.g, b.b));

            var pixels = tex.GetPixels();
            Map = new Heightmap.Biome[pixels.Length]; 
            for (int i = 0; i < pixels.Length; i++)
            {
                Map[i] = BiomeColorMapping.OrderBy(d => ColorDistance(pixels[i], d.color)).First().biome;
            }
            return true;
        }

        public Heightmap.Biome GetValue(float x, float y)
        {
            float xa = x * (this.Size - 1);
            float ya = y * (this.Size - 1);

            int xi = Mathf.FloorToInt(xa);
            int yi = Mathf.FloorToInt(ya);

            float xd = xa - xi;
            float yd = ya - yi;

            // "Interpolate" the 4 corners (sum the weights of the biomes at the four corners)
            Heightmap.Biome GetBiome(int _x, int _y) => this.Map[Mathf.Clamp(_y, 0, this.Size - 1) * this.Size + Mathf.Clamp(_x, 0, this.Size - 1)];

            var biomes = new Heightmap.Biome[4];
            var biomeWeights = new float[4];
            int numBiomes = 0;
            int topBiomeIdx = 0;
            void SampleBiomeWeighted(int xs, int ys, float weight)
            {
                var biome = GetBiome(xs, ys);
                int i = 0;
                for (; i < numBiomes; ++i)
                {
                    if (biomes[i] == biome)
                    {
                        if (biomeWeights[i] + weight > biomeWeights[topBiomeIdx])
                            topBiomeIdx = i;
                        biomeWeights[i] += weight;
                        return;
                    }
                }

                if (i == numBiomes)
                {
                    if (biomeWeights[numBiomes] + weight > biomeWeights[topBiomeIdx])
                        topBiomeIdx = numBiomes;
                    biomes[numBiomes] = biome;
                    biomeWeights[numBiomes++] = weight;
                }
            }
            SampleBiomeWeighted(xi + 0, yi + 0, (1 - xd) * (1 - yd));
            SampleBiomeWeighted(xi + 1, yi + 0, xd * (1 - yd));
            SampleBiomeWeighted(xi + 0, yi + 1, (1 - xd) * yd);
            SampleBiomeWeighted(xi + 1, yi + 1, xd * yd);

            return biomes[topBiomeIdx];
                    
            // // Get the rounded value for the diff, so we can choose which of the 4 corner value to return
            // int xo = Mathf.RoundToInt(xd); 
            // int yo = Mathf.RoundToInt(yd);
            //
            // int xf = Mathf.Clamp(xi + xo, 0, this.Size - 1);
            // int yf = Mathf.Clamp(yi + yo, 0, this.Size - 1);
            //
            // return this.Map[yf * this.Size + xf];
        }
    }
}