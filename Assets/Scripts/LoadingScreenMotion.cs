using UnityEngine;

/// <summary>
/// Keeps loading presentation alive while asynchronous scene work is in progress.
/// </summary>
public sealed class LoadingScreenMotion : MonoBehaviour
{
    [SerializeField] private RectTransform logo;
    [SerializeField] private float pulseAmount = 0.035f;
    [SerializeField] private float pulseSpeed = 2.2f;

    private Vector3 defaultScale;

    private void Awake()
    {
        if (logo != null)
            defaultScale = logo.localScale;
    }

    private void Update()
    {
        if (logo == null)
            return;

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
        logo.localScale = defaultScale * pulse;
    }

    private void OnDisable()
    {
        if (logo != null)
            logo.localScale = defaultScale;
    }
}
