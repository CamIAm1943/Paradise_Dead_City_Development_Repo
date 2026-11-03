using UnityEngine;

[CreateAssetMenu(fileName = "New Status Definition", menuName = "Scriptable Objects/Status Definition")]
public class Status_Definition_SO : ScriptableObject
{
    [Header("Identity")]
    public Status_Type Type;
    public string Display_Name = "Status";

    [TextArea(2, 4)]
    public string Description = "";

    [Header("Visuals")]
    [Tooltip("Optional. If null, the placeholder circle sprite on the icon " +
             "prefab is kept and tinted with Fallback_Color. If assigned, " +
             "the sprite is swapped in.")]
    public Sprite Icon;

    [Tooltip("Color used for the placeholder tint, and for the icon sprite " +
             "when Tint_Icon is on.")]
    public Color Fallback_Color = Color.white;

    [Tooltip("If true, the icon sprite is multiplied by Fallback_Color. " +
             "If false, the sprite is shown as-is and Fallback_Color is ignored.")]
    public bool Tint_Icon = false;

    /// <summary>
    /// Flat bonus to the model's Attack Skill while some condition the
    /// ability is tracking holds. Added on top of the advantage / disadvantage
    /// modifier; unlike advantage, flat bonuses stack additively and are not
    /// subject to the non-stacking rule.
    /// </summary>
    public virtual int Get_Flat_Attack_Skill_Bonus(
        Model_Standard_Behavior Attacker,
        Battle_Board_Behavior Board)
        => 0;

    /// <summary>
    /// Flat bonus to the model's Attack Range under the same condition.
    /// Applied in Show_Attack_Range. Melee is unaffected because melee
    /// already hardcodes adjacency.
    /// </summary>
    public virtual int Get_Flat_Attack_Range_Bonus(
        Model_Standard_Behavior Attacker,
        Battle_Board_Behavior Board)
        => 0;
}