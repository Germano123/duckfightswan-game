using UnityEngine;

namespace DuckFightSwan.Core
{
    /// <summary>
    /// Estados do ciclo de vida principal do jogo.
    /// </summary>
    public enum GameState
    {
        Menu,
        Preparation,
        Combat,
        Shop,
        GameOver
    }

    /// <summary>
    /// GameManager centraliza o fluxo de estados principais do jogo.
    /// Respeita o Princípio da Responsabilidade Única (SRP) ao delegar
    /// o fluxo das batalhas para o MatchManager.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Configurações do Jogo")]
        [SerializeField] private GameState currentGameState = GameState.Menu;

        public GameState CurrentGameState => currentGameState;

        // Eventos para desacoplamento de UI e outros sistemas (DIP/Observer)
        public delegate void GameStateChangedHandler(GameState newState);
        public static event GameStateChangedHandler OnGameStateChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            ChangeState(currentGameState);
        }

        /// <summary>
        /// Transiciona o jogo para um novo estado.
        /// </summary>
        public void ChangeState(GameState newState)
        {
            currentGameState = newState;
            Debug.Log($"[GameManager] Mudando estado do jogo para: {newState}");

            switch (newState)
            {
                case GameState.Menu:
                    HandleMenuState();
                    break;
                case GameState.Preparation:
                    HandlePreparationState();
                    break;
                case GameState.Combat:
                    HandleCombatState();
                    break;
                case GameState.Shop:
                    HandleShopState();
                    break;
                case GameState.GameOver:
                    HandleGameOverState();
                    break;
            }

            OnGameStateChanged?.Invoke(newState);
        }

        private void HandleMenuState()
        {
            // Lógica de inicialização ao entrar no menu
        }

        private void HandlePreparationState()
        {
            // Lógica ao entrar na preparação (e.g. posicionar tropas)
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.PrepareMatch();
            }
        }

        private void HandleCombatState()
        {
            // Lógica de combate ativa
        }

        private void HandleShopState()
        {
            // Lógica de ativação da interface da loja
        }

        private void HandleGameOverState()
        {
            // Lógica de finalização de jogo
        }
    }
}
