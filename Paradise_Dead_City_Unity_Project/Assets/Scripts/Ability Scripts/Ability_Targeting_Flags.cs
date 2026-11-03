using System;

/// <summary>
/// Targeting filters an ability can bypass during range sweeps. Used by
/// ranged attacks, which sweep outward from the attacker and stop at
/// walls and models by default.
///
/// Kept separate from Ability_Combat_Flags because these are consulted
/// at targeting time (which tiles are selectable) while combat flags
/// are consulted at resolution time (how the roll is modified).
/// </summary>
[Flags]
public enum Ability_Targeting_Flags
{
    None = 0,

    /// <summary>The line of fire passes through Wall tiles. The wall
    /// tile itself is never marked as attackable; the sweep just
    /// continues past it.</summary>
    Ignore_Walls = 1 << 0,

    /// <summary>The line of fire passes through tiles occupied by
    /// friendly or enemy models. Enemies on those tiles are still
    /// valid targets; the sweep simply continues past them.</summary>
    Ignore_Models = 1 << 1,
}