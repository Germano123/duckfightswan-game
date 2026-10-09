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
        [SerializeField] private float baseCritChance = 0.20f;
        [SerializeField] private float critMultiplier = 1.5f;

        public float BaseCritChance => baseCritChance;
        public float CritMultiplier => critMultiplier;

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
        /// Mantido para compatibilidade com ICombatCalculator.
        /// </summary>
        public int CalculateDamage(int rawDamage, int targetDefense)
        {
            int calculated = rawDamage - targetDefense;
            return Mathf.Max(calculated, minDamageAmount);
        }

        /// <summary>
        /// Estrutura de retorno com dano arbitrado e status de acerto crítico.
        /// </summary>
        public struct DamageResult
        {
            public int Damage;
            public bool IsCritical;
        }

        /// <summary>
        /// Calcula o dano final considerando defesa e probabilidade de ataque crítico.
        /// </summary>
        public DamageResult CalculateDamageResult(int rawDamage, int targetDefense, bool? forceCrit = null)
        {
            bool isCrit = forceCrit.HasValue ? forceCrit.Value : (Random.value < baseCritChance);
            int baseDamage = CalculateDamage(rawDamage, targetDefense);
            int finalDamage = isCrit ? Mathf.RoundToInt(baseDamage * critMultiplier) : baseDamage;

            return new DamageResult
            {
                Damage = Mathf.Max(finalDamage, minDamageAmount),
                IsCritical = isCrit
            };
        }
    }
}
