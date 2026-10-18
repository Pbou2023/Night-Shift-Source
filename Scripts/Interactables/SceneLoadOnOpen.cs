using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadOnOpen : MonoBehaviour
{
    public string sceneName;

    public void Load()
    {
        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
    }
}
