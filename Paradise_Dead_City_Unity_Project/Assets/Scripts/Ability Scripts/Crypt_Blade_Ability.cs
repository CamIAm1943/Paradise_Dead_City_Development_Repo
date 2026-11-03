using UnityEngine;

[CreateAssetMenu(fileName = "Crypt Blade",
                 menuName = "Scriptable Objects/Abilities/Crypt Blade")]
public class Crypt_Blade_Ability : Ability_Definition_SO
{
    // The logic is now handled directly by the Combat_Manager
    public override bool Can_Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board) => false;
}