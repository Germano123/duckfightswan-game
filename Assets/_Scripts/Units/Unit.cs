using UnityEngine;

namespace DuckFightSwan.Units
{
    /// <summary>
    /// Componente Facade central que integra Stats, Health e Movement.
    /// Respeita o OCP e o DIP ao expor funcionalidades por meio da interface IUnit.
    /// Monitora a célula de terreno abaixo para aplicar os modificadores geográficos da Cardinal.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Movement))]
    public class Unit : MonoBehaviour, IUnit
    {
        [Header("Propriedades Base")]
        [SerializeField] private string unitName = "Unidade";
        [SerializeField] private FactionType faction = FactionType.Ducks;
        [SerializeField] private UnitClassData classData;
        [SerializeField] private Stats baseStats;

        [Header("Progresso e Ações")]
        [SerializeField] private int currentLevel = 1;
        [SerializeField] private int currentXP = 0;
        [SerializeField] private int nextLevelXP = 100;
        [SerializeField] private int actionsPerTurn = 1;
        [SerializeField] private int remainingActions = 1;

        private Health health;
        private Movement movement;
        
        // Caches para monitorar terreno anterior
        private Terrain.TerrainType currentTerrain = Terrain.TerrainType.Field;

        public string UnitName => unitName;
        public FactionType Faction => faction;
        public UnitClassData ClassData => classData;
        public Stats UnitStats => baseStats;
        public Vector3 Position => transform.position;

        public Health Health => health;
        public Movement Movement => movement;
        public Animator Animator { get; private set; }
        public Terrain.TileNode CurrentTile { get; set; }

        public int CurrentLevel => currentLevel;
        public int CurrentXP => currentXP;
        public int NextLevelXP => nextLevelXP;
        public int ActionsPerTurn => actionsPerTurn;
        public int RemainingActions => remainingActions;

        public void ConsumeAction()
        {
            remainingActions = Mathf.Max(0, remainingActions - 1);
            Debug.Log($"[Unit] {unitName} consumiu ação. Restantes: {remainingActions}/{actionsPerTurn}");
        }

        public void ResetTurnActions()
        {
            remainingActions = actionsPerTurn;
        }

        public void LoadLevelData(int level, int xp)
        {
            currentLevel = level;
            currentXP = xp;
            nextLevelXP = Mathf.RoundToInt(100 * Mathf.Pow(1.5f, currentLevel - 1));
            actionsPerTurn = 1 + (currentLevel - 1) / 2;
            remainingActions = actionsPerTurn;

            // Recalcula stats baseado no level
            if (classData != null)
            {
                int newMaxHealth = classData.MaxHealth + (currentLevel - 1) * 10;
                int newDamage = classData.Damage + (currentLevel - 1) * 2;
                int newDefense = classData.Defense + (currentLevel - 1) * 1;
                baseStats = new Stats(newMaxHealth, newDamage, newDefense, classData.Range);
                health.Initialize(baseStats);
                movement.Initialize(baseStats);
            }
        }

        public void AddXP(int amount)
        {
            if (health.IsDead) return;

            currentXP += amount;
            Debug.Log($"[Unit] {unitName} ganhou {amount} de XP. Progresso: {currentXP}/{nextLevelXP}");

            while (currentXP >= nextLevelXP)
            {
                LevelUp();
            }
        }

        private void LevelUp()
        {
            currentLevel++;
            currentXP -= nextLevelXP;
            nextLevelXP = Mathf.RoundToInt(nextLevelXP * 1.5f);

            // Aumenta atributos
            int newMaxHealth = baseStats.MaxHealth + 10;
            int newDamage = baseStats.BaseDamage + 2;
            int newDefense = baseStats.BaseDefense + 1;

            baseStats = new Stats(newMaxHealth, newDamage, newDefense, baseStats.BaseRange);
            health.Initialize(baseStats); // Isso cura e redefine a vida máxima

            actionsPerTurn = 1 + (currentLevel - 1) / 2;
            remainingActions = actionsPerTurn;

            Debug.Log($"[Unit] {unitName} subiu para o Nível {currentLevel}! Nova Vida Máxima: {newMaxHealth}, Novo Dano: {newDamage}, Ações/Turno: {actionsPerTurn}");
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<Movement>();
        }

        private void Start()
        {
            if (classData != null && baseStats == null)
            {
                InitializeClass(classData);
            }
            else if (baseStats == null)
            {
                baseStats = new Stats(100, 10, 5, 1f);
                health.Initialize(baseStats);
                movement.Initialize(baseStats);
            }

            health.OnDeath += HandleDeath;
        }

        /// <summary>
        /// Inicializa dinamicamente os atributos da unidade e instancia seu prefab gráfico ("Gfx") contendo colisores e animações.
        /// </summary>
        public void InitializeClass(UnitClassData data)
        {
            classData = data;
            unitName = classData.ClassName;
            baseStats = new Stats(classData.MaxHealth, classData.Damage, classData.Defense, classData.Range);

            // Garante que referências de componentes de Awake estejam prontas
            if (health == null) health = GetComponent<Health>();
            if (movement == null) movement = GetComponent<Movement>();

            // Remove Gfx antigo se houver para evitar duplicações
            Transform existingGfx = transform.Find("Gfx");
            if (existingGfx != null)
            {
                Destroy(existingGfx.gameObject);
            }

            // Instancia o modelo 3D da classe associada como filho
            if (classData.ModelPrefab != null)
            {
                GameObject modelInstance = Instantiate(classData.ModelPrefab, transform);
                modelInstance.name = "Gfx";
                modelInstance.transform.localPosition = Vector3.zero;
            }

            // Realiza cache do Animator e inicializa subsistemas de vida e movimento
            Animator = GetComponentInChildren<Animator>();
            health.Initialize(baseStats);
            movement.Initialize(baseStats);
        }

        /// <summary>
        /// Define a facção desta unidade.
        /// </summary>
        public void SetFaction(FactionType faction)
        {
            this.faction = faction;
        }

        private Vector3? moveTargetPosition;

        public void SetMoveTarget(Vector3 target)
        {
            moveTargetPosition = target;
        }

        private void Update()
        {
            if (health.IsDead) return;

            if (moveTargetPosition.HasValue)
            {
                movement.MoveTowards(moveTargetPosition.Value);
                if (Vector3.Distance(transform.position, moveTargetPosition.Value) < 0.2f)
                {
                    moveTargetPosition = null;
                }
            }

            // Monitora o relevo e bioma abaixo do colisor da unidade
            CheckTerrainInfluence();
        }

        /// <summary>
        /// Realiza a varredura do solo para identificar biomas e aplicar regras da Cardinal.
        /// </summary>
        private void CheckTerrainInfluence()
        {
            if (CurrentTile == null) return;

            Terrain.TerrainType detectedTerrain = CurrentTile.Type;

            if (detectedTerrain != currentTerrain)
            {
                currentTerrain = detectedTerrain;
                ApplyTerrainModifiers(detectedTerrain);
            }
        }

        /// <summary>
        /// Traduz os modificadores dos biomas do GDD diretamente nos atributos da unidade.
        /// </summary>
        private void ApplyTerrainModifiers(Terrain.TerrainType terrain)
        {
            baseStats.ResetModifiers();

            float rangeMod = 1.0f;
            float damageMod = 1.0f;

            switch (terrain)
            {
                case Terrain.TerrainType.Forest:
                    // Floresta: +20% alcance para arqueiros
                    if (classData != null && classData.ClassType == UnitClassType.Archer)
                    {
                        rangeMod = 1.20f;
                    }
                    break;
                case Terrain.TerrainType.Mountain:
                    // Montanha: +15% dano para arqueiros
                    if (classData != null && classData.ClassType == UnitClassType.Archer)
                    {
                        damageMod = 1.15f;
                    }
                    break;
            }

            baseStats.SetModifiers(rangeMod, damageMod);
            Debug.Log($"[Unit] {gameObject.name} entrou no terreno {terrain}. Novos Modificadores -> Alcance: {rangeMod}, Dano: {damageMod}");
        }

        private void HandleDeath(GameObject killed)
        {
            if (Core.MatchManager.Instance != null)
            {
                Core.MatchManager.Instance.UnregisterUnit(gameObject, faction);
            }
            Destroy(gameObject, 2f); // Aguarda animação de morte antes de expirar
        }

        private void OnDrawGizmosSelected()
        {
            if (baseStats == null) return;

            // Desenha o Attack Range (Vermelho)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, baseStats.Range);
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnDeath -= HandleDeath;
            }
        }
    }
}
