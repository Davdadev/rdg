using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class LoadingController : MonoBehaviour
{
    [SerializeField] private Slider progressBar;
    [SerializeField] private Text tipText;

    private static readonly string[] Tips = new string[]
    {
        "Tip: Grab the enemy flag and get it back to your base to score!",
        "Tip: Watch your surroundings, obstacles can block line of sight.",
        "Tip: Visit the Shop to unlock new skins and upgrades."
    };

    [SerializeField] private float minimumDisplaySeconds = 0.35f;

    void Start()
    {
        if (tipText != null)
            tipText.text = Tips[Random.Range(0, Tips.Length)];

        string target = string.IsNullOrEmpty(SceneLoader.TargetScene) ? "MainMenu" : SceneLoader.TargetScene;
        StartCoroutine(LoadAsync(target));
    }

    IEnumerator LoadAsync(string sceneName)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        float startedAt = Time.unscaledTime;

        while (op.progress < 0.9f)
        {
            if (progressBar != null)
                progressBar.value = Mathf.Clamp01(op.progress / 0.9f);
            yield return null;
        }

        if (progressBar != null) progressBar.value = 1f;
        float remainingHold = minimumDisplaySeconds - (Time.unscaledTime - startedAt);
        if (remainingHold > 0f)
            yield return new WaitForSecondsRealtime(remainingHold);
        op.allowSceneActivation = true;
    }
}
