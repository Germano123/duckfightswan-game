using System;
using System.Collections.Generic;
using UnityEngine;
using Cardinal.TerrainEngine.Sampling;
using Cardinal.TerrainEngine.Classification;

namespace Cardinal.TerrainEngine.Core
{
    /// <summary>
    /// Configuração para o gerador de subgrid com nós costurados (Stitched Nodes).
    /// </summary>
    [System.Serializable]
    public struct StitchedSubgridConfig
    {
        [Tooltip("Fator de subdivisão NxN para cada macro-tile (Padrão: 2 para subgrid 2x2).")]
        public int subgridN;

        [Tooltip("Espessura visual vertical de cada degrau de relevo.")]
        public float heightStep;

        [Tooltip("Altura máxima discreta permitida no relevo.")]
        public int maxElevation;

        [Tooltip("Ativa a interpolação suave de elevação entre macro-tiles vizinhos.")]
        public bool enableHeightStitching;

        [Tooltip("Ativa a geração de zonas de transição ecológica (margens, lamaçais, contrafortes) nas fronteiras.")]
        public bool enableBiomeBlending;

        [Tooltip("Fator de suavização do relevo nas bordas (0 = degrau rígido, 1 = transição totalmente contínua).")]
        public float heightSmoothingFactor;

        public static StitchedSubgridConfig Default => new StitchedSubgridConfig
        {
            subgridN = 2,
            heightStep = 0.5f,
            maxElevation = 3,
            enableHeightStitching = true,
            enableBiomeBlending = true,
            heightSmoothingFactor = 0.75f
        };
    }

    /// <summary>
    /// Gerador de terreno que expande cada macro-tile do Cardinal em um subgrid de NxN micro-tiles,
    /// aplicando o algoritmo de 'Stitched Nodes' (Nós Costurados) para garantir transições suaves
    /// de altitude (sem degraus excessivos) e de tipos de biomas na camada de jogo.
    /// Respeita o SRP e SOLID.
    /// </summary>
    public static class CardinalStitchedSubgridGenerator
    {
        /// <summary>
        /// Gera a malha de subgrid costurada a partir dos tensores físicos contínuos da simulação Cardinal.
        /// </summary>
        public static List<ClassifiedTile> GenerateFromSimulation(
            CardinalSimulationCore simulation,
            int macroWidth,
            int macroDepth,
            StitchedSubgridConfig config)
        {
            int n = Mathf.Max(1, config.subgridN);
            int targetWidth = macroWidth * n;
            int targetDepth = macroDepth * n;

            ClassifiedTile[,] tempGrid = new ClassifiedTile[targetWidth, targetDepth];

            int[] biomeIndices = new int[simulation.DominantBiome.Length];
            for (int i = 0; i < biomeIndices.Length; i++)
            {
                biomeIndices[i] = (int)simulation.DominantBiome[i];
            }

            // 1. Amostragem contínua dos tensores físico-químicos no espaço normalizado do subgrid
            for (int gz = 0; gz < targetDepth; gz++)
            {
                for (int gx = 0; gx < targetWidth; gx++)
                {
                    float u = (gx + 0.5f) / targetWidth;
                    float v = (gz + 0.5f) / targetDepth;

                    SampledCellData sampled = CardinalTerrainSampler.SampleBilinear(
                        simulation.Altitude,
                        simulation.SurfaceWater,
                        simulation.SoilMoisture,
                        simulation.TotalBiomass,
                        biomeIndices,
                        simulation.TotalWindowResolution,
                        u, v
                    );

                    tempGrid[gx, gz] = CardinalTerrainClassifier.Classify(gx, gz, sampled, config.maxElevation);
                }
            }

            // 2. Se a costura estiver ativa e N > 1, refina alturas e biomas com o algoritmo de Stitched Nodes
            if (n > 1)
            {
                RefineStitchedGrid(tempGrid, macroWidth, macroDepth, n, config);
            }

            // 3. Converte a matriz 2D em lista linear
            List<ClassifiedTile> result = new List<ClassifiedTile>(targetWidth * targetDepth);
            for (int gz = 0; gz < targetDepth; gz++)
            {
                for (int gx = 0; gx < targetWidth; gx++)
                {
                    result.Add(tempGrid[gx, gz]);
                }
            }

            return result;
        }

        /// <summary>
        /// Gera a malha de subgrid costurada a partir de uma matriz de macro-tiles já discretizados.
        /// Permite expandir snapshots salvos ou mapas existentes para o novo padrão NxN.
        /// </summary>
        public static List<ClassifiedTile> GenerateFromMacroGrid(
            List<ClassifiedTile> macroTiles,
            int macroWidth,
            int macroDepth,
            StitchedSubgridConfig config)
        {
            int n = Mathf.Max(1, config.subgridN);
            int targetWidth = macroWidth * n;
            int targetDepth = macroDepth * n;

            // Mapeia macro tiles em grade 2D
            ClassifiedTile[,] macroGrid = new ClassifiedTile[macroWidth, macroDepth];
            foreach (var t in macroTiles)
            {
                if (t.x >= 0 && t.x < macroWidth && t.z >= 0 && t.z < macroDepth)
                {
                    macroGrid[t.x, t.z] = t;
                }
            }

            ClassifiedTile[,] subGrid = new ClassifiedTile[targetWidth, targetDepth];

            // 1. Expansão inicial para sub-tiles
            for (int mz = 0; mz < macroDepth; mz++)
            {
                for (int mx = 0; mx < macroWidth; mx++)
                {
                    ClassifiedTile mTile = macroGrid[mx, mz];

                    for (int j = 0; j < n; j++)
                    {
                        for (int i = 0; i < n; i++)
                        {
                            int gx = mx * n + i;
                            int gz = mz * n + j;

                            subGrid[gx, gz] = new ClassifiedTile
                            {
                                x = gx,
                                z = gz,
                                height = mTile.height,
                                type = mTile.type
                            };
                        }
                    }
                }
            }

            // 2. Refinamento de Nós Costurados
            if (n > 1)
            {
                RefineStitchedGrid(subGrid, macroWidth, macroDepth, n, config);
            }

            // 3. Converte para lista
            List<ClassifiedTile> result = new List<ClassifiedTile>(targetWidth * targetDepth);
            for (int gz = 0; gz < targetDepth; gz++)
            {
                for (int gx = 0; gx < targetWidth; gx++)
                {
                    result.Add(subGrid[gx, gz]);
                }
            }

            return result;
        }

        /// <summary>
        /// Núcleo do algoritmo de 'Stitched Nodes':
        /// Analisa vizinhos ortogonais e diagonais, interpolando alturas com pesos cúbicos
        /// e aplicando transições ecológicas suaves (margens, lamaçais, colinas) nas fronteiras.
        /// </summary>
        private static void RefineStitchedGrid(
            ClassifiedTile[,] grid,
            int macroWidth,
            int macroDepth,
            int n,
            StitchedSubgridConfig config)
        {
            int totalWidth = macroWidth * n;
            int totalDepth = macroDepth * n;

            // Extrai alturas em float para cálculo de interpolação contínua
            float[,] floatHeights = new float[totalWidth, totalDepth];
            for (int gz = 0; gz < totalDepth; gz++)
            {
                for (int gx = 0; gx < totalWidth; gx++)
                {
                    floatHeights[gx, gz] = grid[gx, gz].height;
                }
            }

            // A. Costura e Interpolação de Altura (Height Stitching)
            if (config.enableHeightStitching)
            {
                for (int mz = 0; mz < macroDepth; mz++)
                {
                    for (int mx = 0; mx < macroWidth; mx++)
                    {
                        float hCenter = GetMacroAverageHeight(grid, mx, mz, n);

                        for (int j = 0; j < n; j++)
                        {
                            for (int i = 0; i < n; i++)
                            {
                                int gx = mx * n + i;
                                int gz = mz * n + j;

                                // Posição normalizada do nó no macro-tile [0.0, 1.0]
                                float uLocal = (i + 0.5f) / n;
                                float vLocal = (j + 0.5f) / n;

                                // Direções dos vizinhos influenciadores no quadrante
                                int stepX = (uLocal >= 0.5f) ? 1 : -1;
                                int stepZ = (vLocal >= 0.5f) ? 1 : -1;

                                int neighborMx = Mathf.Clamp(mx + stepX, 0, macroWidth - 1);
                                int neighborMz = Mathf.Clamp(mz + stepZ, 0, macroDepth - 1);

                                float hAdjX = GetMacroAverageHeight(grid, neighborMx, mz, n);
                                float hAdjZ = GetMacroAverageHeight(grid, mx, neighborMz, n);
                                float hDiag = GetMacroAverageHeight(grid, neighborMx, neighborMz, n);

                                // Distância relativa ao centro da célula [0.0 no centro, 1.0 na quina mais externa]
                                float tx = Mathf.Abs(uLocal - 0.5f) * 2f; // 0 a 1
                                float tz = Mathf.Abs(vLocal - 0.5f) * 2f; // 0 a 1

                                // Curva cúbica Hermite Smoothstep para suavizar a transição
                                float wx = tx * tx * (3f - 2f * tx) * config.heightSmoothingFactor;
                                float wz = tz * tz * (3f - 2f * tz) * config.heightSmoothingFactor;

                                // Interpolação bilinear com vizinhos
                                float top = Mathf.Lerp(hCenter, hAdjX, wx);
                                float bottom = Mathf.Lerp(hAdjZ, hDiag, wx);
                                float interpolatedH = Mathf.Lerp(top, bottom, wz);

                                floatHeights[gx, gz] = interpolatedH;
                            }
                        }
                    }
                }

                // Aplica e limita declividades (Slope Clamping): nós vizinhos imediatos diferem por no máximo 1 degrau
                ApplySlopeClamping(floatHeights, totalWidth, totalDepth, config.maxElevation);

                for (int gz = 0; gz < totalDepth; gz++)
                {
                    for (int gx = 0; gx < totalWidth; gx++)
                    {
                        var tile = grid[gx, gz];
                        tile.height = Mathf.Clamp(Mathf.RoundToInt(floatHeights[gx, gz]), 0, config.maxElevation);
                        grid[gx, gz] = tile;
                    }
                }
            }

            // B. Costura e Transição Suave de Tipos de Terreno (Stitched Biome Blending)
            if (config.enableBiomeBlending)
            {
                ClassifiedTile[,] blendedGrid = (ClassifiedTile[,])grid.Clone();

                for (int gz = 0; gz < totalDepth; gz++)
                {
                    for (int gx = 0; gx < totalWidth; gx++)
                    {
                        int mx = gx / n;
                        int mz = gz / n;
                        int localX = gx % n;
                        int localZ = gz % n;

                        // Verifica se este sub-nó está situado em uma borda do macro-tile
                        bool isWestEdge  = (localX == 0 && mx > 0);
                        bool isEastEdge  = (localX == n - 1 && mx < macroWidth - 1);
                        bool isSouthEdge = (localZ == 0 && mz > 0);
                        bool isNorthEdge = (localZ == n - 1 && mz < macroDepth - 1);

                        if (!isWestEdge && !isEastEdge && !isSouthEdge && !isNorthEdge)
                        {
                            continue; // Nó no miolo/núcleo do macro-tile preserva seu bioma nativo
                        }

                        CardinalTerrainType myType = grid[gx, gz].type;
                        int myHeight = grid[gx, gz].height;

                        // Analisa os tipos de terrenos dos macro-tiles vizinhos imediatos
                        List<CardinalTerrainType> neighborTypes = new List<CardinalTerrainType>();
                        if (isWestEdge)  neighborTypes.Add(GetMacroDominantType(grid, mx - 1, mz, n));
                        if (isEastEdge)  neighborTypes.Add(GetMacroDominantType(grid, mx + 1, mz, n));
                        if (isSouthEdge) neighborTypes.Add(GetMacroDominantType(grid, mx, mz - 1, n));
                        if (isNorthEdge) neighborTypes.Add(GetMacroDominantType(grid, mx, mz + 1, n));

                        CardinalTerrainType finalType = myType;

                        foreach (var nType in neighborTypes)
                        {
                            if (nType == myType) continue;

                            // 1. Lago (Lake) encontrando Terra Seca (Field / Forest):
                            // O sub-tile de borda gera uma zona de margem/lamaçal (Mud) ou água rasa (River)
                            if (myType == CardinalTerrainType.Lake && (nType == CardinalTerrainType.Field || nType == CardinalTerrainType.Forest))
                            {
                                finalType = CardinalTerrainType.Mud;
                            }
                            else if ((myType == CardinalTerrainType.Field || myType == CardinalTerrainType.Forest) && nType == CardinalTerrainType.Lake)
                            {
                                finalType = CardinalTerrainType.Mud;
                            }
                            // 2. Montanha (Mountain) encontrando Planície (Field):
                            // Se a altura foi suavizada para patamares mais baixos, vira colina de campo ou lama
                            else if (myType == CardinalTerrainType.Mountain && nType == CardinalTerrainType.Field)
                            {
                                if (myHeight <= 1)
                                {
                                    finalType = CardinalTerrainType.Field;
                                }
                            }
                            // 3. Rio (River) encontrando Campo seco:
                            // Borda imediata da calha do rio forma faixa ripária úmida (Mud)
                            else if (myType == CardinalTerrainType.Field && nType == CardinalTerrainType.River)
                            {
                                if (myHeight == 0)
                                {
                                    finalType = CardinalTerrainType.Mud;
                                }
                            }
                        }

                        // Preserva integridade de Lagos: se permaneceu Lake, altura é travada em 0 (depressão hidrológica)
                        if (finalType == CardinalTerrainType.Lake)
                        {
                            var t = blendedGrid[gx, gz];
                            t.height = 0;
                            t.type = finalType;
                            blendedGrid[gx, gz] = t;
                        }
                        else
                        {
                            var t = blendedGrid[gx, gz];
                            t.type = finalType;
                            blendedGrid[gx, gz] = t;
                        }
                    }
                }

                // Aplica a grade suavizada de volta
                for (int gz = 0; gz < totalDepth; gz++)
                {
                    for (int gx = 0; gx < totalWidth; gx++)
                    {
                        grid[gx, gz] = blendedGrid[gx, gz];
                    }
                }
            }
        }

        private static float GetMacroAverageHeight(ClassifiedTile[,] grid, int mx, int mz, int n)
        {
            float sum = 0f;
            int count = 0;
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    sum += grid[mx * n + i, mz * n + j].height;
                    count++;
                }
            }
            return count > 0 ? sum / count : 0f;
        }

        private static CardinalTerrainType GetMacroDominantType(ClassifiedTile[,] grid, int mx, int mz, int n)
        {
            Dictionary<CardinalTerrainType, int> counts = new Dictionary<CardinalTerrainType, int>();
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    var t = grid[mx * n + i, mz * n + j].type;
                    if (!counts.ContainsKey(t)) counts[t] = 0;
                    counts[t]++;
                }
            }

            CardinalTerrainType dominant = CardinalTerrainType.Field;
            int maxCount = -1;
            foreach (var kvp in counts)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    dominant = kvp.Key;
                }
            }
            return dominant;
        }

        /// <summary>
        /// Aplica relaxamento iterativo para garantir que sub-tiles adjacentes não ultrapassem
        /// o limite de declividade suportado pelas regras de escalada tática das tropas.
        /// </summary>
        private static void ApplySlopeClamping(float[,] heights, int width, int depth, int maxHeight)
        {
            int maxIterations = 3;
            for (int iter = 0; iter < maxIterations; iter++)
            {
                bool anyChanged = false;
                for (int z = 0; z < depth; z++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float current = heights[x, z];

                        // Verifica 4 vizinhos diretos
                        int[] dx = { 1, -1, 0, 0 };
                        int[] dz = { 0, 0, 1, -1 };

                        for (int k = 0; k < 4; k++)
                        {
                            int nx = x + dx[k];
                            int nz = z + dz[k];

                            if (nx >= 0 && nx < width && nz >= 0 && nz < depth)
                            {
                                float diff = heights[nx, nz] - current;
                                // Se a diferença for superior a 1.25 degrau, suaviza em direção ao patamar viável
                                if (diff > 1.25f)
                                {
                                    heights[nx, nz] = current + 1.0f;
                                    anyChanged = true;
                                }
                                else if (diff < -1.25f)
                                {
                                    heights[nx, nz] = current - 1.0f;
                                    anyChanged = true;
                                }
                            }
                        }
                    }
                }
                if (!anyChanged) break;
            }
        }
    }
}
