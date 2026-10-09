using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DuckFightSwan.Terrain;

namespace DuckFightSwan.Units
{
    /// <summary>
    /// Gerencia a tomada de decisões da IA inimiga (Cisnes/Swans).
    /// Controla movimentação tática (avanço e recuo/kiting), seleção de alvos,
    /// cadência balanceada de ataques (1 ataque a cada 2-3 turnos) e ritmo compassado.
    /// Respeita o SRP ao isolar a inteligência de batalha dos gerenciadores de ciclo de vida.
    /// </summary>
    public class EnemyAIController : MonoBehaviour
    {
        public static EnemyAIController Instance { get; private set; }

        [Header("Configurações de Ritmo e Pacing")]
        [SerializeField] private float stepDelay = 0.25f;
        [SerializeField] private float attackDelay = 0.4f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Executa a rotina completa do turno de um Cisne específico.
        /// </summary>
        public IEnumerator ExecuteSwanTurn(Unit swan, List<GameObject> activeDucks)
        {
            if (swan == null || swan.Health.IsDead || activeDucks == null || activeDucks.Count == 0)
            {
                yield break;
            }

            // Incrementa o contador de cadência
            swan.TurnsSinceLastAttack++;

            // Filtra patos vivos
            List<Unit> aliveDucks = GetAliveDucks(activeDucks);
            if (aliveDucks.Count == 0)
            {
                yield break;
            }

            Unit nearestDuck = FindNearestDuck(swan, aliveDucks);
            if (nearestDuck == null || swan.CurrentTile == null)
            {
                yield break;
            }

            bool isArcher = swan.ClassData != null && swan.ClassData.ClassType == UnitClassType.Archer;
            float currentDistanceToNearest = Vector2.Distance(
                new Vector2(swan.CurrentTile.X, swan.CurrentTile.Z),
                new Vector2(nearestDuck.CurrentTile.X, nearestDuck.CurrentTile.Z)
            );

            // =========================================================================
            // 1. AVALIAÇÃO DE RECUO TÁTICO (KITING / BAIXA VIDA)
            // =========================================================================
            bool shouldRetreat = false;
            // Arqueiro recua se estiver corpo a corpo (distância 1)
            if (isArcher && currentDistanceToNearest <= 1.5f)
            {
                shouldRetreat = true;
            }
            // Unidade muito ferida (< 35% HP) tenta recuar para terreno defensivo
            else if ((float)swan.Health.CurrentHealth / swan.Health.MaxHealth < 0.35f && currentDistanceToNearest <= 2.5f)
            {
                shouldRetreat = true;
            }

            if (shouldRetreat && GridManager.Instance != null)
            {
                List<TileNode> validMoves = GridManager.Instance.GetValidMoveTargets(swan.CurrentTile, swan);
                TileNode retreatTile = FindBestRetreatTile(swan.CurrentTile, validMoves, nearestDuck.CurrentTile, isArcher);

                if (retreatTile != null)
                {
                    yield return StartCoroutine(MoveSwanToTile(swan, retreatTile));
                }
            }

            // =========================================================================
            // 2. AVALIAÇÃO DE ATAQUE DA POSIÇÃO ATUAL
            // =========================================================================
            if (GridManager.Instance != null && swan.CurrentTile != null)
            {
                List<TileNode> attackTargets = GridManager.Instance.GetValidAttackTargets(swan.CurrentTile, swan);
                if (attackTargets.Count > 0)
                {
                    // Regra de cadência: se turnsSinceLastAttack >= 2, o ataque é garantido.
                    // Se turnsSinceLastAttack == 1, 75% de chance de atacar.
                    bool willAttack = swan.TurnsSinceLastAttack >= 2 || Random.value < 0.75f;

                    if (willAttack)
                    {
                        Unit targetDuck = SelectBestTargetDuck(attackTargets);
                        if (targetDuck != null)
                        {
                            yield return StartCoroutine(PerformAttack(swan, targetDuck));
                            yield break;
                        }
                    }
                }
            }

            // =========================================================================
            // 3. AVALIAÇÃO DE AVANÇO TÁTICO (MOVER PARA ALCANCE OU OBJETIVO)
            // =========================================================================
            if (GridManager.Instance != null && swan.CurrentTile != null)
            {
                List<TileNode> validMoves = GridManager.Instance.GetValidMoveTargets(swan.CurrentTile, swan);
                if (validMoves.Count > 0)
                {
                    // Tenta encontrar um tile onde o Cisne consiga atacar neste turno
                    TileNode bestTileToAttack = null;
                    Unit duckToAttackFromTile = null;

                    if (swan.TurnsSinceLastAttack >= 2 || Random.value < 0.65f)
                    {
                        foreach (var moveTile in validMoves)
                        {
                            List<TileNode> potentialTargets = GridManager.Instance.GetValidAttackTargets(moveTile, swan);
                            if (potentialTargets.Count > 0)
                            {
                                bestTileToAttack = moveTile;
                                duckToAttackFromTile = SelectBestTargetDuck(potentialTargets);
                                break;
                            }
                        }
                    }

                    if (bestTileToAttack != null && duckToAttackFromTile != null)
                    {
                        // Move e depois ataca
                        yield return StartCoroutine(MoveSwanToTile(swan, bestTileToAttack));
                        yield return StartCoroutine(PerformAttack(swan, duckToAttackFromTile));
                        yield break;
                    }
                    else
                    {
                        // Avança em direção ao objetivo / pato mais próximo
                        TileNode advanceTile = FindBestAdvanceTile(swan.CurrentTile, validMoves, nearestDuck.CurrentTile, isArcher);
                        if (advanceTile != null)
                        {
                            yield return StartCoroutine(MoveSwanToTile(swan, advanceTile));
                        }
                    }
                }
            }

            // Pequena pausa final para compassar a percepção do jogador
            yield return new WaitForSeconds(stepDelay);
        }

        private IEnumerator MoveSwanToTile(Unit swan, TileNode targetTile)
        {
            if (swan == null || targetTile == null || GridManager.Instance == null) yield break;

            List<Vector3> waypoints = GridManager.Instance.GetPathTo(targetTile);
            if (waypoints != null && waypoints.Count > 0)
            {
                if (swan.CurrentTile != null)
                {
                    swan.CurrentTile.CurrentUnit = null;
                }

                swan.CurrentTile = targetTile;
                targetTile.CurrentUnit = swan;

                swan.SetMovePath(waypoints);

                float timeout = 2.0f;
                while (swan.IsMoving && timeout > 0f)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }

                // Ajuste fino de posicionamento final
                swan.transform.position = targetTile.GetTopPosition();
                yield return new WaitForSeconds(stepDelay);
            }
        }

        private IEnumerator PerformAttack(Unit swan, Unit target)
        {
            if (swan == null || target == null || target.Health.IsDead) yield break;

            // Vira para olhar o alvo
            FaceTarget(swan, target.transform.position);
            yield return new WaitForSeconds(0.15f);

            int rawDamage = swan.UnitStats.Damage;
            Debug.Log($"[EnemyAI] {swan.UnitName} atacou {target.UnitName} com {rawDamage} de dano bruto (Cadência: {swan.TurnsSinceLastAttack} turnos).");

            target.Health.TakeDamage(new Damage(rawDamage, swan));
            swan.TurnsSinceLastAttack = 0;

            yield return new WaitForSeconds(attackDelay);
        }

        private void FaceTarget(Unit unit, Vector3 targetPos)
        {
            Vector3 dir = targetPos - unit.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
            {
                unit.transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
        }

        private List<Unit> GetAliveDucks(List<GameObject> duckObjs)
        {
            List<Unit> list = new List<Unit>();
            foreach (var obj in duckObjs)
            {
                if (obj == null) continue;
                Unit u = obj.GetComponent<Unit>();
                if (u != null && !u.Health.IsDead)
                {
                    list.Add(u);
                }
            }
            return list;
        }

        private Unit FindNearestDuck(Unit swan, List<Unit> ducks)
        {
            Unit nearest = null;
            float minDist = float.MaxValue;
            Vector2 swanPos = new Vector2(swan.CurrentTile.X, swan.CurrentTile.Z);

            foreach (var duck in ducks)
            {
                if (duck.CurrentTile == null) continue;
                float d = Vector2.Distance(swanPos, new Vector2(duck.CurrentTile.X, duck.CurrentTile.Z));
                if (d < minDist)
                {
                    minDist = d;
                    nearest = duck;
                }
            }
            return nearest;
        }

        private Unit SelectBestTargetDuck(List<TileNode> attackableNodes)
        {
            Unit best = null;
            int lowestHealth = int.MaxValue;

            foreach (var node in attackableNodes)
            {
                if (node.CurrentUnit != null && !node.CurrentUnit.Health.IsDead)
                {
                    if (node.CurrentUnit.Health.CurrentHealth < lowestHealth)
                    {
                        lowestHealth = node.CurrentUnit.Health.CurrentHealth;
                        best = node.CurrentUnit;
                    }
                }
            }
            return best;
        }

        private TileNode FindBestAdvanceTile(TileNode currentTile, List<TileNode> candidates, TileNode duckTile, bool isArcher)
        {
            TileNode best = null;
            float bestScore = float.MaxValue;
            Vector2 targetPos = new Vector2(duckTile.X, duckTile.Z);

            float optimalDistance = isArcher ? 3.0f : 1.0f;

            foreach (var node in candidates)
            {
                float dist = Vector2.Distance(new Vector2(node.X, node.Z), targetPos);
                // Pontuação baseada em quão próximo fica da distância ótima
                float score = Mathf.Abs(dist - optimalDistance);

                // Evita que o arqueiro entre em melee desnecessariamente
                if (isArcher && dist <= 1.0f)
                {
                    score += 5f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = node;
                }
            }

            return best;
        }

        private TileNode FindBestRetreatTile(TileNode currentTile, List<TileNode> candidates, TileNode duckTile, bool isArcher)
        {
            TileNode best = null;
            float maxDist = Vector2.Distance(new Vector2(currentTile.X, currentTile.Z), new Vector2(duckTile.X, duckTile.Z));
            Vector2 duckPos = new Vector2(duckTile.X, duckTile.Z);

            foreach (var node in candidates)
            {
                float dist = Vector2.Distance(new Vector2(node.X, node.Z), duckPos);
                if (dist > maxDist)
                {
                    // Para arqueiros, a distância ideal de recuo é por volta de 2 a 3 blocos
                    if (isArcher && dist > 4.5f) continue;

                    maxDist = dist;
                    best = node;
                }
            }

            return best;
        }
    }
}
