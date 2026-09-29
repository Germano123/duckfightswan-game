using UnityEngine;
using System.Collections.Generic;
using DuckFightSwan.Units;
using DuckFightSwan.Terrain;

namespace DuckFightSwan.Core
{
    /// <summary>
    /// MatchManager orquestra o ciclo de vida de uma partida ativa.
    /// Respeita o SRP ao lidar exclusivamente com as regras de início,
    /// monitoramento de morte e encerramento da batalha.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Header("Status da Partida")]
        [SerializeField] private int currentPhase = 1;
        [SerializeField] private bool isSimulationActive = false;

        // Armazenamento em coleções desacopladas
        private readonly List<GameObject> activeDucks = new List<GameObject>();
        private readonly List<GameObject> activeSwans = new List<GameObject>();

        public int CurrentPhase => currentPhase;
        public bool IsSimulationActive => isSimulationActive;
        public int ActiveDucksCount => activeDucks.Count;
        public int ActiveSwansCount => activeSwans.Count;

        // Callbacks de morte (SRP/DIP/Observer)
        public static event System.Action OnDuckDied;
        public static event System.Action OnSwanDied;
        public static event System.Action<GameObject, FactionType> OnUnitDied;

        public delegate void MatchEndedHandler(bool playerWon, int coinsEarned);
        public static event MatchEndedHandler OnMatchEnded;

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

        /// <summary>
        /// Coloca a partida em modo de preparação.
        /// </summary>
        public void PrepareMatch()
        {
            isSimulationActive = false;
            activeDucks.Clear();
            activeSwans.Clear();

            // Carrega perfil de progressão do jogador
            PlayerProfileData profile = LevelDataManager.LoadProfile();
            currentPhase = profile.currentPhase;

            // Carrega o save da fase correspondente (sempre retorna dados válidos: curados ou procedurais pré-salvos)
            LevelSaveData saveData = LevelDataManager.LoadLevel(currentPhase);
            if (saveData != null)
            {
                if (GridManager.Instance != null)
                {
                    GridManager.Instance.GenerateGrid(saveData);
                }
                if (SpawnManager.Instance != null)
                {
                    SpawnManager.Instance.SpawnTeamsFromSave(saveData.units);
                }
            }

            Debug.Log($"[MatchManager] Fase de preparação da partida {currentPhase} iniciada.");
        }

        /// <summary>
        /// Salva o estado atual do grid e das tropas ativas no arquivo JSON correspondente à fase.
        /// </summary>
        public void SaveCurrentState()
        {
            if (GridManager.Instance == null) return;

            LevelSaveData save = new LevelSaveData
            {
                width = GridManager.Instance.Width,
                depth = GridManager.Instance.Depth,
                heightStep = 0.5f
            };

            // Salva os nós do grid
            for (int x = 0; x < save.width; x++)
            {
                for (int z = 0; z < save.depth; z++)
                {
                    TileNode node = GridManager.Instance.GetNodeAt(x, z);
                    if (node != null)
                    {
                        save.tiles.Add(new TileSaveData
                        {
                            x = node.X,
                            z = node.Z,
                            height = node.Height,
                            type = node.Type
                        });
                    }
                }
            }

            // Salva as unidades aliadas
            foreach (var duckObj in activeDucks)
            {
                if (duckObj == null) continue;
                Unit u = duckObj.GetComponent<Unit>();
                if (u != null && u.CurrentTile != null)
                {
                    save.units.Add(new UnitSaveData
                    {
                        faction = FactionType.Ducks,
                        className = u.ClassData != null ? u.ClassData.ClassName : u.UnitName,
                        x = u.CurrentTile.X,
                        z = u.CurrentTile.Z,
                        currentHealth = u.Health.CurrentHealth,
                        level = u.CurrentLevel,
                        xp = u.CurrentXP
                    });
                }
            }

            // Salva as unidades inimigas
            foreach (var swanObj in activeSwans)
            {
                if (swanObj == null) continue;
                Unit u = swanObj.GetComponent<Unit>();
                if (u != null && u.CurrentTile != null)
                {
                    save.units.Add(new UnitSaveData
                    {
                        faction = FactionType.Swans,
                        className = u.ClassData != null ? u.ClassData.ClassName : u.UnitName,
                        x = u.CurrentTile.X,
                        z = u.CurrentTile.Z,
                        currentHealth = u.Health.CurrentHealth,
                        level = u.CurrentLevel,
                        xp = u.CurrentXP
                    });
                }
            }

            LevelDataManager.SaveLevel(currentPhase, save);
        }

        /// <summary>
        /// Inicia uma nova rodada de combate na arena.
        /// </summary>
        public void StartMatch()
        {
            isSimulationActive = true;
            Debug.Log($"[MatchManager] Partida da Fase {currentPhase} iniciada. Combatentes -> Patos: {activeDucks.Count} | Cisnes: {activeSwans.Count}");
            GameManager.Instance.ChangeState(GameState.Combat);
        }

        /// <summary>
        /// Registra uma unidade ativa na partida para monitoramento.
        /// </summary>
        public void RegisterUnit(GameObject unit, FactionType faction)
        {
            if (faction == FactionType.Ducks)
            {
                if (!activeDucks.Contains(unit)) activeDucks.Add(unit);
            }
            else
            {
                if (!activeSwans.Contains(unit)) activeSwans.Add(unit);
            }
        }

        /// <summary>
        /// Chamado por uma unidade (via Health) quando esta é eliminada.
        /// </summary>
        public void UnregisterUnit(GameObject unit, FactionType faction)
        {
            if (faction == FactionType.Ducks)
            {
                if (activeDucks.Remove(unit))
                {
                    OnDuckDied?.Invoke();
                    OnUnitDied?.Invoke(unit, FactionType.Ducks);
                }
            }
            else
            {
                if (activeSwans.Remove(unit))
                {
                    OnSwanDied?.Invoke();
                    OnUnitDied?.Invoke(unit, FactionType.Swans);
                }
            }

            CheckWinConditions();
            SaveCurrentState();
        }

        /// <summary>
        /// Avalia se alguma das facções foi totalmente eliminada.
        /// </summary>
        private void CheckWinConditions()
        {
            if (!isSimulationActive) return;

            if (activeDucks.Count == 0)
            {
                EndMatch(playerWon: false); // Cisnes venceram (Patos do jogador eliminados)
            }
            else if (activeSwans.Count == 0)
            {
                EndMatch(playerWon: true); // Patos do jogador venceram
            }
        }

        /// <summary>
        /// Finaliza a rodada atual e distribui moedas.
        /// </summary>
        private void EndMatch(bool playerWon)
        {
            isSimulationActive = false;
            int coinsEarned = playerWon ? 100 : 40;

            Debug.Log($"[MatchManager] Partida Encerrada. Vitória do Jogador: {playerWon}. Moedas Ganhas: {coinsEarned}");

            // Atualiza o perfil global de progressão do jogador
            PlayerProfileData profile = LevelDataManager.LoadProfile();
            profile.coins += coinsEarned;

            if (playerWon)
            {
                currentPhase++;
                profile.currentPhase = currentPhase;
            }

            LevelDataManager.SaveProfile(profile);

            OnMatchEnded?.Invoke(playerWon, coinsEarned);
            GameManager.Instance.ChangeState(GameState.GameOver);
        }

        /// <summary>
        /// Finaliza o turno do jogador e processa as ações da IA dos inimigos (Swans).
        /// </summary>
        public void EndTurn()
        {
            if (!isSimulationActive) return;
            StartCoroutine(ExecuteEnemyTurn());
        }

        private System.Collections.IEnumerator ExecuteEnemyTurn()
        {
            Debug.Log("[MatchManager] Turno do Inimigo Iniciado.");

            // Desativa temporariamente interações da câmera
            if (CameraController.Instance != null)
            {
                CameraController.Instance.ClearFocus();
            }

            // Executa a ação de cada Cisne vivo sequencialmente
            // Criamos uma cópia da lista de objetos para evitar erros de modificação concorrente
            List<GameObject> enemiesToProcess = new List<GameObject>(activeSwans);

            foreach (var swanObj in enemiesToProcess)
            {
                if (swanObj == null) continue;
                Unit swan = swanObj.GetComponent<Unit>();
                if (swan == null || swan.Health.IsDead) continue;

                // Foca a câmera no inimigo ativo para feedback visual
                if (CameraController.Instance != null)
                {
                    CameraController.Instance.FocusOn(swan.transform);
                }

                // Encontra o Pato mais próximo
                Unit target = FindNearestDuck(swan);
                if (target != null)
                {
                    float distance = Vector3.Distance(swan.transform.position, target.transform.position);
                    float range = swan.UnitStats.Range;

                    if (distance <= range)
                    {
                        // Ataca se estiver ao alcance!
                        int rawDamage = swan.UnitStats.Damage;
                        Debug.Log($"[MatchManager] IA: {swan.UnitName} atacando {target.UnitName}.");
                        target.Health.TakeDamage(new Damage(rawDamage, swan));
                    }
                    else
                    {
                        // Move-se 1 casa na direção do alvo
                        TileNode nextTile = FindNextTileTowards(swan.CurrentTile, target.CurrentTile);
                        if (nextTile != null && nextTile.CurrentUnit == null)
                        {
                            swan.CurrentTile.CurrentUnit = null; // Libera bloco antigo
                            swan.CurrentTile = nextTile;
                            nextTile.CurrentUnit = swan; // Ocupa novo bloco

                            swan.SetMoveTarget(nextTile.GetTopPosition());
                            Debug.Log($"[MatchManager] IA: {swan.UnitName} moveu-se para ({nextTile.X}, {nextTile.Z})");
                        }
                    }
                }

                yield return new WaitForSeconds(0.8f); // Pequeno atraso dramático/visual
            }

            // Retorna o foco geral da câmera
            if (CameraController.Instance != null)
            {
                CameraController.Instance.ClearFocus();
            }

            // Restaura as ações de todos os Patos vivos para o novo turno
            foreach (var duckObj in activeDucks)
            {
                if (duckObj == null) continue;
                Unit duck = duckObj.GetComponent<Unit>();
                if (duck != null)
                {
                    duck.ResetTurnActions();
                }
            }

            Debug.Log("[MatchManager] Turno do Jogador Iniciado. Ações restauradas.");
            SaveCurrentState();
        }

        private Unit FindNearestDuck(Unit swan)
        {
            Unit nearest = null;
            float minDistance = float.MaxValue;

            foreach (var duckObj in activeDucks)
            {
                if (duckObj == null) continue;
                Unit duck = duckObj.GetComponent<Unit>();
                if (duck == null || duck.Health.IsDead) continue;

                float dist = Vector3.Distance(swan.transform.position, duck.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearest = duck;
                }
            }

            return nearest;
        }

        private TileNode FindNextTileTowards(TileNode start, TileNode target)
        {
            if (GridManager.Instance == null || start == null || target == null) return null;

            List<TileNode> neighbors = GridManager.Instance.GetNeighbors(start);
            TileNode best = null;
            float minDistance = float.MaxValue;

            foreach (var neighbor in neighbors)
            {
                // Regra de altura do GDD: diferença de altura máxima permitida = 2
                if (Mathf.Abs(start.Height - neighbor.Height) > 2) continue;
                if (neighbor.CurrentUnit != null) continue; // Bloco ocupado

                float dist = Vector2.Distance(new Vector2(neighbor.X, neighbor.Z), new Vector2(target.X, target.Z));
                if (dist < minDistance)
                {
                    minDistance = dist;
                    best = neighbor;
                }
            }

            return best;
        }
    }
}
