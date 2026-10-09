using UnityEngine;

namespace DuckFightSwan.Terrain
{
    /// <summary>
    /// Gerencia o cursor visual tridimensional no cenário (World Space).
    /// Indica claramente qual bloco do tabuleiro está atualmente selecionado/focado pelo jogador,
    /// navegável via teclado (WASD / Setas) ou ponteiro do mouse.
    /// Respeita o SRP ao lidar exclusivamente com a renderização e animação do indicador de grid.
    /// </summary>
    public class GridCursor : MonoBehaviour
    {
        public static GridCursor Instance { get; private set; }

        [Header("Configurações do Cursor")]
        [SerializeField] private float moveSpeed = 15f;
        [SerializeField] private float yOffset = 0.05f;

        private GameObject cursorVisual;
        private Renderer cursorRenderer;
        private TileNode currentTile;
        private Vector3 targetPosition;
        private bool isVisible = false;
        private Color baseColor = Color.cyan;

        public TileNode CurrentTile => currentTile;
        public bool IsVisible => isVisible;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                CreateVisualIndicator();
                Hide();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Cria proceduralmente a malha visual do cursor se não houver prefab customizado.
        /// Remove colisores para não interferir nos raycasts de seleção.
        /// </summary>
        private void CreateVisualIndicator()
        {
            if (cursorVisual != null) return;

            cursorVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cursorVisual.name = "GridCursor_Visual";
            cursorVisual.transform.SetParent(transform);

            // Remove o colisor para que o cursor nunca bloqueie os raycasts do mouse
            Collider col = cursorVisual.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            cursorVisual.transform.localScale = new Vector3(0.96f, 0.05f, 0.96f);

            cursorRenderer = cursorVisual.GetComponent<Renderer>();
            if (cursorRenderer != null)
            {
                // Material transparente e brilhante
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = baseColor;
                if (mat.HasProperty("_Mode"))
                {
                    mat.SetFloat("_Mode", 3); // Transparent
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                }
                cursorRenderer.material = mat;
            }
        }

        private void Update()
        {
            if (!isVisible) return;

            // Interpola suavemente até o topo do tile alvo
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);

            // Efeito de pulso para feedback tático de seleção
            if (cursorVisual != null)
            {
                float pulse = 1.0f + 0.04f * Mathf.Sin(Time.time * 8f);
                cursorVisual.transform.localScale = new Vector3(0.96f * pulse, 0.05f, 0.96f * pulse);
            }
        }

        /// <summary>
        /// Exibe o cursor posicionado sobre o bloco inicial informado com a cor desejada.
        /// </summary>
        public void Show(TileNode tile, Color color)
        {
            if (tile == null) return;

            currentTile = tile;
            baseColor = color;
            targetPosition = tile.GetTopPosition() + Vector3.up * yOffset;
            transform.position = targetPosition;

            if (cursorRenderer != null && cursorRenderer.material != null)
            {
                cursorRenderer.material.color = color;
            }

            isVisible = true;
            if (cursorVisual != null)
            {
                cursorVisual.SetActive(true);
            }
        }

        /// <summary>
        /// Move o cursor suavemente para um novo bloco do grid.
        /// </summary>
        public void MoveTo(TileNode newTile)
        {
            if (newTile == null) return;

            currentTile = newTile;
            targetPosition = newTile.GetTopPosition() + Vector3.up * yOffset;

            if (!isVisible)
            {
                transform.position = targetPosition;
                isVisible = true;
                if (cursorVisual != null) cursorVisual.SetActive(true);
            }
        }

        /// <summary>
        /// Oculta o cursor visual do cenário.
        /// </summary>
        public void Hide()
        {
            isVisible = false;
            currentTile = null;
            if (cursorVisual != null)
            {
                cursorVisual.SetActive(false);
            }
        }
    }
}
