using UnityEngine;

[CreateAssetMenu(fileName = "Death on Wheels", menuName = "Scriptable Objects/Abilities/Death on Wheels")]
public class Death_On_Wheels_Ability : Ability_Definition_SO
{
    [Header("Death on Wheels")]
    [Range(1f, 4f)] public float Movement_Multiplier = 2f;
    [Range(1, 3)] public int Shove_Damage = 1;
    [Range(1, 4)] public int Shove_Push = 2;

    public override bool Can_Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        if (Model == null || Board == null) return false;
        if (Model.Ability_Recharge_Remaining > 0) return false;
        if (Model.Has_Status(Status_Type.Death_On_Wheels)) return false;
        return true;
    }

    public override void Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        int New_Movement = Mathf.RoundToInt(Model.Stats.Movement_Range * Movement_Multiplier);
        Model.Movement_Remaining_This_Turn = New_Movement;
        Model.DOW_Damaged_This_Activation.Clear();
        Model.Add_Status(Status_Type.Death_On_Wheels, 1);
        Model.Ability_Recharge_Remaining = Mathf.Max(Recharge_Turns, 1);

        // FIX: Find the highlighter at runtime
        var highlighter = Object.FindAnyObjectByType<Board_Highlight_Renderer>();
        if (highlighter != null) highlighter.Display_Movement_Range(Model);

        Debug.Log($"[Death on Wheels] {Model.Stats.Model_Name} revs: {New_Movement} movement, chain shove active.");
    }
}