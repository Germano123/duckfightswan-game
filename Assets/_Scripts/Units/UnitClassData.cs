using UnityEngine;

namespace DuckFightSwan.Units
{
    /// <summary>
    /// ScriptableObject que define os atributos base de uma classe de unidade.
    /// Respeita o OCP ao permitir a criação de novas classes diretamente via Unity Editor
    /// sem a necessidade de reescrever ou alterar códigos de scripts existentes.
    /// </summary>
    [CreateAssetMenu(fileName = "NewUnitClass", menuName = "DuckFightSwan/Unit Class")]
    public class UnitClassData : ScriptableObject
    {
        [Header("Identificação")]
        [SerializeField] private UnitClassType classType = UnitClassType.Warrior;
        [SerializeField] private GameObject modelPrefab;

        [Header("Atributos de Vida e Defesa")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int defense = 5;

        [Header("Atributos de Ataque e Combate")]
        [SerializeField] private int damage = 10;
        [SerializeField] private float range = 1.0f;

        [Header("Atributos de Deslocamento e Relevo")]
        [SerializeField] private int movePoints = 3;
        [SerializeField] private int maxClimbHeight = 1;

        public UnitClassType ClassType => classType;
        public string ClassName => classType.ToString();
        public GameObject ModelPrefab => modelPrefab;
        public int MaxHealth => maxHealth;
        public int Defense => defense;
        public int Damage => damage;
        public float Range => range;
        public int MovePoints => movePoints;
        public int MaxClimbHeight => maxClimbHeight;
    }
}
