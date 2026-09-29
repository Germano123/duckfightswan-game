namespace DuckFightSwan.Units
{
    /// <summary>
    /// Interface que representa uma unidade de combate no domínio do jogo.
    /// Respeita o Princípio de Segregação de Interface (ISP).
    /// </summary>
    public interface IUnit
    {
        string UnitName { get; }
        FactionType Faction { get; }
        
        /// <summary>
        /// Acesso às estatísticas atuais da unidade.
        /// </summary>
        Stats UnitStats { get; }

        /// <summary>
        /// Retorna a coordenada tridimensional atual no espaço da Unity.
        /// </summary>
        UnityEngine.Vector3 Position { get; }
    }
}
