using UnityEngine;

namespace DuckFightSwan.Units
{
    /// <summary>
    /// Componente visual desacoplado que renderiza uma barra de vida e texto flutuante em World Space
    /// acima da cabeça da unidade, orientando-se dinamicamente (billboard) para a câmera.
    /// Respeita o SRP ao cuidar unicamente da representação gráfica de status da unidade.
    /// </summary>
    public class FloatingHealthBar : MonoBehaviour
    {
        [Header("Configurações Visuais")]
        [SerializeField] private Vector3 offset = new Vector3(0, 1.8f, 0);
        [SerializeField] private Vector2 barSize = new Vector2(1.2f, 0.16f);

        private Health health;
        private Unit unit;

        private GameObject container;
        private Transform bgBar;
        private Transform fillBar;
        private TextMesh textMesh;
        private Material bgMat;
        private Material fillMat;

        private void Awake()
        {
            health = GetComponent<Health>();
            unit = GetComponent<Unit>();
        }

        private void Start()
        {
            BuildVisuals();

            if (health != null)
            {
                health.OnHealthChanged += UpdateHealthBar;
                UpdateHealthBar(health.CurrentHealth, health.MaxHealth);
            }
        }

        private void BuildVisuals()
        {
            if (container != null) return;

            container = new GameObject("FloatingHealthContainer");
            container.transform.SetParent(transform);
            container.transform.localPosition = offset;

            // Shader padrão universal
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            // Fundo da barra (cinza escuro / preto)
            GameObject bgObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgObj.name = "HealthBar_BG";
            bgObj.transform.SetParent(container.transform);
            bgObj.transform.localPosition = Vector3.zero;
            bgObj.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);
            
            // Remove colisor para não obstruir raycasts do mouse
            Collider bgCol = bgObj.GetComponent<Collider>();
            if (bgCol != null) Destroy(bgCol);

            if (shader != null)
            {
                bgMat = new Material(shader) { color = new Color(0.12f, 0.12f, 0.12f, 0.85f) };
                bgObj.GetComponent<Renderer>().material = bgMat;
            }

            // Barra de preenchimento (Verde para Patos, Vermelho para Cisnes)
            GameObject fillObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fillObj.name = "HealthBar_Fill";
            fillObj.transform.SetParent(container.transform);
            fillObj.transform.localPosition = new Vector3(0, 0, -0.01f);
            fillObj.transform.localScale = new Vector3(barSize.x * 0.96f, barSize.y * 0.8f, 1f);

            Collider fillCol = fillObj.GetComponent<Collider>();
            if (fillCol != null) Destroy(fillCol);

            Color factionColor = (unit != null && unit.Faction == FactionType.Ducks)
                ? new Color(0.2f, 0.85f, 0.3f, 1f)
                : new Color(0.9f, 0.25f, 0.2f, 1f);

            if (shader != null)
            {
                fillMat = new Material(shader) { color = factionColor };
                fillObj.GetComponent<Renderer>().material = fillMat;
            }

            fillBar = fillObj.transform;
            bgBar = bgObj.transform;

            // Rótulo textual da vida (ex: "HP 85/100")
            GameObject textObj = new GameObject("HealthLabel");
            textObj.transform.SetParent(container.transform);
            textObj.transform.localPosition = new Vector3(0, barSize.y * 0.5f + 0.18f, -0.02f);

            textMesh = textObj.AddComponent<TextMesh>();
            textMesh.characterSize = 0.08f;
            textMesh.fontSize = 28;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = Color.white;
        }

        public void UpdateHealthBar(int current, int max)
        {
            if (max <= 0) return;

            float ratio = Mathf.Clamp01((float)current / max);

            if (fillBar != null)
            {
                float fullWidth = barSize.x * 0.96f;
                float currentWidth = fullWidth * ratio;
                fillBar.localScale = new Vector3(currentWidth, barSize.y * 0.8f, 1f);

                // Alinha o preenchimento a partir da esquerda
                float leftOffset = -(fullWidth - currentWidth) * 0.5f;
                fillBar.localPosition = new Vector3(leftOffset, 0, -0.01f);
            }

            if (textMesh != null)
            {
                textMesh.text = $"HP {current}/{max}";
            }
        }

        private void LateUpdate()
        {
            if (container == null) return;

            // Billboard effect: sempre direcionado de frente para a câmera ativa
            Camera cam = Camera.main;
            if (cam != null)
            {
                container.transform.rotation = cam.transform.rotation;
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnHealthChanged -= UpdateHealthBar;
            }
            if (bgMat != null) Destroy(bgMat);
            if (fillMat != null) Destroy(fillMat);
        }
    }
}
