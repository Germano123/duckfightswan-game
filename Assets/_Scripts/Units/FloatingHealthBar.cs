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

        [Header("Configurações de Tremor")]
        [SerializeField] private float minShakeIntensity = 0.04f;
        [SerializeField] private float maxShakeIntensity = 0.22f;
        [SerializeField] private float shakeDuration = 0.28f;
        [SerializeField] private float maxDamageRef = 35f;

        [Header("Configurações do Ghost Bar")]
        [SerializeField] private float ghostDrainDelay = 0.18f;
        [SerializeField] private float ghostDrainSpeed = 1.4f;

        private Health health;
        private Unit unit;

        private GameObject container;
        private Transform bgBar;
        private Transform ghostBar;
        private Transform fillBar;
        private TextMesh textMesh;
        private Material bgMat;
        private Material ghostMat;
        private Material fillMat;

        private Vector3 currentShakeOffset = Vector3.zero;
        private Coroutine shakeCoroutine;
        private Coroutine ghostDrainCoroutine;

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
                health.OnDamageTaken += HandleDamageTaken;
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

            // 1. Fundo da barra (cinza escuro / preto)
            GameObject bgObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgObj.name = "HealthBar_BG";
            bgObj.transform.SetParent(container.transform);
            bgObj.transform.localPosition = Vector3.zero;
            bgObj.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);
            
            Collider bgCol = bgObj.GetComponent<Collider>();
            if (bgCol != null) Destroy(bgCol);

            if (shader != null)
            {
                bgMat = new Material(shader) { color = new Color(0.12f, 0.12f, 0.12f, 0.85f) };
                bgObj.GetComponent<Renderer>().material = bgMat;
            }

            // 2. Segundo background: Barra de Dano Gradual (Ghost Bar)
            GameObject ghostObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ghostObj.name = "HealthBar_GhostFill";
            ghostObj.transform.SetParent(container.transform);
            ghostObj.transform.localPosition = new Vector3(0, 0, -0.005f);
            ghostObj.transform.localScale = new Vector3(barSize.x * 0.96f, barSize.y * 0.8f, 1f);

            Collider ghostCol = ghostObj.GetComponent<Collider>();
            if (ghostCol != null) Destroy(ghostCol);

            if (shader != null)
            {
                // Amarelado / esbranquiçado suave para destacar o rastro do dano
                ghostMat = new Material(shader) { color = new Color(0.95f, 0.92f, 0.6f, 0.9f) };
                ghostObj.GetComponent<Renderer>().material = ghostMat;
            }

            // 3. Primeiro background: Barra de preenchimento frontal (diminuição flat direta)
            GameObject fillObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fillObj.name = "HealthBar_Fill";
            fillObj.transform.SetParent(container.transform);
            fillObj.transform.localPosition = new Vector3(0, 0, -0.01f);
            fillObj.transform.localScale = new Vector3(barSize.x * 0.96f, barSize.y * 0.8f, 1f);

            Collider fillCol = fillObj.GetComponent<Collider>();
            if (fillCol != null) Destroy(fillCol);

            if (shader != null)
            {
                fillMat = new Material(shader) { color = new Color(0.2f, 0.85f, 0.3f, 1f) };
                fillObj.GetComponent<Renderer>().material = fillMat;
            }

            fillBar = fillObj.transform;
            ghostBar = ghostObj.transform;
            bgBar = bgObj.transform;

            // 4. Rótulo textual da vida (ex: "HP 85/100")
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

        /// <summary>
        /// Determina a cor da barra de vida com base nas faixas percentuais:
        /// 100% a 65%: Verde
        /// 65% a 30%: Amarela
        /// 30% a 0%: Vermelha
        /// </summary>
        private Color GetHealthColor(float ratio)
        {
            if (ratio >= 0.65f)
            {
                return new Color(0.2f, 0.85f, 0.3f, 1f); // Verde (100% - 65%)
            }
            else if (ratio >= 0.30f)
            {
                return new Color(0.95f, 0.85f, 0.15f, 1f); // Amarela (65% - 30%)
            }
            else
            {
                return new Color(0.9f, 0.2f, 0.2f, 1f); // Vermelha (30% - 0%)
            }
        }

        /// <summary>
        /// Atualiza o preenchimento da vida.
        /// A barra frontal diminui de forma direta (flat), enquanto o segundo background (ghost)
        /// drena gradualmente até o novo valor.
        /// </summary>
        public void UpdateHealthBar(int current, int max)
        {
            if (max <= 0) return;

            float ratio = Mathf.Clamp01((float)current / max);
            float fullWidth = barSize.x * 0.96f;
            float targetWidth = fullWidth * ratio;
            float targetLeftOffset = -(fullWidth - targetWidth) * 0.5f;

            // 1. Atualização direta (flat) da barra frontal
            if (fillBar != null)
            {
                fillBar.localScale = new Vector3(targetWidth, barSize.y * 0.8f, 1f);
                fillBar.localPosition = new Vector3(targetLeftOffset, 0, -0.01f);
            }

            // 2. Atualização dinâmica da cor conforme a faixa percentual (100-65, 65-30, 30-0)
            if (fillMat != null)
            {
                fillMat.color = GetHealthColor(ratio);
            }

            // 3. Atualização gradual do segundo background (ghost bar)
            if (ghostBar != null)
            {
                if (ghostDrainCoroutine != null)
                {
                    StopCoroutine(ghostDrainCoroutine);
                }

                // Se a vida aumentou (cura), atualiza imediatamente sem drain
                if (targetWidth >= ghostBar.localScale.x)
                {
                    ghostBar.localScale = new Vector3(targetWidth, barSize.y * 0.8f, 1f);
                    ghostBar.localPosition = new Vector3(targetLeftOffset, 0, -0.005f);
                }
                else
                {
                    // Dano: inicia esvaziamento gradual
                    ghostDrainCoroutine = StartCoroutine(DrainGhostBarRoutine(targetWidth, fullWidth));
                }
            }

            // 4. Rótulo textual com exibição de patente e nível
            if (textMesh != null)
            {
                string rankTag = unit != null ? unit.GetRankDisplayName() : "";
                if (!string.IsNullOrEmpty(rankTag))
                {
                    textMesh.text = $"{rankTag} | HP {current}/{max}";
                }
                else
                {
                    textMesh.text = $"HP {current}/{max}";
                }
            }
        }

        /// <summary>
        /// Esvazia suavemente o segundo background (ghost bar) para visualizar o rastro de dano.
        /// </summary>
        private System.Collections.IEnumerator DrainGhostBarRoutine(float targetWidth, float fullWidth)
        {
            // Pausa breve de impacto para legibilidade
            yield return new WaitForSeconds(ghostDrainDelay);

            while (ghostBar != null && ghostBar.localScale.x > targetWidth + 0.005f)
            {
                float newWidth = Mathf.MoveTowards(ghostBar.localScale.x, targetWidth, ghostDrainSpeed * Time.deltaTime);
                float newOffset = -(fullWidth - newWidth) * 0.5f;

                ghostBar.localScale = new Vector3(newWidth, barSize.y * 0.8f, 1f);
                ghostBar.localPosition = new Vector3(newOffset, 0, -0.005f);

                yield return null;
            }

            if (ghostBar != null)
            {
                float finalOffset = -(fullWidth - targetWidth) * 0.5f;
                ghostBar.localScale = new Vector3(targetWidth, barSize.y * 0.8f, 1f);
                ghostBar.localPosition = new Vector3(finalOffset, 0, -0.005f);
            }
        }

        /// <summary>
        /// Manipula o evento de dano sofrido disparando o tremor proporcional e o texto flutuante.
        /// </summary>
        private void HandleDamageTaken(int damageAmount, bool isCritical)
        {
            // Dispara o tremor na barra de vida
            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
            }
            shakeCoroutine = StartCoroutine(ShakeRoutine(damageAmount));

            // Instancia o popup de dano flutuante em World Space
            Vector3 spawnPosition = transform.position + offset * 0.85f;
            DamagePopup.Spawn(spawnPosition, damageAmount, isCritical);
        }

        /// <summary>
        /// Tremor da barra de vida com intensidade mínima e máxima proporcional ao dano recebido.
        /// </summary>
        private System.Collections.IEnumerator ShakeRoutine(int damageAmount)
        {
            float damageFactor = Mathf.Clamp01((float)damageAmount / maxDamageRef);
            float intensity = Mathf.Lerp(minShakeIntensity, maxShakeIntensity, damageFactor);
            float elapsed = 0f;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float damp = 1f - (elapsed / shakeDuration);
                float currentAmp = intensity * damp;

                currentShakeOffset = new Vector3(
                    Random.Range(-currentAmp, currentAmp),
                    Random.Range(-currentAmp, currentAmp),
                    0f
                );

                yield return null;
            }

            currentShakeOffset = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (container == null) return;

            // Aplica offset de posição base somado ao tremor atual
            container.transform.localPosition = offset + currentShakeOffset;

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
                health.OnDamageTaken -= HandleDamageTaken;
            }
            if (bgMat != null) Destroy(bgMat);
            if (ghostMat != null) Destroy(ghostMat);
            if (fillMat != null) Destroy(fillMat);
        }
    }
}
