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
            Vector3 direction = (targetPosition - transform.position);
            direction.y = 0f; // Mantém movimento plano horizontal

            if (direction.magnitude > arrivalThreshold)
            {
                direction.Normalize();
                transform.position += direction * (slideSpeed * Time.deltaTime);

                // Rotaciona a unidade na direção do deslocamento
                if (direction != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
                }
            }
        }
    }
}
