using UnityEngine;

namespace DuckFightSwan.Combat
{
    /// <summary>
    /// CombatManager é o ponto único de arbitragem matemática dos ataques.
    /// Respeita o SRP ao conter apenas regras e fórmulas matemáticas de combate.
    /// Implementa ICombatCalculator para respeitar o OCP.
    /// </summary>
    public class CombatManager : MonoBehaviour, ICombatCalculator
    {
        public static CombatManager Instance { get; private set; }

        [Header("Configurações de Balanceamento")]
        [SerializeField] private int minDamageAmount = 1;

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

        /// <summary>
        /// Aplica a fórmula matemática do GDD: Dano Final = Ataque - Defesa (mínimo 1).
        /// </summary>
        public int CalculateDamage(int rawDamage, int targetDefense)
        {
            int calculated = rawDamage - targetDefense;
            return Mathf.Max(calculated, minDamageAmount);
        }
    }
}
