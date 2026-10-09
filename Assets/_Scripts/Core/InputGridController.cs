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
        void UpdateState(InputGridController controller);
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
        public IInteractionState CurrentState => currentInteractionState;

        /// <summary>
        /// Informa se o jogador está em modo de seleção tática (Menu aberto ou cursor de grid ativo).
        /// Usado pelo CameraController para suspender o pan com WASD durante interações.
        /// </summary>
        public bool IsInteracting => 
            (currentInteractionState is InteractionMoveState || 
             currentInteractionState is InteractionAttackState || 
             (UI.ActionSelectionUI.Instance != null && UI.ActionSelectionUI.Instance.IsOpen));

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

            // Teclas de atalho para finalizar o turno do jogador (Enter ou T) apenas fora de sub-ações
            if (!(currentInteractionState is InteractionMoveState || currentInteractionState is InteractionAttackState))
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.T))
                {
                    if (MatchManager.Instance != null)
                    {
                        MatchManager.Instance.EndTurn();
                        return;
                    }
                }
            }

            // Atualiza o estado de interação ativo (WASD no grid, confirmação por Espaço/Enter, etc.)
            currentInteractionState?.UpdateState(this);

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

            // Escuta o clique direito para cancelar ação atual e reabrir menu ou desselecionar
            if (Input.GetMouseButtonDown(1))
            {
                if (currentInteractionState is InteractionMoveState || currentInteractionState is InteractionAttackState)
                {
                    ChangeState(new InteractionIdleState());
                    OpenActionUI();
                }
                else
                {
                    Deselect();
                }
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

        /// <summary>
        /// Seleciona programaticamente uma unidade, focando a câmera nela e opcionalmente abrindo o menu de ações táticas.
        /// </summary>
        public void SelectUnit(Unit unit, bool openUI = true)
        {
            if (unit == null || unit.Health == null || unit.Health.IsDead) return;
            if (unit.RemainingActions <= 0) return;

            SetSelectedUnit(unit);
            Debug.Log($"[InputGridController] Unidade selecionada programaticamente: {unit.UnitName}");

            if (CameraController.Instance != null)
            {
                CameraController.Instance.FocusOn(unit.transform);
            }

            if (openUI)
            {
                OpenActionUI();
            }

            ChangeState(new InteractionIdleState());
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
            if (hit.collider == null) return null;

            // Busca todas as instâncias no parentesco e prioriza a unidade funcional (com CurrentTile e stats válidos)
            Unit[] units = hit.collider.GetComponentsInParent<Unit>();
            if (units == null || units.Length == 0) return null;

            foreach (var u in units)
            {
                if (u.CurrentTile != null && u.UnitStats != null && u.UnitStats.MovePoints > 0)
                {
                    return u;
                }
            }

            // Fallback: se nenhuma tiver todos os requisitos, retorna a da raiz mais alta
            return units[units.Length - 1];
        }

        public TileNode GetTileFromHit(RaycastHit hit)
        {
            if (hit.collider == null) return null;

            // Percorre a árvore hierárquica para cima até encontrar o GameObject Tile_x_z
            Transform curr = hit.collider.transform;
            while (curr != null)
            {
                string objName = curr.name;
                if (objName.StartsWith("Tile_"))
                {
                    string[] parts = objName.Split('_');
                    if (parts.Length >= 3 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int z))
                    {
                        if (GridManager.Instance != null)
                        {
                            return GridManager.Instance.GetNodeAt(x, z);
                        }
                    }
                }
                curr = curr.parent;
            }

            // Fallback: se o clique foi na malha/superfície do terreno sem nome explícito, busca via coordenadas mundiais
            if (GridManager.Instance != null)
            {
                return GridManager.Instance.GetNodeAtWorldPosition(hit.point);
            }

            return null;
        }

        public void OpenActionUI()
        {
            if (UI.ActionSelectionUI.Instance != null && selectedUnit != null)
            {
                UI.ActionSelectionUI.Instance.Show(
                    selectedUnit,
                    onMove: TriggerMoveAction,
                    onAttack: TriggerAttackAction,
                    onCancel: TriggerCancelAction
                );
            }
        }

        /// <summary>
        /// Dispara o estado de interação para mover a tropa selecionada.
        /// </summary>
        public void TriggerMoveAction()
        {
            if (selectedUnit != null && selectedUnit.RemainingActions > 0)
            {
                ChangeState(new InteractionMoveState());
            }
        }

        /// <summary>
        /// Dispara o estado de interação para atacar com a tropa selecionada.
        /// </summary>
        public void TriggerAttackAction()
        {
            if (selectedUnit != null && selectedUnit.RemainingActions > 0)
            {
                ChangeState(new InteractionAttackState());
            }
        }

        /// <summary>
        /// Cancela a ação atual e limpa a seleção da tropa.
        /// </summary>
        public void TriggerCancelAction()
        {
            Deselect();
        }

        public bool TryExecuteMove(Unit unit, TileNode targetTile)
        {
            if (unit == null || unit.CurrentTile == null || targetTile == null) return false;

            // 1. Verifica se o bloco de destino é navegável
            if (!targetTile.IsWalkable)
            {
                Debug.LogWarning($"[InputGridController] Bloco ({targetTile.X}, {targetTile.Z}) bloqueado por obstáculo de cenário (não-caminhável).");
                return false;
            }

            // 2. Verifica se o bloco de destino está ocupado por outra unidade
            if (targetTile.CurrentUnit != null)
            {
                Debug.LogWarning($"[InputGridController] Bloco ({targetTile.X}, {targetTile.Z}) já está ocupado.");
                return false;
            }

            // 2. Recupera o caminho validado pelo algoritmo de relevo do GridManager
            List<Vector3> pathWaypoints = null;
            if (GridManager.Instance != null)
            {
                pathWaypoints = GridManager.Instance.GetPathTo(targetTile);
            }

            // Fallback de caminho direto caso seja adjacente válido e não esteja no cache
            if (pathWaypoints == null || pathWaypoints.Count == 0)
            {
                int deltaX = Mathf.Abs(unit.CurrentTile.X - targetTile.X);
                int deltaZ = Mathf.Abs(unit.CurrentTile.Z - targetTile.Z);

                if (deltaX + deltaZ == 1)
                {
                    bool currentIsWater = unit.CurrentTile.Type == TerrainType.Lake || unit.CurrentTile.Type == TerrainType.River;
                    bool targetIsWater = targetTile.Type == TerrainType.Lake || targetTile.Type == TerrainType.River;
                    bool isWaterMove = currentIsWater || targetIsWater;

                    int deltaH;
                    if (currentIsWater && !targetIsWater) deltaH = targetTile.Height - 1;
                    else if (!currentIsWater && targetIsWater) deltaH = -(unit.CurrentTile.Height - 1);
                    else deltaH = targetTile.Height - unit.CurrentTile.Height;

                    int cost = (deltaH == 2) ? 3 : (deltaH == 1 ? 2 : 1);
                    if (isWaterMove) cost += 1;

                    if ((deltaH <= 1 || (deltaH == 2 && unit.CanClimb(2))) && deltaH >= -2 && unit.MovePoints >= cost)
                    {
                        pathWaypoints = new List<Vector3> { unit.CurrentTile.GetTopPosition(), targetTile.GetTopPosition() };
                    }
                }
            }

            if (pathWaypoints == null || pathWaypoints.Count == 0)
            {
                Debug.LogWarning($"[InputGridController] Bloco ({targetTile.X}, {targetTile.Z}) inalcançável com o deslocamento atual ({unit.MovePoints} pts) ou relevo muito íngreme.");
                return false;
            }

            // 3. Executa a transição física e lógica
            unit.CurrentTile.CurrentUnit = null; // Libera bloco antigo
            unit.CurrentTile = targetTile;
            targetTile.CurrentUnit = unit; // Ocupa novo bloco

            unit.SetMovePath(pathWaypoints);
            Debug.Log($"[InputGridController] Movendo {unit.UnitName} para ({targetTile.X}, {targetTile.Z}) via {pathWaypoints.Count} waypoints. Altura: {targetTile.Height}");

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

            bool isArcher = attacker.ClassData != null && attacker.ClassData.ClassType == UnitClassType.Archer;
            float range = attacker.UnitStats.Range;

            if (isArcher)
            {
                // Ataque à distância: alcance radial no plano XZ
                float distance = Vector2.Distance(
                    new Vector2(attacker.CurrentTile.X, attacker.CurrentTile.Z),
                    new Vector2(defender.CurrentTile.X, defender.CurrentTile.Z));

                if (distance > range + 0.15f)
                {
                    Debug.LogWarning($"[InputGridController] Oponente fora de alcance do arco. Distância: {distance:F2} | Alcance: {range}");
                    return false;
                }
            }
            else
            {
                // Combate corpo a corpo ao redor da posição: células adjacentes em 8 direções
                int dx = Mathf.Abs(attacker.CurrentTile.X - defender.CurrentTile.X);
                int dz = Mathf.Abs(attacker.CurrentTile.Z - defender.CurrentTile.Z);
                int deltaH = Mathf.Abs(attacker.CurrentTile.Height - defender.CurrentTile.Height);

                if (dx > 1 || dz > 1 || (dx == 0 && dz == 0))
                {
                    Debug.LogWarning($"[InputGridController] Oponente fora de alcance corpo a corpo ({dx}, {dz}). Apenas adjacentes.");
                    return false;
                }

                if (deltaH > attacker.MaxClimbHeight)
                {
                    Debug.LogWarning($"[InputGridController] Oponente em relevo inalcançável para combate corpo a corpo ({deltaH} degraus de diferença).");
                    return false;
                }
            }

            // Executa a ação de dano encapsulada
            int rawDamage = attacker.UnitStats.Damage;
            Debug.Log($"[InputGridController] {attacker.UnitName} atacou {defender.UnitName} (Ataque Bruto: {rawDamage})!");
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
        public void UpdateState(InputGridController controller) { }

        public void HandleClick(InputGridController controller, RaycastHit hit)
        {
            Unit clickedUnit = controller.GetUnitFromHit(hit);

            // Fallback: se clicou no tile onde o pato está em vez da malha do modelo
            if (clickedUnit == null)
            {
                TileNode clickedTile = controller.GetTileFromHit(hit);
                if (clickedTile != null && clickedTile.CurrentUnit != null)
                {
                    clickedUnit = clickedTile.CurrentUnit;
                }
            }

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
    /// Estado de Seleção de Movimento com Cursor 3D e controle por WASD / Mouse (SOLID)
    /// </summary>
    public class InteractionMoveState : IInteractionState
    {
        private List<TileNode> validTargets = new List<TileNode>();

        public void Enter(InputGridController controller)
        {
            if (UI.ActionSelectionUI.Instance != null)
            {
                UI.ActionSelectionUI.Instance.Hide();
            }

            if (GridManager.Instance != null && controller.SelectedUnit != null)
            {
                validTargets = GridManager.Instance.GetValidMoveTargets(controller.SelectedUnit.CurrentTile, controller.SelectedUnit);
                GridManager.Instance.HighlightTiles(validTargets, new Color(0.2f, 0.5f, 1f, 1f));
                Debug.Log($"[InteractionMoveState] Destacando {validTargets.Count} blocos de movimento para {controller.SelectedUnit.UnitName} (Deslocamento: {controller.SelectedUnit.MovePoints}).");

                EnsureGridCursor();
                TileNode initialTile = controller.SelectedUnit.CurrentTile;
                if (validTargets.Count > 0 && (initialTile == null || !validTargets.Contains(initialTile)))
                {
                    initialTile = validTargets[0];
                }
                GridCursor.Instance.Show(initialTile, new Color(0.2f, 0.9f, 1.0f, 0.85f));
            }
        }

        public void Exit(InputGridController controller)
        {
            if (GridManager.Instance != null)
            {
                GridManager.Instance.ClearHighlights();
            }

            if (GridCursor.Instance != null)
            {
                GridCursor.Instance.Hide();
            }
        }

        public void UpdateState(InputGridController controller)
        {
            // 1. Navegação de Cursor via WASD / Setas
            Vector3 inputDir = GetInputDirection();
            if (inputDir.sqrMagnitude > 0.01f && validTargets.Count > 0 && GridCursor.Instance != null)
            {
                TileNode current = GridCursor.Instance.CurrentTile ?? controller.SelectedUnit.CurrentTile;
                TileNode bestTile = FindBestTileInDirection(current, validTargets, inputDir);
                if (bestTile != null && bestTile != current)
                {
                    GridCursor.Instance.MoveTo(bestTile);
                }
            }

            // 2. Navegação de Cursor via Mouse Hover
            if (Camera.main != null && validTargets.Count > 0 && GridCursor.Instance != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    TileNode hoveredTile = controller.GetTileFromHit(hit);
                    if (hoveredTile != null && validTargets.Contains(hoveredTile))
                    {
                        if (GridCursor.Instance.CurrentTile != hoveredTile)
                        {
                            GridCursor.Instance.MoveTo(hoveredTile);
                        }
                    }
                }
            }

            // 3. Confirmação de Movimento (Espaço ou Enter)
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (GridCursor.Instance != null && GridCursor.Instance.CurrentTile != null)
                {
                    ConfirmMove(controller, GridCursor.Instance.CurrentTile);
                }
            }

            // 4. Cancelamento (Escape)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                controller.ChangeState(new InteractionIdleState());
                controller.OpenActionUI();
            }
        }

        public void HandleClick(InputGridController controller, RaycastHit hit)
        {
            TileNode clickedTile = controller.GetTileFromHit(hit);

            // Fallback: se clicou numa tropa ou topo de colisor, recupera o tile dessa unidade
            if (clickedTile == null)
            {
                Unit hitUnit = controller.GetUnitFromHit(hit);
                if (hitUnit != null)
                {
                    clickedTile = hitUnit.CurrentTile;
                }
            }

            if (clickedTile != null && validTargets.Contains(clickedTile))
            {
                ConfirmMove(controller, clickedTile);
            }
            else if (GridCursor.Instance != null && GridCursor.Instance.CurrentTile != null && validTargets.Contains(GridCursor.Instance.CurrentTile))
            {
                // Se clicou na proximidade do cursor de seleção ativo
                ConfirmMove(controller, GridCursor.Instance.CurrentTile);
            }
        }

        private void ConfirmMove(InputGridController controller, TileNode targetTile)
        {
            Unit unit = controller.SelectedUnit;
            if (unit == null || targetTile == null) return;

            if (controller.TryExecuteMove(unit, targetTile))
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
        }

        private void EnsureGridCursor()
        {
            if (GridCursor.Instance == null)
            {
                GameObject cursorObj = new GameObject("GridCursor");
                cursorObj.AddComponent<GridCursor>();
            }
        }

        private Vector3 GetInputDirection()
        {
            float camYaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;
            Vector3 forwardDir = Quaternion.Euler(0, camYaw, 0) * Vector3.forward;
            Vector3 rightDir = Quaternion.Euler(0, camYaw, 0) * Vector3.right;

            Vector3 dir = Vector3.zero;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) dir += forwardDir;
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) dir -= forwardDir;
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) dir += rightDir;
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) dir -= rightDir;

            return dir.normalized;
        }

        private TileNode FindBestTileInDirection(TileNode current, List<TileNode> candidates, Vector3 direction)
        {
            if (current == null || candidates == null || candidates.Count == 0) return null;

            TileNode best = null;
            float minDistance = float.MaxValue;
            Vector3 currentPos = new Vector3(current.X, 0f, current.Z);
            Vector3 idealPos = currentPos + direction * 1.5f;

            foreach (var tile in candidates)
            {
                Vector3 tilePos = new Vector3(tile.X, 0f, tile.Z);
                Vector3 toTile = tilePos - currentPos;

                if (Vector3.Dot(direction, toTile.normalized) > 0.15f)
                {
                    float dist = Vector3.Distance(idealPos, tilePos);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        best = tile;
                    }
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Estado de Seleção de Ataque com Cursor 3D e controle por WASD / Mouse (SOLID)
    /// </summary>
    public class InteractionAttackState : IInteractionState
    {
        private List<TileNode> validTargets = new List<TileNode>();
        private int currentTargetIndex = 0;

        public void Enter(InputGridController controller)
        {
            if (UI.ActionSelectionUI.Instance != null)
            {
                UI.ActionSelectionUI.Instance.Hide();
            }

            if (GridManager.Instance != null && controller.SelectedUnit != null)
            {
                validTargets = GridManager.Instance.GetValidAttackTargets(controller.SelectedUnit.CurrentTile, controller.SelectedUnit);
                GridManager.Instance.HighlightTiles(validTargets, new Color(1f, 0.2f, 0.2f, 1f));
                Debug.Log($"[InteractionAttackState] Destacando {validTargets.Count} alvos de ataque para {controller.SelectedUnit.UnitName}.");

                EnsureGridCursor();
                if (validTargets.Count > 0)
                {
                    currentTargetIndex = 0;
                    GridCursor.Instance.Show(validTargets[0], new Color(1f, 0.2f, 0.2f, 0.85f));
                }
            }
        }

        public void Exit(InputGridController controller)
        {
            if (GridManager.Instance != null)
            {
                GridManager.Instance.ClearHighlights();
            }

            if (GridCursor.Instance != null)
            {
                GridCursor.Instance.Hide();
            }
        }

        public void UpdateState(InputGridController controller)
        {
            if (validTargets == null || validTargets.Count == 0) return;

            // 1. Navegação / Alternância entre alvos com WASD / Setas / Tab
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.D) || 
                Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.RightArrow) || 
                Input.GetKeyDown(KeyCode.Tab))
            {
                CycleAttackTarget(1);
            }
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.A) || 
                     Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                CycleAttackTarget(-1);
            }

            // 2. Mouse Hover sobre inimigo ou tile inimigo
            if (Camera.main != null && GridCursor.Instance != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    Unit hitUnit = controller.GetUnitFromHit(hit);
                    TileNode hitTile = controller.GetTileFromHit(hit);

                    TileNode targetTile = null;
                    if (hitTile != null && validTargets.Contains(hitTile))
                    {
                        targetTile = hitTile;
                    }
                    else if (hitUnit != null && hitUnit.CurrentTile != null && validTargets.Contains(hitUnit.CurrentTile))
                    {
                        targetTile = hitUnit.CurrentTile;
                    }

                    if (targetTile != null && GridCursor.Instance.CurrentTile != targetTile)
                    {
                        currentTargetIndex = validTargets.IndexOf(targetTile);
                        GridCursor.Instance.MoveTo(targetTile);
                    }
                }
            }

            // 3. Confirmação (Espaço ou Enter)
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (GridCursor.Instance != null && GridCursor.Instance.CurrentTile != null)
                {
                    Unit targetDefender = GridCursor.Instance.CurrentTile.CurrentUnit;
                    if (targetDefender != null)
                    {
                        ConfirmAttack(controller, targetDefender);
                    }
                }
            }

            // 4. Cancelamento (Escape)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                controller.ChangeState(new InteractionIdleState());
                controller.OpenActionUI();
            }
        }

        public void HandleClick(InputGridController controller, RaycastHit hit)
        {
            Unit defender = controller.GetUnitFromHit(hit);
            TileNode clickedTile = controller.GetTileFromHit(hit);

            if (defender == null && clickedTile != null && clickedTile.CurrentUnit != null)
            {
                defender = clickedTile.CurrentUnit;
            }

            if (defender != null && defender.CurrentTile != null && validTargets.Contains(defender.CurrentTile))
            {
                ConfirmAttack(controller, defender);
            }
        }

        private void CycleAttackTarget(int delta)
        {
            if (validTargets.Count == 0 || GridCursor.Instance == null) return;

            currentTargetIndex = (currentTargetIndex + delta + validTargets.Count) % validTargets.Count;
            GridCursor.Instance.MoveTo(validTargets[currentTargetIndex]);
        }

        private void ConfirmAttack(InputGridController controller, Unit defender)
        {
            Unit attacker = controller.SelectedUnit;
            if (attacker == null || defender == null) return;

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
        }

        private void EnsureGridCursor()
        {
            if (GridCursor.Instance == null)
            {
                GameObject cursorObj = new GameObject("GridCursor");
                cursorObj.AddComponent<GridCursor>();
            }
        }
    }
}
