using UnityEngine;
using DuckFightSwan.AI.StateMachine;
using DuckFightSwan.AI.StateMachine.States;

namespace DuckFightSwan.AI
{
    /// <summary>
    /// Controlador central da FSM de Inteligência Artificial de cada unidade.
    /// Respeita os princípios SOLID ao estender comportamentos genéricos através de composição.
    /// </summary>
    [RequireComponent(typeof(TargetFinder))]
    [RequireComponent(typeof(MovementBehaviour))]
    [RequireComponent(typeof(AttackBehaviour))]
    [RequireComponent(typeof(Units.Unit))]
    public class UnitAIController : MonoBehaviour
    {
        // Cache de Componentes Proprietários
        public Units.Unit SelfUnit { get; private set; }
        public TargetFinder TargetFinder { get; private set; }
        public MovementBehaviour MovementBehaviour { get; private set; }
        public AttackBehaviour AttackBehaviour { get; private set; }

        // FSM do Comportamento
        public StateMachine.StateMachine StateMachine { get; private set; }
        public IState IdleState { get; private set; }
        public IState MoveState { get; private set; }
        public IState AttackState { get; private set; }
        public IState DeadState { get; private set; }

        // Alvo corrente de combate
        public Units.Unit CurrentTarget { get; private set; }

        private void Awake()
        {
            SelfUnit = GetComponent<Units.Unit>();
            TargetFinder = GetComponent<TargetFinder>();
            MovementBehaviour = GetComponent<MovementBehaviour>();
            AttackBehaviour = GetComponent<AttackBehaviour>();

            // Instancia a Máquina de Estados e os Estados Concretos
            StateMachine = new StateMachine.StateMachine();
            IdleState = new UnitIdleState(this);
            MoveState = new UnitMoveState(this);
            AttackState = new UnitAttackState(this);
            DeadState = new UnitDeadState(this);
        }

        private void Start()
        {
            // Inicializa a FSM no estado de espera
            StateMachine.Initialize(IdleState);
            
            // Registra-se para transicionar para morte caso a vida zere (DIP/Observer)
            SelfUnit.Health.OnDeath += HandleDeath;
        }

        private void Update()
        {
            // Abstraído: a inteligência e tomada de decisão agora ocorrem de forma sequencial
            // por turnos gerenciados de forma síncrona pelo MatchManager.
        }

        /// <summary>
        /// Define ou altera o alvo focado da unidade.
        /// </summary>
        public void SetTarget(Units.Unit target)
        {
            CurrentTarget = target;
        }

        private void HandleDeath(GameObject killed)
        {
            StateMachine.ChangeState(DeadState);
        }

        private void OnDestroy()
        {
            if (SelfUnit != null && SelfUnit.Health != null)
            {
                SelfUnit.Health.OnDeath -= HandleDeath;
            }
        }
    }
}
