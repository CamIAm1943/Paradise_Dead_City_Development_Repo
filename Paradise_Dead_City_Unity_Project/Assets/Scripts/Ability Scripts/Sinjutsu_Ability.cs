using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Sinjutsu", menuName = "Scriptable Objects/Abilities/Sinjutsu")]
public class Sinjutsu_Ability : Ability_Definition_SO
{
    [Header("Sinjutsu Settings")]
    [Tooltip("Maximum distance the Ninja can swap with a friendly HUSK.")]
    [Range(1, 8)]
    public int Swap_Range = 4;

    public override bool Can_Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        if (Model == null || Board == null) return false;
        if (Model.Ability_Recharge_Remaining > 0) return false;

        // Must have at least one allied HUSK (Chaff) within range to swap with
        return Find_Targets_In_Range(Model, Board).Count > 0;
    }

    public override void Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        var Targeting = Board.Get_Ability_Targeting_Controller();
        if (Targeting == null)
        {
            Debug.LogWarning("Sinjutsu: no Ability_Targeting_Controller on the board.");
            return;
        }

        var Targets = Find_Targets_In_Range(Model, Board);

        // Start targeting. Min = 1, Max = 1.
        Targeting.Begin(
            Model,
            Swap_Range,
            Targets,
            1,
            1,
            Chosen_Tiles => Resolve_Swap(Model, Board, Chosen_Tiles),
            null
        );
    }

    private void Resolve_Swap(Model_Standard_Behavior Ninja, Battle_Board_Behavior Board, List<Vector2Int> Chosen_Tiles)
    {
        if (Chosen_Tiles.Count == 0) return;

        Vector2Int Husk_Tile = Chosen_Tiles[0];
        Model_Standard_Behavior Husk = Board.Get_Model_At(Husk_Tile.x, Husk_Tile.y);

        if (Husk == null) return;

        Vector2Int Ninja_Tile = new Vector2Int(Ninja.Current_X, Ninja.Current_Y);

        // 1. Swap the Board Array references
        Board.Remove_Model(Ninja_Tile.x, Ninja_Tile.y);
        Board.Remove_Model(Husk_Tile.x, Husk_Tile.y);

        // 2. Swap their grid coordinates
        Ninja.Current_X = Husk_Tile.x;
        Ninja.Current_Y = Husk_Tile.y;

        Husk.Current_X = Ninja_Tile.x;
        Husk.Current_Y = Ninja_Tile.y;

        // 3. Re-register them in the board array
        Board.Place_Model_At(Ninja.Current_X, Ninja.Current_Y, Ninja);
        Board.Place_Model_At(Husk.Current_X, Husk.Current_Y, Husk);

        // 4. Smoothly slide them to their new positions
        Ninja.Set_Position(Board.Get_Unit_Stand_Position(Ninja.Current_X, Ninja.Current_Y), false);
        Husk.Set_Position(Board.Get_Unit_Stand_Position(Husk.Current_X, Husk.Current_Y), false);

        // 5. Apply 1 damage to the Husk
        bool Survived = Husk.Take_Damage(1);
        if (!Survived)
        {
            if (Board.Get_Combat_Manager() != null)
                Board.Get_Combat_Manager().Kill_Model(Husk);
        }

        // 6. Put ability on cooldown
        Ninja.Ability_Recharge_Remaining = Mathf.Max(Recharge_Turns, 1);

        // 7. Refresh UI (do NOT end turn, do NOT set Has_Attacked)
        var CardUI = FindAnyObjectByType<Card_UI_Controller>();
        if (CardUI != null) CardUI.Refresh_Card_For_Model(Ninja);

        Debug.Log($"[Sinjutsu] {Ninja.Stats.Model_Name} swapped places with a Husk!");
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
                if (M.Team != Source.Team) continue;
                if (M.Type != Model_Type.Chaff) continue; // Must be a HUSK

                int Dist = Mathf.Abs(X - Source.Current_X) + Mathf.Abs(Y - Source.Current_Y);
                if (Dist > 0 && Dist <= Swap_Range)
                {
                    Result.Add(new Vector2Int(X, Y));
                }
            }
        }
        return Result;
    }
}