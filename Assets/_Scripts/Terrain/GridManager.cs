using UnityEngine;
using System.Collections.Generic;
using DuckFightSwan.Units;

namespace DuckFightSwan.Terrain
{
    /// <summary>
    /// Gerencia o tabuleiro de Tiles (eixos X e Z), controlando a geração procedural
    /// com limites de declividade e as consultas espaciais do grid.
    /// Respeita o SRP ao lidar apenas com a malha do tabuleiro.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [Header("Configurações do Grid")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private int width = 15;
        [SerializeField] private int depth = 15;
        [SerializeField] private float heightStep = 0.5f;

        [Header("Paleta de Assets 3D (Oficina)")]
        [SerializeField] private TerrainAssetPalette assetPalette;

        public TerrainAssetPalette AssetPalette
        {
            get => assetPalette;
            set => assetPalette = value;
        }

        [Header("Configuração de Subgrid NxN")]
        [SerializeField] private int subgridResolution = 2;
        [SerializeField] private bool enableStitchedSmoothing = true;

        [Header("Configurações de Stitched Nodes & Oficinas")]
        [SerializeField] private bool useSeamOffsets = true;
        [SerializeField] private float seamOffset = 0.2f;
        [SerializeField] private int subgridN = 2;

        public bool UseSeamOffsets
        {
            get => useSeamOffsets;
            set => useSeamOffsets = value;
        }

        public float SeamOffset
        {
            get => seamOffset;
            set => seamOffset = value;
        }

        public int SubgridN
        {
            get => subgridN;
            set => subgridN = value;
        }

        private Material whiteSeamMaterial;

        private TileNode[,] grid;

        public int Width => width;
        public int Depth => depth;
        public int SubgridResolution => subgridResolution;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // Start foi removido. A geração do grid agora é disparada exclusivamente por MatchManager.PrepareMatch().

        /// <summary>
        /// Gera a malha tridimensional de tiles e constrói o grafo de nós na memória.
        /// Suporta o carregamento a partir de dados salvos.
        /// </summary>
        public void GenerateGrid(LevelSaveData saveData = null)
        {
            // Limpa filhos antigos se houver
            foreach (Transform child in transform)
            {
                if (child.gameObject != null)
                {
                    Destroy(child.gameObject);
                }
            }

            // Dicionário temporário para carregar biomas e alturas do save
            Dictionary<Vector2Int, TileSaveData> savedTiles = new Dictionary<Vector2Int, TileSaveData>();

            if (saveData != null)
            {
                width = saveData.width;
                depth = saveData.depth;
                heightStep = saveData.heightStep;

                if (saveData.tiles != null)
                {
                    foreach (var t in saveData.tiles)
                    {
                        savedTiles[new Vector2Int(t.x, t.z)] = t;
                    }
                }
            }
            else
            {
                // Geração procedural avançada usando Cardinal Stitched Nodes
                int n = Mathf.Max(1, subgridResolution);
                int macroW = Mathf.Max(4, width / n);
                int macroD = Mathf.Max(4, depth / n);

                var manager = new Cardinal.TerrainEngine.Snapshots.CardinalSnapshotManager(24, 24, 42);
                var snapshot = manager.CaptureSnapshot(macroW, macroD, heightStep, 3, n, enableStitchedSmoothing, true);

                width = snapshot.width;
                depth = snapshot.depth;

                foreach (var tile in snapshot.tiles)
                {
                    savedTiles[new Vector2Int(tile.x, tile.z)] = new TileSaveData
                    {
                        x = tile.x,
                        z = tile.z,
                        height = tile.height,
                        type = (TerrainType)(int)tile.type
                    };
                }
            }

            grid = new TileNode[width, depth];
            int[,] heights = new int[width, depth];

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    int height;
                    TerrainType type;

                    if (saveData != null && savedTiles.TryGetValue(new Vector2Int(x, z), out TileSaveData tData))
                    {
                        height = tData.height;
                        type = tData.type;
                    }
                    else
                    {
                        // Geração procedimental regular
                        int minAllowed = 0;
                        int maxAllowed = 3;

                        if (x > 0)
                        {
                            minAllowed = Mathf.Max(minAllowed, heights[x - 1, z] - 2);
                            maxAllowed = Mathf.Min(maxAllowed, heights[x - 1, z] + 2);
                        }

                        if (z > 0)
                        {
                            minAllowed = Mathf.Max(minAllowed, heights[x, z - 1] - 2);
                            maxAllowed = Mathf.Min(maxAllowed, heights[x, z - 1] + 2);
                        }

                        if (minAllowed > maxAllowed)
                        {
                            height = minAllowed;
                        }
                        else
                        {
                            height = Random.Range(minAllowed, maxAllowed + 1);
                        }

                        // Determina o tipo de bioma baseado na altura para simular dados do Cardinal
                        type = TerrainType.Field;
                        if (height == 3)
                        {
                            type = TerrainType.Mountain;
                        }
                        else if (height == 2)
                        {
                            type = Random.value > 0.5f ? TerrainType.Forest : TerrainType.Field;
                        }
                        else if (height == 0)
                        {
                            type = Random.value > 0.5f ? TerrainType.Mud : TerrainType.River;
                        }
                    }

                    heights[x, z] = height;

                    // Instancia o visual do tile com suporte a paleta de assets 3D ou fallback para cubo
                    SpawnTileVisual(x, z, height, type);

                    // Cria o nó lógico
                    grid[x, z] = new TileNode(x, z, height, type);
                }
            }

            Debug.Log($"[GridManager] Tabuleiro lógico de {width}x{depth} gerado com sucesso.");

            // Aplica a decoração procedural de Stitched Nodes e determinação de Walkability
            if (TerrainDecorator.Instance != null)
            {
                TerrainDecorator.Instance.DecorateGrid(grid, width, depth, assetPalette, saveData?.units);
            }
        }

        /// <summary>
        /// Material puro sem textura e 100% branco para representar as linhas de costura.
        /// </summary>
        public Material GetOrCreateWhiteSeamMaterial()
        {
            if (whiteSeamMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") 
                             ?? Shader.Find("Standard") 
                             ?? Shader.Find("Unlit/Color")
                             ?? Shader.Find("Diffuse");
                whiteSeamMaterial = new Material(shader)
                {
                    name = "Mat_Seam_PureWhite_Untextured",
                    color = Color.white
                };
                if (whiteSeamMaterial.HasProperty("_BaseColor"))
                {
                    whiteSeamMaterial.SetColor("_BaseColor", Color.white);
                }
            }
            return whiteSeamMaterial;
        }

        /// <summary>
        /// Calcula a coordenada espacial com espaçamento (offset) de 0.2 entre os subgrids e as linhas de costura.
        /// </summary>
        public float CalculateWorldCoordWithSeams(int gridCoord)
        {
            if (!useSeamOffsets) return gridCoord;
            if (gridCoord < subgridN) return gridCoord;
            if (gridCoord == subgridN) return gridCoord + seamOffset;
            return gridCoord + (2f * seamOffset);
        }

        /// <summary>
        /// Retorna a posição 3D no espaço global do topo de um bloco, considerando os offsets de costura.
        /// </summary>
        public Vector3 GetWorldPositionForTile(int x, int height, int z)
        {
            float visualHeight = height * heightStep + 1.0f;
            float worldX = CalculateWorldCoordWithSeams(x);
            float worldZ = CalculateWorldCoordWithSeams(z);
            Vector3 localPos = new Vector3(worldX, visualHeight - 0.5f, worldZ);
            return transform.TransformPoint(localPos);
        }

        /// <summary>
        /// Instancia a representação visual do tile no espaço 3D.
        /// Consulta a TerrainAssetPalette para utilizar os modelos 3D fornecidos pelo instrutor,
        /// com fallback transparente para o tilePrefab cúbico padrão.
        /// Se o nó for uma linha de costura, instancia um cubo sem textura e 100% branco com offset 0.2.
        /// </summary>
        public void SpawnTileVisual(int x, int z, int height, TerrainType type)
        {
            TileNode node = GetNodeAt(x, z);
            bool isSeam = node != null && node.IsSeam;

            TerrainVisualEntry entry = (assetPalette != null && !isSeam) ? assetPalette.GetEntry(type) : null;
            GameObject prefabToSpawn = (entry != null && entry.baseTilePrefab != null) ? entry.baseTilePrefab : tilePrefab;

            if (prefabToSpawn == null) return;

            GameObject tileObj = Instantiate(prefabToSpawn, transform);

            float visualHeight = height * heightStep + 1.0f;
            float worldX = CalculateWorldCoordWithSeams(x);
            float worldZ = CalculateWorldCoordWithSeams(z);

            tileObj.transform.localScale = new Vector3(0.95f, visualHeight, 0.95f);
            tileObj.transform.localPosition = new Vector3(worldX, visualHeight / 2f - 0.5f, worldZ);

            if (isSeam)
            {
                tileObj.name = (node != null && node.Role == TileRole.CrossroadCenter) 
                    ? "Tile_Seam_Crossroad_2_2" 
                    : $"Tile_Seam_{x}_{z}";

                Renderer seamRenderer = tileObj.GetComponentInChildren<Renderer>();
                if (seamRenderer != null)
                {
                    seamRenderer.material = GetOrCreateWhiteSeamMaterial();
                    seamRenderer.material.color = Color.white;
                    if (seamRenderer.material.HasProperty("_BaseColor"))
                    {
                        seamRenderer.material.SetColor("_BaseColor", Color.white);
                    }
                }
                return; // Linhas de costura brancas não recebem props decorativos de bioma
            }

            tileObj.name = $"Tile_{type}_{x}_{z}";

            // Se for o prefab padrão sem modelo 3D específico, aplica cor do bioma
            if (entry == null || entry.baseTilePrefab == null)
            {
                Renderer renderer = tileObj.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = GetTerrainColor(type);
                }
            }

            // Instancia decorações 3D no topo do bloco (ex: árvores para Forest, rochas para Mountain)
            if (entry != null && entry.decorationPrefabs != null && entry.decorationPrefabs.Length > 0)
            {
                if (UnityEngine.Random.value <= entry.decorationChance)
                {
                    GameObject decorPrefab = entry.decorationPrefabs[UnityEngine.Random.Range(0, entry.decorationPrefabs.Length)];
                    if (decorPrefab != null)
                    {
                        GameObject decorObj = Instantiate(decorPrefab, tileObj.transform);
                        Vector3 topWorldPos = new Vector3(worldX, visualHeight - 0.5f, worldZ);
                        decorObj.transform.position = transform.TransformPoint(topWorldPos);

                        if (entry.randomRotation)
                        {
                            float randomY = UnityEngine.Random.Range(0, 4) * 90f;
                            decorObj.transform.localRotation = Quaternion.Euler(0, randomY, 0);
                        }

                        if (entry.randomScaleVariation)
                        {
                            float scaleFactor = UnityEngine.Random.Range(0.9f, 1.1f);
                            decorObj.transform.localScale = Vector3.one * scaleFactor;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gera um tabuleiro comparativo utilizando ruído de Perlin isolado, permitindo
        /// aos estudantes visualizarem o contraste entre o ruído matemático cego e o relevo causal do Cardinal.
        /// </summary>
        public void GeneratePerlinNoiseComparison(float scale = 0.15f)
        {
            foreach (Transform child in transform)
            {
                if (child.gameObject != null) Destroy(child.gameObject);
            }

            grid = new TileNode[width, depth];
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    float noiseVal = Mathf.PerlinNoise(x * scale, z * scale);
                    int height = Mathf.Clamp(Mathf.RoundToInt(noiseVal * 3f), 0, 3);
                    TerrainType type = TerrainType.Field;

                    if (height == 3) type = TerrainType.Mountain;
                    else if (height == 2) type = (noiseVal > 0.6f) ? TerrainType.Forest : TerrainType.Field;
                    else if (height == 0) type = (noiseVal < 0.25f) ? TerrainType.Lake : TerrainType.River;

                    SpawnTileVisual(x, z, height, type);
                    grid[x, z] = new TileNode(x, z, height, type);
                }
            }

            Debug.Log($"[GridManager] Tabuleiro comparativo de Perlin Noise ({width}x{depth}) gerado com sucesso.");
        }

        /// <summary>
        /// Paleta de cores para representação visual dos tipos de terreno derivados da simulação Cardinal.
        /// </summary>
        public static Color GetTerrainColor(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Field:    return new Color(0.38f, 0.68f, 0.28f); // Grama / Planície
                case TerrainType.Forest:   return new Color(0.12f, 0.42f, 0.16f); // Floresta verde escuro
                case TerrainType.Mountain: return new Color(0.55f, 0.55f, 0.58f); // Rocha cinza
                case TerrainType.River:    return new Color(0.20f, 0.55f, 0.85f); // Água de rio
                case TerrainType.Lake:     return new Color(0.08f, 0.30f, 0.65f); // Lago profundo
                case TerrainType.Mud:      return new Color(0.42f, 0.30f, 0.20f); // Lamaçais
                default:                   return Color.gray;
            }
        }

        public TileNode GetNodeAt(int x, int z)
        {
            if (grid == null || x < 0 || x >= width || z < 0 || z >= depth) return null;
            return grid[x, z];
        }

        /// <summary>
        /// Recupera o TileNode correspondente a uma posição arbitrária no mundo (arredondamento XZ considerando offsets de costura).
        /// </summary>
        public TileNode GetNodeAtWorldPosition(Vector3 worldPos)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            int x = FindNearestGridCoord(localPos.x, width);
            int z = FindNearestGridCoord(localPos.z, depth);
            return GetNodeAt(x, z);
        }

        private int FindNearestGridCoord(float localCoord, int maxLimit)
        {
            if (!useSeamOffsets) return Mathf.Clamp(Mathf.RoundToInt(localCoord), 0, maxLimit - 1);

            int bestCoord = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < maxLimit; i++)
            {
                float expectedWorld = CalculateWorldCoordWithSeams(i);
                float dist = Mathf.Abs(expectedWorld - localCoord);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestCoord = i;
                }
            }
            return bestCoord;
        }

        /// <summary>
        /// Retorna os nós vizinhos adjacentes diretos (Norte, Sul, Leste, Oeste).
        /// </summary>
        public List<TileNode> GetNeighbors(TileNode node)
        {
            List<TileNode> neighbors = new List<TileNode>();
            if (node == null) return neighbors;

            int[] dx = { 0, 0, 1, -1 };
            int[] dz = { 1, -1, 0, 0 };

            for (int i = 0; i < 4; i++)
            {
                TileNode neighbor = GetNodeAt(node.X + dx[i], node.Z + dz[i]);
                if (neighbor != null)
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors;
        }

        private readonly Dictionary<Vector2Int, Color> originalColors = new Dictionary<Vector2Int, Color>();

        /// <summary>
        /// Altera temporariamente a cor dos blocos indicados para indicar área de ação.
        /// </summary>
        public void HighlightTiles(List<TileNode> nodes, Color highlightColor)
        {
            ClearHighlights();
            foreach (var node in nodes)
            {
                Transform tileTransform = transform.Find($"Tile_{node.Type}_{node.X}_{node.Z}")
                                       ?? transform.Find($"Tile_{node.X}_{node.Z}")
                                       ?? transform.Find($"Tile_Seam_{node.X}_{node.Z}")
                                       ?? transform.Find("Tile_Seam_Crossroad_2_2");

                if (tileTransform != null)
                {
                    Renderer renderer = tileTransform.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                    {
                        Vector2Int key = new Vector2Int(node.X, node.Z);
                        if (!originalColors.ContainsKey(key))
                        {
                            originalColors[key] = renderer.material.color;
                        }
                        renderer.material.color = highlightColor;
                    }
                }
            }
        }

        /// <summary>
        /// Gera a demonstração da oficina de Stitched Nodes:
        /// Expande 4 macro-tiles do Cardinal (2x2) em um tabuleiro 5x5 na Unity com offset 0.2
        /// e cubos brancos puros nas linhas de costura e no nó central 4-way em (2, 2).
        /// </summary>
        public void GenerateStitchedDemo5x5()
        {
            // Limpa filhos antigos
            foreach (Transform child in transform)
            {
                if (child.gameObject != null) Destroy(child.gameObject);
            }

            useSeamOffsets = true;
            seamOffset = 0.2f;
            subgridN = 2;

            width = 5;
            depth = 5;
            grid = new TileNode[5, 5];

            // 4 Macro-Células do Cardinal para demonstrar transições contrastantes:
            // Quadrante (0,0): Rio (H = 0)
            // Quadrante (1,0): Lama (H = 0)
            // Quadrante (0,1): Campo (H = 1)
            // Quadrante (1,1): Montanha (H = 2)
            TerrainType q00_Type = TerrainType.River;    int q00_H = 0;
            TerrainType q10_Type = TerrainType.Mud;      int q10_H = 0;
            TerrainType q01_Type = TerrainType.Field;    int q01_H = 1;
            TerrainType q11_Type = TerrainType.Mountain; int q11_H = 2;

            for (int x = 0; x < 5; x++)
            {
                for (int z = 0; z < 5; z++)
                {
                    bool isSeamX = (x == 2);
                    bool isSeamZ = (z == 2);

                    int h;
                    TerrainType type;
                    TileRole role;

                    if (isSeamX && isSeamZ)
                    {
                        // Nó Central de Interseção 4-Way (2, 2)
                        role = TileRole.CrossroadCenter;
                        h = Mathf.RoundToInt((q00_H + q10_H + q01_H + q11_H) / 4f);
                        type = TerrainType.Field; // Tipo base neutro (renderizado como cubo branco)
                    }
                    else if (isSeamX)
                    {
                        // Coluna de Costura Vertical (X = 2)
                        role = TileRole.SeamVertical;
                        if (z < 2)
                        {
                            // Conecta Rio (Q00) e Lama (Q10)
                            h = Mathf.RoundToInt((q00_H + q10_H) / 2f);
                            type = TerrainType.Mud;
                        }
                        else
                        {
                            // Conecta Campo (Q01) e Montanha (Q11)
                            h = Mathf.RoundToInt((q01_H + q11_H) / 2f);
                            type = TerrainType.Field;
                        }
                    }
                    else if (isSeamZ)
                    {
                        // Linha de Costura Horizontal (Z = 2)
                        role = TileRole.SeamHorizontal;
                        if (x < 2)
                        {
                            // Conecta Rio (Q00) e Campo (Q01)
                            h = Mathf.RoundToInt((q00_H + q01_H) / 2f);
                            type = TerrainType.Mud;
                        }
                        else
                        {
                            // Conecta Lama (Q10) e Montanha (Q11)
                            h = Mathf.RoundToInt((q10_H + q11_H) / 2f);
                            type = TerrainType.Mountain;
                        }
                    }
                    else
                    {
                        // Subgrid de Núcleo (Core)
                        role = TileRole.Core;
                        if (x < 2 && z < 2)
                        {
                            type = q00_Type; h = q00_H;
                        }
                        else if (x > 2 && z < 2)
                        {
                            type = q10_Type; h = q10_H;
                        }
                        else if (x < 2 && z > 2)
                        {
                            type = q01_Type; h = q01_H;
                        }
                        else
                        {
                            type = q11_Type; h = q11_H;
                        }
                    }

                    TileNode node = new TileNode(x, z, h, type);
                    node.Role = role;
                    grid[x, z] = node;

                    SpawnTileVisual(x, z, h, type);
                }
            }

            Debug.Log("[GridManager] Demonstração 5x5 de Stitched Nodes com Offset 0.2 gerada com sucesso!");

            if (TerrainDecorator.Instance != null)
            {
                TerrainDecorator.Instance.DecorateGrid(grid, width, depth, assetPalette, null);
            }
        }

        /// <summary>
        /// Restaura a cor original de todos os blocos destacados.
        /// </summary>
        public void ClearHighlights()
        {
            foreach (var kvp in originalColors)
            {
                Transform tileTransform = transform.Find($"Tile_{kvp.Key.x}_{kvp.Key.y}");
                if (tileTransform != null)
                {
                    Renderer renderer = tileTransform.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = kvp.Value;
                    }
                }
            }
            originalColors.Clear();
        }

        private readonly Dictionary<TileNode, List<Vector3>> lastCalculatedMovePaths = new Dictionary<TileNode, List<Vector3>>();

        /// <summary>
        /// Retorna a lista de waypoints do caminho calculado até um nó de destino.
        /// </summary>
        public List<Vector3> GetPathTo(TileNode target)
        {
            if (target != null && lastCalculatedMovePaths.TryGetValue(target, out var path))
            {
                return path;
            }
            return null;
        }

        /// <summary>
        /// Calcula todos os alvos de movimento válidos a partir do nó inicial usando Dijkstra,
        /// respeitando os pontos de movimento e as regras de custo de relevo.
        /// </summary>
        public List<TileNode> GetValidMoveTargets(TileNode startNode, Unit unit)
        {
            List<TileNode> validTargets = new List<TileNode>();
            lastCalculatedMovePaths.Clear();

            if (startNode == null || unit == null) return validTargets;

            int movePoints = unit.MovePoints;

            // Dijkstra: minCost armazena o menor custo para alcançar cada nó
            Dictionary<TileNode, int> minCost = new Dictionary<TileNode, int>();
            Dictionary<TileNode, List<Vector3>> paths = new Dictionary<TileNode, List<Vector3>>();
            List<TileNode> openSet = new List<TileNode>();

            minCost[startNode] = 0;
            paths[startNode] = new List<Vector3> { startNode.GetTopPosition() };
            openSet.Add(startNode);

            while (openSet.Count > 0)
            {
                openSet.Sort((a, b) => minCost[a].CompareTo(minCost[b]));
                TileNode current = openSet[0];
                openSet.RemoveAt(0);

                int currentCost = minCost[current];

                // Vizinhos ortogonais (Norte, Sul, Leste, Oeste)
                List<TileNode> neighbors = GetNeighbors(current);
                foreach (var neighbor in neighbors)
                {
                    // Bloqueios ecológico-táticos
                    if (!neighbor.IsWalkable) continue; // Bloco bloqueado por obstáculo de cenário ou borda
                    if (neighbor.CurrentUnit != null) continue; // Bloco ocupado

                    bool currentIsWater = current.Type == TerrainType.Lake || current.Type == TerrainType.River;
                    bool neighborIsWater = neighbor.Type == TerrainType.Lake || neighbor.Type == TerrainType.River;
                    bool isWaterMove = currentIsWater || neighborIsWater;

                    int deltaH;
                    if (currentIsWater && !neighborIsWater)
                    {
                        // Ao sair da água (H=0) para a terra, a terra básica (H=1) é a altura padrão de referência (deltaH = 0).
                        // Se a terra for elevada (H=2), deltaH relativo é 1.
                        deltaH = neighbor.Height - 1;
                    }
                    else if (!currentIsWater && neighborIsWater)
                    {
                        // Ao descer para a água a partir de terra normal (H=1), deltaH relativo é 0.
                        deltaH = -(current.Height - 1);
                    }
                    else
                    {
                        // Terra -> Terra ou Água -> Água
                        deltaH = neighbor.Height - current.Height;
                    }

                    int stepCost;
                    if (deltaH > 0)
                    {
                        // Subida
                        if (deltaH == 1)
                        {
                            // 1 unidade mais alta: 1 movimento pra frente + 1 para cima = 2
                            stepCost = 2;
                        }
                        else if (deltaH == 2)
                        {
                            // 2 unidades mais altas: verifica se a unidade específica pode escalar
                            if (!unit.CanClimb(2))
                            {
                                continue;
                            }
                            // Se permitido: 1 pra frente + 2 para cima = 3
                            stepCost = 3;
                        }
                        else
                        {
                            // Relevo íngreme demais (> 2)
                            continue;
                        }
                    }
                    else
                    {
                        // Plano ou descida
                        if (deltaH < -2)
                        {
                            continue; // Queda íngreme demais
                        }
                        stepCost = 1;
                    }

                    // Penalidade de água: +1 para qualquer movimento em qualquer direção que envolva água
                    if (isWaterMove)
                    {
                        stepCost += 1;
                    }

                    int newCost = currentCost + stepCost;
                    if (newCost <= movePoints)
                    {
                        if (!minCost.ContainsKey(neighbor) || newCost < minCost[neighbor])
                        {
                            minCost[neighbor] = newCost;

                            List<Vector3> newPath = new List<Vector3>(paths[current])
                            {
                                neighbor.GetTopPosition()
                            };
                            paths[neighbor] = newPath;

                            if (!openSet.Contains(neighbor))
                            {
                                openSet.Add(neighbor);
                            }
                        }
                    }
                }
            }

            foreach (var kvp in minCost)
            {
                if (kvp.Key != startNode)
                {
                    validTargets.Add(kvp.Key);
                    lastCalculatedMovePaths[kvp.Key] = paths[kvp.Key];
                }
            }

            return validTargets;
        }

        public List<TileNode> GetValidMoveTargets(TileNode startNode)
        {
            if (startNode != null && startNode.CurrentUnit != null)
            {
                return GetValidMoveTargets(startNode, startNode.CurrentUnit);
            }
            return new List<TileNode>();
        }

        /// <summary>
        /// Retorna os blocos ocupados por inimigos que estejam dentro do alcance de ataque da unidade.
        /// Diferencia combate à distância (Arqueiro) de combate corpo a corpo (Guerreiro / Escudeiro).
        /// </summary>
        public List<TileNode> GetValidAttackTargets(TileNode startNode, Unit unit)
        {
            List<TileNode> validTargets = new List<TileNode>();
            if (startNode == null || unit == null) return validTargets;

            bool isArcher = unit.ClassData != null && unit.ClassData.ClassType == UnitClassType.Archer;
            float range = unit.UnitStats.Range;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    TileNode node = GetNodeAt(x, z);
                    if (node != null && node.CurrentUnit != null && node.CurrentUnit.Faction != unit.Faction)
                    {
                        if (isArcher)
                        {
                            // Ataque à distância: alcance radial horizontal
                            float distance = Vector2.Distance(new Vector2(startNode.X, startNode.Z), new Vector2(node.X, node.Z));
                            if (distance <= range + 0.1f)
                            {
                                validTargets.Add(node);
                            }
                        }
                        else
                        {
                            // Combate corpo a corpo ao redor da posição (8 direções ao redor)
                            int dx = Mathf.Abs(startNode.X - node.X);
                            int dz = Mathf.Abs(startNode.Z - node.Z);
                            int deltaH = Mathf.Abs(startNode.Height - node.Height);

                            if (dx <= 1 && dz <= 1 && (dx != 0 || dz != 0))
                            {
                                if (deltaH <= unit.MaxClimbHeight)
                                {
                                    validTargets.Add(node);
                                }
                            }
                        }
                    }
                }
            }
            return validTargets;
        }

        public List<TileNode> GetValidAttackTargets(TileNode startNode, float range)
        {
            if (startNode != null && startNode.CurrentUnit != null)
            {
                return GetValidAttackTargets(startNode, startNode.CurrentUnit);
            }
            return new List<TileNode>();
        }
    }
}
