using UnityEngine;
using System.Collections.Generic;
using DuckFightSwan.Units;

namespace DuckFightSwan.Terrain
{
    /// <summary>
    /// SpawnManager gerencia o posicionamento inicial das equipes na arena.
    /// Respeita o SRP e o OCP ao conter apenas regras de validação de posicionamento geométrico,
    /// sem acoplamento direto com a lógica interna de combate.
    /// </summary>
    public class SpawnManager : MonoBehaviour
    {
        public static SpawnManager Instance { get; private set; }

        [Header("Prefabs de Facções")]
        [SerializeField] private GameObject troopPrefab;

        [Header("Classes Disponíveis (ScriptableObjects)")]
        [SerializeField] private List<UnitClassData> availableClasses = new List<UnitClassData>();

        [Header("Parâmetros de Spawn")]
        [SerializeField] private float minDistanceBetweenTeams = 10f;

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
            // SpawnTeams foi removido. Toda a instanciação e carregamento de tropas agora é feito via SpawnTeamsFromSave a partir do JSON da fase correspondente.
        }

        /// <summary>
        /// Spawna as equipes a partir de posições específicas recuperadas do arquivo de salvamento.
        /// </summary>
        public void SpawnTeamsFromSave(List<UnitSaveData> saveData)
        {
            if (GridManager.Instance == null || saveData == null) return;

            int spawnedCount = 0;
            foreach (var u in saveData)
            {
                if (SpawnSingleUnit(u) != null)
                {
                    spawnedCount++;
                }
            }

            Debug.Log($"[SpawnManager] {spawnedCount} unidades restauradas do arquivo de salvamento.");
        }

        /// <summary>
        /// Instancia e registra uma unidade individual na posição do grid especificada pelo UnitSaveData.
        /// </summary>
        public Unit SpawnSingleUnit(UnitSaveData u)
        {
            if (GridManager.Instance == null || u == null) return null;

            TileNode tile = GridManager.Instance.GetNodeAt(u.x, u.z);
            if (tile == null || tile.CurrentUnit != null)
            {
                tile = FindNearestWalkableTile(u.x, u.z);
            }

            if (tile == null)
            {
                Debug.LogWarning($"[SpawnManager] Não foi possível encontrar tile disponível para ({u.x}, {u.z}).");
                return null;
            }

            GameObject prefab = troopPrefab;
            if (prefab == null)
            {
                Debug.LogError($"[SpawnManager] Prefab de tropa abstrato (troopPrefab) não está associado no Inspector do SpawnManager!");
                return null;
            }

            Vector3 spawnPos = tile.GetTopPosition();
            Quaternion rotation = u.faction == FactionType.Ducks ? Quaternion.identity : Quaternion.Euler(0, 180f, 0);

            GameObject unitObj = Instantiate(prefab, spawnPos, rotation);
            unitObj.name = u.faction == FactionType.Ducks ? $"Player_Duck_{u.className}" : $"Enemy_Swan_{u.className}";

            Unit unit = unitObj.GetComponent<Unit>();
            if (unit != null)
            {
                // Configura a facção antes de inicializar a classe
                unit.SetFaction(u.faction);

                // Encontra o ScriptableObject que define os atributos e o modelo 3D da classe salva
                string factionPrefix = u.faction == FactionType.Ducks ? "Duck" : "Swan";
                UnitClassData matchedClass = availableClasses.Find(c => 
                    c.name.IndexOf(factionPrefix, System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                    c.name.IndexOf(u.className, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    ?? availableClasses.Find(c => 
                    c.name.IndexOf(u.className, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.ClassName.Equals(u.className, System.StringComparison.OrdinalIgnoreCase) || 
                    c.ClassType.ToString().Equals(u.className, System.StringComparison.OrdinalIgnoreCase));

                if (matchedClass != null)
                {
                    unit.InitializeClass(matchedClass);
                }
                else
                {
                    Debug.LogWarning($"[SpawnManager] Classe {u.className} não encontrada na lista de classes do spawner. Usando classe padrão.");
                    if (availableClasses.Count > 0)
                    {
                        unit.InitializeClass(availableClasses[0]);
                    }
                }

                unit.CurrentTile = tile;
                tile.CurrentUnit = unit;

                // Carrega os dados de nível, XP, patente e vida salvos
                unit.LoadLevelData(u.level, u.xp, u.rank);
                if (u.currentHealth > 0)
                {
                    bool isPhase1Tutorial = Core.MatchManager.Instance == null || Core.MatchManager.Instance.CurrentPhase == 1;
                    if (u.faction == FactionType.Swans && isPhase1Tutorial && u.currentHealth <= 30)
                    {
                        unit.OverrideStats(30, 5, 1);
                    }
                    else
                    {
                        unit.Health.SetCurrentHealth(u.currentHealth);
                    }
                }
            }
            else
            {
                Debug.LogError($"[SpawnManager] O prefab {prefab.name} não possui o componente Unit!");
            }

            // Registra no MatchManager
            if (Core.MatchManager.Instance != null)
            {
                Core.MatchManager.Instance.RegisterUnit(unitObj, u.faction);
            }
            else
            {
                Debug.LogError("[SpawnManager] MatchManager.Instance está nulo ao tentar registrar unidade!");
            }

            return unit;
        }

        private TileNode FindNearestWalkableTile(int targetX, int targetZ)
        {
            if (GridManager.Instance == null) return null;

            for (int r = 1; r <= 4; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                        TileNode node = GridManager.Instance.GetNodeAt(targetX + dx, targetZ + dz);
                        if (node != null && node.CurrentUnit == null)
                        {
                            return node;
                        }
                    }
                }
            }
            return null;
        }
    }
}
