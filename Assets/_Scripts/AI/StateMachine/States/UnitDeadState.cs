using UnityEngine;
using DuckFightSwan.AI;

namespace DuckFightSwan.AI.StateMachine.States
{
    /// <summary>
    /// Estado de Morte (Dead) da Unidade.
    /// Interrompe qualquer processamento lógico ou físico de movimentação.
    /// </summary>
    public class UnitDeadState : IState
    {
        private readonly UnitAIController controller;

        public UnitDeadState(UnitAIController controller)
        {
            this.controller = controller;
        }

        public void Enter()
        {
            Debug.Log($"[UnitDeadState] {controller.gameObject.name} desativado logicamente devido à morte.");
            // Desativa todos os colisores nos filhos para evitar obstruir outras unidades
            var colliders = controller.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }
        }

        public void Update()
        {
            // Unidade morta não realiza atualizações
        }

        public void Exit()
        {
        }
    }
}
