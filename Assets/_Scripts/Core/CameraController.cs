using UnityEngine;

namespace DuckFightSwan.Core
{
    /// <summary>
    /// Gerencia o movimento e foco suave da câmera na unidade selecionada.
    /// Respeita o SRP ao lidar exclusivamente com translação e zoom de câmera.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public static CameraController Instance { get; private set; }

        [Header("Configurações de Foco")]
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Vector3 focusOffset = new Vector3(0f, 3.5f, -3f);

        private Transform target;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private bool isFocusing = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                initialPosition = transform.position;
                initialRotation = transform.rotation;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            if (isFocusing && target != null)
            {
                // Calcula a posição desejada com zoom e foco
                Vector3 desiredPosition = target.position + focusOffset;
                transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smoothSpeed);

                // Rotaciona suavemente para olhar para o alvo
                Quaternion targetRotation = Quaternion.LookRotation((target.position + Vector3.up * 0.5f) - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * smoothSpeed);
            }
            else if (!isFocusing)
            {
                // Retorna suavemente para a visão geral original
                transform.position = Vector3.Lerp(transform.position, initialPosition, Time.deltaTime * smoothSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, initialRotation, Time.deltaTime * smoothSpeed);
            }
        }

        /// <summary>
        /// Aproxima e foca a câmera na unidade desejada.
        /// </summary>
        public void FocusOn(Transform unitTransform)
        {
            target = unitTransform;
            isFocusing = true;
            Debug.Log($"[CameraController] Focando câmera em: {unitTransform.name}");
        }

        /// <summary>
        /// Limpa o foco da câmera, retornando-a à visão geral do tabuleiro.
        /// </summary>
        public void ClearFocus()
        {
            target = null;
            isFocusing = false;
            Debug.Log("[CameraController] Visão focada da câmera encerrada.");
        }
    }
}
