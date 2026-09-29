using UnityEngine;
using UnityEngine.UI;
using DuckFightSwan.Units;

namespace DuckFightSwan.UI
{
    /// <summary>
    /// Painel de seleção de ações ("Mover" / "Atacar" / "Cancelar") exibido perto da unidade selecionada.
    /// Respeita o SRP ao lidar apenas com a renderização e cliques da interface de ação.
    /// </summary>
    public class ActionSelectionUI : MonoBehaviour
    {
        public static ActionSelectionUI Instance { get; private set; }

        [Header("Elementos de Interface")]
        [SerializeField] private GameObject actionPanel;
        [SerializeField] private Button moveActionButton;
        [SerializeField] private Button attackActionButton;
        [SerializeField] private Button cancelActionButton;
        [SerializeField] private TMPro.TextMeshProUGUI unitInfoText;

        [Header("Configuração de Posicionamento")]
        [SerializeField] private Vector3 screenOffset = new Vector3(0f, 80f, 0f);

        private Unit selectedUnit;
        private System.Action onMoveSelected;
        private System.Action onAttackSelected;
        private System.Action onCancelSelected;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (actionPanel != null) actionPanel.SetActive(false);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (moveActionButton != null) moveActionButton.onClick.AddListener(SelectMoveAction);
            if (attackActionButton != null) attackActionButton.onClick.AddListener(SelectAttackAction);
            if (cancelActionButton != null) cancelActionButton.onClick.AddListener(SelectCancelAction);
        }

        private void Update()
        {
            // Reposiciona o painel dinamicamente em relação à tela se a unidade estiver selecionada
            if (actionPanel != null && actionPanel.activeSelf && selectedUnit != null && Camera.main != null)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(selectedUnit.transform.position);
                // Se a unidade está atrás da câmera, esconde o painel
                if (screenPos.z < 0)
                {
                    actionPanel.SetActive(false);
                }
                else
                {
                    actionPanel.SetActive(true);
                    actionPanel.transform.position = screenPos + screenOffset;
                }
            }
        }

        /// <summary>
        /// Mostra o painel de ações próximo à unidade selecionada.
        /// </summary>
        public void Show(Unit unit, System.Action onMove, System.Action onAttack, System.Action onCancel)
        {
            selectedUnit = unit;
            onMoveSelected = onMove;
            onAttackSelected = onAttack;
            onCancelSelected = onCancel;

            if (unitInfoText != null)
            {
                unitInfoText.text = $"{unit.UnitName} (Lvl {unit.CurrentLevel})\nHP: {unit.Health.CurrentHealth}/{unit.Health.MaxHealth}\nEstrelas: ⭐ {unit.RemainingActions}/{unit.ActionsPerTurn}";
            }

            // Habilita/desabilita botões baseado nas ações restantes
            if (moveActionButton != null) moveActionButton.interactable = unit.RemainingActions > 0;
            if (attackActionButton != null) attackActionButton.interactable = unit.RemainingActions > 0;

            if (actionPanel != null)
            {
                actionPanel.SetActive(true);
                Update(); // Atualiza posição imediatamente
            }
        }

        /// <summary>
        /// Oculta o painel de ações.
        /// </summary>
        public void Hide()
        {
            selectedUnit = null;
            if (actionPanel != null) actionPanel.SetActive(false);
        }

        private void SelectMoveAction()
        {
            onMoveSelected?.Invoke();
        }

        private void SelectAttackAction()
        {
            onAttackSelected?.Invoke();
        }

        private void SelectCancelAction()
        {
            onCancelSelected?.Invoke();
        }
    }
}
