using UnityEngine;
using DuckFightSwan.Units;
using DuckFightSwan.Terrain;
using DuckFightSwan.Combat;
using System.Collections.Generic;

namespace DuckFightSwan.Core
{
    /// <summary>
    /// Interface para estados de interação tática (SOLID / State Pattern).
    /// </summary>
    public interface IInteractionState
    {
        void Enter(InputGridController controller);
        void Exit(InputGridController controller);
        void HandleClick(InputGridController controller, RaycastHit hit);
    }

    /// <summary>
    /// Controlador central para interações manuais com o Grid.
    /// Respeita o SRP ao gerenciar o loop de Input e delegar a lógica de estados de ação (SOLID).
    /// </summary>
    public class InputGridController : MonoBehaviour
    {
        public static InputGridController Instance { get; private set; }

        [Header("Configuração de Seleção")]
        [SerializeField] private LayerMask clickMask;

        private IInteractionState currentInteractionState;
        private Unit selectedUnit;

        public Unit SelectedUnit => selectedUnit;

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
            ChangeState(new InteractionIdleState());
        }

        private void Update()
        {
            // Bloqueia qualquer input e seleção no grid durante o turno das tropas inimigas
            if (MatchManager.Instance != null && MatchManager.Instance.IsEnemyTurn)
            {
                return;
            }

            // Evita processar cliques no grid quando o jogador interage com elementos de UI
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            // Escuta o clique esquerdo do mouse
            if (Input.GetMouseButtonDown(0))
            {
                HandleInputClick();
            }
        }

        public void ChangeState(IInteractionState newState)
        {
            if (currentInteractionState != null)
            {
                currentInteractionState.Exit(this);
            }

            currentInteractionState = newState;

            if (currentInteractionState != null)
            {
                currentInteractionState.Enter(this);
            }
        }

        public void SetSelectedUnit(Unit unit)
        {
            selectedUnit = unit;
        }

        public void Deselect()
        {
            selectedUnit = null;

            if (CameraController.Instance != null)
            {
                CameraController.Instance.ClearFocus();
            }

            if (UI.ActionSelectionUI.Instance != null)
            {
                UI.ActionSelectionUI.Instance.Hide();
            }

            if (GridManager.Instance != null)
            {
                GridManager.Instance.ClearHighlights();
            }

            ChangeState(new InteractionIdleState());
            Debug.Log("[InputGridController] Seleção limpa e retornada para Idle.");
        }

        private void HandleInputClick()
        {
            if (Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                if (currentInteractionState != null)
                {
                    currentInteractionState.HandleClick(this, hit);
                }
            }
        }

        // Métodos auxiliares para extrair dados do Raycast
        public Unit GetUnitFromHit(RaycastHit hit)
        {
            return hit.collider.GetComponentInParent<Unit>();
        }

        public TileNode GetTileFromHit(RaycastHit hit)
        {
            GameObject hitObj = hit.collider.gameObject;
            if (hitObj.name.StartsWith("Tile_"))
            {
                string[] parts = hitObj.name.Split('_');
                if (parts.Length >= 3 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int z))
                {
                    if (GridManager.Instance != null)
                    {
                        return GridManager.Instance.GetNodeAt(x, z);
                    }
                }
            }
            return null;
        }

        public void OpenActionUI()
        {
            if (UI.ActionSelectionUI.Instance != null && selectedUnit != null)
            {
                UI.ActionSelectionUI.Instance.Show(
                    selectedUnit,
                    onMove: () => ChangeState(new InteractionMoveState()),
                    onAttack: () => ChangeState(new InteractionAttackState()),
                    onCancel: () => Deselect()
                );
            }
        }

        public bool TryExecuteMove(Unit unit, TileNode targetTile)
        {
            if (unit == null || unit.CurrentTile == null || targetTile == null) return false;

            // 1. Verifica se o bloco de destino está ocupado por outra unidade
            if (targetTile.CurrentUnit != null)
            {
                Debug.LogWarning($"[InputGridController] Bloco ({targetTile.X}, {targetTile.Z}) já está ocupado.");
                return false;
            }

            // 2. Verifica distância (apenas 1 casa adjacente horizontal/vertical/diagonal para turnos)
            int deltaX = Mathf.Abs(unit.CurrentTile.X - targetTile.X);
            int deltaZ = Mathf.Abs(unit.CurrentTile.Z - targetTile.Z);

            if (deltaX > 1 || deltaZ > 1)
            {
                Debug.LogWarning("[InputGridController] Bloco muito distante. Apenas movimentação adjacente é permitida por clique.");
                return false;
            }

            // Regra ecológico-tática: Lagos profundos são intransitáveis para tropas terrestres
            if (targetTile.Type == TerrainType.Lake)
            {
                Debug.LogWarning("[InputGridController] Movimento inválido! Não é possível entrar em lagos profundos.");
                return false;
            }

            // 3. Regra de altura: Terrenos com altura de 2 ou mais são inalcançáveis por hora
            if (targetTile.Height >= 2)
            {
                Debug.LogWarning($"[InputGridController] Movimento inválido! Terrenos com altura de 2 ou mais são inalcançáveis por hora. Altura do destino: {targetTile.Height}");
                return false;
            }

            // 4. Regra de degrau: Diferença de altura máxima permitida = 1
            int heightDiff = Mathf.Abs(unit.CurrentTile.Height - targetTile.Height);
            if (heightDiff > 1)
            {
                Debug.LogWarning($"[InputGridController] Movimento inválido! Diferença de altura muito alta ({unit.CurrentTile.Height} para {targetTile.Height}). Diferença máxima permitida é 1.");
                return false;
            }

            // 5. Executa a transição física e lógica
            unit.CurrentTile.CurrentUnit = null; // Libera bloco antigo
            unit.CurrentTile = targetTile;
            targetTile.CurrentUnit = unit; // Ocupa novo bloco

            unit.SetMoveTarget(targetTile.GetTopPosition());
            Debug.Log($"[InputGridController] Movendo {unit.UnitName} para ({targetTile.X}, {targetTile.Z}). Altura do Bloco: {targetTile.Height}");

            // Persiste a nova posição das tropas no save
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.SaveCurrentState();
            }

            return true;
        }

        public bool TryExecuteAttack(Unit attacker, Unit defender)
        {
            if (attacker == null || defender == null || attacker.CurrentTile == null || defender.CurrentTile == null) return false;

            // 1. Calcula a distância espacial
            float distance = Vector3.Distance(attacker.transform.position, defender.transform.position);
            float range = attacker.UnitStats.Range;

            // 2. Valida se o defensor está dentro do raio de ataque da classe
            if (distance > range)
            {
                Debug.LogWarning($"[InputGridController] Oponente fora de alcance de ataque. Distância: {distance:F2} | Alcance: {range}");
                return false;
            }

            // 3. Executa a ação de dano encapsulada
            int rawDamage = attacker.UnitStats.Damage;
            Debug.Log($"[InputGridController] {attacker.UnitName} atacou {defender.UnitName} desferindo ataque (Ataque Bruto: {rawDamage})!");
            defender.Health.TakeDamage(new Damage(rawDamage, attacker));

            // Persiste a nova vida das tropas no save
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.SaveCurrentState();
            }

            return true;
        }
    }

    /// <summary>
    /// Estado de Espera / Seleção Inicial (SOLID)
    /// </summary>
    public class InteractionIdleState : IInteractionState
    {
        public void Enter(InputGridController controller) { }
        public void Exit(InputGridController controller) { }

        public void HandleClick(InputGridController controller, RaycastHit hit)
        {
            Unit clickedUnit = controller.GetUnitFromHit(hit);
            if (clickedUnit != null && clickedUnit.Faction == FactionType.Ducks)
            {
                if (clickedUnit.RemainingActions <= 0)
                {
                    Debug.LogWarning($"[InputGridController] Tropa {clickedUnit.UnitName} já esgotou suas ações neste turno!");
                    return;
                }

                controller.SetSelectedUnit(clickedUnit);
                Debug.Log($"[InputGridController] Tropa selecionada em IdleState: {clickedUnit.UnitName}");

                if (CameraController.Instance != null)
                {
                    CameraController.Instance.FocusOn(clickedUnit.transform);
                }

                controller.OpenActionUI();
            }
        }
    }

    /// <summary>
    /// Estado de Seleção de Movimento (SOLID)
    /// </summary>
    public class InteractionMoveState : IInteractionState
    {
        public void Enter(InputGridController controller)
        {
            if (GridManager.Instance != null && controller.SelectedUnit != null)
            {
                var validTargets = GridManager.Instance.GetValidMoveTargets(controller.SelectedUnit.CurrentTile);
                GridManager.Instance.HighlightTiles(validTargets, new Color(0.2f, 0.4f, 1f, 1f));
                Debug.Log($"[InteractionMoveState] Destacando {validTargets.Count} blocos de movimento.");
            }
        }

        public void Exit(InputGridController controller)
        {
            if (GridManager.Instance != null)
            {
                GridManager.Instance.ClearHighlights();
            }
        }

        public void HandleClick(InputGridController controller, RaycastHit hit)
        {
            TileNode clickedTile = controller.GetTileFromHit(hit);
            Unit unit = controller.SelectedUnit;

            if (clickedTile != null && unit != null)
            {
                if (controller.TryExecuteMove(unit, clickedTile))
                {
                    unit.ConsumeAction();
                    if (unit.RemainingActions > 0)
                    {
                        controller.ChangeState(new InteractionIdleState());
                        controller.OpenActionUI();
                    }
                    else
                    {
                        controller.Deselect();
                    }
                }
                else
                {
                    // Falhou movimento, retorna ao estado de seleção
                    controller.ChangeState(new InteractionIdleState());
                    controller.OpenActionUI();
                }
            }
            else
            {
                // Clique fora, retorna ao estado de seleção
                controller.ChangeState(new InteractionIdleState());
                controller.OpenActionUI();
            }
        }
    }

    /// <summary>
    /// Estado de Seleção de Ataque (SOLID)
    /// </summary>
    public class InteractionAttackState : IInteractionState
    {
        public void Enter(InputGridController controller)
        {
            if (GridManager.Instance != null && controller.SelectedUnit != null)
            {
                var validTargets = GridManager.Instance.GetValidAttackTargets(controller.SelectedUnit.CurrentTile, controller.SelectedUnit.UnitStats.Range);
                GridManager.Instance.HighlightTiles(validTargets, new Color(1f, 0.2f, 0.2f, 1f));
                Debug.Log($"[InteractionAttackState] Destacando {validTargets.Count} alvos de ataque.");
            }
        }

        public void Exit(InputGridController controller)
        {
            if (GridManager.Instance != null)
            {
                GridManager.Instance.ClearHighlights();
            }
        }

        public void HandleClick(InputGridController controller, RaycastHit hit)
        {
            Unit defender = controller.GetUnitFromHit(hit);
            Unit attacker = controller.SelectedUnit;

            if (defender != null && attacker != null && defender.Faction == FactionType.Swans)
            {
                if (controller.TryExecuteAttack(attacker, defender))
                {
                    attacker.ConsumeAction();
                    if (attacker.RemainingActions > 0)
                    {
                        controller.ChangeState(new InteractionIdleState());
                        controller.OpenActionUI();
                    }
                    else
                    {
                        controller.Deselect();
                    }
                }
                else
                {
                    // Falhou ataque, retorna ao estado de seleção
                    controller.ChangeState(new InteractionIdleState());
                    controller.OpenActionUI();
                }
            }
            else
            {
                // Clique inválido, retorna ao estado de seleção
                controller.ChangeState(new InteractionIdleState());
                controller.OpenActionUI();
            }
        }
    }
}
