using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ResultsController : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Text headlineText;
    [SerializeField] Text scoreText;
    [SerializeField] Text rewardText;

    [Header("Rewarded ad")]
    [SerializeField] GameObject doubleRewardButton;
    [SerializeField] Text doubleRewardLabel;

    void Start()
    {
        if (panel != null) panel.SetActive(false);

        if (GameSessionState.PendingResults)
            ShowFromSession();
    }

    public void ShowFromSession()
    {
        if (!GameSessionState.PendingResults) return;

        bool isDraw = !GameSessionState.Winner.HasValue;
        bool localWon = !isDraw
            && GameSessionState.LocalTeam.HasValue
            && GameSessionState.LocalTeam.Value == GameSessionState.Winner.Value;

        if (headlineText != null)
            headlineText.text = isDraw ? "Draw" : (localWon ? "Victory!" : "Defeat");

        if (scoreText != null)
            scoreText.text = $"Red {GameSessionState.RedScore}  —  Blue {GameSessionState.BlueScore}";

        RefreshReward();

        if (doubleRewardLabel != null) doubleRewardLabel.text = "WATCH AD\nDOUBLE COINS";
        RefreshDoubleRewardButton();

        if (panel != null) panel.SetActive(true);
    }

    void RefreshReward()
    {
        if (rewardText != null)
            rewardText.text = $"+{GameSessionState.LastCoinReward} coins";
    }

    // Only offer the doubler when it hasn't already been used this match and an ad is
    // actually available, so the player is never shown a button that does nothing.
    void RefreshDoubleRewardButton()
    {
        bool canOffer = !GameSessionState.RewardDoubled
                        && AdService.Instance != null && AdService.Instance.RewardedReady
                        && GameSessionState.LastCoinReward > 0;
        if (doubleRewardButton != null) doubleRewardButton.SetActive(canOffer);
    }

    public void OnDoubleRewardPressed()
    {
        if (GameSessionState.RewardDoubled || AdService.Instance == null) return;

        if (doubleRewardButton != null) doubleRewardButton.SetActive(false);

        AdService.Instance.ShowRewarded(watched =>
        {
            if (!watched)
            {
                // Player bailed or no fill - say so plainly and restore the button rather than
                // leaving it hidden forever; RewardDoubled is still false, so this is a real retry.
                GameFeedUI.Instance.Toast("Ad unavailable - reward unchanged", new Color(1f, 0.7f, 0.4f));
                RefreshDoubleRewardButton();
                return;
            }

            int bonus = GameSessionState.LastCoinReward;
            PlayerDataService.AddCoins(bonus);
            GameSessionState.LastCoinReward += bonus;
            GameSessionState.RewardDoubled = true;

            RefreshReward();
            GameFeedUI.Instance.Banner("COINS DOUBLED!", new Color(1f, 0.88f, 0.3f), 1.2f);
            GameFeedUI.Instance.Toast($"+{bonus} bonus coins", new Color(1f, 0.85f, 0.3f));
        });
    }

    public void OnHomePressed()
    {
        ShutdownNetworkIfNeeded();
        GameSessionState.Clear();
        // Interstitial on the way out of a match, rate-limited inside AdService.
        AdService.Instance?.TryShowInterstitial();
        SceneLoader.LoadScene(SceneLoader.MainMenu);
    }

    public void OnRematchPressed()
    {
        ShutdownNetworkIfNeeded();
        GameSessionState.Clear();
        AdService.Instance?.TryShowInterstitial();
        SceneLoader.LoadScene("Lobby");
    }

    static void ShutdownNetworkIfNeeded()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
    }
}
