using UnityEngine;

namespace DuckFightSwan.AI
{
    /// <summary>
    /// AttackBehaviour gerencia a cadência de golpes e emissão de danos da IA.
    /// Respeita o SRP ao lidar exclusivamente com a lógica temporal e execução de ataques.
    /// </summary>
    [RequireComponent(typeof(Units.Unit))]
    public class AttackBehaviour : MonoBehaviour
    {
        [Header("Cooldown")]
        [SerializeField] private float attackCooldown = 1.5f;
        private float lastAttackTime = 0f;

        private Units.Unit selfUnit;

        private void Start()
        {
            selfUnit = GetComponent<Units.Unit>();
        }

        /// <summary>
        /// Executa um ataque contra o alvo respeitando o tempo de recarga.
        /// </summary>
        public void ExecuteAttack(Units.Unit target)
        {
            if (selfUnit == null || target == null) return;

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                lastAttackTime = Time.time;
                Debug.Log($"[AttackBehaviour] {gameObject.name} atacou {target.name}!");

                // Cria o pacote de danos contendo o poder ofensivo atualizado da unidade
                Units.Damage damagePackage = new Units.Damage(selfUnit.UnitStats.Damage, selfUnit);
                
                // Aplica o dano diretamente à saúde da vítima
                target.Health.TakeDamage(damagePackage);
            }
        }
    }
}
