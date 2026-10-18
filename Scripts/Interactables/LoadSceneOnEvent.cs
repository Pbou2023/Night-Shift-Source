using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneOnEvent : MonoBehaviour
{
    [SerializeField] private string sceneName = "GameOver";
    public void Load() => SceneManager.LoadScene(sceneName);
    public void ReloadCurrent() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
}
