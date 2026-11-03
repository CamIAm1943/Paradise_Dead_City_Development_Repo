using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Status_Icon_UI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image Icon_Image;

    [Header("Hover")]
    [SerializeField] private float Hover_Scale = 1.35f;
    [SerializeField] private float Hover_Y_Offset = 8f;
    [SerializeField] private float Hover_Lerp_Speed = 14f;
    [SerializeField] private bool Bring_To_Front_On_Hover = true;

    [Header("Tooltip Timing")]
    [Tooltip("Extra delay in seconds after the icon finishes its scale-up " +
             "animation before the tooltip appears. Set to 0 for an " +
             "immediate pop the instant the icon is fully grown.")]
    [SerializeField] private float Tooltip_Show_Delay = 0.1f;

    private RectTransform RT;
    private Vector2 Base_Position;
    private int Base_Sibling_Index;
    private bool Base_Sibling_Recorded;
    private bool Hover_Active;
    private float Hover_T;

    private Status_Definition_SO Current_Definition;
    private Status_Tooltip_UI Tooltip;

    private bool Tooltip_Pending;
    private float Hover_Elapsed;

    private void Awake()
    {
        RT = (RectTransform)transform;
    }

    public void Set_Tooltip(Status_Tooltip_UI tooltip)
    {
        Tooltip = tooltip;
    }

    public void Set_From_Definition(Status_Definition_SO Definition)
    {
        Current_Definition = Definition;

        if (Icon_Image == null) return;

        if (Definition == null)
        {
            Icon_Image.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            return;
        }

        if (Definition.Icon != null)
        {
            Icon_Image.sprite = Definition.Icon;
            Icon_Image.color = Definition.Tint_Icon ? Definition.Fallback_Color : Color.white;
        }
        else
        {
            Icon_Image.color = Definition.Fallback_Color;
        }
    }

    public void Set_Base_Position(Vector2 Position)
    {
        Base_Position = Position;

        if (!Hover_Active)
            RT.anchoredPosition = Position;

        if (!Base_Sibling_Recorded)
        {
            Hover_T = 0f;
            RT.localScale = Vector3.one;
        }
    }

    public void Record_Base_Sibling_Index()
    {
        Base_Sibling_Index = transform.GetSiblingIndex();
        Base_Sibling_Recorded = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Hover_Active = true;
        Hover_Elapsed = 0f;

        if (Bring_To_Front_On_Hover)
            transform.SetAsLastSibling();

        // Arm the tooltip. It fires from Update once the icon has finished
        // scaling and the extra delay has elapsed.
        if (Tooltip != null && Current_Definition != null)
            Tooltip_Pending = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Hover_Active = false;
        Tooltip_Pending = false;

        if (Bring_To_Front_On_Hover && Base_Sibling_Recorded)
            transform.SetSiblingIndex(Base_Sibling_Index);

        if (Tooltip != null)
            Tooltip.Hide();
    }

    private void OnDisable()
    {
        if (Tooltip != null)
            Tooltip.Hide();

        Hover_Active = false;
        Tooltip_Pending = false;
        Hover_T = 0f;
        Hover_Elapsed = 0f;

        if (RT != null)
        {
            RT.localScale = Vector3.one;
            RT.anchoredPosition = Base_Position;
        }
    }

    private void Update()
    {
        float target = Hover_Active ? 1f : 0f;
        Hover_T = Mathf.MoveTowards(Hover_T, target, Time.unscaledDeltaTime * Hover_Lerp_Speed);

        float scale = Mathf.Lerp(1f, Hover_Scale, Hover_T);
        RT.localScale = new Vector3(scale, scale, 1f);

        Vector2 pos = Base_Position + new Vector2(0f, Hover_Y_Offset * Hover_T);
        RT.anchoredPosition = pos;

        if (Tooltip_Pending)
        {
            Hover_Elapsed += Time.unscaledDeltaTime;

            // The scale-up is complete when Hover_T reaches 1. The extra
            // Hover_Elapsed gate gives the tooltip a beat of separation
            // from the icon's animation, matching the "phases in" feel.
            // Reading the corners at this moment captures the icon's
            // final-size pose, so the tooltip anchors against where the
            // icon ends up, not where it started.
            if (Hover_T >= 1f && Hover_Elapsed >= Tooltip_Show_Delay)
            {
                Tooltip_Pending = false;
                Tooltip.Show(Current_Definition, RT);
            }
        }
    }
}