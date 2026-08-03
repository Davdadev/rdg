using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public static VirtualJoystick Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;

    private Vector2 inputVector;
    private float joystickRadius;

    public Vector2 InputDirection => inputVector;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (background == null) background = GetComponent<RectTransform>();
        joystickRadius = background.sizeDelta.x / 2f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            // Normalize coordinates to stay relative to center
            float x = localPoint.x / joystickRadius;
            float y = localPoint.y / joystickRadius;

            inputVector = new Vector2(x, y);
            if (inputVector.sqrMagnitude > 1f)
            {
                inputVector.Normalize();
            }

            // Move the visual handle knob
            handle.anchoredPosition = new Vector2(inputVector.x * joystickRadius, inputVector.y * joystickRadius);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        inputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}