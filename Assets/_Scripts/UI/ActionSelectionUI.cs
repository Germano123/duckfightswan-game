using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DuckFightSwan.Units;

namespace DuckFightSwan.UI
{
    /// <summary>
    /// Painel de seleção de ações ("Mover" / "Atacar" / "Cancelar") exibido para a unidade selecionada.
    /// Respeita o SRP ao lidar exclusivamente com a exibição e navegação (Mouse e WASD) do menu.
    /// Permite navegação pelas teclas W/S ou setas, e confirmação via Espaço ou Enter.
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

        private int currentButtonIndex = 0;
        private Button[] menuButtons;

        public bool IsOpen => actionPanel != null && actionPanel.activeSelf;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                menuButtons = new Button[] { moveActionButton, attackActionButton, cancelActionButton };
                SetupButtonListeners();
                if (actionPanel != null) actionPanel.SetActive(false);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            menuButtons = new Button[] { moveActionButton, attackActionButton, cancelActionButton };
            SetupButtonListeners();
        }

        /// <summary>
        /// Registra os ouvintes dos botões com proteção contra inscrições duplicadas.
        /// </summary>
        private void SetupButtonListeners()
        {
            if (moveActionButton != null)
            {
                moveActionButton.onClick.RemoveListener(SelectMoveAction);
                moveActionButton.onClick.AddListener(SelectMoveAction);
            }

            if (attackActionButton != null)
            {
                attackActionButton.onClick.RemoveListener(SelectAttackAction);
                attackActionButton.onClick.AddListener(SelectAttackAction);
            }

            if (cancelActionButton != null)
            {
                cancelActionButton.onClick.RemoveListener(SelectCancelAction);
                cancelActionButton.onClick.AddListener(SelectCancelAction);
            }
        }

        private void Update()
        {
            if (!IsOpen) return;

            UpdatePosition();
            HandleKeyboardNavigation();
        }

        /// <summary>
        /// Mantém o painel ancorado à tela ou à unidade sem oscilar fora dos limites da janela.
        /// </summary>
        private void UpdatePosition()
        {
            if (actionPanel == null || selectedUnit == null || Camera.main == null) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(selectedUnit.transform.position);
            if (screenPos.z > 0)
            {
                // Clampa dentro da área visível da tela com margem de segurança
                float minX = 180f;
                float maxX = Screen.width - 180f;
                float minY = 120f;
                float maxY = Screen.height - 120f;

                float px = Mathf.Clamp(screenPos.x + screenOffset.x, minX, maxX);
                float py = Mathf.Clamp(screenPos.y + screenOffset.y, minY, maxY);

                actionPanel.transform.position = new Vector3(px, py, 0f);
            }
        }

        /// <summary>
        /// Processa navegação por teclado: W/S (ou setas) para alternar opções, Espaço/Enter para confirmar.
        /// </summary>
        private void HandleKeyboardNavigation()
        {
            if (menuButtons == null || menuButtons.Length == 0) return;

            // Navegação para Cima (W ou Seta para Cima)
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                CycleButtonSelection(-1);
            }
            // Navegação para Baixo (S ou Seta para Baixo)
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                CycleButtonSelection(1);
            }

            // Confirmação (Espaço ou Enter)
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ExecuteCurrentSelectedButton();
            }

            // Cancelamento (Escape)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SelectCancelAction();
            }
        }

        private void CycleButtonSelection(int direction)
        {
            int attempts = 0;
            int nextIndex = currentButtonIndex;

            do
            {
                nextIndex = (nextIndex + direction + menuButtons.Length) % menuButtons.Length;
                attempts++;
                // Só seleciona botões válidos e clicáveis
                if (menuButtons[nextIndex] != null && menuButtons[nextIndex].interactable)
                {
                    currentButtonIndex = nextIndex;
                    HighlightCurrentButton();
                    break;
                }
            } while (attempts < menuButtons.Length);
        }

        private void HighlightCurrentButton()
        {
            if (menuButtons == null || currentButtonIndex < 0 || currentButtonIndex >= menuButtons.Length) return;

            Button activeBtn = menuButtons[currentButtonIndex];
            if (activeBtn != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(activeBtn.gameObject);
            }
        }

        private void ExecuteCurrentSelectedButton()
        {
            switch (currentButtonIndex)
            {
                case 0:
                    if (moveActionButton != null && moveActionButton.interactable) SelectMoveAction();
                    break;
                case 1:
                    if (attackActionButton != null && attackActionButton.interactable) SelectAttackAction();
                    break;
                case 2:
                    SelectCancelAction();
                    break;
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
                string climbInfo = unit.MaxClimbHeight >= 2 ? " (Escala +2)" : "";
                unitInfoText.text = $"{unit.UnitName} (Lvl {unit.CurrentLevel})\nHP: {unit.Health.CurrentHealth}/{unit.Health.MaxHealth}\nDeslocamento: {unit.MovePoints} casas{climbInfo}\nAlcance: {unit.UnitStats.Range}\nAções: ⭐ {unit.RemainingActions}/{unit.ActionsPerTurn}";
            }

            // Habilita/desabilita botões baseado nas ações restantes
            if (moveActionButton != null) moveActionButton.interactable = unit.RemainingActions > 0;
            if (attackActionButton != null) attackActionButton.interactable = unit.RemainingActions > 0;

            // Define o botão inicial (Mover se disponível, senão Atacar, senão Cancelar)
            if (moveActionButton != null && moveActionButton.interactable)
            {
                currentButtonIndex = 0;
            }
            else if (attackActionButton != null && attackActionButton.interactable)
            {
                currentButtonIndex = 1;
            }
            else
            {
                currentButtonIndex = 2;
            }

            if (actionPanel != null)
            {
                actionPanel.SetActive(true);
                UpdatePosition();
                HighlightCurrentButton();
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

        // =========================================================================
        // MÉTODOS PÚBLICOS DE AÇÃO (Compatíveis com Inspector OnClick e código)
        // =========================================================================

        public void SelectMoveAction()
        {
            if (actionPanel != null && !actionPanel.activeSelf) return;

            Hide();

            if (onMoveSelected != null)
            {
                onMoveSelected.Invoke();
            }
            else if (Core.InputGridController.Instance != null)
            {
                Core.InputGridController.Instance.TriggerMoveAction();
            }
        }

        public void SelectAttackAction()
        {
            if (actionPanel != null && !actionPanel.activeSelf) return;

            Hide();

            if (onAttackSelected != null)
            {
                onAttackSelected.Invoke();
            }
            else if (Core.InputGridController.Instance != null)
            {
                Core.InputGridController.Instance.TriggerAttackAction();
            }
        }

        public void SelectCancelAction()
        {
            if (actionPanel != null && !actionPanel.activeSelf) return;

            Hide();

            if (onCancelSelected != null)
            {
                onCancelSelected.Invoke();
            }
            else if (Core.InputGridController.Instance != null)
            {
                Core.InputGridController.Instance.TriggerCancelAction();
            }
        }

        // Aliases para exibição intuitiva na lista de métodos do Unity Inspector
        public void OnMoveClicked() => SelectMoveAction();
        public void OnAttackClicked() => SelectAttackAction();
        public void OnCancelClicked() => SelectCancelAction();
    }
}
