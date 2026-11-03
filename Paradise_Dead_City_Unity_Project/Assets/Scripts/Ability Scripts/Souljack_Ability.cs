using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Souljack", menuName = "Scriptable Objects/Abilities/Souljack")]
public class Souljack_Ability : Ability_Definition_SO
{
    [Header("Souljack Settings")]
    [Range(1, 8)]
    public int Jack_Range = 4;

    [Tooltip("Bonus added to a Chaff's defense roll when resisting Souljack.")]
    [Range(0, 2)]
    public int Chaff_Resist_Bonus = 1;

    public override bool Can_Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        if (Model == null || Board == null) return false;
        if (Model.Ability_Recharge_Remaining > 0) return false;
        if (Model.Has_Attacked_This_Turn) return false;

        return Find_Targets_In_Range(Model, Board).Count > 0;
    }

    public override void Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        if (Board == null) return;

        var Targeting = Board.Get_Ability_Targeting_Controller();
        if (Targeting == null) return;

        var Targets = Find_Targets_In_Range(Model, Board);

        Targeting.Begin(
            Model, Jack_Range, Targets, 1, 1,
            Chosen_Tiles => Resolve_Jack(Model, Board, Chosen_Tiles),
            null);
    }

    private void Resolve_Jack(Model_Standard_Behavior Lich, Battle_Board_Behavior Board,
                          List<Vector2Int> Chosen_Tiles)
    {
        if (Chosen_Tiles == null || Chosen_Tiles.Count == 0) return;

        var Target = Board.Get_Model_At(Chosen_Tiles[0].x, Chosen_Tiles[0].y);
        if (Target == null) return;

        int Lich_Roll = Random.Range(1, 7);
        int Target_Roll = Random.Range(1, 7);
        if (Target.Type == Model_Type.Chaff) Target_Roll += Chaff_Resist_Bonus;

        Debug.Log($"<color=cyan>[Souljack]</color> {Lich.Stats.Model_Name} rolls {Lich_Roll} vs {Target.Stats.Model_Name}'s {Target_Roll}.");

        Lich.Has_Attacked_This_Turn = true;
        Lich.Has_Ended_Turn = true;
        Lich.Ability_Recharge_Remaining = Mathf.Max(Recharge_Turns, 1);

        if (Lich_Roll <= Target_Roll)
        {
            Lich.Apply_Vulnerability();
            Debug.Log($"<color=red>[Souljack] FAILED!</color> {Lich.Stats.Model_Name} loses control and becomes Vulnerable.");

            var FailedCardUI = Object.FindAnyObjectByType<Card_UI_Controller>();
            if (FailedCardUI != null) FailedCardUI.Refresh_Card_For_Model(Lich);
            return;
        }

        Target.Original_Team = Target.Team;
        Target.Team = Lich.Team;
        Target.Add_Status(Status_Type.Souljacked, 2);

        // 1. Change the model's 3D color
        Target.Update_Team_Color();

        // 2. Refresh the Bottom Army UI
        var armyUI = Object.FindAnyObjectByType<Army_UI_Controller>();
        if (armyUI != null) armyUI.Setup_Army_Bar(Target.Team);

        // 3. Refresh the Top Initiative UI (FIXES THE ICON COLOR)
        var initUI = Object.FindAnyObjectByType<Initiative_UI_Controller>();
        if (initUI != null) initUI.Refresh_Team_Colors();

        Target.Has_Ended_Turn = false;
        Target.Has_Attacked_This_Turn = false;
        Target.Movement_Remaining_This_Turn = Target.Stats.Movement_Range;

        Debug.Log($"<color=purple>[Souljack] SUCCESS!</color> {Lich.Stats.Model_Name} takes control of {Target.Stats.Model_Name}!");

        Turn_System_Hook_Bridge.On_Turn_End -= Revert_On_Turn_End;
        Turn_System_Hook_Bridge.On_Turn_End += Revert_On_Turn_End;

        var CardUI = Object.FindAnyObjectByType<Card_UI_Controller>();
        if (CardUI != null) CardUI.Refresh_Card_For_Model(Lich);
    }

    private void Revert_On_Turn_End(Model_Standard_Behavior model, Battle_Board_Behavior board)
    {
        if (model.Original_Team == -1) return;

        model.Team = model.Original_Team;
        model.Original_Team = -1;
        model.Remove_Status(Status_Type.Souljacked);

        // 1. Fix the 3D model color
        model.Update_Team_Color();

        // 2. Fix the Army UI
        var armyUI = Object.FindAnyObjectByType<Army_UI_Controller>();
        if (armyUI != null) armyUI.Setup_Army_Bar(1);

        // 3. Fix the Initiative Bar Color
        var initUI = Object.FindAnyObjectByType<Initiative_UI_Controller>();
        if (initUI != null) initUI.Refresh_Team_Colors();

        Turn_System_Hook_Bridge.On_Turn_End -= Revert_On_Turn_End;

        Debug.Log($"<color=cyan>[Souljack]</color> {model.Stats.Model_Name} snapped back to their original team.");
    }

    private List<Vector2Int> Find_Targets_In_Range(Model_Standard_Behavior Source, Battle_Board_Behavior Board)
    {
        var Result = new List<Vector2Int>();
        int Count_X = Board.Get_Tile_Count_X();
        int Count_Y = Board.Get_Tile_Count_Y();

        for (int X = 0; X < Count_X; X++)
            for (int Y = 0; Y < Count_Y; Y++)
            {
                var M = Board.Get_Model_At(X, Y);
                if (M == null || !M.gameObject.activeSelf || M.Team == Source.Team) continue;
                int Dist = Mathf.Abs(X - Source.Current_X) + Mathf.Abs(Y - Source.Current_Y);
                if (Dist > 0 && Dist <= Jack_Range) Result.Add(new Vector2Int(X, Y));
            }
        return Result;
    }
}