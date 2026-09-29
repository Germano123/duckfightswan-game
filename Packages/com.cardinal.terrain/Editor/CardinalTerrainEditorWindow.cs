using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Cardinal.TerrainEngine.Snapshots;
using DuckFightSwan.Terrain;
using DuckFightSwan.Units;

namespace Cardinal.TerrainEngine.Editor
{
    public class CardinalTerrainEditorWindow : EditorWindow
    {
        [Header("Configurações do Planeta Cardinal")]
        private int seed = 42;
        private int macroX = 24;
        private int macroY = 24;

        [Header("Escala e Dimensões do Jogo (Duck Fight Swans)")]
        private int boardWidth = 15;
        private int boardDepth = 15;
        private int maxElevation = 3;
        private float heightStep = 0.5f;

        [Header("Épocas e Saltos Temporais")]
        private readonly List<EpochConfig> epochs = new List<EpochConfig>
        {
            new EpochConfig { epochName = "Primeira Luta - Ano 1", year = 1, ticksToSimulate = 0 },
            new EpochConfig { epochName = "Segunda Luta - Ano 15", year = 15, ticksToSimulate = 1500 },
            new EpochConfig { epochName = "Terceira Luta - Ano 30", year = 30, ticksToSimulate = 1500 }
        };

        private Vector2 scrollPos;
        private string statusMessage = "Pronto para gerar terrenos.";

        [MenuItem("Window/Cardinal/Terrain Generator")]
        public static void OpenWindow()
        {
            var window = GetWindow<CardinalTerrainEditorWindow>("Cardinal Terrain");
            window.minSize = new Vector2(420, 520);
            window.Show();
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Cardinal System: Terrain Dev Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Gera tabuleiros táticos para Duck Fight Swans a partir da simulação causal do Cardinal em segundo plano.", 
                MessageType.Info);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("1. Configurações Globais do Relevo", EditorStyles.boldLabel);
            seed = EditorGUILayout.IntField("Semente (Seed)", seed);
            macroX = EditorGUILayout.IntSlider("Coordenada X no Planeta", macroX, 0, 49);
            macroY = EditorGUILayout.IntSlider("Coordenada Y (Latitude)", macroY, 0, 49);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("2. Dimensões do Tabuleiro (Unity)", EditorStyles.boldLabel);
            boardWidth = EditorGUILayout.IntSlider("Largura (X)", boardWidth, 8, 30);
            boardDepth = EditorGUILayout.IntSlider("Profundidade (Z)", boardDepth, 8, 30);
            maxElevation = EditorGUILayout.IntSlider("Altura Máxima dos Blocos", maxElevation, 1, 5);
            heightStep = EditorGUILayout.FloatField("Degrau Vertical (HeightStep)", heightStep);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("3. Épocas da Campanha (Snapshots Temporais)", EditorStyles.boldLabel);
            for (int i = 0; i < epochs.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                epochs[i].epochName = EditorGUILayout.TextField("Nome da Época", epochs[i].epochName);
                epochs[i].year = EditorGUILayout.IntField("Ano", epochs[i].year);
                epochs[i].ticksToSimulate = EditorGUILayout.IntField("Ticks de Simulação", epochs[i].ticksToSimulate);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("4. Ações de Geração", EditorStyles.boldLabel);

            if (GUILayout.Button("Visualizar Ano 1 na Cena (Preview)", GUILayout.Height(32)))
            {
                PreviewInScene(0);
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Bake e Exportar Snapshots para Duck Fight Swans", GUILayout.Height(40)))
            {
                BakeAllSnapshots();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private void PreviewInScene(int epochIndex)
        {
            if (GridManager.Instance == null)
            {
                statusMessage = "Aviso: GridManager não encontrado na cena atual. Abra SampleScene para visualizar.";
                return;
            }

            var manager = new CardinalSnapshotManager((uint)macroX, (uint)macroY, (uint)seed);
            int ticks = (epochIndex < epochs.Count) ? epochs[epochIndex].ticksToSimulate : 0;
            var snapshot = manager.StepAndCapture(ticks, boardWidth, boardDepth, heightStep, maxElevation);

            LevelSaveData saveData = ConvertToLevelSaveData(snapshot);
            GridManager.Instance.GenerateGrid(saveData);

            statusMessage = $"Preview gerado com sucesso no GridManager ({boardWidth}x{boardDepth})!";
        }

        private void BakeAllSnapshots()
        {
            var manager = new CardinalSnapshotManager((uint)macroX, (uint)macroY, (uint)seed);
            string outputDir = Application.dataPath;

            for (int i = 0; i < epochs.Count; i++)
            {
                var epoch = epochs[i];
                var snapshot = manager.StepAndCapture(epoch.ticksToSimulate, boardWidth, boardDepth, heightStep, maxElevation);
                snapshot.year = epoch.year;
                snapshot.epochName = epoch.epochName;

                LevelSaveData saveData = ConvertToLevelSaveData(snapshot);

                // Adiciona tropas padrão na fase 1
                if (i == 0)
                {
                    saveData.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Warrior", x = 1, z = 3, currentHealth = 100 });
                    saveData.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Archer", x = 1, z = 6, currentHealth = 100 });
                    saveData.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = boardWidth - 2, z = 3, currentHealth = 100 });
                    saveData.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = boardWidth - 2, z = 6, currentHealth = 100 });
                }

                int phaseNumber = i + 1;
                LevelDataManager.SaveLevel(phaseNumber, saveData);

                // Grava também com o nome da época para controle de campanha
                string epochPath = Path.Combine(outputDir, $"level_save_epoch_{epoch.year}.json");
                File.WriteAllText(epochPath, JsonUtility.ToJson(saveData, true));
            }

            AssetDatabase.Refresh();
            statusMessage = $"Sucesso! {epochs.Count} snapshots exportados para as fases 1 a {epochs.Count} em {outputDir}.";
        }

        private LevelSaveData ConvertToLevelSaveData(CardinalSnapshotData snapshot)
        {
            var data = new LevelSaveData
            {
                width = snapshot.width,
                depth = snapshot.depth,
                heightStep = snapshot.heightStep,
                tiles = new List<TileSaveData>(),
                units = new List<UnitSaveData>()
            };

            foreach (var tile in snapshot.tiles)
            {
                data.tiles.Add(new TileSaveData
                {
                    x = tile.x,
                    z = tile.z,
                    height = tile.height,
                    type = (TerrainType)(int)tile.type
                });
            }

            return data;
        }
    }
}
