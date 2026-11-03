using TMPro;
using UnityEngine;

public class Turn_Timer_UI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Match_Flow_Manager MatchFlow;
    [SerializeField] private TextMeshProUGUI Timer_Text;

    [Header("Colors")]
    [SerializeField] private Color Normal_Color = Color.black;
    [SerializeField] private Color Warning_Color = Color.red;

    [Tooltip("At what percentage of time remaining should it turn red? (0.3 = 30%)")]
    [SerializeField] private float Warning_Threshold = 0.3f;

    private void Update()
    {
        if (MatchFlow == null || Timer_Text == null) return;

        if (MatchFlow.Current_Phase != Game_State.Gameplay ||
            MatchFlow.Active_Player == 0 ||
            MatchFlow.Active_Model == null)
        {
            Timer_Text.text = "";
            return;
        }

        float Time_Remaining = MatchFlow.Get_Time_Remaining();
        float Percent_Remaining = MatchFlow.Get_Time_Percentage();

        Timer_Text.text = Mathf.CeilToInt(Time_Remaining).ToString();

        if (Percent_Remaining > Warning_Threshold)
        {
            Timer_Text.color = Normal_Color;
        }
        else
        {
            float t = Warning_Threshold > 0f ? Percent_Remaining / Warning_Threshold : 0f;
            Timer_Text.color = Color.Lerp(Warning_Color, Normal_Color, t);
        }
    }
}