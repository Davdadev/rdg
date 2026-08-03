using System;
using UnityEngine;

// Lightweight IAP wrapper. Wire product IDs in the Unity Dashboard (Services > IAP)
// and replace the dev stubs below with Unity Purchasing callbacks when store is configured.
public class IAPService : MonoBehaviour
{
    public static IAPService Instance { get; private set; }

    public const string GemPackSmall = "gem_pack_small";
    public const string GemPackLarge = "gem_pack_large";

    public event Action<int> OnGemsPurchased;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void BuyGemPackSmall() => GrantGemPack(50, GemPackSmall);
    public void BuyGemPackLarge() => GrantGemPack(150, GemPackLarge);

    void GrantGemPack(int gems, string productId)
    {
        Debug.Log($"IAP purchase requested: {productId}. Granting {gems} gems (dev stub until store is linked).");
        PlayerDataService.Gems += gems;
        OnGemsPurchased?.Invoke(gems);
    }
}
