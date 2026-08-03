using UnityEngine;
using UnityEngine.SceneManagement;

// Generic scene transition helper: call SceneLoader.LoadScene("SceneName")
// from anywhere (menu buttons, match-end screen, etc) and it routes through
// the LoadingScreen scene with a real progress bar.
public static class SceneLoader
{
    public static string TargetScene { get; private set; }

    public static void LoadScene(string sceneName)
    {
        TargetScene = sceneName;
        SceneManager.LoadScene("LoadingScreen");
    }
}
