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

        [Header("Controle de Interface de Turnos")]
        [SerializeField] private UnityEngine.UI.Button endTurnButton;
        [SerializeField] private TMPro.TextMeshProUGUI endTurnButtonText;

        public int CurrentPhase => currentPhase;
        public bool IsSimulationActive => isSimulationActive;
        public bool IsEnemyTurn { get; private set; }
        public int ActiveDucksCount => activeDucks.Count;
        public int ActiveSwansCount => activeSwans.Count;
        public List<GameObject> ActiveDucks => activeDucks;
        public List<GameObject> ActiveSwans => activeSwans;

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

        private void Start()
        {
            if (endTurnButton == null)
            {
                GameObject btnObj = GameObject.Find("Button (3)");
                if (btnObj != null)
                {
                    endTurnButton = btnObj.GetComponent<UnityEngine.UI.Button>();
                    endTurnButtonText = btnObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                }
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveListener(EndTurn);
                endTurnButton.onClick.AddListener(EndTurn);
            }

            if (GetComponent<EnemyAIController>() == null)
            {
                gameObject.AddComponent<EnemyAIController>();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                HandleDebugF1();
            }
        }

        /// <summary>
        /// Método de Debug acionado por F1:
        /// 1. Se houver cisnes inimigos vivos, elimina todos instantaneamente com dano massivo para avançar de etapa/onda.
        /// 2. Se todos os cisnes já foram eliminados ou a fase foi concluída, avança diretamente para a próxima fase com promoção dos patos.
        /// </summary>
        public void HandleDebugF1()
        {
            if (activeSwans.Count > 0)
            {
                Debug.Log($"[MatchManager] [DEBUG F1] Eliminando {activeSwans.Count} cisnes inimigos ativos...");
                List<GameObject> swansToKill = new List<GameObject>(activeSwans);
                foreach (var swan in swansToKill)
                {
                    if (swan != null)
                    {
                        Unit u = swan.GetComponent<Unit>();
                        if (u != null && u.Health != null && !u.Health.IsDead)
                        {
                            u.Health.TakeDamage(new Damage(9999, null));
                        }
                    }
                }
            }
            else
            {
                Debug.Log("[MatchManager] [DEBUG F1] Sem inimigos ativos. Avançando para a próxima fase com promoção militar...");
                AdvanceToNextPhaseWithPromotion();
            }
        }

        /// <summary>
        /// Avança para a próxima fase / época, coletando os patos sobreviventes, promovendo-os para o próximo nível e patente militar.
        /// </summary>
        public void AdvanceToNextPhaseWithPromotion()
        {
            List<UnitSaveData> promotedSurvivors = new List<UnitSaveData>();

            // Coleta os patos atualmente sobreviventes
            foreach (var duckObj in activeDucks)
            {
                if (duckObj != null)
                {
                    Unit unit = duckObj.GetComponent<Unit>();
                    if (unit != null && !unit.Health.IsDead)
                    {
                        MilitaryRank nextRank = unit.CurrentRank;
                        if (nextRank == MilitaryRank.Recruit) nextRank = MilitaryRank.Veteran;
                        else if (nextRank == MilitaryRank.Veteran) nextRank = MilitaryRank.Elite;
                        else if (nextRank == MilitaryRank.Elite) nextRank = MilitaryRank.Commander;

                        promotedSurvivors.Add(new UnitSaveData
                        {
                            faction = FactionType.Ducks,
                            className = unit.ClassData != null ? unit.ClassData.ClassName : "Warrior",
                            x = unit.CurrentTile != null ? unit.CurrentTile.X : 1,
                            z = unit.CurrentTile != null ? unit.CurrentTile.Z : 2,
                            currentHealth = 0, // Será restaurado para o novo MaxHealth pelo spawner
                            level = unit.CurrentLevel + 1, // Ganha +1 nível pelo triunfo da época
                            xp = 0,
                            rank = (int)nextRank
                        });
                    }
                }
            }

            // Fallback se não sobrou nenhum pato
            if (promotedSurvivors.Count == 0)
            {
                promotedSurvivors.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Warrior", level = 2, rank = (int)MilitaryRank.Veteran });
                promotedSurvivors.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Archer", level = 2, rank = (int)MilitaryRank.Veteran });
                promotedSurvivors.Add(new UnitSaveData { faction = FactionType.Ducks, className = "Squire", level = 2, rank = (int)MilitaryRank.Veteran });
            }

            int nextPhase = currentPhase + 1;

            // Salva os sobreviventes promovidos para a próxima fase
            LevelDataManager.PrepareNextPhaseWithSurvivors(nextPhase, promotedSurvivors);

            // Atualiza o perfil geral do jogador
            PlayerProfileData profile = LevelDataManager.LoadProfile();
            profile.currentPhase = nextPhase;
            profile.coins += 100;
            LevelDataManager.SaveProfile(profile);

            currentPhase = nextPhase;

            // Destrói objetos antigos de tropas da cena para carregar a nova fase limpa
            foreach (var d in new List<GameObject>(activeDucks)) if (d != null) Destroy(d);
            foreach (var s in new List<GameObject>(activeSwans)) if (s != null) Destroy(s);
            activeDucks.Clear();
            activeSwans.Clear();

            // Prepara e inicializa a nova fase
            PrepareMatch();
            StartMatch();

            // Atualiza HUD e banner de nova época
            if (UI.HUD.Instance != null)
            {
                UI.HUD.Instance.UpdateHUDValues();
            }

            int epochYear = LevelDataManager.GetEpochYearForPhase(currentPhase);
            if (Combat.StageWaveController.Instance != null)
            {
                Combat.StageWaveController.Instance.ShowBanner($"⏳ NOVA ÉPOCA ALCANÇADA! (Ano {epochYear}) ⏳\nPatos promovidos a Veteranos! Inimigos de alto nível entram na batalha!", 4.5f);
            }
        }

        /// <summary>
        /// Coloca a partida em modo de preparação e ativa a simulação para combate.
        /// </summary>
        public void PrepareMatch()
        {
            isSimulationActive = true;
            IsEnemyTurn = false;
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

            if (Combat.StageWaveController.Instance != null)
            {
                Combat.StageWaveController.Instance.ResetStages();
            }

            int epochYear = LevelDataManager.GetEpochYearForPhase(currentPhase);
            Debug.Log($"[MatchManager] Fase de preparação da partida {currentPhase} iniciada (Ano {epochYear} do Conflito no Vale).");
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
            StartCoroutine(RoutineSelectInitialDuck());
        }

        private System.Collections.IEnumerator RoutineSelectInitialDuck()
        {
            yield return null; // Aguarda a inicialização e posicionamento das unidades no tabuleiro
            SelectFirstAvailableDuckAndFocus();
        }

        /// <summary>
        /// Seleciona o primeiro pato vivo com ações disponíveis, focando a câmera e abrindo o menu de ações táticas.
        /// </summary>
        public void SelectFirstAvailableDuckAndFocus()
        {
            if (activeDucks == null || activeDucks.Count == 0) return;

            foreach (var duckObj in activeDucks)
            {
                if (duckObj == null) continue;
                Unit duck = duckObj.GetComponent<Unit>();
                if (duck != null && duck.Health != null && !duck.Health.IsDead && duck.RemainingActions > 0)
                {
                    if (InputGridController.Instance != null)
                    {
                        InputGridController.Instance.SelectUnit(duck, openUI: true);
                    }
                    break;
                }
            }
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
                if (Combat.StageWaveController.Instance != null && Combat.StageWaveController.Instance.HasNextStage())
                {
                    Combat.StageWaveController.Instance.AdvanceToNextStage();
                }
                else
                {
                    EndMatch(playerWon: true); // Patos do jogador venceram
                }
            }
        }

        /// <summary>
        /// Finaliza a rodada atual e distribui moedas.
        /// </summary>
        private void EndMatch(bool playerWon)
        {
            isSimulationActive = false;
            IsEnemyTurn = false;
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
            if (!isSimulationActive)
            {
                StartMatch();
            }

            if (IsEnemyTurn) return;

            // Desseleciona qualquer unidade e fecha menus de ação
            if (InputGridController.Instance != null)
            {
                InputGridController.Instance.Deselect();
            }

            StartCoroutine(ExecuteEnemyTurn());
        }

        private System.Collections.IEnumerator ExecuteEnemyTurn()
        {
            IsEnemyTurn = true;
            Debug.Log("[MatchManager] Turno do Inimigo Iniciado.");

            if (endTurnButton != null)
            {
                endTurnButton.interactable = false;
                if (endTurnButtonText != null) endTurnButtonText.text = "Turno Inimigo...";
            }

            // Desativa temporariamente interações da câmera
            if (CameraController.Instance != null)
            {
                CameraController.Instance.ClearFocus();
            }

            // Garante que o componente de IA esteja ativo
            EnemyAIController ai = EnemyAIController.Instance ?? GetComponent<EnemyAIController>() ?? gameObject.AddComponent<EnemyAIController>();

            // Executa a ação de cada Cisne vivo sequencialmente
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

                yield return new WaitForSeconds(0.3f);

                // Executa as ações de avanço, recuo ou ataque do cisne via IA
                yield return StartCoroutine(ai.ExecuteSwanTurn(swan, activeDucks));
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

            IsEnemyTurn = false;
            Debug.Log("[MatchManager] Turno do Jogador Iniciado. Ações restauradas.");

            if (endTurnButton != null)
            {
                endTurnButton.interactable = true;
                if (endTurnButtonText != null) endTurnButtonText.text = "Finalizar turno";
            }

            SaveCurrentState();

            // Seleciona a primeira personagem do jogador disponível e foca a câmera ao iniciar o turno do jogador
            SelectFirstAvailableDuckAndFocus();
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
