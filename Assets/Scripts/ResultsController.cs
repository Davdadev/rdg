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

        // Only offer the doubler when an ad is actually available and it hasn't already been
        // used against this result, so the player is never shown a button that does nothing
        // or that would double an already-doubled reward.
        bool canOffer = AdService.Instance != null && AdService.Instance.RewardedReady
                        && GameSessionState.LastCoinReward > 0 && !GameSessionState.RewardDoubled;
        if (doubleRewardButton != null) doubleRewardButton.SetActive(canOffer);
        if (doubleRewardLabel != null) doubleRewardLabel.text = "WATCH AD\nDOUBLE COINS";

        if (panel != null) panel.SetActive(true);
    }

    void RefreshReward()
    {
        if (rewardText != null)
            rewardText.text = $"+{GameSessionState.LastCoinReward} coins";
    }

    public void OnDoubleRewardPressed()
    {
        if (GameSessionState.RewardDoubled || AdService.Instance == null) return;

        if (doubleRewardButton != null) doubleRewardButton.SetActive(false);

        AdService.Instance.ShowRewarded(watched =>
        {
            if (!watched)
            {
                // Player bailed or no fill - restore the button so they can retry, no penalty.
                if (doubleRewardButton != null) doubleRewardButton.SetActive(true);
                GameFeedUI.Instance.Toast("Ad unavailable - reward unchanged", new Color(1f, 0.7f, 0.4f));
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
