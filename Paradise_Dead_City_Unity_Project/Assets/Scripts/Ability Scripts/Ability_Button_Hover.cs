using UnityEngine;
using UnityEngine.EventSystems;

public class Ability_Button_Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Card_UI_Controller Card;
    [SerializeField] private Status_Tooltip_UI Tooltip;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Card == null || Tooltip == null) return;

        var Model = Card.Get_Displayed_Model();
        if (Model == null || Model.Stats == null) return;

        var Ability = Model.Stats.Ability_Data;
        if (Ability == null) return;

        Tooltip.Show(Ability.Ability_Name, Ability.Description, (RectTransform)transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (Tooltip != null)
            Tooltip.Hide();
    }
}