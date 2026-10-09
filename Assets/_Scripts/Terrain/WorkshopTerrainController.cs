using UnityEngine;

namespace DuckFightSwan.Terrain
{
    /// <summary>
    /// Controlador interativo de terreno para a oficina Cardinal.
    /// Permite que estudantes e instrutores alternem entre épocas (Ano 1, 15, 30)
    /// e visualizem o comparativo com Perlin Noise ao vivo em tempo de execução (Play Mode).
    /// </summary>
    public class WorkshopTerrainController : MonoBehaviour
    {
        [Header("Configuração de Inicialização")]
        [SerializeField] private bool autoLoadOnStart = true;
        [SerializeField] private int defaultEpochYear = 1;

        [Header("Interface da Oficina")]
        [SerializeField] private bool showOnScreenControls = true;

        private string currentStatus = "Pronto. Pressione [1], [2], [3] ou clique nos botões para alternar as épocas.";
        private string activeEpochLabel = "Ano 1";

        private void Start()
        {
            if (autoLoadOnStart)
            {
                LoadEpoch(defaultEpochYear);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                LoadEpoch(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                LoadEpoch(15);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                LoadEpoch(30);
            }
            else if (Input.GetKeyDown(KeyCode.P))
            {
                GeneratePerlinComparison();
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                GenerateStitchedDemo();
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                ReloadCurrent();
            }
        }

        /// <summary>
        /// Carrega os dados do snapshot da época indicada e reconstrói o tabuleiro 3D.
        /// </summary>
        public void LoadEpoch(int year)
        {
            if (GridManager.Instance == null)
            {
                Debug.LogWarning("[WorkshopTerrainController] GridManager.Instance não encontrado!");
                return;
            }

            LevelSaveData data = LevelDataManager.LoadEpoch(year);
            if (data != null)
            {
                GridManager.Instance.GenerateGrid(data);
                activeEpochLabel = $"Ano {year}";
                currentStatus = $"Época carregada: Ano {year} ({data.width}x{data.depth} tiles - {data.tiles.Count} nós).";
            }
            else
            {
                currentStatus = $"Erro ao carregar dados do Ano {year}.";
            }
        }

        /// <summary>
        /// Gera a visualização comparativa com Perlin Noise puro.
        /// </summary>
        public void GeneratePerlinComparison()
        {
            if (GridManager.Instance == null) return;

            GridManager.Instance.GeneratePerlinNoiseComparison();
            activeEpochLabel = "Perlin Noise (Matemático Puro)";
            currentStatus = "Comparativo Ativo: Ruído de Perlin isolado (sem causalidade ambiental).";
        }

        /// <summary>
        /// Gera a demonstração pedagógica de Stitched Grid Expansion (5x5) com subgrids 2x2,
        /// linha/coluna de costura em cubos brancos e offset espacial de 0.2.
        /// </summary>
        public void GenerateStitchedDemo()
        {
            if (GridManager.Instance == null) return;

            GridManager.Instance.GenerateStitchedDemo5x5();
            activeEpochLabel = "Stitched Nodes 5x5 (Offset 0.2)";
            currentStatus = "Stitched Grid 5x5 ativo: 4 quadrantes 2x2 isolados por costura branca (offset 0.2).";
        }

        /// <summary>
        /// Recarrega a época atual para reembaralhar rotações e decorações aleatórias.
        /// </summary>
        public void ReloadCurrent()
        {
            if (activeEpochLabel.Contains("Ano 15")) LoadEpoch(15);
            else if (activeEpochLabel.Contains("Ano 30")) LoadEpoch(30);
            else if (activeEpochLabel.Contains("Perlin")) GeneratePerlinComparison();
            else if (activeEpochLabel.Contains("Stitched")) GenerateStitchedDemo();
            else LoadEpoch(1);
        }

        private void OnGUI()
        {
            if (!showOnScreenControls) return;

            // Painel da oficina no canto superior esquerdo
            GUILayout.BeginArea(new Rect(15, 15, 360, 250), GUI.skin.box);
            
            GUILayout.Label("<b>Cardinal System • Oficina Unity</b>", GUILayout.ExpandWidth(true));
            GUILayout.Label($"<b>Estado Atual:</b> <color=cyan>{activeEpochLabel}</color>");
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ano 1 (Inicial)", GUILayout.Height(32)))
            {
                LoadEpoch(1);
            }
            if (GUILayout.Button("Ano 15 (Erosão)", GUILayout.Height(32)))
            {
                LoadEpoch(15);
            }
            if (GUILayout.Button("Ano 30 (Seca/Vau)", GUILayout.Height(32)))
            {
                LoadEpoch(30);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Perlin Noise", GUILayout.Height(28)))
            {
                GeneratePerlinComparison();
            }
            if (GUILayout.Button("Stitched Demo 5x5", GUILayout.Height(28)))
            {
                GenerateStitchedDemo();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("<size=10><b>Atalhos:</b> [1] Ano 1 | [2] Ano 15 | [3] Ano 30 | [P] Perlin | [S] Stitched</size>");
            GUILayout.Label($"<size=9><i>{currentStatus}</i></size>");

            GUILayout.EndArea();
        }
    }
}
