namespace DuckFightSwan.Units
{
    /// <summary>
    /// Classes de combate disponíveis para as unidades.
    /// </summary>
    public enum UnitClassType
    {
        Warrior,
        Archer,
        Squire
    }

    /// <summary>
    /// Facções / Equipes participantes das simulações.
    /// </summary>
    public enum FactionType
    {
        Ducks,
        Swans
    }

    /// <summary>
    /// Hierarquia de patentes militares das tropas entre épocas.
    /// Subir de patente aumenta substancialmente atributos como vida, dano, defesa e movimento.
    /// </summary>
    public enum MilitaryRank
    {
        Recruit = 1,   // Soldado Raso / Recruta (Níveis 1-2)
        Veteran = 2,   // Veterano de Batalha (Níveis 3-4)
        Elite = 3,     // Campeão de Elite (Níveis 5-6)
        Commander = 4  // Comandante Supremo / Lenda (Níveis 7+)
    }
}
