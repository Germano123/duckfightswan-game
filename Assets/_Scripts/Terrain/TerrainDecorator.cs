using System.Collections.Generic;
using UnityEngine;
using DuckFightSwan.Units;

namespace DuckFightSwan.Terrain
{
    /// <summary>
    /// Contexto de vizinhança calculado para um tile específico na lógica de Stitched Nodes.
    /// </summary>
    public struct NeighborContext
    {
        public bool hasWaterNorth;
        public bool hasWaterEast;
        public bool hasWaterSouth;
        public bool hasWaterWest;

        public bool hasHigherNorth;
        public bool hasHigherEast;
        public bool hasHigherSouth;
        public bool hasHigherWest;

        public bool hasLowerNorth;
        public bool hasLowerEast;
        public bool hasLowerSouth;
        public bool hasLowerWest;

        public int forestNeighborCount;
        public int waterNeighborCount;
    }

    /// <summary>
    /// Gerenciador procedural de decoração de cenário.
    /// Aplica a lógica de Stitched Nodes para completar o cenário entre blocos vizinhos,
    /// garante a delimitação de tiles Walkable vs Non-Walkable com bloqueio de movimentação,
    /// e valida a conectividade navegável do tabuleiro via BFS.
    /// </summary>
    public class TerrainDecorator : MonoBehaviour
    {
        private static TerrainDecorator _instance;
        public static TerrainDecorator Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<TerrainDecorator>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("TerrainDecorator");
                        _instance = go.AddComponent<TerrainDecorator>();
                    }
                }
                return _instance;
            }
        }

        [Header("Configurações de Decoração")]
        [Range(0f, 1f)]
        [SerializeField] private float obstacleTreeChance = 0.15f;

        [Range(0f, 1f)]
        [SerializeField] private float walkableFloraChance = 0.40f;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Ponto de entrada chamado pelo GridManager após gerar a matriz de nós.
        /// </summary>
        public void DecorateGrid(TileNode[,] grid, int width, int depth, TerrainAssetPalette palette, List<UnitSaveData> spawnUnits)
        {
            if (grid == null) return;

            // Limpa decorações anteriores caso existam
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            HashSet<Vector2Int> reservedSpawnTiles = new HashSet<Vector2Int>();
            if (spawnUnits != null)
            {
                foreach (var u in spawnUnits)
                {
                    reservedSpawnTiles.Add(new Vector2Int(u.x, u.z));
                }
            }

            // 1. Determina a matriz de Walkability respeitando regras de relevo e borda
            AssignWalkability(grid, width, depth, reservedSpawnTiles);

            // 2. Garante que exista caminho aberto entre as facções (BFS)
            EnsureBoardConnectivity(grid, width, depth, reservedSpawnTiles);

            // 3. Instancia os props conectando vizinhos (Stitched Nodes)
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    DecorateSingleTile(grid[x, z], grid, width, depth, palette, reservedSpawnTiles);
                }
            }

            Debug.Log($"[TerrainDecorator] Tabuleiro decorado com sucesso com Stitched Nodes ({width}x{depth}).");
        }

        private void AssignWalkability(TileNode[,] grid, int width, int depth, HashSet<Vector2Int> spawnTiles)
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    TileNode node = grid[x, z];
                    if (node.IsSeam)
                    {
                        node.IsWalkable = true;
                        continue;
                    }

                    bool isBorder = (width > 5 && depth > 5) && (x == 0 || x == width - 1 || z == 0 || z == depth - 1);
                    bool isSpawn = spawnTiles.Contains(new Vector2Int(x, z));

                    if (isSpawn)
                    {
                        node.IsWalkable = true; // Spawn das tropas é sempre navegável
                    }
                    else if (isBorder)
                    {
                        node.IsWalkable = false; // Bordas formam a moldura do diorama
                    }
                    else if (node.Height >= 3)
                    {
                        node.IsWalkable = false; // Paredões inacessíveis
                    }
                    else if (node.Type == TerrainType.Forest && Random.value < obstacleTreeChance)
                    {
                        // Obstáculo natural de floresta (árvore volumosa)
                        node.IsWalkable = false;
                    }
                    else
                    {
                        node.IsWalkable = true;
                    }
                }
            }
        }

        private void EnsureBoardConnectivity(TileNode[,] grid, int width, int depth, HashSet<Vector2Int> spawnTiles)
        {
            if (spawnTiles.Count < 2) return;

            // Coleta pontos de partida (patos à esquerda) e alvos (gansos à direita)
            Vector2Int startPoint = new Vector2Int(1, depth / 2);
            Vector2Int endPoint = new Vector2Int(width - 2, depth / 2);

            foreach (var pos in spawnTiles)
            {
                if (pos.x <= 2) startPoint = pos;
                if (pos.x >= width - 3) endPoint = pos;
            }

            // Executa BFS para verificar se existe caminho livre
            bool pathExists = CheckPathBFS(grid, width, depth, startPoint, endPoint);

            if (!pathExists)
            {
                Debug.LogWarning("[TerrainDecorator] Caminho bloqueado por obstáculos procedurais. Desobstruindo corredor central...");
                // Desobstrui o corredor central
                int midZ = depth / 2;
                for (int x = 1; x < width - 1; x++)
                {
                    grid[x, midZ].IsWalkable = true;
                    if (midZ + 1 < depth - 1) grid[x, midZ + 1].IsWalkable = true;
                }
            }
        }

        private bool CheckPathBFS(TileNode[,] grid, int width, int depth, Vector2Int start, Vector2Int target)
        {
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

            queue.Enqueue(start);
            visited.Add(start);

            int[] dx = { 0, 0, 1, -1 };
            int[] dz = { 1, -1, 0, 0 };

            while (queue.Count > 0)
            {
                Vector2Int curr = queue.Dequeue();
                if (curr == target) return true;

                for (int i = 0; i < 4; i++)
                {
                    int nx = curr.x + dx[i];
                    int nz = curr.y + dz[i];

                    if (nx >= 0 && nx < width && nz >= 0 && nz < depth)
                    {
                        Vector2Int next = new Vector2Int(nx, nz);
                        if (!visited.Contains(next) && grid[nx, nz].IsWalkable)
                        {
                            visited.Add(next);
                            queue.Enqueue(next);
                        }
                    }
                }
            }

            return false;
        }

        private NeighborContext AnalyzeNeighbors(TileNode node, TileNode[,] grid, int width, int depth)
        {
            NeighborContext ctx = new NeighborContext();
            int x = node.X;
            int z = node.Z;

            // Norte (+Z)
            if (z + 1 < depth)
            {
                TileNode n = grid[x, z + 1];
                if (IsWater(n)) { ctx.hasWaterNorth = true; ctx.waterNeighborCount++; }
                if (n.Height > node.Height) ctx.hasHigherNorth = true;
                if (n.Height < node.Height) ctx.hasLowerNorth = true;
                if (n.Type == TerrainType.Forest) ctx.forestNeighborCount++;
            }

            // Leste (+X)
            if (x + 1 < width)
            {
                TileNode n = grid[x + 1, z];
                if (IsWater(n)) { ctx.hasWaterEast = true; ctx.waterNeighborCount++; }
                if (n.Height > node.Height) ctx.hasHigherEast = true;
                if (n.Height < node.Height) ctx.hasLowerEast = true;
                if (n.Type == TerrainType.Forest) ctx.forestNeighborCount++;
            }

            // Sul (-Z)
            if (z - 1 >= 0)
            {
                TileNode n = grid[x, z - 1];
                if (IsWater(n)) { ctx.hasWaterSouth = true; ctx.waterNeighborCount++; }
                if (n.Height > node.Height) ctx.hasHigherSouth = true;
                if (n.Height < node.Height) ctx.hasLowerSouth = true;
                if (n.Type == TerrainType.Forest) ctx.forestNeighborCount++;
            }

            // Oeste (-X)
            if (x - 1 >= 0)
            {
                TileNode n = grid[x - 1, z];
                if (IsWater(n)) { ctx.hasWaterWest = true; ctx.waterNeighborCount++; }
                if (n.Height > node.Height) ctx.hasHigherWest = true;
                if (n.Height < node.Height) ctx.hasLowerWest = true;
                if (n.Type == TerrainType.Forest) ctx.forestNeighborCount++;
            }

            return ctx;
        }

        private bool IsWater(TileNode node)
        {
            return node.Type == TerrainType.River || node.Type == TerrainType.Lake;
        }

        private void DecorateSingleTile(TileNode node, TileNode[,] grid, int width, int depth, TerrainAssetPalette palette, HashSet<Vector2Int> spawnTiles)
        {
            if (node == null || node.IsSeam) return;

            bool isSpawn = spawnTiles != null && spawnTiles.Contains(new Vector2Int(node.X, node.Z));
            bool isBorder = (width > 5 && depth > 5) && (node.X == 0 || node.X == width - 1 || node.Z == 0 || node.Z == depth - 1);
            Vector3 topPos = node.GetTopPosition();

            NeighborContext ctx = AnalyzeNeighbors(node, grid, width, depth);

            // 1. Caso NÃO-CAMINHÁVEL (Árvores densas, rochedos ou moldura de diorama)
            if (!node.IsWalkable)
            {
                SpawnObstructiveProp(node, ctx, isBorder, topPos);
                return;
            }

            // 2. Caso CAMINHÁVEL COM TRANSIÇÃO DE ÁGUA (Stitched Water Edge)
            // Se o tile é terra/lama e encosta em água, instancia juncos aquáticos voltados para a água
            if (!IsWater(node) && ctx.waterNeighborCount > 0 && !isSpawn)
            {
                SpawnWaterEdgeReeds(node, ctx, topPos);
            }

            // 3. Caso CAMINHÁVEL PADRÃO (Grama rasteira, flores, pedregulhos finos)
            if (!IsWater(node) && Random.value < walkableFloraChance)
            {
                SpawnWalkableFlora(node, topPos, isSpawn);
            }
            else if (IsWater(node) && Random.value < 0.20f && !isSpawn)
            {
                SpawnLilyPad(node, topPos);
            }
        }

        private void SpawnObstructiveProp(TileNode node, NeighborContext ctx, bool isBorder, Vector3 topPos)
        {
            string propName;
            Color propColor;
            Vector3 scale;

            if (isBorder)
            {
                // Moldura de diorama: cerca campestre ou pinheiro/carvalho de borda
                propName = node.Type == TerrainType.Mountain ? "Prop_Rock_Large_A_Obstructive" : "Prop_Tree_Oak_A_Obstructive";
                propColor = node.Type == TerrainType.Mountain ? new Color(0.45f, 0.45f, 0.48f) : new Color(0.18f, 0.45f, 0.22f);
                scale = new Vector3(0.6f, 1.4f, 0.6f);
            }
            else if (node.Height >= 3 || node.Type == TerrainType.Mountain)
            {
                // Picos e falésias rochosas
                propName = "Prop_Rock_CanyonBoulder_A_Obstructive";
                propColor = new Color(0.50f, 0.50f, 0.52f);
                scale = new Vector3(0.7f, 1.0f, 0.7f);
            }
            else
            {
                // Árvore densa de floresta
                propName = node.Type == TerrainType.Forest ? "Prop_Tree_Autumn_A_Obstructive" : "Prop_Tree_Oak_A_Obstructive";
                propColor = node.Type == TerrainType.Forest ? new Color(0.65f, 0.40f, 0.15f) : new Color(0.15f, 0.48f, 0.20f);
                scale = new Vector3(0.65f, 1.5f, 0.65f);
            }

            GameObject propObj = CreateStylizedProp(propName, PrimitiveType.Cylinder, propColor, topPos, scale);
            node.SpawnedProp = propObj;
        }

        private void SpawnWaterEdgeReeds(TileNode node, NeighborContext ctx, Vector3 topPos)
        {
            // Calcula rotação alinhada para a margem da água
            float rotY = 0f;
            Vector3 offset = Vector3.zero;

            if (ctx.hasWaterNorth) { rotY = 0f; offset = new Vector3(0, 0, 0.35f); }
            else if (ctx.hasWaterEast) { rotY = 90f; offset = new Vector3(0.35f, 0, 0); }
            else if (ctx.hasWaterSouth) { rotY = 180f; offset = new Vector3(0, 0, -0.35f); }
            else if (ctx.hasWaterWest) { rotY = 270f; offset = new Vector3(-0.35f, 0, 0); }

            Vector3 spawnPos = topPos + offset;
            GameObject reeds = CreateStylizedProp("Prop_Water_Reeds_A_Obstructive", PrimitiveType.Cylinder, new Color(0.35f, 0.55f, 0.25f), spawnPos, new Vector3(0.2f, 0.6f, 0.2f));
            reeds.transform.localRotation = Quaternion.Euler(0, rotY, 0);
        }

        private void SpawnWalkableFlora(TileNode node, Vector3 topPos, bool isSpawn)
        {
            // Props rasteiros que não colidem com unidades
            string propName = Random.value > 0.5f ? "Prop_Flora_GrassTuft_A_Walkable" : "Prop_Flora_WildFlowers_A_Walkable";
            Color floraColor = propName.Contains("WildFlowers") ? new Color(0.95f, 0.85f, 0.25f) : new Color(0.30f, 0.65f, 0.30f);

            // Jitter posicional sutil para quebrar a grade rígida
            Vector3 jitter = isSpawn ? Vector3.zero : new Vector3(Random.Range(-0.2f, 0.2f), 0, Random.Range(-0.2f, 0.2f));
            Vector3 spawnPos = topPos + jitter;

            CreateStylizedProp(propName, PrimitiveType.Cube, floraColor, spawnPos, new Vector3(0.25f, 0.10f, 0.25f));
        }

        private void SpawnLilyPad(TileNode node, Vector3 topPos)
        {
            Vector3 jitter = new Vector3(Random.Range(-0.15f, 0.15f), 0, Random.Range(-0.15f, 0.15f));
            CreateStylizedProp("Prop_Water_LilyPad_A_Walkable", PrimitiveType.Cylinder, new Color(0.20f, 0.55f, 0.25f), topPos + jitter, new Vector3(0.35f, 0.02f, 0.35f));
        }

        private GameObject CreateStylizedProp(string propName, PrimitiveType primType, Color color, Vector3 worldPos, Vector3 scale)
        {
            GameObject propObj = GameObject.CreatePrimitive(primType);
            propObj.name = propName;
            propObj.transform.SetParent(transform);
            propObj.transform.position = worldPos + new Vector3(0, scale.y / 2f, 0);
            propObj.transform.localScale = scale;

            // Remove o colisor do Unity para que o raycast do mouse atinja o bloco de grid subjacente
            Collider col = propObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = propObj.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = new Material(Shader.Find("Standard"));
                rend.material.color = color;
            }

            return propObj;
        }
    }
}
