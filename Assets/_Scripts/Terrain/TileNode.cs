using UnityEngine;
using DuckFightSwan.Units;

namespace DuckFightSwan.Terrain
{
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
        /// Referência da tropa que está atualmente ocupando este nó lógica.
        /// </summary>
        public Unit CurrentUnit { get; set; }

        public TileNode(int x, int z, int height, TerrainType type)
        {
            X = x;
            Z = z;
            Height = height;
            Type = type;
        }

        /// <summary>
        /// Retorna a coordenada tridimensional exata no topo deste bloco no espaço global (World Space).
        /// </summary>
        public Vector3 GetTopPosition()
        {
            float visualHeight = Height * 0.5f + 1.0f; // Multiplicador de altura padrão
            Vector3 localPos = new Vector3(X, visualHeight - 0.5f, Z);
            if (GridManager.Instance != null)
            {
                return GridManager.Instance.transform.TransformPoint(localPos);
            }
            return localPos;
        }
    }
}
