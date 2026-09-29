using UnityEngine;
using DuckFightSwan.AI;

namespace DuckFightSwan.AI.StateMachine.States
{
    /// <summary>
    /// Estado de Aproximação (Move) da Unidade.
    /// Conduz a unidade em direção ao seu alvo de combate.
    /// </summary>
    public class UnitMoveState : IState
    {
        private readonly UnitAIController controller;

        public UnitMoveState(UnitAIController controller)
        {
            this.controller = controller;
        }

        public void Enter()
        {
            Debug.Log($"[UnitMoveState] {controller.gameObject.name} movendo-se em direção a {controller.CurrentTarget?.name}");
        }

        public void Update()
        {
            // Valida se o alvo ainda existe e está vivo
            if (controller.CurrentTarget == null || controller.CurrentTarget.Health.IsDead)
            {
                controller.SetTarget(null);
                controller.StateMachine.ChangeState(controller.IdleState);
                return;
            }

            // Move-se em direção ao alvo usando o MovementBehaviour.
            // Retorna true se estiver no alcance de ataque.
            bool inRange = controller.MovementBehaviour.GuideTowardsTarget(controller.CurrentTarget);

            if (inRange)
            {
                controller.StateMachine.ChangeState(controller.AttackState);
            }
        }

        public void Exit()
        {
        }
    }
}
