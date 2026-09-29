using UnityEngine;

namespace DuckFightSwan.AI
{
    /// <summary>
    /// MovementBehaviour gerencia a aproximação física da IA em direção ao alvo de combate.
    /// Respeita o SRP ao coordenar a transição de movimentação baseada na distância do alvo.
    /// </summary>
    [RequireComponent(typeof(Units.Unit))]
    public class MovementBehaviour : MonoBehaviour
    {
        private Units.Unit selfUnit;

        private void Start()
        {
            selfUnit = GetComponent<Units.Unit>();
        }

        /// <summary>
        /// Move a unidade em direção ao alvo se estiver fora do alcance de ataque.
        /// Retorna true se a unidade atingiu o alcance e está pronta para atacar.
        /// </summary>
        public bool GuideTowardsTarget(Units.Unit target)
        {
            if (selfUnit == null || target == null) return false;

            float distance = Vector3.Distance(transform.position, target.transform.position);
            float attackRange = selfUnit.UnitStats.Range;

            if (distance > attackRange)
            {
                // Conduz a movimentação da unidade
                selfUnit.Movement.MoveTowards(target.transform.position);
                return false;
            }

            return true; // No alcance
        }
    }
}
