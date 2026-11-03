using UnityEngine;

public abstract class Ability_Definition_SO : ScriptableObject
{
    [Header("Status Representation")]
    [Tooltip("If set, this ability appears as a permanent status icon on " +
         "the model's card. Used for passives (always-on) so the " +
         "player can hover to read what the ability does. Leave " +
         "unassigned for actives — their status is applied and " +
         "removed by the ability itself when relevant.")]
    public Status_Definition_SO Status_Display;

    [Header("Identity")]
    public string Ability_Name = "Unnamed Ability";

    [TextArea(2, 5)]
    public string Description = "";

    public Ability_Kind Kind = Ability_Kind.Passive;

    [Header("Recharge")]
    [Tooltip("Turns before the ability can be used again. 0 for passives " +
             "and for actives with no cooldown. A value of 2 means: use on " +
             "turn N, ready again on turn N+2.")]
    [Range(0, 10)]
    public int Recharge_Turns = 0;


    // ============================================================
    // LIFECYCLE HOOKS
    // ============================================================

    /// <summary>Called once when the model spawns.</summary>
    public virtual void On_Equip(Model_Standard_Behavior Model) { }

    /// <summary>Called at the start of the owner's turn, after recharge ticks.</summary>
    public virtual void On_Owner_Turn_Start(Model_Standard_Behavior Model) { }

    /// <summary>Called at the end of the owner's turn.</summary>
    public virtual void On_Owner_Turn_End(Model_Standard_Behavior Model) { }


    // ============================================================
    // ACTIVATION HOOKS
    // ============================================================

    /// <summary>
    /// Whether the ability can currently be activated. Used by the card
    /// UI to enable or disable the Ability button. Passives return false.
    /// </summary>
    public virtual bool Can_Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
        => false;

    /// <summary>
    /// Called when the player presses the Ability button for a model
    /// whose ability reports Can_Activate = true. Implementations may
    /// resolve immediately or hand off to a targeting phase.
    /// </summary>
    public virtual void Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board) { }


    // ============================================================
    // COMBAT HOOKS
    // ============================================================

    /// <summary>
    /// Flags applied to the attack roll when this ability's owner attacks.
    /// Called once per attack, before the roll. Passives that modify
    /// combat override this; everything else inherits the no-op.
    /// </summary>
    public virtual Ability_Combat_Flags Get_Combat_Flags(
        Model_Standard_Behavior Attacker,
        Model_Standard_Behavior Defender,
        Battle_Board_Behavior Board)
        => Ability_Combat_Flags.None;

    /// <summary>
    /// Targeting flags applied when this ability's owner is choosing
    /// targets. Called once per targeting pass. Abilities that let the
    /// user shoot through walls or models override this.
    /// </summary>
    public virtual Ability_Targeting_Flags Get_Targeting_Flags(
        Model_Standard_Behavior Model,
        Battle_Board_Behavior Board)
        => Ability_Targeting_Flags.None;

    /// <summary>
    /// Flat bonus to the model's Attack Skill while some condition the
    /// ability is tracking holds. Added on top of the advantage /
    /// disadvantage modifier; unlike advantage, flat bonuses stack
    /// additively and are not subject to the non-stacking rule.
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
    public virtual int Get_Flat_Attack_Range_Bonus(Model_Standard_Behavior Attacker, Battle_Board_Behavior Board)
        => 0;

    /// <summary>
    /// Called after an attack successfully hits, but before damage is dealt.
    /// Passives that trigger on hit (like applying poison) override this.
    /// </summary>
    public virtual void On_Attack_Hit(Model_Standard_Behavior Attacker, Model_Standard_Behavior Defender, Battle_Board_Behavior Board)
    { }
}