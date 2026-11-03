using UnityEngine;

[CreateAssetMenu(fileName = "Dokujutsu", menuName = "Scriptable Objects/Abilities/Dokujutsu")]
public class Dokujutsu_Ability : Ability_Definition_SO
{
    public override void On_Attack_Hit(Model_Standard_Behavior Attacker, Model_Standard_Behavior Defender, Battle_Board_Behavior Board)
    {
        if (Defender == null) return;

        // 1. Inflict Rot
        Defender.Add_Status(Status_Type.Rot, 2);
        Defender.Add_Status(Status_Type.Vulnerable, 2); // Rot also applies Vulnerable

        Debug.Log($"[Dokujutsu] {Defender.Stats.Model_Name} was poisoned! Applied Rot for 2 turns.");

        // 2. Roll for Hazard
        int Hazard_Roll = Random.Range(1, 7);

        if (Hazard_Roll == 1)
        {
            // 4 ticks = 2 full rounds
            Board.Create_Temporary_Hazard(Defender.Current_X, Defender.Current_Y, 4);
            Debug.Log($"[Dokujutsu] Critical poison! A Hazard was left at ({Defender.Current_X}, {Defender.Current_Y}) for 2 rounds.");
        }
    }
}