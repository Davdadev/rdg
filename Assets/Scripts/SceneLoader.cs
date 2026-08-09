using UnityEngine;
using UnityEngine.SceneManagement;

// Generic scene transition helper: call SceneLoader.LoadScene("SceneName")
// from anywhere (menu buttons, match-end screen, etc) and it routes through
// the LoadingScreen scene with a real progress bar.
public static class SceneLoader
{
    // Named once so swapping the menu scene is a one-line change rather than a hunt through
    // every screen that offers a "back to menu" button.
    public const string MainMenu = "MenuFigma";

    public static string TargetScene { get; private set; }

    public static void LoadScene(string sceneName)
    {
        TargetScene = sceneName;
        SceneManager.LoadScene("LoadingScreen");
    }
}
