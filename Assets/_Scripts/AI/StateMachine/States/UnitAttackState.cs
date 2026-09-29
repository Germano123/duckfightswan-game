using UnityEngine;
using DuckFightSwan.AI;

namespace DuckFightSwan.AI.StateMachine.States
{
    /// <summary>
    /// Estado de Combate (Attack) da Unidade.
    /// Realiza golpes automáticos contra o alvo dentro do alcance.
    /// </summary>
    public class UnitAttackState : IState
    {
        private readonly UnitAIController controller;

        public UnitAttackState(UnitAIController controller)
        {
            this.controller = controller;
        }

        public void Enter()
        {
            Debug.Log($"[UnitAttackState] {controller.gameObject.name} iniciou combate contra {controller.CurrentTarget?.name}");
        }

        public void Update()
        {
            // Valida o alvo
            if (controller.CurrentTarget == null || controller.CurrentTarget.Health.IsDead)
            {
                controller.SetTarget(null);
                controller.StateMachine.ChangeState(controller.IdleState);
                return;
            }

            // Verifica se o alvo saiu do alcance
            float distance = Vector3.Distance(controller.transform.position, controller.CurrentTarget.transform.position);
            float range = controller.SelfUnit.UnitStats.Range;

            if (distance > range)
            {
                controller.StateMachine.ChangeState(controller.MoveState);
                return;
            }

            // Ataca
            controller.AttackBehaviour.ExecuteAttack(controller.CurrentTarget);
        }

        public void Exit()
        {
        }
    }
}
