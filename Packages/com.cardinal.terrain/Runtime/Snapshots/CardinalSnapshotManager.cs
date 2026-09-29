using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Cardinal.TerrainEngine.Core;
using Cardinal.TerrainEngine.Sampling;
using Cardinal.TerrainEngine.Classification;

namespace Cardinal.TerrainEngine.Snapshots
{
    [System.Serializable]
    public class EpochConfig
    {
        public string epochName = "Ano 1";
        public int year = 1;
        public int ticksToSimulate = 0; // Ticks a executar antes deste snapshot
    }

    [System.Serializable]
    public class CardinalSnapshotData
    {
        public int width;
        public int depth;
        public float heightStep;
        public int year;
        public string epochName;
        public List<ClassifiedTile> tiles = new List<ClassifiedTile>();
    }

    /// <summary>
    /// Gerenciador de simulação e exportação de snapshots temporais da paisagem.
    /// Permite saltos temporais de anos/décadas entre fases do jogo.
    /// </summary>
    public class CardinalSnapshotManager
    {
        private readonly CardinalSimulationCore simulation;
        private readonly uint cellX;
        private readonly uint cellY;

        public CardinalSnapshotManager(uint macroX = 24, uint macroY = 24, uint seed = 42)
        {
            cellX = macroX;
            cellY = macroY;
            simulation = new CardinalSimulationCore(50, 20, seed);
            simulation.GeneratePatch(cellX, cellY);
        }

        /// <summary>
        /// Gera uma malha de snapshot reamostrada para a dimensão do grid de jogo.
        /// </summary>
        public CardinalSnapshotData CaptureSnapshot(int targetWidth, int targetDepth, float heightStep = 0.5f, int maxElevation = 3)
        {
            var snapshot = new CardinalSnapshotData
            {
                width = targetWidth,
                depth = targetDepth,
                heightStep = heightStep,
                tiles = new List<ClassifiedTile>()
            };

            int[] biomeIndices = new int[simulation.DominantBiome.Length];
            for (int i = 0; i < biomeIndices.Length; i++)
            {
                biomeIndices[i] = (int)simulation.DominantBiome[i];
            }

            for (int z = 0; z < targetDepth; z++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    float u = (float)x / Mathf.Max(1, targetWidth - 1);
                    float v = (float)z / Mathf.Max(1, targetDepth - 1);

                    SampledCellData sampled = CardinalTerrainSampler.SampleBilinear(
                        simulation.Altitude,
                        simulation.SurfaceWater,
                        simulation.SoilMoisture,
                        simulation.TotalBiomass,
                        biomeIndices,
                        simulation.TotalWindowResolution,
                        u, v
                    );

                    ClassifiedTile tile = CardinalTerrainClassifier.Classify(x, z, sampled, maxElevation);
                    snapshot.tiles.Add(tile);
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Avança a simulação por um número de ticks e captura a nova época.
        /// </summary>
        public CardinalSnapshotData StepAndCapture(int ticksCount, int targetWidth, int targetDepth, float heightStep = 0.5f, int maxElevation = 3)
        {
            if (ticksCount > 0)
            {
                simulation.TickSimulation(cellY, ticksCount);
            }

            return CaptureSnapshot(targetWidth, targetDepth, heightStep, maxElevation);
        }

        /// <summary>
        /// Executa uma sequência completa de épocas e salva os arquivos JSON correspondentes.
        /// </summary>
        public List<CardinalSnapshotData> BakeEpochs(
            List<EpochConfig> epochs, 
            int width, 
            int depth, 
            string outputDirectory,
            float heightStep = 0.5f,
            int maxElevation = 3)
        {
            var results = new List<CardinalSnapshotData>();

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            for (int i = 0; i < epochs.Count; i++)
            {
                var epoch = epochs[i];
                var snapshot = StepAndCapture(epoch.ticksToSimulate, width, depth, heightStep, maxElevation);
                snapshot.year = epoch.year;
                snapshot.epochName = epoch.epochName;
                results.Add(snapshot);

                // Salva JSON no formato Cardinal e no formato compatível com DuckFightSwan
                string json = JsonUtility.ToJson(snapshot, true);
                string path = Path.Combine(outputDirectory, $"cardinal_snapshot_epoch_{epoch.year}.json");
                File.WriteAllText(path, json);
                Debug.Log($"[CardinalSnapshotManager] Snapshot da Época '{epoch.epochName}' (Ano {epoch.year}) salvo em: {path}");
            }

            return results;
        }
    }
}
