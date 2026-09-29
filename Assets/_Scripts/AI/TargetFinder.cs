using UnityEngine;

namespace DuckFightSwan.AI
{
    /// <summary>
    /// TargetFinder é responsável por localizar e rastrear o inimigo mais próximo.
    /// Respeita o SRP ao conter apenas lógica de escaneamento geométrico de alvos.
    /// </summary>
    public class TargetFinder : MonoBehaviour
    {
        private Units.Unit selfUnit;

        private void Start()
        {
            selfUnit = GetComponent<Units.Unit>();
        }

        /// <summary>
        /// Escaneia a cena e retorna a Unit inimiga mais próxima que não esteja morta dentro de um raio limite.
        /// </summary>
        public Units.Unit FindNearestTarget(float maxDistance = float.MaxValue)
        {
            if (selfUnit == null) return null;

            // Encontra todas as unidades ativas na cena
            Units.Unit[] allUnits = FindObjectsByType<Units.Unit>(FindObjectsSortMode.None);
            Units.Unit closestTarget = null;
            float shortestDistance = float.MaxValue;

            foreach (Units.Unit candidate in allUnits)
            {
                // Ignora se for da mesma facção ou se já estiver morto
                if (candidate.Faction == selfUnit.Faction || candidate.Health.IsDead)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, candidate.transform.position);

                if (distance < shortestDistance && distance <= maxDistance)
                {
                    shortestDistance = distance;
                    closestTarget = candidate;
                }
            }

            return closestTarget;
        }
    }
}
