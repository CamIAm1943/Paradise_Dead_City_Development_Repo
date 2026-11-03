using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Kinshiru", menuName = "Scriptable Objects/Abilities/Kinshiru")]
public class Kinshiru_Ability : Ability_Definition_SO
{
    [Header("Kinshiru Settings")]
    [Tooltip("Maximum distance the Shinobi can place the seal.")]
    [Range(1, 8)]
    public int Seal_Range = 4;

    public override bool Can_Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        if (Model == null || Board == null) return false;
        if (Model.Ability_Recharge_Remaining > 0) return false;

        // Must have an enemy within range to target
        return Find_Targets_In_Range(Model, Board).Count > 0;
    }

    public override void Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        var Targeting = Board.Get_Ability_Targeting_Controller();
        if (Targeting == null)
        {
            Debug.LogWarning("Kinshiru: no Ability_Targeting_Controller on the board.");
            return;
        }

        var Targets = Find_Targets_In_Range(Model, Board);

        // Start targeting. Max selections = 1.
        Targeting.Begin(
        Model,
        Seal_Range,
        Targets,
        1,
        1,
        Chosen_Tiles => Apply_Seal(Model, Board, Chosen_Tiles),
        null
    );
    }

    private void Apply_Seal(Model_Standard_Behavior Model, Battle_Board_Behavior Board, List<Vector2Int> Chosen_Tiles)
    {
        if (Chosen_Tiles.Count == 0) return;

        var Target = Board.Get_Model_At(Chosen_Tiles[0].x, Chosen_Tiles[0].y);
        if (Target == null) return;

        // Apply seal for 2 turns (survives the tick at the start of their turn)
        Target.Add_Status(Status_Type.Kinshiru, 2);

        Model.Ability_Recharge_Remaining = Mathf.Max(Recharge_Turns, 1);

        

        Debug.Log($"[Kinshiru] {Model.Stats.Model_Name} placed a seal on {Target.Stats.Model_Name}!");
    }

    private List<Vector2Int> Find_Targets_In_Range(Model_Standard_Behavior Source, Battle_Board_Behavior Board)
    {
        var Result = new List<Vector2Int>();
        int Count_X = Board.Get_Tile_Count_X();
        int Count_Y = Board.Get_Tile_Count_Y();

        for (int X = 0; X < Count_X; X++)
        {
            for (int Y = 0; Y < Count_Y; Y++)
            {
                var M = Board.Get_Model_At(X, Y);
                if (M == null || !M.gameObject.activeSelf) continue;
                if (M.Team == Source.Team) continue;

                int Dist = Mathf.Abs(X - Source.Current_X) + Mathf.Abs(Y - Source.Current_Y);
                if (Dist > 0 && Dist <= Seal_Range)
                {
                    Result.Add(new Vector2Int(X, Y));
                }
            }
        }
        return Result;
    }
}