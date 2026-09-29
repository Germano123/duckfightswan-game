namespace DuckFightSwan.Combat
{
    /// <summary>
    /// Interface que define a estratégia para cálculo matemático de dano em combate.
    /// Respeita o Princípio do Aberto/Fechado (OCP) ao permitir novos tipos
    /// de cálculo de dano sem quebrar o CombatManager principal.
    /// </summary>
    public interface ICombatCalculator
    {
        /// <summary>
        /// Calcula o dano final subtraindo a defesa do poder ofensivo.
        /// </summary>
        int CalculateDamage(int rawDamage, int targetDefense);
    }
}
