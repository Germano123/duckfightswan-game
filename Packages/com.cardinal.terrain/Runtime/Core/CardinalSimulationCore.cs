using System;
using UnityEngine;

namespace Cardinal.TerrainEngine.Core
{
    public enum CardinalBiomeType : byte
    {
        TropicalRainforest = 0,
        Savanna            = 1,
        Desert             = 2,
        TemperateForest    = 3,
        Taiga              = 4,
        Tundra             = 5,
        AridShrubland      = 6,
        Count              = 7
    }

    public struct BiomeConfig
    {
        public CardinalBiomeType type;
        public string name;
        public float tempMin, tempOptLow, tempOptHigh, tempMax;
        public float moistMin, moistOptLow, moistOptHigh, moistMax;
        public float altMin, altMax;
        public float demandN, demandP, demandK;
        public float maxBiomass;
        public float growthRate;
        public float mortalityRate;
    }

    /// <summary>
    /// Núcleo de simulação ecológica e geomorfológica Cardinal em C# gerenciado.
    /// Garante que o pacote funcione out-of-the-box na Unity mesmo antes da compilação de binários nativos.
    /// </summary>
    public class CardinalSimulationCore
    {
        public uint GridSize { get; private set; }
        public uint SubResolution { get; private set; }
        public uint TotalWindowResolution { get; private set; }
        public uint Seed { get; private set; }

        public float[] Altitude { get; private set; }
        public float[] SurfaceWater { get; private set; }
        public float[] SoilMoisture { get; private set; }
        public float[] AirHumidity { get; private set; }

        public float[] NutrientN { get; private set; }
        public float[] NutrientP { get; private set; }
        public float[] NutrientK { get; private set; }

        public float[] TotalBiomass { get; private set; }
        public CardinalBiomeType[] DominantBiome { get; private set; }
        public float[] DominanceShare { get; private set; }

        private readonly BiomeConfig[] biomeConfigs;

        public CardinalSimulationCore(uint macroGridSize = 50, uint subCellRes = 20, uint seed = 42)
        {
            GridSize = macroGridSize;
            SubResolution = subCellRes;
            TotalWindowResolution = subCellRes * 3; // Janela 3x3 macro células = 60x60
            Seed = seed;

            int total = (int)(TotalWindowResolution * TotalWindowResolution);

            Altitude = new float[total];
            SurfaceWater = new float[total];
            SoilMoisture = new float[total];
            AirHumidity = new float[total];

            NutrientN = new float[total];
            NutrientP = new float[total];
            NutrientK = new float[total];

            TotalBiomass = new float[total];
            DominantBiome = new CardinalBiomeType[total];
            DominanceShare = new float[total];

            biomeConfigs = InitializeBiomeConfigs();
        }

        private BiomeConfig[] InitializeBiomeConfigs()
        {
            return new BiomeConfig[]
            {
                new BiomeConfig {
                    type = CardinalBiomeType.TropicalRainforest, name = "Tropical Rainforest",
                    tempMin = 18f, tempOptLow = 24f, tempOptHigh = 30f, tempMax = 38f,
                    moistMin = 0.50f, moistOptLow = 0.70f, moistOptHigh = 1.0f, moistMax = 1.0f,
                    altMin = 0.0f, altMax = 0.65f, demandN = 0.60f, demandP = 0.40f, demandK = 0.50f,
                    maxBiomass = 1.00f, growthRate = 0.12f, mortalityRate = 0.02f
                },
                new BiomeConfig {
                    type = CardinalBiomeType.Savanna, name = "Savanna",
                    tempMin = 15f, tempOptLow = 22f, tempOptHigh = 32f, tempMax = 42f,
                    moistMin = 0.15f, moistOptLow = 0.30f, moistOptHigh = 0.60f, moistMax = 0.80f,
                    altMin = 0.0f, altMax = 0.70f, demandN = 0.30f, demandP = 0.20f, demandK = 0.30f,
                    maxBiomass = 0.60f, growthRate = 0.08f, mortalityRate = 0.02f
                },
                new BiomeConfig {
                    type = CardinalBiomeType.Desert, name = "Desert",
                    tempMin = 10f, tempOptLow = 25f, tempOptHigh = 45f, tempMax = 55f,
                    moistMin = 0.00f, moistOptLow = 0.00f, moistOptHigh = 0.15f, moistMax = 0.30f,
                    altMin = 0.0f, altMax = 0.85f, demandN = 0.05f, demandP = 0.05f, demandK = 0.05f,
                    maxBiomass = 0.15f, growthRate = 0.03f, mortalityRate = 0.04f
                },
                new BiomeConfig {
                    type = CardinalBiomeType.TemperateForest, name = "Temperate Forest",
                    tempMin = -5f, tempOptLow = 10f, tempOptHigh = 22f, tempMax = 30f,
                    moistMin = 0.30f, moistOptLow = 0.50f, moistOptHigh = 0.85f, moistMax = 1.00f,
                    altMin = 0.0f, altMax = 0.60f, demandN = 0.40f, demandP = 0.30f, demandK = 0.40f,
                    maxBiomass = 0.80f, growthRate = 0.09f, mortalityRate = 0.02f
                },
                new BiomeConfig {
                    type = CardinalBiomeType.Taiga, name = "Taiga",
                    tempMin = -20f, tempOptLow = -5f, tempOptHigh = 12f, tempMax = 20f,
                    moistMin = 0.25f, moistOptLow = 0.40f, moistOptHigh = 0.75f, moistMax = 0.95f,
                    altMin = 0.10f, altMax = 0.80f, demandN = 0.25f, demandP = 0.20f, demandK = 0.25f,
                    maxBiomass = 0.65f, growthRate = 0.06f, mortalityRate = 0.025f
                },
                new BiomeConfig {
                    type = CardinalBiomeType.Tundra, name = "Tundra",
                    tempMin = -35f, tempOptLow = -15f, tempOptHigh = 2f, tempMax = 10f,
                    moistMin = 0.10f, moistOptLow = 0.25f, moistOptHigh = 0.60f, moistMax = 0.90f,
                    altMin = 0.20f, altMax = 1.00f, demandN = 0.10f, demandP = 0.10f, demandK = 0.10f,
                    maxBiomass = 0.25f, growthRate = 0.03f, mortalityRate = 0.03f
                },
                new BiomeConfig {
                    type = CardinalBiomeType.AridShrubland, name = "Arid Shrubland",
                    tempMin = 16f, tempOptLow = 26f, tempOptHigh = 38f, tempMax = 48f,
                    moistMin = 0.08f, moistOptLow = 0.18f, moistOptHigh = 0.35f, moistMax = 0.50f,
                    altMin = 0.00f, altMax = 0.75f, demandN = 0.15f, demandP = 0.15f, demandK = 0.20f,
                    maxBiomass = 0.35f, growthRate = 0.05f, mortalityRate = 0.03f
                }
            };
        }

        public void GeneratePatch(uint cellX, uint cellY, float seaLevel = 0.45f, float oceanDepth = 0.15f)
        {
            int res = (int)TotalWindowResolution;
            float step = 1.0f / TotalWindowResolution;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int idx = y * res + x;

                    // Coordenada contínua no planeta
                    float worldX = (cellX - 1 + (float)x / SubResolution) * 0.1f + Seed * 0.05f;
                    float worldY = (cellY - 1 + (float)y / SubResolution) * 0.1f + Seed * 0.05f;

                    // Ruído Fractal fBm
                    float alt = Mathf.PerlinNoise(worldX * 1.5f, worldY * 1.5f) * 0.6f +
                                Mathf.PerlinNoise(worldX * 3.0f, worldY * 3.0f) * 0.25f +
                                Mathf.PerlinNoise(worldX * 6.0f, worldY * 6.0f) * 0.15f;
                    alt = Mathf.Clamp01(alt);

                    Altitude[idx] = alt;

                    // Condição de água
                    if (alt < seaLevel)
                    {
                        SurfaceWater[idx] = (seaLevel - alt) + oceanDepth;
                        SoilMoisture[idx] = 0.05f; // Saturação máxima
                    }
                    else
                    {
                        SurfaceWater[idx] = 0f;
                        SoilMoisture[idx] = 0.02f;
                    }

                    AirHumidity[idx] = 0.05f + SurfaceWater[idx] * 0.02f;

                    // Pedologia inicial (Curva parabólica de altitude)
                    float fertility = 4.0f * alt * (1.0f - alt);
                    NutrientN[idx] = fertility * 1.0f;
                    NutrientP[idx] = fertility * 0.7f;
                    NutrientK[idx] = fertility * 0.85f;
                }
            }

            // Atualiza afinidades e biomassa inicial
            UpdateBiomes(CalculateLatitudeTemperature(cellY));
        }

        public float CalculateLatitudeTemperature(uint macroY)
        {
            float normalizedY = (2.0f * macroY / Mathf.Max(1, GridSize - 1)) - 1.0f;
            float factor = Mathf.Cos(normalizedY * (Mathf.PI / 2.0f));
            return -10.0f + (30.0f - (-10.0f)) * factor;
        }

        public void TickSimulation(uint cellY, int ticksCount = 100, float dt = 0.1f)
        {
            float meanTemp = CalculateLatitudeTemperature(cellY);
            int res = (int)TotalWindowResolution;

            for (int t = 0; t < ticksCount; t++)
            {
                // 1. Escoamento Hidrológico D8
                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        int currIdx = y * res + x;
                        float water = SurfaceWater[currIdx];
                        if (water <= 0.001f) continue;

                        float currHead = Altitude[currIdx] + water;
                        int lowestX = x;
                        int lowestY = y;
                        float maxDrop = 0f;

                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = x + dx;
                                int ny = y + dy;

                                if (nx >= 0 && nx < res && ny >= 0 && ny < res)
                                {
                                    int nIdx = ny * res + nx;
                                    float nHead = Altitude[nIdx] + SurfaceWater[nIdx];
                                    float drop = currHead - nHead;
                                    if (drop > maxDrop)
                                    {
                                        maxDrop = drop;
                                        lowestX = nx;
                                        lowestY = ny;
                                    }
                                }
                            }
                        }

                        if ((lowestX != x || lowestY != y) && maxDrop > 0f)
                        {
                            int targetIdx = lowestY * res + lowestX;
                            float flow = Mathf.Min(water, (maxDrop * 0.5f) * 0.2f);
                            SurfaceWater[currIdx] -= flow;
                            SurfaceWater[targetIdx] += flow;

                            // Lixiviação de NPK arrastada pelo fluxo
                            float nFlow = NutrientN[currIdx] * 0.02f * flow;
                            float kFlow = NutrientK[currIdx] * 0.01f * flow;
                            float pFlow = NutrientP[currIdx] * 0.002f * flow;

                            NutrientN[currIdx] -= nFlow; NutrientN[targetIdx] += nFlow;
                            NutrientK[currIdx] -= kFlow; NutrientK[targetIdx] += kFlow;
                            NutrientP[currIdx] -= pFlow; NutrientP[targetIdx] += pFlow;
                        }
                    }
                }

                // 2. Absorção e Saturação do Solo
                for (int i = 0; i < SurfaceWater.Length; i++)
                {
                    if (SurfaceWater[i] > 0f && SoilMoisture[i] < 0.05f)
                    {
                        float space = 0.05f - SoilMoisture[i];
                        float absorb = Mathf.Min(SurfaceWater[i], space * 0.05f);
                        SurfaceWater[i] -= absorb;
                        SoilMoisture[i] += absorb;
                    }
                }
            }

            // Atualiza biomas após os ticks
            UpdateBiomes(meanTemp);
        }

        private void UpdateBiomes(float temperature)
        {
            int total = (int)(TotalWindowResolution * TotalWindowResolution);

            for (int i = 0; i < total; i++)
            {
                float alt = Altitude[i];
                float water = SurfaceWater[i];
                float soilSat = Mathf.Clamp01(SoilMoisture[i] / 0.05f);

                if (water > 0.08f)
                {
                    DominantBiome[i] = CardinalBiomeType.Desert;
                    TotalBiomass[i] = 0f;
                    DominanceShare[i] = 0f;
                    continue;
                }

                float bestAffinity = -1f;
                CardinalBiomeType bestType = CardinalBiomeType.Savanna;
                float totalAffinity = 0f;

                for (int b = 0; b < 7; b++)
                {
                    var prop = biomeConfigs[b];
                    float tAff = EvalTrapezoid(temperature, prop.tempMin, prop.tempOptLow, prop.tempOptHigh, prop.tempMax);
                    float mAff = EvalTrapezoid(soilSat, prop.moistMin, prop.moistOptLow, prop.moistOptHigh, prop.moistMax);
                    float aAff = (alt < prop.altMin || alt > prop.altMax) ? 0.2f : 1.0f;

                    float nAff = Mathf.Clamp01(NutrientN[i] / Mathf.Max(0.01f, prop.demandN));
                    float combined = tAff * mAff * aAff * (0.6f + nAff * 0.4f);

                    totalAffinity += combined;
                    if (combined > bestAffinity)
                    {
                        bestAffinity = combined;
                        bestType = prop.type;
                    }
                }

                DominantBiome[i] = bestType;
                DominanceShare[i] = totalAffinity > 0 ? (bestAffinity / totalAffinity) : 1f;
                TotalBiomass[i] = bestAffinity * biomeConfigs[(int)bestType].maxBiomass;
            }
        }

        private static float EvalTrapezoid(float val, float min, float optLow, float optHigh, float max)
        {
            if (val <= min || val >= max) return 0f;
            if (val >= optLow && val <= optHigh) return 1f;
            if (val < optLow) return (val - min) / (optLow - min);
            return (max - val) / (max - optHigh);
        }
    }
}
