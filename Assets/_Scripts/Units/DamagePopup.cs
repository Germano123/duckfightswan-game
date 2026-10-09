using UnityEngine;
using System.Collections;

namespace DuckFightSwan.Units
{
    /// <summary>
    /// Componente de texto flutuante em World Space para exibição dinâmica de dano e acerto crítico.
    /// Respeita o SRP ao lidar exclusivamente com a renderização visual e animação do dano sofrido.
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        [Header("Configurações de Legibilidade e Escala")]
        [SerializeField] private float minScale = 1.0f;
        [SerializeField] private float maxScale = 2.2f;
        [SerializeField] private float maxDamageRef = 35f;

        [Header("Tempos da Animação")]
        [SerializeField] private float floatDistance = 0.9f;
        [SerializeField] private float fadeInDuration = 0.35f;
        [SerializeField] private float holdDuration = 0.30f;
        [SerializeField] private float fadeOutDuration = 0.30f;

        private TextMesh damageTextMesh;
        private TextMesh critTextMesh;
        private Color baseColor;

        /// <summary>
        /// Método de fábrica para instanciar e iniciar o popup de dano no mundo.
        /// </summary>
        public static DamagePopup Spawn(Vector3 worldPosition, int damageAmount, bool isCritical)
        {
            GameObject popupObj = new GameObject("DamagePopup");

            // Leve dispersão aleatória para evitar sobreposição caso múltiplos danos ocorram
            Vector3 randomOffset = new Vector3(
                Random.Range(-0.15f, 0.15f),
                Random.Range(0.05f, 0.20f),
                Random.Range(-0.15f, 0.15f)
            );

            popupObj.transform.position = worldPosition + randomOffset;

            DamagePopup popup = popupObj.AddComponent<DamagePopup>();
            popup.Initialize(damageAmount, isCritical);
            return popup;
        }

        public void Initialize(int damageAmount, bool isCritical)
        {
            // Define a cor base: amarelada/dourada para crítico, branca de alto contraste para normal
            baseColor = isCritical 
                ? new Color(1.0f, 0.88f, 0.15f, 1f) 
                : new Color(1.0f, 1.0f, 1.0f, 1f);

            // Calcula escala proporcional com mínimo legível garantido
            float damageFactor = Mathf.Clamp01((float)damageAmount / maxDamageRef);
            float calculatedScale = Mathf.Lerp(minScale, maxScale, damageFactor);
            if (isCritical)
            {
                calculatedScale *= 1.2f;
            }
            transform.localScale = Vector3.one * calculatedScale;

            // Cria o TextMesh do valor de dano
            GameObject damageTextObj = new GameObject("DamageNumber");
            damageTextObj.transform.SetParent(transform);
            damageTextObj.transform.localPosition = Vector3.zero;

            damageTextMesh = damageTextObj.AddComponent<TextMesh>();
            damageTextMesh.text = damageAmount.ToString();
            damageTextMesh.characterSize = 0.08f;
            damageTextMesh.fontSize = 32;
            damageTextMesh.fontStyle = FontStyle.Bold;
            damageTextMesh.alignment = TextAlignment.Center;
            damageTextMesh.anchor = TextAnchor.MiddleCenter;
            damageTextMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f); // Inicia transparente (fade-in)

            // Se for ataque crítico, cria o segundo texto "Critical" acima do dano
            if (isCritical)
            {
                GameObject critTextObj = new GameObject("CriticalLabel");
                critTextObj.transform.SetParent(transform);
                critTextObj.transform.localPosition = new Vector3(0f, 0.35f, 0f);

                critTextMesh = critTextObj.AddComponent<TextMesh>();
                critTextMesh.text = "Critical";
                critTextMesh.characterSize = 0.06f;
                critTextMesh.fontSize = 26;
                critTextMesh.fontStyle = FontStyle.Bold;
                critTextMesh.alignment = TextAlignment.Center;
                critTextMesh.anchor = TextAnchor.MiddleCenter;
                critTextMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            }

            // Inicia o ciclo de vida e animação
            StartCoroutine(AnimatePopup());
        }

        private IEnumerator AnimatePopup()
        {
            Vector3 startPos = transform.position;
            Vector3 maxPos = startPos + Vector3.up * floatDistance;

            // FASE 1: Subida até a altura máxima com FADE-IN gradual (alpha 0 -> 1)
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInDuration);

                // Interpolação suave de posição até a altura máxima
                transform.position = Vector3.Lerp(startPos, maxPos, Mathf.SmoothStep(0f, 1f, t));

                // Alpha atinge 1.0 exatamente ao chegar na altura máxima
                SetAlpha(t);
                yield return null;
            }

            // Garante posição exata no ápice e 100% de visibilidade
            transform.position = maxPos;
            SetAlpha(1.0f);

            // FASE 2: Pausa no ápice para leitura nítida do dano e do rótulo "Critical"
            yield return new WaitForSeconds(holdDuration);

            // FASE 3: Fade-Out suave com leve flutuação adicional antes de sumir
            Vector3 endPos = maxPos + Vector3.up * 0.25f;
            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);

                transform.position = Vector3.Lerp(maxPos, endPos, t);
                SetAlpha(1.0f - t);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void SetAlpha(float alpha)
        {
            if (damageTextMesh != null)
            {
                damageTextMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }

            if (critTextMesh != null)
            {
                critTextMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }
        }

        private void LateUpdate()
        {
            // Billboard effect: sempre direcionado de frente para a câmera
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = cam.transform.rotation;
            }
        }
    }
}
