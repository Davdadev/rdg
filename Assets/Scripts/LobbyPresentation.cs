using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only motion for the matchmaking lobby.
/// Network state remains owned by MatchmakingService and LobbyController.
/// </summary>
public sealed class LobbyPresentation : MonoBehaviour
{
    [SerializeField] private RectTransform searchGlyph;
    [SerializeField] private Text elapsedText;

    private float startedAt;

    private void OnEnable()
    {
        startedAt = Time.unscaledTime;
    }

    private void Update()
    {
        if (searchGlyph != null)
            searchGlyph.Rotate(0f, 0f, -180f * Time.unscaledDeltaTime);

        if (elapsedText != null)
        {
            int elapsed = Mathf.FloorToInt(Time.unscaledTime - startedAt);
            elapsedText.text = $"SEARCHING  •  {elapsed / 60:00}:{elapsed % 60:00}";
        }
    }
}
