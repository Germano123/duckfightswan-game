using UnityEngine;

namespace DuckFightSwan.Units
{
    /// <summary>
    /// Stats armazena as estatísticas básicas de combate de uma unidade.
    /// Respeita o SRP ao lidar apenas com a manutenção dos valores numéricos de atributos.
    /// </summary>
    [System.Serializable]
    public class Stats
    {
        [SerializeField] private int maxHealth;
        [SerializeField] private int damage;
        [SerializeField] private int defense;
        [SerializeField] private float range;
        [SerializeField] private int movePoints = 3;
        [SerializeField] private int maxClimbHeight = 1;

        // Modificadores temporários por influência do terreno
        private float rangeModifier = 1.0f;
        private float damageModifier = 1.0f;

        public int MaxHealth => maxHealth;
        public int BaseDamage => damage;
        public int BaseDefense => defense;
        public float BaseRange => range;
        public int MovePoints => movePoints;
        public int MaxClimbHeight => maxClimbHeight;

        // Atributos dinâmicos recalculados com modificadores ambientais
        public int Damage => Mathf.RoundToInt(damage * damageModifier);
        public int Defense => defense;
        public float Range => range * rangeModifier;

        public Stats(int maxHealth, int damage, int defense, float range, int movePoints = 3, int maxClimbHeight = 1)
        {
            this.maxHealth = maxHealth;
            this.damage = damage;
            this.defense = defense;
            this.range = range;
            this.movePoints = movePoints;
            this.maxClimbHeight = maxClimbHeight;
        }

        public void SetModifiers(float rangeMod, float damageMod)
        {
            rangeModifier = rangeMod;
            damageModifier = damageMod;
        }

        public void ResetModifiers()
        {
            rangeModifier = 1.0f;
            damageModifier = 1.0f;
        }
    }
}
