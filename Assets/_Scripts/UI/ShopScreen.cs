using UnityEngine;
using UnityEngine.UI;

namespace DuckFightSwan.UI
{
    /// <summary>
    /// ShopScreen gerencia as interações de upgrades e recrutamento entre partidas.
    /// Respeita o SRP ao lidar apenas com a camada de visualização dos botões da loja,
    /// disparando eventos que serão interpretados pelos gerenciadores.
    /// </summary>
    public class ShopScreen : MonoBehaviour
    {
        [Header("Painéis de UI")]
        [SerializeField] private GameObject shopContainer;
        [SerializeField] private TMPro.TextMeshProUGUI coinBalanceText;

        [Header("Upgrades Globais")]
        [SerializeField] private Button upgradeHealthButton;
        [SerializeField] private Button upgradeDamageButton;
        [SerializeField] private Button upgradeDefenseButton;
        [SerializeField] private Button upgradeSpeedButton;

        [Header("Recrutamento")]
        [SerializeField] private Button recruitWarriorButton;
        [SerializeField] private Button recruitSquireButton;
        [SerializeField] private Button recruitArcherButton;

        [Header("Navegação")]
        [SerializeField] private Button startNextMatchButton;

        private void Start()
        {
            // Vincula ações de upgrades
            if (upgradeHealthButton != null) upgradeHealthButton.onClick.AddListener(() => PurchaseUpgrade("Health"));
            if (upgradeDamageButton != null) upgradeDamageButton.onClick.AddListener(() => PurchaseUpgrade("Damage"));
            if (upgradeDefenseButton != null) upgradeDefenseButton.onClick.AddListener(() => PurchaseUpgrade("Defense"));
            if (upgradeSpeedButton != null) upgradeSpeedButton.onClick.AddListener(() => PurchaseUpgrade("Speed"));

            // Vincula ações de recrutamento
            if (recruitWarriorButton != null) recruitWarriorButton.onClick.AddListener(() => RecruitUnit("Warrior"));
            if (recruitSquireButton != null) recruitSquireButton.onClick.AddListener(() => RecruitUnit("Squire"));
            if (recruitArcherButton != null) recruitArcherButton.onClick.AddListener(() => RecruitUnit("Archer"));

            // Vincula navegação
            if (startNextMatchButton != null) startNextMatchButton.onClick.AddListener(OnStartNextMatchClicked);

            // Escuta mudanças de estado do jogo (DIP)
            Core.GameManager.OnGameStateChanged += HandleGameStateChanged;

            // Inicialização visual
            if (shopContainer != null)
            {
                shopContainer.SetActive(Core.GameManager.Instance == null);
            }
        }

        private void OnDestroy()
        {
            Core.GameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void PurchaseUpgrade(string attributeName)
        {
            Debug.Log($"[ShopScreen] Solicitada compra de upgrade global de: {attributeName}");
            // Comunica-se com o sistema de persistência/save e repassa a compra
            UpdateCoinBalanceText();
        }

        private void RecruitUnit(string unitClass)
        {
            Debug.Log($"[ShopScreen] Recrutada nova unidade da classe: {unitClass}");
            // Aumenta a contagem de spawn do exército do jogador
        }

        private void OnStartNextMatchClicked()
        {
            if (shopContainer != null) shopContainer.SetActive(false);

            // Retorna ao estado de combate e orquestra a geração de um novo mapa
            Core.GameManager.Instance.ChangeState(Core.GameState.Combat);
            if (Core.MatchManager.Instance != null)
            {
                Core.MatchManager.Instance.StartMatch();
            }
        }

        private void UpdateCoinBalanceText()
        {
            if (coinBalanceText != null)
            {
                coinBalanceText.text = "Saldo: 💰 100 Moedas";
            }
        }

        private void HandleGameStateChanged(Core.GameState newState)
        {
            if (shopContainer != null)
            {
                shopContainer.SetActive(newState == Core.GameState.Shop);
            }

            if (newState == Core.GameState.Shop)
            {
                UpdateCoinBalanceText();
            }
        }
    }
}
