using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Status_Row_UI : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private Status_Icon_UI Icon_Prefab;

    [Header("Tooltip")]
    [Tooltip("Shared tooltip that appears above whichever icon is hovered. " +
             "Optional — leave null for no tooltip.")]
    [SerializeField] private Status_Tooltip_UI Tooltip;

    [Header("Layout (normalized to the row's own rect)")]
    [Range(0.1f, 1f)]
    [SerializeField] private float Icon_Height_Fraction = 0.8f;

    [Range(0f, 0.4f)]
    [SerializeField] private float Padding_Left_Fraction = 0.05f;

    [Range(0f, 0.4f)]
    [SerializeField] private float Padding_Right_Fraction = 0.05f;

    [Range(0f, 0.4f)]
    [SerializeField] private float Padding_Top_Fraction = 0.1f;

    [Range(0f, 0.2f)]
    [SerializeField] private float Spacing_Fraction = 0.02f;

    [SerializeField] private int Overlap_Threshold = 3;

    [Range(0.3f, 1f)]
    [SerializeField] private float Overlap_Step_Fraction = 0.7f;

    [SerializeField] private int Max_Icons = 8;

    private readonly List<Status_Icon_UI> Pool = new List<Status_Icon_UI>();
    private bool Layout_Dirty;
    private int Pending_Count;

    public void Show_Statuses(IReadOnlyList<Status_Instance> Statuses)
    {
        if (Icon_Prefab == null)
        {
            Debug.LogWarning("Status_Row_UI: Icon_Prefab is not assigned.");
            return;
        }

        int Wanted = Statuses != null ? Mathf.Min(Statuses.Count, Max_Icons) : 0;

        while (Pool.Count < Wanted)
        {
            Status_Icon_UI Icon = Instantiate(Icon_Prefab, transform);
            Icon.Set_Tooltip(Tooltip);
            Pool.Add(Icon);
        }

        for (int i = 0; i < Pool.Count; i++)
        {
            bool Use = i < Wanted;
            Pool[i].gameObject.SetActive(Use);
            if (Use)
                Pool[i].Set_From_Definition(Statuses[i].Definition);
        }

        if (Wanted == 0) return;

        if (!Try_Layout(Wanted))
        {
            Layout_Dirty = true;
            Pending_Count = Wanted;
        }
    }

    public void Clear()
    {
        for (int i = 0; i < Pool.Count; i++)
            Pool[i].gameObject.SetActive(false);

        Layout_Dirty = false;

        if (Tooltip != null)
            Tooltip.Hide();
    }

    private void LateUpdate()
    {
        if (Layout_Dirty && Try_Layout(Pending_Count))
            Layout_Dirty = false;
    }

    private bool Try_Layout(int Count)
    {
        RectTransform Row_RT = (RectTransform)transform;
        if (Row_RT.rect.width <= 0f || Row_RT.rect.height <= 0f)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(Row_RT);
            if (Row_RT.rect.width <= 0f || Row_RT.rect.height <= 0f)
                return false;
        }

        Apply_Layout(Count, Row_RT);
        return true;
    }

    private void Apply_Layout(int Count, RectTransform Row_RT)
    {
        float Row_W = Row_RT.rect.width;
        float Row_H = Row_RT.rect.height;

        float Pad_L = Padding_Left_Fraction * Row_W;
        float Pad_R = Padding_Right_Fraction * Row_W;
        float Pad_T = Padding_Top_Fraction * Row_H;

        float Available_W = Row_W - Pad_L - Pad_R;
        if (Available_W <= 0f) return;

        float Icon_Size = Row_H * Icon_Height_Fraction;
        float Icon_Max_W = Available_W / Mathf.Max(1, Mathf.Min(Count, Overlap_Threshold));
        if (Icon_Size > Icon_Max_W)
            Icon_Size = Icon_Max_W;

        float Spacing = Spacing_Fraction * Row_W;
        float Step = (Count <= Overlap_Threshold)
            ? Icon_Size + Spacing
            : Icon_Size * Overlap_Step_Fraction;

        float Total_W = Icon_Size + (Count - 1) * Step;

        if (Total_W > Available_W && Count > 1)
        {
            Step = (Available_W - Icon_Size) / (Count - 1);
            Total_W = Icon_Size + (Count - 1) * Step;
        }

        // Right-aligned: fill from the right padding inward.
        float Start_Left = Pad_L + (Available_W - Total_W);

        for (int i = 0; i < Count; i++)
        {
            RectTransform Icon_RT = (RectTransform)Pool[i].transform;

            Icon_RT.anchorMin = new Vector2(0f, 1f);
            Icon_RT.anchorMax = new Vector2(0f, 1f);
            Icon_RT.pivot = new Vector2(0.5f, 1f);
            Icon_RT.localScale = Vector3.one;

            Icon_RT.sizeDelta = new Vector2(Icon_Size, Icon_Size);

            float x = Start_Left + i * Step + Icon_Size * 0.5f;
            float y = -Pad_T;

            Pool[i].Set_Base_Position(new Vector2(x, y));
        }

        for (int i = Count - 1; i >= 0; i--)
            Pool[i].transform.SetAsLastSibling();

        for (int i = 0; i < Count; i++)
            Pool[i].Record_Base_Sibling_Index();
    }
}