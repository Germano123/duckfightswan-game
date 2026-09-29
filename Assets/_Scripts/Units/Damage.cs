namespace DuckFightSwan.Units
{
    /// <summary>
    /// Encapsula informações sobre uma ação de dano sofrido em batalha.
    /// Respeita o SRP ao conter apenas o modelo de dados de dano.
    /// </summary>
    public class Damage
    {
        public int Amount { get; }
        public IUnit Source { get; }

        public Damage(int amount, IUnit source)
        {
            Amount = amount;
            Source = source;
        }
    }
}
