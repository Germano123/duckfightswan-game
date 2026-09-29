using UnityEngine;
using UnityEngine.UI;

namespace DuckFightSwan.UI
{
    /// <summary>
    /// ResultScreen exibe os painéis informativos de vitória ou derrota após o combate.
    /// Respeita o SRP ao lidar apenas com a apresentação do desfecho da partida.
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        [Header("Painéis e Textos")]
        [SerializeField] private GameObject screenContainer;
        [SerializeField] private TMPro.TextMeshProUGUI resultTitleText;
        [SerializeField] private TMPro.TextMeshProUGUI rewardSummaryText;

        [Header("Navegação")]
        [SerializeField] private Button nextStepButton;

        private void Start()
        {
            if (screenContainer != null) screenContainer.SetActive(false);

            if (nextStepButton != null)
            {
                nextStepButton.onClick.AddListener(OnNextStepClicked);
            }
        }

        private void OnEnable()
        {
            Core.MatchManager.OnMatchEnded += ShowResult;
        }

        private void OnDisable()
        {
            Core.MatchManager.OnMatchEnded -= ShowResult;
        }

        /// <summary>
        /// Desenha a tela na interface configurando os textos e cores correspondentes.
        /// </summary>
        public void ShowResult(bool playerWon, int coinsEarned)
        {
            if (screenContainer == null) return;

            screenContainer.SetActive(true);

            if (resultTitleText != null)
            {
                resultTitleText.text = playerWon ? "VITÓRIA!" : "DERROTA...";
                resultTitleText.color = playerWon ? Color.yellow : Color.red;
            }

            if (rewardSummaryText != null)
            {
                rewardSummaryText.text = $"Você recebeu 💰 {coinsEarned} moedas de recompensa.";
            }
        }

        private void OnNextStepClicked()
        {
            if (screenContainer != null) screenContainer.SetActive(false);

            // Transiciona o jogo para a tela de upgrades
            Core.GameManager.Instance.ChangeState(Core.GameState.Shop);
        }
    }
}
