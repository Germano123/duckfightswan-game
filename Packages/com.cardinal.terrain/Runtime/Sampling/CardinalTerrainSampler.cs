using UnityEngine;

namespace Cardinal.TerrainEngine.Sampling
{
    public struct SampledCellData
    {
        public float altitude;
        public float surfaceWater;
        public float soilMoisture;
        public float biomass;
        public int dominantBiome;
    }

    /// <summary>
    /// Reamostrador espacial com interpolação bilinear da grade do Cardinal para o grid de jogo da Unity.
    /// </summary>
    public static class CardinalTerrainSampler
    {
        public static SampledCellData SampleBilinear(
            float[] altitudeGrid,
            float[] waterGrid,
            float[] soilGrid,
            float[] biomassGrid,
            int[] biomeGrid,
            uint sourceResolution,
            float normalizedU,
            float normalizedV)
        {
            float px = Mathf.Clamp(normalizedU * (sourceResolution - 1), 0f, sourceResolution - 1);
            float py = Mathf.Clamp(normalizedV * (sourceResolution - 1), 0f, sourceResolution - 1);

            int x0 = (int)px;
            int y0 = (int)py;
            int x1 = Mathf.Min(x0 + 1, (int)sourceResolution - 1);
            int y1 = Mathf.Min(y0 + 1, (int)sourceResolution - 1);

            float fx = px - x0;
            float fy = py - y0;

            int idx00 = y0 * (int)sourceResolution + x0;
            int idx10 = y0 * (int)sourceResolution + x1;
            int idx01 = y1 * (int)sourceResolution + x0;
            int idx11 = y1 * (int)sourceResolution + x1;

            float alt = BiLerp(altitudeGrid[idx00], altitudeGrid[idx10], altitudeGrid[idx01], altitudeGrid[idx11], fx, fy);
            float water = BiLerp(waterGrid[idx00], waterGrid[idx10], waterGrid[idx01], waterGrid[idx11], fx, fy);
            float soil = BiLerp(soilGrid[idx00], soilGrid[idx10], soilGrid[idx01], soilGrid[idx11], fx, fy);
            float bio = BiLerp(biomassGrid[idx00], biomassGrid[idx10], biomassGrid[idx01], biomassGrid[idx11], fx, fy);

            // Bioma dominante do vizinho mais próximo
            int nearestIdx = (fx < 0.5f) ? ((fy < 0.5f) ? idx00 : idx01) : ((fy < 0.5f) ? idx10 : idx11);
            int biome = biomeGrid != null ? biomeGrid[nearestIdx] : 0;

            return new SampledCellData
            {
                altitude = alt,
                surfaceWater = water,
                soilMoisture = soil,
                biomass = bio,
                dominantBiome = biome
            };
        }

        private static float BiLerp(float v00, float v10, float v01, float v11, float fx, float fy)
        {
            float top = Mathf.Lerp(v00, v10, fx);
            float bottom = Mathf.Lerp(v01, v11, fx);
            return Mathf.Lerp(top, bottom, fy);
        }
    }
}
