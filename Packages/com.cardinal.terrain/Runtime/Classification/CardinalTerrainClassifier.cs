using UnityEngine;
using Cardinal.TerrainEngine.Sampling;

namespace Cardinal.TerrainEngine.Classification
{
    public enum CardinalTerrainType
    {
        Field = 0,
        Forest = 1,
        Mountain = 2,
        River = 3,
        Lake = 4,
        Mud = 5
    }

    public struct ClassifiedTile
    {
        public int x;
        public int z;
        public int height;
        public CardinalTerrainType type;
    }

    /// <summary>
    /// Classificador que deriva causalmente as propriedades discretas do terreno (Height e TerrainType)
    /// a partir das variáveis físico-químicas contínuas do Cardinal System.
    /// </summary>
    public static class CardinalTerrainClassifier
    {
        public static ClassifiedTile Classify(int x, int z, SampledCellData data, int maxElevation = 3)
        {
            // 1. Discretização de Altura (0 a maxElevation)
            int discreteHeight = Mathf.Clamp(Mathf.RoundToInt(data.altitude * maxElevation), 0, maxElevation);

            // 2. Classificação Causal do Tipo de Terreno
            CardinalTerrainType terrainType = CardinalTerrainType.Field;

            // Se houver lâmina d'água superficial acumulada significativa
            if (data.surfaceWater > 0.08f)
            {
                // Depressão plana ou bacia profunda = Lago; Relevo com inclinação = Rio
                terrainType = (data.altitude < 0.35f) ? CardinalTerrainType.Lake : CardinalTerrainType.River;
            }
            // Lâmina d'água rasa ou solo hiper-úmido (saturação > 80%) = Lamaçal
            else if (data.surfaceWater > 0.005f || data.soilMoisture > 0.038f)
            {
                terrainType = CardinalTerrainType.Mud;
            }
            // Altura máxima ou pico rochoso íngreme = Montanha
            else if (discreteHeight >= maxElevation || data.altitude > 0.70f)
            {
                terrainType = CardinalTerrainType.Mountain;
            }
            // Biomas florestais com densidade de biomassa consolidada = Floresta
            else if ((data.dominantBiome == 0 || data.dominantBiome == 3 || data.dominantBiome == 4) && data.biomass > 0.25f)
            {
                terrainType = CardinalTerrainType.Forest;
            }
            // Planícies, savanas e campos abertos
            else
            {
                terrainType = CardinalTerrainType.Field;
            }

            return new ClassifiedTile
            {
                x = x,
                z = z,
                height = discreteHeight,
                type = terrainType
            };
        }
    }
}
