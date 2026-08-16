using System;
using UnityEngine;
using UnityEngine.Purchasing;

// Real Unity Purchasing wrapper.
//
// STORE SETUP REQUIRED before any of this transacts:
//   App Store Connect -> My Apps -> In-App Purchases -> create "remove_ads" as a
//   Non-Consumable at the ~$1.99 price tier, plus the two gem consumables. The product
//   ids below must match the App Store ids exactly, and the products must be in at least
//   "Ready to Submit" state or the store returns them as unavailable.
//
// Uses the legacy IStoreListener surface. It is marked [Obsolete] in com.unity.purchasing
// 5.x but is not dead code - it is a supported shim over UnityIAPServices, and it is a
// fraction of the code the new async API needs for a three-product catalog. The pragma is
// file-wide because the obsolete types appear in the class declaration itself.
#pragma warning disable 618, 612

public class IAPService : MonoBehaviour, IDetailedStoreListener
{
    public static IAPService Instance { get; private set; }

    public const string GemPackSmall = "gem_pack_small";
    public const string GemPackLarge = "gem_pack_large";
    public const string RemoveAds = "remove_ads";

    const string AdsRemovedKey = "AdsRemoved";

    /// <summary>
    /// True once the player owns the Remove Ads non-consumable. Cached in a static so
    /// AdService can test it on every interstitial attempt without touching PlayerPrefs.
    /// </summary>
    public static bool AdsRemoved { get; private set; }

    /// <summary>True once the store has returned its catalog, i.e. prices are readable.</summary>
    public static bool StoreReady { get; private set; }

    // Static so UI can subscribe before the service GameObject exists - MainMenuController
    // creates it lazily, and the shop panel may wake up first. Same pattern as PlayerDataService.
    public static event Action OnStoreStateChanged;
    public static event Action<string> OnPurchaseFailedMessage;

    public event Action<int> OnGemsPurchased;

    static bool initializeRequested;

    // Static, not instance fields: the store connection outlives any single GameObject.
    // UnityPurchasing.Initialize is guarded to run once per process (see initializeRequested
    // above), so if the DontDestroyOnLoad object ever gets destroyed and MainMenuController
    // lazily creates a new one, that new instance must still be able to reach the already-
    // initialized controller instead of finding it null forever.
    static IStoreController controller;
    static IAppleExtensions appleExtensions;

    // The entitlement has to be known before the first interstitial can fire, which can happen
    // before this component exists. Seed the cached value at load; the receipt check in
    // OnInitialized is what makes it survive a reinstall.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void SeedCachedEntitlement()
    {
        AdsRemoved = PlayerPrefs.GetInt(AdsRemovedKey, 0) == 1;
        StoreReady = false;
        initializeRequested = false;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializePurchasing();
    }

    void OnDestroy()
    {
        // Only clear the singleton if this was it - the early-out Destroy(gameObject) above
        // for a duplicate instance must not null out the real one's Instance reference.
        if (Instance == this) Instance = null;
    }

    void InitializePurchasing()
    {
        // ConfigurationBuilder is a singleton in IAP 5.x, so a second AddProduct pass throws
        // "Same key". One shot per process.
        if (initializeRequested) return;
        initializeRequested = true;

        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
        builder.AddProduct(GemPackSmall, ProductType.Consumable);
        builder.AddProduct(GemPackLarge, ProductType.Consumable);
        builder.AddProduct(RemoveAds, ProductType.NonConsumable);

        UnityPurchasing.Initialize(this, builder);
    }

    // ---------- store listener ----------

    public void OnInitialized(IStoreController storeController, IExtensionProvider extensions)
    {
        controller = storeController;
        appleExtensions = extensions.GetExtension<IAppleExtensions>();
        StoreReady = true;

        // Re-derive the entitlement from the store rather than trusting PlayerPrefs alone, so a
        // reinstall (or a wiped device) restores Remove Ads without the player having to ask.
        // Deliberately grant-only: a failed receipt refresh or an offline launch must never
        // revoke something the player paid for.
        var removeAds = controller.products.WithID(RemoveAds);
        if (removeAds != null && removeAds.hasReceipt)
            SetAdsRemoved(true);

        OnStoreStateChanged?.Invoke();
    }

    public void OnInitializeFailed(InitializationFailureReason error) => OnInitializeFailed(error, null);

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        StoreReady = false;
        Debug.LogWarning($"[IAPService] Store init failed: {error} - {message}");
        OnStoreStateChanged?.Invoke();
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        // Also the restore path: Apple replays owned non-consumables through here after
        // RestorePurchases(), and IAP replays fetched orders at startup.
        string id = args.purchasedProduct.definition.id;

        switch (id)
        {
            case RemoveAds:
                SetAdsRemoved(true);
                break;
            case GemPackSmall:
                GrantGems(50);
                break;
            case GemPackLarge:
                GrantGems(150);
                break;
            default:
                Debug.LogWarning($"[IAPService] Unrecognised product purchased: {id}");
                // Complete would finish the transaction and consume the receipt with nothing
                // granted - real money, no goods, no recovery. Pending leaves the purchase
                // outstanding so the store retries delivery instead of silently eating it.
                return PurchaseProcessingResult.Pending;
        }

        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        string id = product != null ? product.definition.id : "unknown";
        Debug.LogWarning($"[IAPService] Purchase failed [{id}]: {failureReason}");

        // UserCancelled is not an error worth shouting about - the player made a choice.
        OnPurchaseFailedMessage?.Invoke(
            failureReason == PurchaseFailureReason.UserCancelled ? "" : "Purchase failed");
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        OnPurchaseFailed(product, failureDescription.reason);
    }

    // ---------- purchasing ----------

    public void BuyGemPackSmall() => Purchase(GemPackSmall);
    public void BuyGemPackLarge() => Purchase(GemPackLarge);

    public void BuyRemoveAds()
    {
        if (AdsRemoved) return;
        Purchase(RemoveAds);
    }

    void Purchase(string productId)
    {
        var product = GetProduct(productId);
        if (product == null || !product.availableToPurchase)
        {
            Debug.LogWarning($"[IAPService] Product unavailable: {productId}");
            OnPurchaseFailedMessage?.Invoke("Store unavailable");
            return;
        }

        controller.InitiatePurchase(product);
    }

    /// <summary>
    /// Apple rejects apps that sell non-consumables without a visible restore control, so this
    /// must stay reachable from the UI. Restored purchases come back through ProcessPurchase.
    /// </summary>
    public void RestorePurchases(Action<bool> onComplete = null)
    {
        if (!StoreReady)
        {
            OnPurchaseFailedMessage?.Invoke("Store unavailable");
            onComplete?.Invoke(false);
            return;
        }

        if (appleExtensions != null && Application.platform == RuntimePlatform.IPhonePlayer)
        {
            appleExtensions.RestoreTransactions((success, error) =>
            {
                if (!success)
                {
                    Debug.LogWarning($"[IAPService] Restore failed: {error}");
                    OnPurchaseFailedMessage?.Invoke("Restore failed");
                }

                // Success only means Apple answered; the entitlement itself arrives via
                // ProcessPurchase, which may already have run by the time this fires.
                OnStoreStateChanged?.Invoke();
                onComplete?.Invoke(success);
            });
            return;
        }

        // Everywhere else non-consumables are re-fetched during initialization, so there is
        // nothing to request - just report what we already know.
        var removeAds = GetProduct(RemoveAds);
        if (removeAds != null && removeAds.hasReceipt)
            SetAdsRemoved(true);

        OnStoreStateChanged?.Invoke();
        onComplete?.Invoke(true);
    }

    // ---------- queries ----------

    /// <summary>
    /// Store-localized price string, e.g. "$1.99" / "1,99 €" / "¥300". Null until the catalog
    /// arrives. Never hardcode a price - the same product is a different number and a different
    /// currency in every storefront.
    /// </summary>
    public string GetLocalizedPrice(string productId)
    {
        var product = GetProduct(productId);
        return product?.metadata?.localizedPriceString;
    }

    public bool IsProductAvailable(string productId)
    {
        var product = GetProduct(productId);
        return product != null && product.availableToPurchase;
    }

    Product GetProduct(string productId)
    {
        return controller?.products?.WithID(productId);
    }

    // ---------- grants ----------

    static void SetAdsRemoved(bool value)
    {
        if (AdsRemoved == value) return;

        AdsRemoved = value;
        PlayerPrefs.SetInt(AdsRemovedKey, value ? 1 : 0);
        PlayerPrefs.Save();
        OnStoreStateChanged?.Invoke();
    }

    void GrantGems(int gems)
    {
        PlayerDataService.Gems += gems;
        OnGemsPurchased?.Invoke(gems);
    }
}
