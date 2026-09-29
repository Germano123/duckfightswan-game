using UnityEngine;

namespace DuckFightSwan.AI.StateMachine
{
    /// <summary>
    /// Gerenciador genérico de estados (FSM).
    /// Respeita o SRP ao lidar exclusivamente com a mudança de estado e execução dos callbacks.
    /// </summary>
    public class StateMachine
    {
        public IState CurrentState { get; private set; }

        /// <summary>
        /// Inicializa a máquina de estados definindo o estado inicial.
        /// </summary>
        public void Initialize(IState initialState)
        {
            CurrentState = initialState;
            CurrentState.Enter();
        }

        /// <summary>
        /// Transiciona para um novo estado executando Exit() do anterior e Enter() do novo.
        /// </summary>
        public void ChangeState(IState newState)
        {
            if (newState == null) return;

            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }

        /// <summary>
        /// Executa a rotina de atualização do estado ativo.
        /// </summary>
        public void Update()
        {
            CurrentState?.Update();
        }
    }
}
