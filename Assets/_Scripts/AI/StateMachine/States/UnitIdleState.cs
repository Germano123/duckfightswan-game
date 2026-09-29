using UnityEngine;
using DuckFightSwan.AI;

namespace DuckFightSwan.AI.StateMachine.States
{
    /// <summary>
    /// Estado de Espera (Idle) da Unidade.
    /// Fica parado e busca por alvos no campo se a simulação estiver em andamento.
    /// </summary>
    public class UnitIdleState : IState
    {
        private readonly UnitAIController controller;

        public UnitIdleState(UnitAIController controller)
        {
            this.controller = controller;
        }

        public void Enter()
        {
            // Para animações de movimento e garante velocidade zerada (opcional visual)
            Debug.Log($"[UnitIdleState] {controller.gameObject.name} entrou em Idle.");
        }

        public void Update()
        {
            // Só executa se a simulação de combate do MatchManager estiver ativa
            if (Core.MatchManager.Instance == null || !Core.MatchManager.Instance.IsSimulationActive) return;

            // Busca inimigo mais próximo na cena inteira (busca global para o protótipo básico)
            Units.Unit target = controller.TargetFinder.FindNearestTarget();

            if (target != null)
            {
                controller.SetTarget(target);
                controller.StateMachine.ChangeState(controller.MoveState);
            }
        }

        public void Exit()
        {
        }
    }
}
