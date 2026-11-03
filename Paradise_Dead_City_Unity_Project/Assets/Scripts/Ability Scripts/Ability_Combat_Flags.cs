using System;

/// <summary>
/// Modifiers a passive ability can contribute to the attack roll.
/// Flags are additive — an ability can declare several at once.
///
/// These are intentionally limited to "ignore a disadvantage source"
/// for now. Future abilities that add new advantages should get their
/// own flags rather than being folded in here, so the non-stacking
/// rule stays easy to reason about.
/// </summary>
[Flags]
public enum Ability_Combat_Flags
{
    None = 0,
    Ignore_Cover_Disadvantage = 1 << 0,
    Ignore_Elevation_Disadvantage = 1 << 1,
    Ignore_Vulnerable_Disadvantage = 1 << 2,
}