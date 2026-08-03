using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Lightweight, reusable feedback for uGUI menu buttons.
/// </summary>
public sealed class MenuButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private float focusedScale = 1.04f;
    [SerializeField] private float pressedScale = 0.97f;
    [SerializeField] private float transitionSpeed = 14f;

    private Vector3 defaultScale;
    private bool isFocused;
    private bool isPressed;

    private void Awake()
    {
        defaultScale = transform.localScale;
    }

    private void OnEnable()
    {
        isFocused = false;
        isPressed = false;
        transform.localScale = defaultScale;
    }

    private void Update()
    {
        float multiplier = isPressed ? pressedScale : isFocused ? focusedScale : 1f;
        Vector3 target = defaultScale * multiplier;
        transform.localScale = Vector3.Lerp(transform.localScale, target, transitionSpeed * Time.unscaledDeltaTime);
    }

    private void OnDisable()
    {
        transform.localScale = defaultScale;
    }

    public void OnPointerEnter(PointerEventData eventData) => isFocused = true;
    public void OnPointerExit(PointerEventData eventData) => isFocused = false;
    public void OnPointerDown(PointerEventData eventData) => isPressed = true;
    public void OnPointerUp(PointerEventData eventData) => isPressed = false;
    public void OnSelect(BaseEventData eventData) => isFocused = true;
    public void OnDeselect(BaseEventData eventData) => isFocused = false;
}
