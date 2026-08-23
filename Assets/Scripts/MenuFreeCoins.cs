using UnityEngine;
using UnityEngine.UI;

// Rewarded-ad entry point on the main menu.
//
// The button hides itself unless an ad is actually loaded, so it is never a control
// that does nothing when tapped. With no Game ID configured the service never
// initialises, the button stays hidden, and the menu is unaffected.
public class MenuFreeCoins : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] int coinsPerAd = 75;
    [SerializeField] float pollInterval = 1f;

    float timer;
    bool busy;
    CanvasGroup group;

    void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(Show);

        // Hide via CanvasGroup, never SetActive: deactivating our own GameObject
        // stops Update() and the button can then never re-appear.
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        SetVisible(false);
    }

    void Update()
    {
        if (busy) return;
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = pollInterval;

        SetVisible(AdService.Instance != null && AdService.Instance.RewardedReady);
    }

    void SetVisible(bool v)
    {
        if (group == null) return;
        group.alpha = v ? 1f : 0f;
        group.interactable = v;
        group.blocksRaycasts = v;
    }

    void Show()
    {
        if (AdService.Instance == null || busy) return;
        busy = true;
        SetVisible(false);

        AdService.Instance.ShowRewarded(watched =>
        {
            busy = false;
            if (watched)
            {
                PlayerDataService.GrantCoins(coinsPerAd);
                GameFeedUI.Instance.Toast($"+{coinsPerAd} coins", GameUI.Coin);
            }
            else
            {
                GameFeedUI.Instance.Toast("Ad unavailable", new Color(1f, 0.7f, 0.4f));
            }
        });
    }
}
