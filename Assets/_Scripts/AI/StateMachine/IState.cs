namespace DuckFightSwan.AI.StateMachine
{
    /// <summary>
    /// Interface que define o contrato básico de um estado.
    /// Respeita o Princípio de Segregação de Interface (ISP).
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// Chamado ao entrar no estado.
        /// </summary>
        void Enter();

        /// <summary>
        /// Chamado a cada frame de física/lógica (Update/FixedUpdate).
        /// </summary>
        void Update();

        /// <summary>
        /// Chamado ao sair do estado.
        /// </summary>
        void Exit();
    }
}
