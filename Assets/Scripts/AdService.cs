using System;
using UnityEngine;
using UnityEngine.Advertisements;

// Central wrapper around Unity Ads.
//
// SETUP REQUIRED before ads serve real fill:
//   1. Unity Dashboard -> Monetization -> enable Unity Ads for this project.
//   2. Copy the iOS and Android Game IDs into the fields below (or set them in the
//      inspector on the NetworkBootstrap prefab).
//   3. Create placements named to match RewardedPlacement / InterstitialPlacement.
// Until then testMode stays on and Unity serves test ads, which is the correct
// behaviour for development - shipping with testMode on earns nothing.
public class AdService : MonoBehaviour, IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
{
    public static AdService Instance { get; private set; }

    [Header("Unity Dashboard Game IDs")]
    [SerializeField] string iosGameId = "";
    [SerializeField] string androidGameId = "";

    [Tooltip("Leave on until the store listing is live. Never ship enabled.")]
    [SerializeField] bool testMode = true;

    [Header("Placements")]
    [SerializeField] string rewardedPlacementIOS = "Rewarded_iOS";
    [SerializeField] string rewardedPlacementAndroid = "Rewarded_Android";
    [SerializeField] string interstitialPlacementIOS = "Interstitial_iOS";
    [SerializeField] string interstitialPlacementAndroid = "Interstitial_Android";

    [Header("Pacing")]
    [Tooltip("Minimum seconds between interstitials so we don't hammer the player.")]
    [SerializeField] float interstitialCooldown = 120f;

    public bool IsInitialized { get; private set; }
    public bool RewardedReady { get; private set; }
    public bool InterstitialReady { get; private set; }

    string RewardedPlacement =>
        Application.platform == RuntimePlatform.Android ? rewardedPlacementAndroid : rewardedPlacementIOS;
    string InterstitialPlacement =>
        Application.platform == RuntimePlatform.Android ? interstitialPlacementAndroid : interstitialPlacementIOS;

    Action<bool> pendingRewardCallback;   // true = watched to completion
    float lastInterstitialTime = -999f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Initialize();
    }

    void Initialize()
    {
        string gameId = Application.platform == RuntimePlatform.Android ? androidGameId : iosGameId;

        if (string.IsNullOrWhiteSpace(gameId))
        {
            // No dashboard IDs yet. Stay unitialized rather than throwing - callers
            // fall back to the no-ad path so gameplay is never blocked.
            Debug.LogWarning("[AdService] No Game ID configured; ads disabled. Set them in the Unity Dashboard and on this component.");
            return;
        }

        if (!Advertisement.isSupported)
        {
            Debug.Log("[AdService] Ads not supported on this platform (editor/standalone) - running without ads.");
            return;
        }

        Advertisement.Initialize(gameId, testMode, this);
    }

    // ---------- initialization listener ----------

    public void OnInitializationComplete()
    {
        IsInitialized = true;
        Debug.Log("[AdService] Initialized.");
        LoadRewarded();
        LoadInterstitial();
    }

    public void OnInitializationFailed(UnityAdsInitializationError error, string message)
    {
        IsInitialized = false;
        Debug.LogWarning($"[AdService] Init failed: {error} - {message}");
    }

    // ---------- loading ----------

    public void LoadRewarded()
    {
        if (!IsInitialized) return;
        RewardedReady = false;
        Advertisement.Load(RewardedPlacement, this);
    }

    public void LoadInterstitial()
    {
        if (!IsInitialized) return;
        InterstitialReady = false;
        Advertisement.Load(InterstitialPlacement, this);
    }

    public void OnUnityAdsAdLoaded(string placementId)
    {
        if (placementId == RewardedPlacement) RewardedReady = true;
        else if (placementId == InterstitialPlacement) InterstitialReady = true;
    }

    public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
    {
        Debug.LogWarning($"[AdService] Load failed [{placementId}]: {error} - {message}");
        if (placementId == RewardedPlacement) RewardedReady = false;
        else if (placementId == InterstitialPlacement) InterstitialReady = false;
    }

    // ---------- showing ----------

    /// <summary>
    /// Shows a rewarded ad. The callback always fires exactly once: true only if the
    /// player actually watched it through. If no ad is available the callback fires
    /// false immediately so the caller can just carry on.
    /// </summary>
    public void ShowRewarded(Action<bool> onFinished)
    {
        if (!IsInitialized || !RewardedReady)
        {
            onFinished?.Invoke(false);
            LoadRewarded();
            return;
        }

        pendingRewardCallback = onFinished;
        Advertisement.Show(RewardedPlacement, this);
    }

    /// <summary>Shows an interstitial if one is ready and the cooldown has elapsed.</summary>
    public bool TryShowInterstitial()
    {
        if (!IsInitialized || !InterstitialReady) return false;
        if (Time.unscaledTime - lastInterstitialTime < interstitialCooldown) return false;

        lastInterstitialTime = Time.unscaledTime;
        Advertisement.Show(InterstitialPlacement, this);
        return true;
    }

    public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState state)
    {
        if (placementId == RewardedPlacement)
        {
            bool earned = state == UnityAdsShowCompletionState.COMPLETED;
            var cb = pendingRewardCallback;
            pendingRewardCallback = null;
            cb?.Invoke(earned);
            LoadRewarded();
        }
        else if (placementId == InterstitialPlacement)
        {
            LoadInterstitial();
        }
    }

    public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
    {
        Debug.LogWarning($"[AdService] Show failed [{placementId}]: {error} - {message}");
        if (placementId == RewardedPlacement)
        {
            var cb = pendingRewardCallback;
            pendingRewardCallback = null;
            cb?.Invoke(false);
            LoadRewarded();
        }
    }

    public void OnUnityAdsShowStart(string placementId) { }
    public void OnUnityAdsShowClick(string placementId) { }
}
