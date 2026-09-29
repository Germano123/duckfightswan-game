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

        private TileNode[,] grid;

        public int Width => width;
        public int Depth => depth;

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

            if (saveData != null)
            {
                width = saveData.width;
                depth = saveData.depth;
                heightStep = saveData.heightStep;
            }

            grid = new TileNode[width, depth];
            int[,] heights = new int[width, depth];

            // Dicionário temporário para carregar biomas e alturas do save
            Dictionary<Vector2Int, TileSaveData> savedTiles = new Dictionary<Vector2Int, TileSaveData>();
            if (saveData != null && saveData.tiles != null)
            {
                foreach (var t in saveData.tiles)
                {
                    savedTiles[new Vector2Int(t.x, t.z)] = t;
                }
            }

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

                    // Instancia o tile visual (objeto sem scripts para performance)
                    if (tilePrefab != null)
                    {
                        GameObject tileObj = Instantiate(tilePrefab, transform);
                        tileObj.name = $"Tile_{x}_{z}";
                        
                        float visualHeight = height * heightStep + 1.0f;
                        tileObj.transform.localScale = new Vector3(0.95f, visualHeight, 0.95f);
                        tileObj.transform.localPosition = new Vector3(x, visualHeight / 2f - 0.5f, z);

                        // Aplica cor temática baseada no tipo de terreno da Cardinal
                        Renderer renderer = tileObj.GetComponentInChildren<Renderer>();
                        if (renderer != null)
                        {
                            renderer.material.color = GetTerrainColor(type);
                        }
                    }

                    // Cria o nó lógico
                    grid[x, z] = new TileNode(x, z, height, type);
                }
            }

            Debug.Log($"[GridManager] Tabuleiro lógico de {width}x{depth} gerado com sucesso.");
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
                Transform tileTransform = transform.Find($"Tile_{node.X}_{node.Z}");
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
        /// Restaura a cor original de todos os blocos destacados.
        /// </summary>
        public void ClearHighlights()
        {
            foreach (var kvp in originalColors)
            {
                Transform tileTransform = transform.Find($"Tile_{kvp.Key.x}_{kvp.Key.y}");
                if (tileTransform != null)
                {
                    Renderer renderer = tileTransform.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = kvp.Value;
                    }
                }
            }
            originalColors.Clear();
        }

        /// <summary>
        /// Calcula todos os alvos de movimento válidos a partir de um nó inicial (8 direções, distância de 1 bloco, altura < 2).
        /// </summary>
        public List<TileNode> GetValidMoveTargets(TileNode startNode)
        {
            List<TileNode> validTargets = new List<TileNode>();
            if (startNode == null) return validTargets;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;

                    TileNode neighbor = GetNodeAt(startNode.X + dx, startNode.Z + dz);
                    if (neighbor != null && neighbor.CurrentUnit == null)
                    {
                        // Regra ecológico-tática: Lagos profundos são intransitáveis para tropas terrestres
                        if (neighbor.Type == TerrainType.Lake) continue;

                        // Regra: Terrenos com 2 ou mais de altura são inalcançáveis
                        if (neighbor.Height < 2)
                        {
                            // Regra de degrau: diferença de altura máxima permitida entre o início e o destino <= 1
                            if (Mathf.Abs(startNode.Height - neighbor.Height) <= 1)
                            {
                                validTargets.Add(neighbor);
                            }
                        }
                    }
                }
            }
            return validTargets;
        }

        /// <summary>
        /// Retorna os blocos ocupados por inimigos que estejam dentro do alcance de ataque da unidade.
        /// </summary>
        public List<TileNode> GetValidAttackTargets(TileNode startNode, float range)
        {
            List<TileNode> validTargets = new List<TileNode>();
            if (startNode == null) return validTargets;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    TileNode node = GetNodeAt(x, z);
                    if (node != null && node.CurrentUnit != null && node.CurrentUnit.Faction == FactionType.Swans)
                    {
                        float distance = Vector3.Distance(startNode.GetTopPosition(), node.GetTopPosition());
                        if (distance <= range)
                        {
                            validTargets.Add(node);
                        }
                    }
                }
            }
            return validTargets;
        }
    }
}
