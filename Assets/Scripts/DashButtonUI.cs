using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Mobile dash button with a radial cooldown sweep. Finds the local player itself,
// so it keeps working across respawns without any scene wiring.
public class DashButtonUI : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] Image cooldownFill;
    [SerializeField] Image icon;
    [SerializeField] Text label;

    NetworkPlayerController local;
    float findTimer;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (local != null) local.RequestDash();
    }

    void Update()
    {
        if (local == null)
        {
            findTimer -= Time.deltaTime;
            if (findTimer <= 0f)
            {
                findTimer = 0.5f;
                var obj = NetworkManager.Singleton?.SpawnManager?.GetLocalPlayerObject();
                if (obj != null) local = obj.GetComponent<NetworkPlayerController>();
            }
            SetVisual(0f, false);
            return;
        }

        float n = local.DashCooldownNormalised;
        SetVisual(n, local.DashReady);
    }

    void SetVisual(float normalised, bool ready)
    {
        if (cooldownFill != null)
            cooldownFill.fillAmount = 1f - normalised; // shrinks as it recharges

        if (icon != null)
            icon.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.45f);

        if (label != null)
        {
            label.text = ready ? "DASH" : Mathf.CeilToInt((1f - normalised) * 6f).ToString();
            label.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.7f);
        }

        // Gentle pulse when it comes back up so the player notices.
        transform.localScale = Vector3.one * (ready ? 1f + Mathf.Sin(Time.time * 4f) * 0.04f : 0.94f);
    }
}
