using UnityEngine;

namespace DuckFightSwan.Units
{
    /// <summary>
    /// Movement gerencia o deslocamento físico da unidade no cenário 3D.
    /// Respeita o SRP ao lidar apenas com a física de translação de posição.
    /// </summary>
    public class Movement : MonoBehaviour
    {
        [Header("Configurações de Navegação")]
        [SerializeField] private float arrivalThreshold = 0.2f;
        [SerializeField] private float slideSpeed = 4.0f; // Velocidade visual de deslizamento no grid

        public void Initialize(Stats stats)
        {
            // Abstraído: a velocidade visual de deslizamento no grid é fixa
        }

        /// <summary>
        /// Move o GameObject em direção a um ponto de destino 3D.
        /// </summary>
        public void MoveTowards(Vector3 targetPosition)
        {
            float distance = Vector3.Distance(transform.position, targetPosition);

            if (distance > arrivalThreshold)
            {
                // Interpolação suave em 3 dimensões (X, Y do degrau de relevo, Z)
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, slideSpeed * Time.deltaTime);

                // Orientação rotacional da unidade apenas no plano horizontal
                Vector3 horizontalDir = targetPosition - transform.position;
                horizontalDir.y = 0f;

                if (horizontalDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(horizontalDir.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
                }
            }
            else
            {
                transform.position = targetPosition;
            }
        }
    }
}
