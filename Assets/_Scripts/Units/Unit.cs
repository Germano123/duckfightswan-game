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

        [Header("Progresso, Patentes e Ações")]
        [SerializeField] private int currentLevel = 1;
        [SerializeField] private MilitaryRank currentRank = MilitaryRank.Recruit;
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
        public MilitaryRank CurrentRank => currentRank;
        public Stats UnitStats => baseStats;
        public Vector3 Position => transform.position;

        public Health Health => health;
        public Movement Movement => movement;
        public Animator Animator { get; private set; }
        private Terrain.TileNode currentTile;
        public Terrain.TileNode CurrentTile
        {
            get
            {
                if (currentTile == null && Terrain.GridManager.Instance != null)
                {
                    currentTile = Terrain.GridManager.Instance.GetNodeAtWorldPosition(transform.position);
                    if (currentTile != null && currentTile.CurrentUnit == null)
                    {
                        currentTile.CurrentUnit = this;
                    }
                }
                return currentTile;
            }
            set => currentTile = value;
        }

        public int CurrentLevel => currentLevel;
        public int CurrentXP => currentXP;
        public int NextLevelXP => nextLevelXP;
        public int ActionsPerTurn => actionsPerTurn;
        public int RemainingActions => remainingActions;
        public int MovePoints => (baseStats != null && baseStats.MovePoints > 0) ? baseStats.MovePoints : (classData != null && classData.MovePoints > 0 ? classData.MovePoints : 3);
        public int MaxClimbHeight => (baseStats != null && baseStats.MaxClimbHeight > 0) ? baseStats.MaxClimbHeight : (classData != null && classData.MaxClimbHeight > 0 ? classData.MaxClimbHeight : 1);

        public bool CanClimb(int heightDifference) => heightDifference <= MaxClimbHeight;

        /// <summary>
        /// Contador de turnos decorridos desde o último ataque (para controle de cadência da IA).
        /// </summary>
        public int TurnsSinceLastAttack { get; set; } = 1;

        public void ConsumeAction()
        {
            remainingActions = Mathf.Max(0, remainingActions - 1);
            Debug.Log($"[Unit] {unitName} consumiu ação. Restantes: {remainingActions}/{actionsPerTurn}");
        }

        public void ResetTurnActions()
        {
            remainingActions = actionsPerTurn;
        }

        public void LoadLevelData(int level, int xp, int rank = 1)
        {
            currentLevel = Mathf.Max(1, level);
            currentXP = Mathf.Max(0, xp);
            currentRank = System.Enum.IsDefined(typeof(MilitaryRank), rank) ? (MilitaryRank)rank : MilitaryRank.Recruit;
            nextLevelXP = Mathf.RoundToInt(100 * Mathf.Pow(1.5f, currentLevel - 1));
            actionsPerTurn = 1 + (currentLevel - 1) / 2;
            remainingActions = actionsPerTurn;

            RecalculateStats();
        }

        /// <summary>
        /// Recalcula os atributos dinâmicos combinando a classe base, a progressão de nível e a camada da patente militar.
        /// Subir de patente fornece um salto expressivo de Vida, Dano, Defesa e Pontos de Movimento.
        /// </summary>
        public void RecalculateStats()
        {
            if (classData == null) return;

            // Bônus contínuo de Nível
            int levelBonusHp = (currentLevel - 1) * 12;
            int levelBonusDmg = (currentLevel - 1) * 3;
            int levelBonusDef = (currentLevel - 1) * 1;

            // Bônus expressivo por Camada de Patente Militar (Rank Tier)
            int rankBonusHp = 0;
            int rankBonusDmg = 0;
            int rankBonusDef = 0;
            int rankBonusMov = 0;
            int rankBonusClimb = 0;

            switch (currentRank)
            {
                case MilitaryRank.Veteran:
                    rankBonusHp = 35;
                    rankBonusDmg = 8;
                    rankBonusDef = 3;
                    rankBonusMov = 1;
                    break;
                case MilitaryRank.Elite:
                    rankBonusHp = 60;
                    rankBonusDmg = 14;
                    rankBonusDef = 6;
                    rankBonusMov = 1;
                    rankBonusClimb = 1;
                    break;
                case MilitaryRank.Commander:
                    rankBonusHp = 95;
                    rankBonusDmg = 22;
                    rankBonusDef = 9;
                    rankBonusMov = 2;
                    rankBonusClimb = 1;
                    break;
            }

            int totalHp = classData.MaxHealth + levelBonusHp + rankBonusHp;
            int totalDmg = classData.Damage + levelBonusDmg + rankBonusDmg;
            int totalDef = classData.Defense + levelBonusDef + rankBonusDef;
            int totalMov = classData.MovePoints + rankBonusMov;
            int totalClimb = classData.MaxClimbHeight + rankBonusClimb;

            baseStats = new Stats(totalHp, totalDmg, totalDef, classData.Range, totalMov, totalClimb);

            if (health == null) health = GetComponent<Health>();
            if (movement == null) movement = GetComponent<Movement>();

            if (health != null) health.Initialize(baseStats);
            if (movement != null) movement.Initialize(baseStats);
        }

        /// <summary>
        /// Promove a unidade para uma patente militar superior, aumentando expressivamente seus atributos e restaurando sua vida.
        /// </summary>
        public void PromoteRank(MilitaryRank newRank)
        {
            if (newRank <= currentRank) return;

            currentRank = newRank;
            RecalculateStats();
            if (health != null)
            {
                health.SetCurrentHealth(baseStats.MaxHealth); // Restaura totalmente para a nova jornada
            }

            Debug.Log($"[Unit] 🎖️ {unitName} foi promovido para {currentRank}! Nova Vida Máx: {baseStats.MaxHealth}, Novo Dano: {baseStats.BaseDamage}, Movimento: {baseStats.MovePoints}");
        }

        public string GetRankDisplayName()
        {
            string rankPrefix = "";
            switch (currentRank)
            {
                case MilitaryRank.Veteran: rankPrefix = "★ "; break;
                case MilitaryRank.Elite: rankPrefix = "★★ "; break;
                case MilitaryRank.Commander: rankPrefix = "👑 "; break;
            }
            return $"{rankPrefix}Nv.{currentLevel}";
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

            // Verifica promoção natural por nível
            if (currentLevel >= 7 && currentRank < MilitaryRank.Commander)
            {
                currentRank = MilitaryRank.Commander;
            }
            else if (currentLevel >= 5 && currentRank < MilitaryRank.Elite)
            {
                currentRank = MilitaryRank.Elite;
            }
            else if (currentLevel >= 3 && currentRank < MilitaryRank.Veteran)
            {
                currentRank = MilitaryRank.Veteran;
            }

            RecalculateStats();

            actionsPerTurn = 1 + (currentLevel - 1) / 2;
            remainingActions = actionsPerTurn;

            Debug.Log($"[Unit] {unitName} subiu para o Nível {currentLevel} ({currentRank})! Nova Vida Máxima: {baseStats.MaxHealth}, Novo Dano: {baseStats.BaseDamage}, Ações/Turno: {actionsPerTurn}");
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<Movement>();
        }

        private void Start()
        {
            if (classData != null && (baseStats == null || baseStats.MovePoints <= 0))
            {
                InitializeClass(classData);
            }
            else if (baseStats == null || baseStats.MovePoints <= 0)
            {
                baseStats = new Stats(100, 10, 5, 1f, 3, 1);
                health.Initialize(baseStats);
                movement.Initialize(baseStats);
            }

            if (GetComponent<FloatingHealthBar>() == null)
            {
                gameObject.AddComponent<FloatingHealthBar>();
            }

            health.OnDeath += HandleDeath;
        }

        /// <summary>
        /// Inicializa dinamicamente os atributos da unidade e instancia seu prefab gráfico ("Gfx") contendo colisores e animações.
        /// </summary>
        public void InitializeClass(UnitClassData data)
        {
            if (data == null) return;

            classData = data;
            unitName = classData.ClassName;
            baseStats = new Stats(classData.MaxHealth, classData.Damage, classData.Defense, classData.Range, classData.MovePoints, classData.MaxClimbHeight);

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

                // Remove componentes conflitantes que possam ter vindo do modelo gráfico legado
                foreach (var rogueUnit in modelInstance.GetComponentsInChildren<Unit>(true))
                {
                    if (rogueUnit != this) Destroy(rogueUnit);
                }
                foreach (var rogueHealth in modelInstance.GetComponentsInChildren<Health>(true))
                {
                    if (rogueHealth != health) Destroy(rogueHealth);
                }
                foreach (var rogueMovement in modelInstance.GetComponentsInChildren<Movement>(true))
                {
                    if (rogueMovement != movement) Destroy(rogueMovement);
                }
            }

            // Realiza cache do Animator e inicializa subsistemas de vida e movimento
            Animator = GetComponentInChildren<Animator>();
            health.Initialize(baseStats);
            movement.Initialize(baseStats);
        }

        /// <summary>
        /// Sobrescreve os atributos de combate e vida da unidade para ondas ou tutoriais customizados.
        /// </summary>
        public void OverrideStats(int maxHealth, int damage, int defense = -1, float range = -1, int movePoints = -1)
        {
            if (baseStats == null) return;
            int def = defense >= 0 ? defense : baseStats.BaseDefense;
            float rng = range >= 0 ? range : baseStats.BaseRange;
            int mp = movePoints >= 0 ? movePoints : baseStats.MovePoints;
            int climb = baseStats.MaxClimbHeight;

            baseStats = new Stats(maxHealth, damage, def, rng, mp, climb);
            if (health == null) health = GetComponent<Health>();
            if (movement == null) movement = GetComponent<Movement>();
            if (health != null) health.Initialize(baseStats);
            if (movement != null) movement.Initialize(baseStats);
        }

        /// <summary>
        /// Define a facção desta unidade.
        /// </summary>
        public void SetFaction(FactionType faction)
        {
            this.faction = faction;
        }

        private readonly System.Collections.Generic.Queue<Vector3> movePathQueue = new System.Collections.Generic.Queue<Vector3>();

        public bool IsMoving => movePathQueue.Count > 0;

        /// <summary>
        /// Define uma sequência de pontos de relevo (waypoints) que a unidade percorrerá suavemente.
        /// </summary>
        public void SetMovePath(System.Collections.Generic.List<Vector3> waypoints)
        {
            movePathQueue.Clear();
            if (waypoints != null)
            {
                foreach (var pt in waypoints)
                {
                    movePathQueue.Enqueue(pt);
                }
            }
        }

        /// <summary>
        /// Move a unidade para um ponto específico de destino (único waypoint).
        /// </summary>
        public void SetMoveTarget(Vector3 target)
        {
            movePathQueue.Clear();
            movePathQueue.Enqueue(target);
        }

        private void Update()
        {
            if (health.IsDead) return;

            if (movePathQueue.Count > 0)
            {
                Vector3 currentTarget = movePathQueue.Peek();
                movement.MoveTowards(currentTarget);
                if (Vector3.Distance(transform.position, currentTarget) <= 0.08f)
                {
                    transform.position = currentTarget;
                    movePathQueue.Dequeue();
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
                case Terrain.TerrainType.Mud:
                    // Lama: instabilidade na base (-10% dano físico)
                    damageMod = 0.90f;
                    break;
                case Terrain.TerrainType.River:
                    // Rio: arrasto de correnteza (-15% dano em combate aquático)
                    damageMod = 0.85f;
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
