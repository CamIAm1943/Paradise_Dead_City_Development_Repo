using UnityEngine;

[CreateAssetMenu(fileName = "Raining Bodies",
                 menuName = "Scriptable Objects/Abilities/Raining Bodies")]
public class Raining_Bodies_Ability : Ability_Definition_SO
{
    public override Ability_Combat_Flags Get_Combat_Flags(
        Model_Standard_Behavior Attacker,
        Model_Standard_Behavior Defender,
        Battle_Board_Behavior Board)
    {
        return Ability_Combat_Flags.Ignore_Cover_Disadvantage
             | Ability_Combat_Flags.Ignore_Elevation_Disadvantage;
    }

    public override Ability_Targeting_Flags Get_Targeting_Flags(
        Model_Standard_Behavior Model,
        Battle_Board_Behavior Board)
    {
        return Ability_Targeting_Flags.Ignore_Walls
             | Ability_Targeting_Flags.Ignore_Models;
    }
}