using UnityEngine;
using DuckFightSwan.Terrain;

namespace DuckFightSwan.Core
{
    /// <summary>
    /// Gerencia a câmera tática orbital com navegação livre (WASD), rotação (Q/E)
    /// em torno do pivô central de visão, zoom e foco em unidades.
    /// Respeita o SRP ao lidar exclusivamente com a projeção e controles de visão.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public static CameraController Instance { get; private set; }

        [Header("Velocidades de Navegação")]
        [Tooltip("Velocidade de deslocamento horizontal com WASD")]
        [SerializeField] private float panSpeed = 12f;

        [Tooltip("Velocidade de rotação orbital em graus/segundo com Q/E")]
        [SerializeField] private float rotationSpeed = 90f;

        [Tooltip("Fator de suavização (interpolação lerp) da câmera")]
        [SerializeField] private float smoothSpeed = 8f;

        [Tooltip("Sensibilidade do scroll do mouse para o zoom (valores moderados evitam saltos bruscos)")]
        [SerializeField] private float zoomSensitivity = 1.2f;

        [Tooltip("Fator de suavização específico para a interpolação de zoom")]
        [SerializeField] private float zoomSmoothSpeed = 5f;

        [Header("Distâncias e Ângulos")]
        [SerializeField] private float minDistance = 4f;
        [SerializeField] private float maxDistance = 25f;
        [SerializeField] private float focusDistance = 6.5f;

        [Header("Limites do Tabuleiro")]
        [SerializeField] private bool useBounds = true;
        [SerializeField] private float boundsPadding = 5f;

        // Estado do Pivô Central e Orientação Orbital
        private Vector3 currentPivot;
        private Vector3 targetPivot;
        private float currentYaw;
        private float targetYaw;
        private float currentPitch;
        private float targetPitch;
        private float currentDistance;
        private float targetDistance;

        // Estado de Foco em Unidades
        private Transform target;
        private bool isFocusing = false;

        // Referência inicial para Reset
        private Vector3 initialPivot;
        private float initialYaw;
        private float initialPitch;
        private float initialDistance;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            InitializeCameraOrientation();
        }

        /// <summary>
        /// Calibra o pivô inicial, distância e ângulos pitch/yaw baseado na posição atual da câmera na cena.
        /// Garante inicialização sem solavancos visuais.
        /// </summary>
        private void InitializeCameraOrientation()
        {
            Ray ray = new Ray(transform.position, transform.forward);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float enter))
            {
                initialPivot = ray.GetPoint(enter);
                initialDistance = enter;
            }
            else
            {
                initialPivot = new Vector3(7f, 0f, 7f);
                initialDistance = 12f;
            }

            initialPitch = transform.eulerAngles.x;
            initialYaw = transform.eulerAngles.y;

            currentPivot = targetPivot = initialPivot;
            currentDistance = targetDistance = Mathf.Clamp(initialDistance, minDistance, maxDistance);
            currentPitch = targetPitch = initialPitch;
            currentYaw = targetYaw = initialYaw;
        }

        private void Update()
        {
            HandleInputs();
        }

        private void LateUpdate()
        {
            UpdateCameraTransform();
        }

        /// <summary>
        /// Captura comandos do jogador:
        /// - WASD: deslocamento relativo à rotação atual da câmera
        /// - Q: rotação no sentido horário
        /// - E: rotação no sentido anti-horário
        /// - Mouse Scroll: zoom in/out
        /// - Espaço / Home: recentralizar visão
        /// </summary>
        private void HandleInputs()
        {
            // Leitura de eixos de translação (WASD e Setas)
            float moveX = 0f;
            float moveZ = 0f;

            // Se o jogador estiver interagindo com o menu de ação ou navegando o cursor do grid,
            // não movemos a câmera com WASD nem recentralizamos com Espaço para permitir uso tático.
            // Rotação com Q/E e Zoom com scroll wheel permanecem livres.
            bool isInteracting = InputGridController.Instance != null && InputGridController.Instance.IsInteracting;

            if (!isInteracting)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) moveZ += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) moveZ -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) moveX += 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) moveX -= 1f;

                // Suporte aos eixos configurados da Unity caso teclas não estejam ativas
                if (Mathf.Approximately(moveX, 0f)) moveX = Input.GetAxisRaw("Horizontal");
                if (Mathf.Approximately(moveZ, 0f)) moveZ = Input.GetAxisRaw("Vertical");
            }

            // Leitura de rotação orbital (Q = sentido horário / E = sentido anti-horário)
            float rotInput = 0f;
            if (Input.GetKey(KeyCode.Q)) rotInput += 1f; // Sentido horário (aumenta o yaw)
            if (Input.GetKey(KeyCode.E)) rotInput -= 1f; // Sentido anti-horário (diminui o yaw)

            // Desacopla o foco automático se o jogador assumir controle manual
            bool hasManualMove = Mathf.Abs(moveX) > 0.05f || Mathf.Abs(moveZ) > 0.05f || Mathf.Abs(rotInput) > 0.05f;
            if (isFocusing && hasManualMove)
            {
                isFocusing = false;
                target = null;
                Debug.Log("[CameraController] Controle manual assumido pelo jogador. Foco desacoplado.");
            }

            // Atualização do Pivô Alvo
            if (isFocusing && target != null)
            {
                targetPivot = target.position;
            }
            else if (hasManualMove && (Mathf.Abs(moveX) > 0.05f || Mathf.Abs(moveZ) > 0.05f))
            {
                // Calcula direções relativas ao ângulo yaw atual da câmera no plano XZ
                Vector3 forward = Quaternion.Euler(0f, currentYaw, 0f) * Vector3.forward;
                Vector3 right = Quaternion.Euler(0f, currentYaw, 0f) * Vector3.right;
                Vector3 moveDir = (forward * moveZ + right * moveX).normalized;

                targetPivot += moveDir * (panSpeed * Time.deltaTime);

                // Aplica limites de tabuleiro
                if (useBounds)
                {
                    ApplyBoundsClamping();
                }
            }

            // Atualização do Yaw Alvo (Rotação orbital em torno do centro de visão)
            if (Mathf.Abs(rotInput) > 0.05f)
            {
                targetYaw += rotInput * (rotationSpeed * Time.deltaTime);
            }

            // Leitura e aplicação de Zoom (Scroll Wheel)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                targetDistance = Mathf.Clamp(targetDistance - scroll * zoomSensitivity * 10f, minDistance, maxDistance);
            }

            // Teclas de atalho para recentralizar visão (Espaço só se não estiver em ação/menu)
            if ((!isInteracting && Input.GetKeyDown(KeyCode.Space)) || Input.GetKeyDown(KeyCode.Home))
            {
                ResetToOverview();
            }
        }

        /// <summary>
        /// Aplica os limites calculados do grid ao pivô para evitar perda de enquadramento.
        /// </summary>
        private void ApplyBoundsClamping()
        {
            float minX = -boundsPadding;
            float maxX = 15f + boundsPadding;
            float minZ = -boundsPadding;
            float maxZ = 15f + boundsPadding;

            if (GridManager.Instance != null)
            {
                maxX = GridManager.Instance.Width + boundsPadding;
                maxZ = GridManager.Instance.Depth + boundsPadding;
            }

            targetPivot.x = Mathf.Clamp(targetPivot.x, minX, maxX);
            targetPivot.z = Mathf.Clamp(targetPivot.z, minZ, maxZ);
        }

        /// <summary>
        /// Interpola suavemente o pivô, ângulos e distância, e atualiza a posição/rotação da câmera.
        /// </summary>
        private void UpdateCameraTransform()
        {
            currentPivot = Vector3.Lerp(currentPivot, targetPivot, Time.deltaTime * smoothSpeed);
            currentYaw = Mathf.Lerp(currentYaw, targetYaw, Time.deltaTime * smoothSpeed);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * smoothSpeed);
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * zoomSmoothSpeed);

            Quaternion orientation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 offset = orientation * new Vector3(0f, 0f, -currentDistance);

            transform.position = currentPivot + offset;
            transform.rotation = orientation;
        }

        /// <summary>
        /// Centraliza suavemente a câmera na unidade desejada com distância de combate.
        /// </summary>
        public void FocusOn(Transform unitTransform)
        {
            if (unitTransform == null) return;

            target = unitTransform;
            targetPivot = unitTransform.position;
            targetDistance = focusDistance;
            isFocusing = true;
            Debug.Log($"[CameraController] Focando câmera em: {unitTransform.name}");
        }

        /// <summary>
        /// Encerra o foco automático sem puxar a visão abruptamente, mantendo o controle na posição atual.
        /// </summary>
        public void ClearFocus()
        {
            target = null;
            isFocusing = false;
            targetDistance = Mathf.Max(targetDistance, initialDistance);
            Debug.Log("[CameraController] Foco automático encerrado. Câmera mantida na posição atual.");
        }

        /// <summary>
        /// Centraliza suavemente a câmera em uma coordenada de mundo específica.
        /// </summary>
        public void FocusOnPosition(Vector3 worldPos)
        {
            target = null;
            targetPivot = worldPos;
            targetDistance = focusDistance;
            isFocusing = false;
        }

        /// <summary>
        /// Retorna a câmera suavemente à visão panorâmica central do tabuleiro.
        /// </summary>
        public void ResetToOverview()
        {
            isFocusing = false;
            target = null;

            if (GridManager.Instance != null)
            {
                targetPivot = new Vector3(GridManager.Instance.Width / 2f, 0f, GridManager.Instance.Depth / 2f);
            }
            else
            {
                targetPivot = initialPivot;
            }

            targetYaw = initialYaw;
            targetPitch = initialPitch;
            targetDistance = initialDistance;
            Debug.Log("[CameraController] Visão resetada para o centro panorâmico.");
        }
    }
}
