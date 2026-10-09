using System;
using UnityEngine;

namespace DuckFightSwan.Terrain
{
    [Serializable]
    public class TerrainVisualEntry
    {
        [Tooltip("Tipo de terreno derivado da simulação Cardinal.")]
        public TerrainType terrainType;

        [Tooltip("Modelo 3D modular do chão/bloco (deixe vazio para usar o cubo padrão com cor).")]
        public GameObject baseTilePrefab;

        [Tooltip("Modelos 3D decorativos posicionados no topo do bloco (ex: árvores para Forest, rochas para Mountain).")]
        public GameObject[] decorationPrefabs;

        [Range(0f, 1f)]
        [Tooltip("Probabilidade de instanciar uma decoração neste bloco (0 a 1).")]
        public float decorationChance = 0.5f;

        [Tooltip("Permite rotações aleatórias em múltiplos de 90 graus nas decorações.")]
        public bool randomRotation = true;

        [Tooltip("Variação sutil de escala (± 10%) nas decorações.")]
        public bool randomScaleVariation = true;
    }

    /// <summary>
    /// Paleta de configuração de Assets e Modelos 3D para os terrenos da oficina Cardinal.
    /// Permite que o instrutor e os estudantes associem modelos 3D aos tipos de terreno.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTerrainAssetPalette", menuName = "Cardinal/Terrain Asset Palette")]
    public class TerrainAssetPalette : ScriptableObject
    {
        [Header("Mapeamento Visual dos Biomas")]
        public TerrainVisualEntry[] entries;

        /// <summary>
        /// Retorna a configuração visual correspondente ao tipo de terreno informado.
        /// </summary>
        public TerrainVisualEntry GetEntry(TerrainType type)
        {
            if (entries == null) return null;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].terrainType == type)
                {
                    return entries[i];
                }
            }

            return null;
        }
    }
}
