using UnityEngine;

namespace DuckFightSwan.Units
{
    /// <summary>
    /// Health gerencia a saúde e eventos de morte da unidade.
    /// Respeita o SRP ao conter apenas lógica de recebimento e controle de vida.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private int currentHealth;
        private Stats unitStats;
        private bool isDead = false;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => unitStats != null ? unitStats.MaxHealth : 100;
        public bool IsDead => isDead;

        public delegate void DeathHandler(GameObject killedObject);
        public event DeathHandler OnDeath;

        public delegate void HealthChangedHandler(int current, int max);
        public event HealthChangedHandler OnHealthChanged;

        public void Initialize(Stats stats)
        {
            unitStats = stats;
            currentHealth = stats.MaxHealth;
        }

        public void SetCurrentHealth(int health)
        {
            currentHealth = Mathf.Clamp(health, 0, MaxHealth);
            if (currentHealth <= 0)
            {
                Die();
            }
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        public void TakeDamage(Damage damageInfo)
        {
            if (isDead) return;

            // Invoca o CombatManager para resolver a subtração de dano - defesa
            int actualDamage = Combat.CombatManager.Instance.CalculateDamage(damageInfo.Amount, unitStats.Defense);
            currentHealth = Mathf.Max(currentHealth - actualDamage, 0);

            Debug.Log($"[Health] {gameObject.name} sofreu {actualDamage} de dano (Dano original: {damageInfo.Amount}). Vida restante: {currentHealth}");

            OnHealthChanged?.Invoke(currentHealth, MaxHealth);

            Unit attackerUnit = damageInfo.Source as Unit;
            Unit defenderUnit = GetComponent<Unit>();

            if (currentHealth <= 0)
            {
                if (attackerUnit != null)
                {
                    attackerUnit.AddXP(50); // Derrotar inimigo dá muito XP
                }
                Die();
            }
            else
            {
                // Participar da batalha dá um pouco de XP
                if (attackerUnit != null)
                {
                    attackerUnit.AddXP(10);
                }
                if (defenderUnit != null)
                {
                    defenderUnit.AddXP(10);
                }
            }
        }

        private void Die()
        {
            isDead = true;
            Debug.Log($"[Health] {gameObject.name} foi eliminado.");
            OnDeath?.Invoke(gameObject);
        }
    }
}
