using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ResultsController : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Text headlineText;
    [SerializeField] Text scoreText;
    [SerializeField] Text rewardText;

    void Start()
    {
        if (panel != null) panel.SetActive(false);

        if (GameSessionState.PendingResults)
            ShowFromSession();
    }

    public void ShowFromSession()
    {
        if (!GameSessionState.Winner.HasValue) return;

        Team winner = GameSessionState.Winner.Value;
        bool localWon = GameSessionState.LocalTeam.HasValue && GameSessionState.LocalTeam.Value == winner;

        if (headlineText != null)
            headlineText.text = localWon ? "Victory!" : "Defeat";

        if (scoreText != null)
            scoreText.text = $"Red {GameSessionState.RedScore}  —  Blue {GameSessionState.BlueScore}";

        if (rewardText != null)
            rewardText.text = $"+{GameSessionState.LastCoinReward} coins";

        if (panel != null) panel.SetActive(true);
    }

    public void OnHomePressed()
    {
        ShutdownNetworkIfNeeded();
        GameSessionState.Clear();
        SceneLoader.LoadScene("MainMenu");
    }

    public void OnRematchPressed()
    {
        ShutdownNetworkIfNeeded();
        GameSessionState.Clear();
        SceneLoader.LoadScene("Lobby");
    }

    static void ShutdownNetworkIfNeeded()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
    }
}
