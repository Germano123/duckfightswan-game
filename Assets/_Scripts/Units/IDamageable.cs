namespace DuckFightSwan.Units
{
    /// <summary>
    /// Interface para entidades que podem receber danos em batalha.
    /// Respeita o ISP ao separar a lógica de ataque/dano da IA e locomoção.
    /// </summary>
    public interface IDamageable
    {
        int CurrentHealth { get; }
        int MaxHealth { get; }
        bool IsDead { get; }

        /// <summary>
        /// Aplica dano à saúde da entidade.
        /// </summary>
        void TakeDamage(Damage damageInfo);
    }
}
