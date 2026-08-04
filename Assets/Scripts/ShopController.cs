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
        SelectSkin(PlayerDataService.EquippedSkinIndex);
    }

    void OnDisable()
    {
        PlayerDataService.OnCurrencyChanged -= RefreshSelected;
        PlayerDataService.OnLoadoutChanged -= RefreshSelected;
    }

    public void Show()
    {
        if (panel != null) panel.SetActive(true);
        SelectSkin(PlayerDataService.EquippedSkinIndex);
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
                PlayerDataService.Coins += coinsPerAd;
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

    public void OnSelectKnight() => SelectSkin(0);
    public void OnSelectNinja() => SelectSkin(1);
    public void OnSelectRanger() => SelectSkin(2);
    public void OnSelectMedic() => SelectSkin(3);
    public void OnSelectCommander() => SelectSkin(4);
}
