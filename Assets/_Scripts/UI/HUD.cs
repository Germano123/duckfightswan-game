using UnityEngine;
using UnityEngine.UI;
using DuckFightSwan.Core;
using DuckFightSwan.Units;

namespace DuckFightSwan.UI
{
    /// <summary>
    /// HUD exibe a telemetria em tempo real da simulação.
    /// Respeita o SRP ao lidar apenas com a atualização dos elementos visuais na tela.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Header("Elementos de Texto")]
        [SerializeField] private TMPro.TextMeshProUGUI phaseText;
        [SerializeField] private TMPro.TextMeshProUGUI coinsText;
        [SerializeField] private TMPro.TextMeshProUGUI teamCountText;

        [Header("Controle de Tempo")]
        [SerializeField] private Button speed1xButton;
        [SerializeField] private Button speed2xButton;
        [SerializeField] private Button speed4xButton;

        [Header("Controle de Fluxo")]
        [SerializeField] private Button startCombatButton;

        public static HUD Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void Start()
        {
            // Vincula botões de velocidade (DIP)
            if (speed1xButton != null) speed1xButton.onClick.AddListener(() => SetSimulationSpeed(1f));
            if (speed2xButton != null) speed2xButton.onClick.AddListener(() => SetSimulationSpeed(2f));
            if (speed4xButton != null) speed4xButton.onClick.AddListener(() => SetSimulationSpeed(4f));

            if (startCombatButton != null)
            {
                startCombatButton.onClick.AddListener(OnStartCombatClicked);
            }

            UpdateHUDValues();
        }

        private void OnEnable()
        {
            // Registra listeners de eventos para desacouplar a UI dos managers
            Core.MatchManager.OnMatchEnded += HandleMatchEnded;
            Core.MatchManager.OnUnitDied += HandleUnitDied;
            Core.GameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            Core.MatchManager.OnMatchEnded -= HandleMatchEnded;
            Core.MatchManager.OnUnitDied -= HandleUnitDied;
            Core.GameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        /// <summary>
        /// Atualiza os textos numéricos lendo os dados dos managers.
        /// </summary>
        public void UpdateHUDValues()
        {
            if (Core.MatchManager.Instance != null && phaseText != null)
            {
                if (Combat.StageWaveController.Instance != null && Core.MatchManager.Instance.CurrentPhase == 1)
                {
                    phaseText.text = $"Fase: 01 (Etapa {Combat.StageWaveController.Instance.CurrentStage}/{Combat.StageWaveController.Instance.MaxStages})";
                }
                else
                {
                    phaseText.text = $"Fase: {Core.MatchManager.Instance.CurrentPhase:00}";
                }
            }

            if (coinsText != null)
            {
                Terrain.PlayerProfileData profile = Terrain.LevelDataManager.LoadProfile();
                coinsText.text = $"Moedas: 💰 {profile.coins}";
            }
        }

        /// <summary>
        /// Atualiza a contagem visual de unidades vivas na arena.
        /// </summary>
        public void UpdateTeamCounts(int ducksLeft, int swansLeft)
        {
            if (teamCountText != null)
            {
                teamCountText.text = $"🦆 {ducksLeft}  |  🦢 {swansLeft}";
            }
        }

        private void SetSimulationSpeed(float speed)
        {
            Time.timeScale = speed;
            Debug.Log($"[HUD] Velocidade da simulação física ajustada para: {speed}x");
        }

        private void OnStartCombatClicked()
        {
            if (Core.MatchManager.Instance != null)
            {
                if (Core.GameManager.Instance.CurrentGameState == Core.GameState.Preparation)
                {
                    Core.MatchManager.Instance.StartMatch();
                }
                else if (Core.GameManager.Instance.CurrentGameState == Core.GameState.Combat)
                {
                    Core.MatchManager.Instance.EndTurn();
                }
            }
        }

        private void HandleUnitDied(GameObject unit, FactionType faction)
        {
            if (Core.MatchManager.Instance != null)
            {
                UpdateTeamCounts(Core.MatchManager.Instance.ActiveDucksCount, Core.MatchManager.Instance.ActiveSwansCount);
            }
        }

        private void HandleGameStateChanged(Core.GameState newState)
        {
            if (startCombatButton != null)
            {
                TMPro.TextMeshProUGUI btnText = startCombatButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (newState == Core.GameState.Preparation)
                {
                    startCombatButton.gameObject.SetActive(true);
                    if (btnText != null) btnText.text = "Iniciar Combate";
                }
                else if (newState == Core.GameState.Combat)
                {
                    startCombatButton.gameObject.SetActive(true);
                    if (btnText != null) btnText.text = "Fim do Turno";
                }
                else
                {
                    startCombatButton.gameObject.SetActive(false);
                }
            }

            UpdateHUDValues();
            if (Core.MatchManager.Instance != null)
            {
                UpdateTeamCounts(Core.MatchManager.Instance.ActiveDucksCount, Core.MatchManager.Instance.ActiveSwansCount);
            }
        }

        private void HandleMatchEnded(bool playerWon, int coins)
        {
            // Desativa elementos de HUD tático e aguarda tela de resultado
        }
    }
}
