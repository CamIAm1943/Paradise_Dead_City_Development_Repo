using UnityEngine;

[CreateAssetMenu(fileName = "Duty Bound",
                 menuName = "Scriptable Objects/Abilities/Duty Bound")]
public class Duty_Bound_Ability : Ability_Definition_SO
{
    [Header("Duty Bound")]
    [Tooltip("Flat bonus to Attack Skill while Duty Bound is active.")]
    [Range(1, 3)]
    public int Attack_Skill_Bonus = 1;

    [Tooltip("Flat bonus to Attack Range while Duty Bound is active.")]
    [Range(1, 3)]
    public int Attack_Range_Bonus = 1;

    public override bool Can_Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        if (Model == null || Board == null) return false;
        if (Model.Ability_Recharge_Remaining > 0) return false;
        if (Model.Has_Status(Status_Type.Duty_Bound)) return false;
        if (Model.Has_Moved_This_Turn) return false;

        return true;
    }

    public override void Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        // Duration 1: ticks off at the start of the owner's next turn,
        // so the buff is live for the rest of the current turn only.
        Model.Add_Status(Status_Type.Duty_Bound, 1);

        Model.Ability_Recharge_Remaining = Mathf.Max(Recharge_Turns, 1);

        Debug.Log($"[Duty Bound] {Model.Stats.Model_Name} braces: " +
                  $"+{Attack_Skill_Bonus} Skill, +{Attack_Range_Bonus} Range, " +
                  $"cannot move this turn.");
    }

    public override int Get_Flat_Attack_Skill_Bonus(Model_Standard_Behavior Attacker, Battle_Board_Behavior Board)
    {
        return Attacker.Has_Status(Status_Type.Duty_Bound) ? Attack_Skill_Bonus : 0;
    }

    public override int Get_Flat_Attack_Range_Bonus(Model_Standard_Behavior Attacker, Battle_Board_Behavior Board)
    {
        return Attacker.Has_Status(Status_Type.Duty_Bound) ? Attack_Range_Bonus : 0;
    }
}