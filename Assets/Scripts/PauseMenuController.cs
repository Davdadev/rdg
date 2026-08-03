using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] GameObject settingsPanel;

    bool isPaused;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        if (panel != null) panel.SetActive(isPaused);
        if (!isPaused && settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void OnResumePressed()
    {
        isPaused = false;
        if (panel != null) panel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void OnSettingsPressed()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void OnForfeitPressed()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        GameSessionState.Clear();
        Time.timeScale = 1f;
        SceneLoader.LoadScene("MainMenu");
    }
}
