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

        [Header("Configurações de Stitched Subgrid (NxN)")]
        private int subgridN = 2;
        private bool enableHeightStitching = true;
        private bool enableBiomeBlending = true;

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

        [MenuItem("Window/Cardinal/Bake Snapshots (2x2 Subgrid)")]
        public static void BakeSnapshotsBatch()
        {
            var window = GetWindow<CardinalTerrainEditorWindow>("Cardinal Terrain");
            window.subgridN = 2;
            window.enableHeightStitching = true;
            window.enableBiomeBlending = true;
            window.BakeAllSnapshots();
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Cardinal System: Terrain Dev Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Gera tabuleiros táticos para Duck Fight Swans a partir da simulação causal do Cardinal em segundo plano, com suporte a Subgrid NxN e Nós Costurados (Stitched Nodes).", 
                MessageType.Info);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("1. Configurações Globais do Relevo", EditorStyles.boldLabel);
            seed = EditorGUILayout.IntField("Semente (Seed)", seed);
            macroX = EditorGUILayout.IntSlider("Coordenada X no Planeta", macroX, 0, 49);
            macroY = EditorGUILayout.IntSlider("Coordenada Y (Latitude)", macroY, 0, 49);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("2. Dimensões Macro & Subgrid NxN (Unity)", EditorStyles.boldLabel);
            boardWidth = EditorGUILayout.IntSlider("Largura Macro (X)", boardWidth, 6, 25);
            boardDepth = EditorGUILayout.IntSlider("Profundidade Macro (Z)", boardDepth, 6, 25);
            maxElevation = EditorGUILayout.IntSlider("Altura Máxima dos Blocos", maxElevation, 1, 5);
            heightStep = EditorGUILayout.FloatField("Degrau Vertical (HeightStep)", heightStep);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Parâmetros do Subgrid & Nós Costurados:", EditorStyles.miniBoldLabel);
            subgridN = EditorGUILayout.IntSlider("Subgrid por Tile (NxN)", subgridN, 1, 4);
            enableHeightStitching = EditorGUILayout.Toggle("Costura Suave de Relevo", enableHeightStitching);
            enableBiomeBlending = EditorGUILayout.Toggle("Transição Ecológica de Biomas", enableBiomeBlending);

            int finalW = boardWidth * subgridN;
            int finalD = boardDepth * subgridN;
            EditorGUILayout.HelpBox($"Escala Final: Macro {boardWidth}x{boardDepth} -> Subgrid {subgridN}x{subgridN} = {finalW}x{finalD} tiles ({finalW * finalD} nós táticos no jogo).", MessageType.Info);

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
            EditorGUILayout.LabelField("4. Ações de Geração & Pré-Visualização", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview Ano 1", GUILayout.Height(30)))
            {
                PreviewInScene(0);
            }
            if (GUILayout.Button("Preview Ano 15", GUILayout.Height(30)))
            {
                PreviewInScene(1);
            }
            if (GUILayout.Button("Preview Ano 30", GUILayout.Height(30)))
            {
                PreviewInScene(2);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Bake e Exportar Todos os Snapshots (.JSON)", GUILayout.Height(38)))
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
                statusMessage = "Aviso: GridManager não encontrado na cena atual. Abra uma cena com GridManager para visualizar.";
                return;
            }

            var manager = new CardinalSnapshotManager((uint)macroX, (uint)macroY, (uint)seed);
            CardinalSnapshotData snapshot = null;

            // Avança cumulativamente a simulação até a época solicitada
            for (int i = 0; i <= epochIndex && i < epochs.Count; i++)
            {
                int ticks = epochs[i].ticksToSimulate;
                snapshot = manager.StepAndCapture(ticks, boardWidth, boardDepth, heightStep, maxElevation, subgridN, enableHeightStitching, enableBiomeBlending);
            }

            if (snapshot != null)
            {
                LevelSaveData saveData = ConvertToLevelSaveData(snapshot);
                GridManager.Instance.GenerateGrid(saveData);

                string epName = (epochIndex < epochs.Count) ? epochs[epochIndex].epochName : $"Época {epochIndex + 1}";
                statusMessage = $"Preview da '{epName}' gerado com sucesso no GridManager ({snapshot.width}x{snapshot.depth})!";
            }
        }

        private void BakeAllSnapshots()
        {
            string outputDir = Path.Combine(Application.dataPath, "_Data", "Epochs");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            var manager = new CardinalSnapshotManager((uint)macroX, (uint)macroY, (uint)seed);

            for (int i = 0; i < epochs.Count; i++)
            {
                var epoch = epochs[i];
                var snapshot = manager.StepAndCapture(epoch.ticksToSimulate, boardWidth, boardDepth, heightStep, maxElevation, subgridN, enableHeightStitching, enableBiomeBlending);
                snapshot.year = epoch.year;
                snapshot.epochName = epoch.epochName;

                LevelSaveData saveData = ConvertToLevelSaveData(snapshot);

                // Adiciona tropas padrão na fase 1 posicionadas no centro das macro-células
                if (i == 0)
                {
                    int duckX = 1 * subgridN + subgridN / 2;
                    int swanX = (boardWidth - 2) * subgridN + subgridN / 2;
                    int z1 = 3 * subgridN + subgridN / 2;
                    int z2 = 6 * subgridN + subgridN / 2;

                    saveData.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Warrior", x = duckX, z = z1, currentHealth = 100 });
                    saveData.units.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Archer", x = duckX, z = z2, currentHealth = 100 });
                    saveData.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = swanX, z = z1, currentHealth = 100 });
                    saveData.units.Add(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = swanX, z = z2, currentHealth = 100 });
                }

                int phaseNumber = i + 1;
                LevelDataManager.SaveLevel(phaseNumber, saveData);

                // Grava também com o nome da época para controle de campanha
                string epochPath = Path.Combine(outputDir, $"level_save_epoch_{epoch.year}.json");
                File.WriteAllText(epochPath, JsonUtility.ToJson(saveData, true));
            }

            AssetDatabase.Refresh();
            statusMessage = $"Sucesso! {epochs.Count} snapshots exportados com Subgrid {subgridN}x{subgridN} ({boardWidth * subgridN}x{boardDepth * subgridN}) em {outputDir}.";
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
