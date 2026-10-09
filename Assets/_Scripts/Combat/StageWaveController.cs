using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DuckFightSwan.Core;
using DuckFightSwan.Terrain;
using DuckFightSwan.Units;

namespace DuckFightSwan.Combat
{
    /// <summary>
    /// Gerencia as etapas táticas e ondas de reforços da Fase 1.
    /// Respeita o SRP ao controlar a progressão de ondas, reforços e regras especiais de esquadrão.
    /// </summary>
    public class StageWaveController : MonoBehaviour
    {
        private static StageWaveController _instance;
        public static StageWaveController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<StageWaveController>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("StageWaveController");
                        _instance = go.AddComponent<StageWaveController>();
                    }
                }
                return _instance;
            }
        }

        [Header("Configuração de Etapas")]
        [SerializeField] private int currentStage = 1;
        [SerializeField] private int maxStages = 3;

        // Controle interno de transição
        private bool isTransitioning = false;
        private string activeBannerText = "";
        private float bannerTimer = 0f;

        public int CurrentStage => currentStage;
        public int MaxStages => maxStages;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            ResetStages();
        }

        /// <summary>
        /// Reinicia a contagem de etapas para a etapa 1.
        /// </summary>
        public void ResetStages()
        {
            currentStage = 1;
            isTransitioning = false;
            activeBannerText = "";
            bannerTimer = 0f;
        }

        /// <summary>
        /// Verifica se há um próximo confronto a ser executado na época/fase atual da demo.
        /// </summary>
        public bool HasNextStage()
        {
            if (MatchManager.Instance == null) return false;
            if (MatchManager.Instance.CurrentPhase > 3) return false; // Fases procedurais pós-demo não usam etapas múltiplas
            return currentStage < maxStages;
        }

        /// <summary>
        /// Avança para o próximo confronto da época atual.
        /// </summary>
        public void AdvanceToNextStage()
        {
            if (isTransitioning) return;
            StartCoroutine(RoutineAdvanceToNextStage());
        }

        private IEnumerator RoutineAdvanceToNextStage()
        {
            isTransitioning = true;
            currentStage++;

            int phase = MatchManager.Instance != null ? MatchManager.Instance.CurrentPhase : 1;
            Debug.Log($"[StageWaveController] Avançando para o Confronto {currentStage} de {maxStages} na Fase {phase}!");

            if (phase == 1)
            {
                if (currentStage == 2) yield return StartCoroutine(RoutinePhase1Stage2Embush());
                else if (currentStage == 3) yield return StartCoroutine(RoutinePhase1Stage3PincerAndReinforcements());
            }
            else if (phase == 2)
            {
                if (currentStage == 2) yield return StartCoroutine(RoutinePhase2Stage2PlateauArchers());
                else if (currentStage == 3) yield return StartCoroutine(RoutinePhase2Stage3BattalionClash());
            }
            else if (phase == 3)
            {
                if (currentStage == 2) yield return StartCoroutine(RoutinePhase3Stage2CliffAmbush());
                else if (currentStage == 3) yield return StartCoroutine(RoutinePhase3Stage3PraetorianGuard());
            }

            // Atualiza HUD e contagem de tropas
            if (UI.HUD.Instance != null)
            {
                UI.HUD.Instance.UpdateHUDValues();
                if (MatchManager.Instance != null)
                {
                    UI.HUD.Instance.UpdateTeamCounts(MatchManager.Instance.ActiveDucksCount, MatchManager.Instance.ActiveSwansCount);
                }
            }

            isTransitioning = false;
        }

        // =========================================================================
        // FASE 1 (ANO 1 – MARGENS DO RIO) | GRID 7x7
        // =========================================================================

        private IEnumerator RoutinePhase1Stage2Embush()
        {
            ShowBanner("⚔️ EMBOSCADA NOS JUNCOS! ⚔️\nAsa-de-Ferro: 'Gansos emergindo ao Norte e ao Sul! Mantenham as fileiras!'\nCapitão Ganso: 'Cerquem o rio! Nenhum pato escapará!'", 4.0f);

            yield return new WaitForSeconds(2.0f);

            if (SpawnManager.Instance != null)
            {
                // 2 Gansos do Norte (X=3, 4, Z=6)
                Unit n1 = SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 3, z = 6, currentHealth = 40, level = 1 });
                if (n1 != null) n1.OverrideStats(40, 8, 2);

                Unit n2 = SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 4, z = 6, currentHealth = 35, level = 1 });
                if (n2 != null) n2.OverrideStats(35, 7, 1, 3f);

                // 2 Gansos do Sul (X=3, 4, Z=0)
                Unit s1 = SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 3, z = 0, currentHealth = 40, level = 1 });
                if (s1 != null) s1.OverrideStats(40, 8, 2);

                Unit s2 = SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = 4, z = 0, currentHealth = 35, level = 1 });
                if (s2 != null) s2.OverrideStats(35, 7, 2);

                Debug.Log("[StageWaveController] Fase 1 - Confronto 2: 4 Gansos de emboscada spawnados!");
            }

            if (CameraController.Instance != null)
            {
                CameraController.Instance.FocusOnPosition(new Vector3(3.5f, 1f, 5.5f));
                yield return new WaitForSeconds(1.5f);
                CameraController.Instance.ResetToOverview();
            }
        }

        private IEnumerator RoutinePhase1Stage3PincerAndReinforcements()
        {
            // Regra do Teto de 4 Patos (Mecânica do Mensageiro)
            if (MatchManager.Instance != null)
            {
                List<GameObject> ducks = new List<GameObject>(MatchManager.Instance.ActiveDucks);
                if (ducks.Count >= 3)
                {
                    GameObject messengerDuck = ducks.Find(d => d.GetComponent<Unit>()?.ClassData?.ClassType == UnitClassType.Squire) ?? ducks[ducks.Count - 1];
                    if (messengerDuck != null)
                    {
                        Unit messengerUnit = messengerDuck.GetComponent<Unit>();
                        if (messengerUnit != null && messengerUnit.CurrentTile != null)
                        {
                            messengerUnit.CurrentTile.CurrentUnit = null;
                        }

                        ShowBanner("🏃 Batedor: 'Vou buscar reforços na colina! Segurem as margens!'", 3f);
                        MatchManager.Instance.UnregisterUnit(messengerDuck, FactionType.Ducks);
                        Destroy(messengerDuck);

                        yield return new WaitForSeconds(1.8f);
                    }
                }
            }

            ShowBanner("⚠️ ATAQUE EM PINÇA! ⚠️\nAsa-de-Ferro: 'Resistam ao cerco! Dois guerreiros de cobertura chegam pela retaguarda!'", 4.2f);

            yield return new WaitForSeconds(1.5f);

            if (SpawnManager.Instance != null)
            {
                // Flanco Leste - 3 Gansos (X=6, Z=2, 3, 4)
                Unit e1 = SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 6, z = 2, currentHealth = 45, level = 1 });
                if (e1 != null) e1.OverrideStats(45, 9, 2);

                Unit e2 = SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = 6, z = 3, currentHealth = 40, level = 1 });
                if (e2 != null) e2.OverrideStats(40, 8, 2);

                Unit e3 = SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 6, z = 4, currentHealth = 35, level = 1 });
                if (e3 != null) e3.OverrideStats(35, 8, 1, 3f);

                // Flanco Norte - 2 Gansos (X=2, Z=6 e X=3, Z=5)
                Unit nw1 = SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 2, z = 6, currentHealth = 45, level = 1 });
                if (nw1 != null) nw1.OverrideStats(45, 9, 2);

                Unit nw2 = SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 3, z = 5, currentHealth = 35, level = 1 });
                if (nw2 != null) nw2.OverrideStats(35, 8, 1, 3f);

                // Reforços Aliados de Cobertura (2 Patos em X=0, Z=4, 5)
                Unit d1 = SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Ducks, className = "Warrior", x = 0, z = 4, currentHealth = 100, level = 1 });
                Unit d2 = SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Ducks, className = "Archer", x = 0, z = 5, currentHealth = 100, level = 1 });

                Debug.Log("[StageWaveController] Fase 1 - Confronto 3: Pinça de 5 Gansos e 2 Patos de Cobertura spawnados!");
            }

            if (CameraController.Instance != null)
            {
                CameraController.Instance.FocusOnPosition(new Vector3(0.5f, 1f, 4.5f));
                yield return new WaitForSeconds(1.8f);
                CameraController.Instance.ResetToOverview();
            }
        }

        // =========================================================================
        // FASE 2 (ANO 15 – COLINAS DE OUTONO) | GRID 10x10
        // =========================================================================

        private IEnumerator RoutinePhase2Stage2PlateauArchers()
        {
            ShowBanner("🏹 ARQUEIROS NO PLATÔ! 🏹\nAsa-de-Ferro: 'A patrulha recuou, mas arqueiros dominam o cume!'\nGeneral Penabranca: 'Disparem do alto! Nenhuma pena deve sobrar!'", 4.5f);

            yield return new WaitForSeconds(2.0f);

            if (SpawnManager.Instance != null)
            {
                // 2 Arqueiros no cume elevado (X=5, Z=4 e X=5, Z=6)
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 5, z = 4, currentHealth = 0, level = 3, rank = (int)MilitaryRank.Veteran });
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 5, z = 6, currentHealth = 0, level = 3, rank = (int)MilitaryRank.Veteran });

                // 2 Combatentes nas encostas (X=8, Z=3 e X=8, Z=7)
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 8, z = 3, currentHealth = 0, level = 3, rank = (int)MilitaryRank.Veteran });
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = 8, z = 7, currentHealth = 0, level = 3, rank = (int)MilitaryRank.Veteran });

                Debug.Log("[StageWaveController] Fase 2 - Confronto 2: 4 Gansos Veteranos spawnados no platô!");
            }

            if (CameraController.Instance != null)
            {
                CameraController.Instance.FocusOnPosition(new Vector3(5f, 1.5f, 5f));
                yield return new WaitForSeconds(1.8f);
                CameraController.Instance.ResetToOverview();
            }
        }

        private IEnumerator RoutinePhase2Stage3BattalionClash()
        {
            ShowBanner("🛡️ O BATALHÃO DE CHOQUE! 🛡️\nAsa-de-Ferro: 'O platô é nosso! Mas o líder avança com sua guarda!'\nLíder Ganso: 'Batalhão, esmaguem os patos! Pela Dinastia!'", 4.5f);

            yield return new WaitForSeconds(2.0f);

            if (SpawnManager.Instance != null)
            {
                // Batalhão de 4 Gansos Veteranos pesados em X=9, Z=2, 4, 6, 8
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 9, z = 2, currentHealth = 0, level = 3, rank = (int)MilitaryRank.Veteran });
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 9, z = 4, currentHealth = 0, level = 3, rank = (int)MilitaryRank.Veteran });
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = 9, z = 6, currentHealth = 0, level = 3, rank = (int)MilitaryRank.Veteran });
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 9, z = 8, currentHealth = 0, level = 3, rank = (int)MilitaryRank.Veteran });

                Debug.Log("[StageWaveController] Fase 2 - Confronto 3: Batalhão de 4 Gansos Veteranos spawnado!");
            }

            if (CameraController.Instance != null)
            {
                CameraController.Instance.FocusOnPosition(new Vector3(9f, 1f, 5f));
                yield return new WaitForSeconds(1.8f);
                CameraController.Instance.ResetToOverview();
            }
        }

        // =========================================================================
        // FASE 3 (ANO 30 – DESFILADEIRO DOS GANSOS) | GRID 12x12
        // =========================================================================

        private IEnumerator RoutinePhase3Stage2CliffAmbush()
        {
            ShowBanner("⚡ EMBOSCADA NAS ESCARPAS! ⚡\nAsa-de-Ferro: 'As sentinelas caíram! Olhem para as rochas acima!'\nVoz dos Penhascos: 'Vocês entraram na garganta da morte!'", 4.5f);

            yield return new WaitForSeconds(2.0f);

            if (SpawnManager.Instance != null)
            {
                // 2 Arqueiros de Elite nas escarpas laterais (X=7, Z=2 e X=7, Z=9)
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 7, z = 2, currentHealth = 0, level = 5, rank = (int)MilitaryRank.Elite });
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 7, z = 9, currentHealth = 0, level = 5, rank = (int)MilitaryRank.Elite });

                // 2 Combatentes de Elite no meio do estreito (X=8, Z=5 e X=8, Z=6)
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 8, z = 5, currentHealth = 0, level = 5, rank = (int)MilitaryRank.Elite });
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = 8, z = 6, currentHealth = 0, level = 5, rank = (int)MilitaryRank.Elite });

                Debug.Log("[StageWaveController] Fase 3 - Confronto 2: 4 Gansos de Elite spawnados nas escarpas!");
            }

            if (CameraController.Instance != null)
            {
                CameraController.Instance.FocusOnPosition(new Vector3(7.5f, 2f, 5.5f));
                yield return new WaitForSeconds(1.8f);
                CameraController.Instance.ResetToOverview();
            }
        }

        private IEnumerator RoutinePhase3Stage3PraetorianGuard()
        {
            ShowBanner("👑 A GUARDA PRETORIANA FINAL! 👑\nAsa-de-Ferro: 'Esta é a batalha decisiva de 30 anos!'\nGrande Cisne: 'A Dinastia não cairá diante de patos insolentes!'", 5.0f);

            yield return new WaitForSeconds(2.0f);

            if (SpawnManager.Instance != null)
            {
                // Guarda Pretoriana de 5 Cisnes de Elite protegendo a saída do estreito (X=10, 11)
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 10, z = 4, currentHealth = 0, level = 5, rank = (int)MilitaryRank.Elite });
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 10, z = 5, currentHealth = 0, level = 5, rank = (int)MilitaryRank.Elite });
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Squire", x = 10, z = 7, currentHealth = 0, level = 5, rank = (int)MilitaryRank.Elite });
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Archer", x = 11, z = 4, currentHealth = 0, level = 5, rank = (int)MilitaryRank.Elite });
                SpawnManager.Instance.SpawnSingleUnit(new UnitSaveData { faction = FactionType.Swans, className = "Warrior", x = 11, z = 6, currentHealth = 0, level = 5, rank = (int)MilitaryRank.Elite });

                Debug.Log("[StageWaveController] Fase 3 - Confronto 3: Guarda Pretoriana de 5 Gansos de Elite spawnada!");
            }

            if (CameraController.Instance != null)
            {
                CameraController.Instance.FocusOnPosition(new Vector3(10.5f, 1f, 5.5f));
                yield return new WaitForSeconds(2.0f);
                CameraController.Instance.ResetToOverview();
            }
        }

        public void ShowBanner(string text, float duration)
        {
            activeBannerText = text;
            bannerTimer = duration;
        }

        private void Update()
        {
            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.deltaTime;
                if (bannerTimer <= 0f)
                {
                    activeBannerText = "";
                }
            }
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(activeBannerText) || bannerTimer <= 0f) return;

            // Renderiza banner centralizado estilizado no topo da tela
            float bannerWidth = Mathf.Min(680f, Screen.width * 0.85f);
            float bannerHeight = 85f;
            float x = (Screen.width - bannerWidth) / 2f;
            float y = 55f;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            Texture2D bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.08f, 0.10f, 0.14f, 0.90f));
            bgTex.Apply();
            boxStyle.normal.background = bgTex;

            GUI.Box(new Rect(x, y, bannerWidth, bannerHeight), GUIContent.none, boxStyle);

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = new Color(1f, 0.88f, 0.40f); // Dourado claro

            GUI.Label(new Rect(x + 10, y + 5, bannerWidth - 20, bannerHeight - 10), activeBannerText, labelStyle);
        }
    }
}
