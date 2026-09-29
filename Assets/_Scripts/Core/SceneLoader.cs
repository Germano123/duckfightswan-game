using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuckFightSwan.Core
{
    /// <summary>
    /// SceneLoader abstrai e centraliza o carregamento de cenas na Unity.
    /// Respeita o SRP ao conter apenas lógica de carregamento.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Carrega uma cena pelo nome.
        /// </summary>
        public void LoadScene(string sceneName)
        {
            Debug.Log($"[SceneLoader] Carregando cena: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// Carrega uma cena pelo índice de compilação.
        /// </summary>
        public void LoadScene(int sceneBuildIndex)
        {
            Debug.Log($"[SceneLoader] Carregando cena de índice: {sceneBuildIndex}");
            SceneManager.LoadScene(sceneBuildIndex);
        }
    }
}
