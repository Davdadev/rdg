using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] Text coinsText;
    [SerializeField] Text gemsText;
    [SerializeField] Text currencyText;
    [SerializeField] Text playerNameText;
    [SerializeField] Image avatarImage;
    [SerializeField] ShopController shopController;
    [SerializeField] SettingsController settingsController;
    [Tooltip("Optional dedicated profile screen. If left empty, the Player button opens Settings, which already holds the name field.")]
    [SerializeField] ProfileController profileController;

    void Start()
    {
        PlayerDataService.EnsureDefaults();
        EnsureServicesExist();
        RefreshAll();

        PlayerDataService.OnCurrencyChanged += RefreshCurrency;
        PlayerDataService.OnLoadoutChanged += RefreshAvatar;
    }

    void OnDestroy()
    {
        PlayerDataService.OnCurrencyChanged -= RefreshCurrency;
        PlayerDataService.OnLoadoutChanged -= RefreshAvatar;
    }

    void EnsureServicesExist()
    {
        if (FindAnyObjectByType<NetworkBootstrap>() == null)
        {
            var bootstrapPrefab = Resources.Load<GameObject>("NetworkBootstrap");
            if (bootstrapPrefab != null)
                Instantiate(bootstrapPrefab);
        }

        if (FindAnyObjectByType<IAPService>() == null)
        {
            var iap = new GameObject("IAPService");
            iap.AddComponent<IAPService>();
        }

        if (FindAnyObjectByType<AdService>() == null)
        {
            var ads = new GameObject("AdService");
            ads.AddComponent<AdService>();
        }
    }

    void RefreshAll()
    {
        RefreshCurrency();
        RefreshAvatar();
        if (playerNameText != null)
            playerNameText.text = PlayerDataService.PlayerName;
    }

    void RefreshCurrency()
    {
        string coins = PlayerDataService.Coins.ToString();
        if (coinsText != null) coinsText.text = coins;
        if (currencyText != null) currencyText.text = $"◈  {coins}";
        if (gemsText != null) gemsText.text = PlayerDataService.Gems.ToString();
    }

    void RefreshAvatar()
    {
        if (avatarImage == null) return;
        var sprite = SkinCatalog.GetPreviewSprite(PlayerDataService.EquippedSkinIndex);
        avatarImage.sprite = sprite;
        avatarImage.enabled = sprite != null;
    }

    public void OnPlayPressed()
    {
        SceneLoader.LoadScene("Lobby");
    }

    public void OnShopPressed()
    {
        if (shopController != null)
            shopController.Show();
    }

    public void OnSettingsPressed()
    {
        if (settingsController != null)
            settingsController.Show();
    }

    // Wired to the menu's Player button. Opens the profile screen if one is
    // assigned; otherwise lazily creates a code-built one, then falls back to
    // Settings only if profile creation somehow fails.
    public void OnPlayerPressed()
    {
        if (profileController == null)
        {
            profileController = FindAnyObjectByType<ProfileController>();
            if (profileController == null)
            {
                var go = new GameObject("ProfileController");
                profileController = go.AddComponent<ProfileController>();
            }
        }

        if (profileController != null)
            profileController.Show();
        else if (settingsController != null)
            settingsController.Show();
    }
}
