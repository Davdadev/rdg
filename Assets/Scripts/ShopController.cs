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
        SelectSkin(PlayerDataService.EquippedSkinIndex);
        RefreshRemoveAds();
    }

    void OnDisable()
    {
        PlayerDataService.OnCurrencyChanged -= RefreshSelected;
        PlayerDataService.OnLoadoutChanged -= RefreshSelected;
        IAPService.OnStoreStateChanged -= RefreshRemoveAds;
        IAPService.OnPurchaseFailedMessage -= ShowPurchaseFailure;
    }

    public void Show()
    {
        if (panel != null) panel.SetActive(true);
        SelectSkin(PlayerDataService.EquippedSkinIndex);
        RefreshRemoveAds();
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

    // Free-coins-for-an-ad. Deliberately opt-in: nothing here is gated behind an ad,
    // it is only an extra way to earn.
    public void OnWatchAdForCoinsPressed()
    {
        if (AdService.Instance == null)
        {
            if (statusText != null) statusText.text = "Ads unavailable";
            return;
        }

        if (watchAdButton != null) watchAdButton.SetActive(false);

        AdService.Instance.ShowRewarded(watched =>
        {
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
    // but the status label (e.g. "Contacting store...") still has to clear, so write it
    // unconditionally rather than only on a non-empty message.
    void ShowPurchaseFailure(string message)
    {
        removeAdsPending = false;
        if (statusText != null) statusText.text = message;
        RefreshRemoveAds();
    }

    public void OnBuyGemPackSmallPressed() => BuyGemPack(IAPService.GemPackSmall);
    public void OnBuyGemPackLargePressed() => BuyGemPack(IAPService.GemPackLarge);

    // Consumables have no entitlement to track, so unlike RemoveAds there is no pending
    // latch here - GrantGems raises PlayerDataService.OnCurrencyChanged, which RefreshSelected
    // is already subscribed to, so the status text gets overwritten with the post-purchase
    // state on its own. A cancel/failure still routes through ShowPurchaseFailure above.
    void BuyGemPack(string productId)
    {
        if (IAPService.Instance == null)
        {
            if (statusText != null) statusText.text = "Store unavailable";
            return;
        }

        if (statusText != null) statusText.text = "Contacting store...";

        if (productId == IAPService.GemPackSmall) IAPService.Instance.BuyGemPackSmall();
        else IAPService.Instance.BuyGemPackLarge();
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
