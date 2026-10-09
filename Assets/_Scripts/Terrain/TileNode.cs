using UnityEngine;
using DuckFightSwan.Units;

namespace DuckFightSwan.Terrain
{
    /// <summary>
    /// Papel estrutural do nó na arquitetura de Stitched Nodes.
    /// </summary>
    public enum TileRole
    {
        Core,               // Sub-tiles normais do interior dos macro-tiles
        SeamVertical,       // Faixa de costura vertical (ex: X = 2)
        SeamHorizontal,     // Faixa de costura horizontal (ex: Z = 2)
        CrossroadCenter     // Nó central de confluência 4-Way (ex: X = 2, Z = 2)
    }

    /// <summary>
    /// Classe de dados pura (sem herança de MonoBehaviour) representando um nó do grid.
    /// Excelente para mapas grandes, pois reduz drasticamente a sobrecarga (overhead) da Unity.
    /// Respeita o SRP ao lidar apenas com as informações estruturais do tabuleiro.
    /// </summary>
    public class TileNode
    {
        public int X { get; private set; }
        public int Z { get; private set; }
        public int Height { get; private set; }
        public TerrainType Type { get; private set; }
        
        /// <summary>
        /// Papel estrutural do nó na malha (núcleo de macro-tile vs linha de costura vs confluência).
        /// </summary>
        public TileRole Role { get; set; } = TileRole.Core;

        /// <summary>
        /// Informa se o nó atua como faixa de costura ou encruzilhada de transição.
        /// </summary>
        public bool IsSeam => Role != TileRole.Core;

        /// <summary>
        /// Referência da tropa que está atualmente ocupando este nó lógica.
        /// </summary>
        public Unit CurrentUnit { get; set; }

        /// <summary>
        /// Determina se o bloco é navegável por tropas ou se está bloqueado por obstáculos de cenário (árvores, rochedos).
        /// </summary>
        public bool IsWalkable { get; set; } = true;

        /// <summary>
        /// Referência ao GameObject do prop decorativo/obstrutivo instanciado neste bloco.
        /// </summary>
        public GameObject SpawnedProp { get; set; }

        public TileNode(int x, int z, int height, TerrainType type)
        {
            X = x;
            Z = z;
            Height = height;
            Type = type;
            IsWalkable = true;
            Role = TileRole.Core;
        }

        /// <summary>
        /// Retorna a coordenada tridimensional exata no topo deste bloco no espaço global (World Space),
        /// respeitando os offsets de costura do GridManager caso estejam ativos.
        /// </summary>
        public Vector3 GetTopPosition()
        {
            if (GridManager.Instance != null)
            {
                return GridManager.Instance.GetWorldPositionForTile(X, Height, Z);
            }
            float visualHeight = Height * 0.5f + 1.0f;
            return new Vector3(X, visualHeight - 0.5f, Z);
        }
    }
}
