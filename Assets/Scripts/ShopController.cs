using UnityEngine;
using UnityEngine.UI;

public class ShopController : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Text itemNameText;
    [SerializeField] Text priceText;
    [SerializeField] Text statusText;
    [SerializeField] Image previewImage;
    [SerializeField] GameObject buyButton;
    [SerializeField] GameObject equipButton;

    int selectedSkin;

    void OnEnable()
    {
        PlayerDataService.EnsureDefaults();
        PlayerDataService.OnCurrencyChanged += RefreshSelected;
        PlayerDataService.OnLoadoutChanged += RefreshSelected;
        IAPService.OnStoreStateChanged += RefreshRemoveAds;
        IAPService.OnPurchaseFailedMessage += ShowPurchaseFailure;
        SubscribeGemPackEvents();
        SelectSkin(PlayerDataService.EquippedSkinIndex);
        RefreshRemoveAds();
        RefreshGemPacks();
    }

    void OnDisable()
    {
        PlayerDataService.OnCurrencyChanged -= RefreshSelected;
        PlayerDataService.OnLoadoutChanged -= RefreshSelected;
        IAPService.OnStoreStateChanged -= RefreshRemoveAds;
        IAPService.OnPurchaseFailedMessage -= ShowPurchaseFailure;
        if (IAPService.Instance != null) IAPService.Instance.OnGemsPurchased -= OnGemsPurchased;
    }

    public void Show()
    {
        if (panel != null) panel.SetActive(true);
        SubscribeGemPackEvents();
        SelectSkin(PlayerDataService.EquippedSkinIndex);
        RefreshRemoveAds();
        RefreshGemPacks();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void SelectSkin(int skinIndex)
    {
        selectedSkin = Mathf.Clamp(skinIndex, 0, SkinCatalog.SkinCount - 1);
        RefreshSelected();
    }

    void RefreshSelected()
    {
        var entry = SkinCatalog.Get(selectedSkin);
        bool owned = PlayerDataService.OwnsSkin(selectedSkin);
        bool equipped = PlayerDataService.EquippedSkinIndex == selectedSkin;

        if (itemNameText != null) itemNameText.text = entry.Name;
        if (previewImage != null)
        {
            var sprite = SkinCatalog.GetPreviewSprite(selectedSkin);
            previewImage.sprite = sprite;
            previewImage.enabled = sprite != null;
        }

        if (owned)
        {
            if (priceText != null) priceText.text = equipped ? "Equipped" : "Owned";
            if (statusText != null) statusText.text = "";
            if (buyButton != null) buyButton.SetActive(false);
            if (equipButton != null) equipButton.SetActive(!equipped);
        }
        else
        {
            if (entry.GemPrice > 0)
                priceText.text = $"{entry.GemPrice} gems";
            else
                priceText.text = $"{entry.CoinPrice} coins";

            if (statusText != null) statusText.text = "Locked";
            if (buyButton != null) buyButton.SetActive(true);
            if (equipButton != null) equipButton.SetActive(false);
        }
    }

    public void OnBuyPressed()
    {
        var entry = SkinCatalog.Get(selectedSkin);
        if (PlayerDataService.OwnsSkin(selectedSkin)) return;

        bool purchased = false;
        if (entry.GemPrice > 0)
            purchased = PlayerDataService.TrySpendGems(entry.GemPrice);
        else if (entry.CoinPrice > 0)
            purchased = PlayerDataService.TrySpendCoins(entry.CoinPrice);
        else if (entry.Free)
            purchased = true;

        if (!purchased)
        {
            if (statusText != null) statusText.text = "Not enough currency";
            return;
        }

        PlayerDataService.UnlockSkin(selectedSkin);
        PlayerDataService.EquippedSkinIndex = selectedSkin;
        RefreshSelected();
    }

    public void OnEquipPressed()
    {
        PlayerDataService.EquippedSkinIndex = selectedSkin;
        RefreshSelected();
    }

    public void OnClosePressed() => Hide();

    [Header("Rewarded ad")]
    [SerializeField] GameObject watchAdButton;
    [SerializeField] int coinsPerAd = 75;
    [SerializeField] float adButtonPollInterval = 1f;

    float adButtonPollTimer;
    bool watchAdInProgress;

    // Mirrors MenuFreeCoins' poll: RewardedReady can flip true again at any time (a new ad
    // finishes loading), and nothing else tells this panel to look, so without polling the
    // button stays hidden for the rest of the panel's life after one tap while no ad was ready.
    void Update()
    {
        if (watchAdInProgress) return;

        adButtonPollTimer -= Time.unscaledDeltaTime;
        if (adButtonPollTimer > 0f) return;
        adButtonPollTimer = adButtonPollInterval;

        RefreshAdButton();
    }

    // Free-coins-for-an-ad. Deliberately opt-in: nothing here is gated behind an ad,
    // it is only an extra way to earn.
    public void OnWatchAdForCoinsPressed()
    {
        if (AdService.Instance == null)
        {
            if (statusText != null) statusText.text = "Ads unavailable";
            return;
        }

        watchAdInProgress = true;
        if (watchAdButton != null) watchAdButton.SetActive(false);

        AdService.Instance.ShowRewarded(watched =>
        {
            watchAdInProgress = false;

            if (watched)
            {
                PlayerDataService.AddCoins(coinsPerAd);
                if (statusText != null) statusText.text = $"+{coinsPerAd} coins!";
                GameFeedUI.Instance.Toast($"+{coinsPerAd} coins", new Color(1f, 0.85f, 0.3f));
            }
            else if (statusText != null)
            {
                statusText.text = "Ad unavailable";
            }

            RefreshAdButton();
        });
    }

    public void RefreshAdButton()
    {
        if (watchAdButton != null)
            watchAdButton.SetActive(AdService.Instance != null && AdService.Instance.RewardedReady);
    }

    [Header("Remove Ads (IAP)")]
    [SerializeField] GameObject removeAdsButton;
    [SerializeField] Text removeAdsPriceText;
    [SerializeField] GameObject restorePurchasesButton;

    bool removeAdsPending;

    public void RefreshRemoveAds()
    {
        // Apple rejects apps that sell a non-consumable without a visible restore control,
        // so this one is never conditionally hidden the way the ad button is.
        if (restorePurchasesButton != null) restorePurchasesButton.SetActive(true);

        bool owned = IAPService.AdsRemoved;

        // Price comes from the storefront. A hardcoded "$2" is wrong in every non-USD region
        // and wrong again the moment the price tier changes.
        string price = IAPService.Instance != null
            ? IAPService.Instance.GetLocalizedPrice(IAPService.RemoveAds)
            : null;

        if (removeAdsPending && owned)
        {
            removeAdsPending = false;
            if (statusText != null) statusText.text = "Ads removed";
        }

        if (removeAdsPriceText != null)
            removeAdsPriceText.text = owned ? "Owned" : (price ?? "Unavailable");

        // Once owned the entry stays on screen but dead, rather than disappearing - a vanished
        // item reads as a bug, a greyed-out one reads as a receipt.
        var button = removeAdsButton != null ? removeAdsButton.GetComponent<Button>() : null;
        if (button != null) button.interactable = !owned && price != null;
    }

    public void OnRemoveAdsPressed()
    {
        if (IAPService.AdsRemoved) return;

        if (IAPService.Instance == null)
        {
            if (statusText != null) statusText.text = "Store unavailable";
            return;
        }

        removeAdsPending = true;
        if (statusText != null) statusText.text = "Contacting store...";
        IAPService.Instance.BuyRemoveAds();
    }

    public void OnRestorePurchasesPressed()
    {
        if (IAPService.Instance == null)
        {
            if (statusText != null) statusText.text = "Store unavailable";
            return;
        }

        if (statusText != null) statusText.text = "Restoring...";
        IAPService.Instance.RestorePurchases(success =>
        {
            if (statusText != null)
            {
                if (IAPService.AdsRemoved) statusText.text = "Purchases restored";
                else statusText.text = success ? "Nothing to restore" : "Restore failed";
            }

            RefreshRemoveAds();
        });
    }

    // An empty message means the player cancelled, which is not a failure worth reporting -
    // but "Contacting store..." is still sitting on screen from before the purchase, so it
    // has to be cleared either way rather than only overwritten on a real failure message.
    void ShowPurchaseFailure(string message)
    {
        removeAdsPending = false;
        if (statusText != null) statusText.text = message;
        RefreshRemoveAds();
    }

    [Header("Gem Packs (IAP)")]
    [SerializeField] GameObject gemPackSmallButton;
    [SerializeField] Text gemPackSmallPriceText;
    [SerializeField] GameObject gemPackLargeButton;
    [SerializeField] Text gemPackLargePriceText;

    // Gems otherwise have no way in: BuyGemPackSmall/Large on IAPService had no caller at
    // all, so the gem balance could only ever read 0. This is the public surface a scene
    // button can be wired to; Ranger (SkinCatalog id 2) is the matching gem sink.
    public void RefreshGemPacks()
    {
        string smallPrice = IAPService.Instance != null
            ? IAPService.Instance.GetLocalizedPrice(IAPService.GemPackSmall)
            : null;
        string largePrice = IAPService.Instance != null
            ? IAPService.Instance.GetLocalizedPrice(IAPService.GemPackLarge)
            : null;

        if (gemPackSmallPriceText != null) gemPackSmallPriceText.text = smallPrice ?? "Unavailable";
        if (gemPackLargePriceText != null) gemPackLargePriceText.text = largePrice ?? "Unavailable";

        var smallButton = gemPackSmallButton != null ? gemPackSmallButton.GetComponent<Button>() : null;
        if (smallButton != null) smallButton.interactable = smallPrice != null;

        var largeButton = gemPackLargeButton != null ? gemPackLargeButton.GetComponent<Button>() : null;
        if (largeButton != null) largeButton.interactable = largePrice != null;
    }

    public void OnBuyGemPackSmallPressed()
    {
        if (IAPService.Instance == null)
        {
            if (statusText != null) statusText.text = "Store unavailable";
            return;
        }

        if (statusText != null) statusText.text = "Contacting store...";
        IAPService.Instance.BuyGemPackSmall();
    }

    public void OnBuyGemPackLargePressed()
    {
        if (IAPService.Instance == null)
        {
            if (statusText != null) statusText.text = "Store unavailable";
            return;
        }

        if (statusText != null) statusText.text = "Contacting store...";
        IAPService.Instance.BuyGemPackLarge();
    }

    void SubscribeGemPackEvents()
    {
        if (IAPService.Instance == null) return;
        // Idempotent: -= before += so repeated calls (OnEnable and Show can both run) never
        // double-subscribe, without needing a separate "already subscribed" flag.
        IAPService.Instance.OnGemsPurchased -= OnGemsPurchased;
        IAPService.Instance.OnGemsPurchased += OnGemsPurchased;
    }

    void OnGemsPurchased(int amount)
    {
        if (statusText != null) statusText.text = $"+{amount} gems!";
        GameFeedUI.Instance.Toast($"+{amount} gems", new Color(0.6f, 0.85f, 1f));
        RefreshGemPacks();
    }

    public void OnSelectKnight() => SelectSkin(0);
    public void OnSelectNinja() => SelectSkin(1);
    public void OnSelectRanger() => SelectSkin(2);
    // Medic and Commander are not in the catalog yet (placeholder art was removed).
    // Kept as no-ops so any lingering scene button reference doesn't throw; they
    // clamp harmlessly if called. Restore real SelectSkin(3)/(4) when art ships.
    public void OnSelectMedic() { }
    public void OnSelectCommander() { }
}
