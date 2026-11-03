using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Logic Maze",
                 menuName = "Scriptable Objects/Abilities/Logic Maze")]
public class Logic_Maze_Ability : Ability_Definition_SO
{
    [Header("Logic Maze Settings")]
    [Tooltip("Maximum distance the Minotaur can target an enemy.")]
    [Range(1, 8)]
    public int Maze_Range = 4;

    [Header("Forced Movement")]
    [Tooltip("How many tiles the victim is forced to move toward the Minotaur at the start of their turn.")]
    [Range(1, 4)]
    public int Forced_Move_Distance = 1;

    public override bool Can_Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        if (Model == null || Board == null) return false;
        if (Model.Ability_Recharge_Remaining > 0) return false;

        return Find_Targets_In_Range(Model, Board).Count > 0;
    }

    public override void Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        var Targeting = Board.Get_Ability_Targeting_Controller();
        if (Targeting == null) return;

        var Targets = Find_Targets_In_Range(Model, Board);

        Targeting.Begin(
            Model,
            Maze_Range,
            Targets,
            1,
            1,
            Chosen_Tiles => Apply_Maze(Model, Board, Chosen_Tiles),
            () =>
            {
                // --- FIX: On Cancel, redraw movement range ---
                var Interaction = Object.FindAnyObjectByType<Gameplay_Interaction_Handler>();
                if (Interaction != null)
                {
                    Interaction.Select_Model_Public(Model);
                    if (Interaction.Get_Highlighter() != null)
                        Interaction.Get_Highlighter().Display_Movement_Range(Model);
                }
                // ---------------------------------------------
            }
        );
    }

    private void Apply_Maze(Model_Standard_Behavior Model, Battle_Board_Behavior Board, List<Vector2Int> Chosen_Tiles)
    {
        if (Chosen_Tiles.Count == 0) return;

        var Target = Board.Get_Model_At(Chosen_Tiles[0].x, Chosen_Tiles[0].y);
        if (Target == null) return;

        Target.Add_Status(Status_Type.Logic_Maze, 2);

        // --- FIX: Store the Minotaur reference ---
        Target.Logic_Maze_Target_Model = Model;
        // -----------------------------------------

        Model.Ability_Recharge_Remaining = Mathf.Max(Recharge_Turns, 1);

        Debug.Log($"[Logic Maze] {Model.Stats.Model_Name} trapped {Target.Stats.Model_Name} in a Logic Maze!");

        var Interaction = Object.FindAnyObjectByType<Gameplay_Interaction_Handler>();
        if (Interaction != null)
        {
            Interaction.Select_Model_Public(Model);
            if (Interaction.Get_Highlighter() != null)
                Interaction.Get_Highlighter().Display_Movement_Range(Model);
        }
    }

    private void Handle_Logic_Maze_Turn_Start(Model_Standard_Behavior model, Battle_Board_Behavior board)
    {
        if (model == null || board == null) return;
        if (!model.Has_Status(Status_Type.Logic_Maze)) return;

        // Fallback: find the Minotaur dynamically if reference is lost
        if (model.Logic_Maze_Target_Model == null)
        {
            for (int x = 0; x < board.Get_Tile_Count_X(); x++)
            {
                for (int y = 0; y < board.Get_Tile_Count_Y(); y++)
                {
                    var m = board.Get_Model_At(x, y);
                    if (m != null && m.Ability is Logic_Maze_Ability)
                    {
                        model.Logic_Maze_Target_Model = m;
                        break;
                    }
                }
            }
        }

        if (model.Logic_Maze_Target_Model == null) return;

        var pathfinder = Object.FindAnyObjectByType<Board_Pathfinder>();
        if (pathfinder == null) return;

        for (int step = 0; step < Forced_Move_Distance; step++)
        {
            Vector2Int loopPos = new Vector2Int(model.Current_X, model.Current_Y);
            Vector2Int minoPos = new Vector2Int(model.Logic_Maze_Target_Model.Current_X, model.Logic_Maze_Target_Model.Current_Y);

            // Inside Handle_Logic_Maze_Turn_Start
            Vector2Int targetPos = pathfinder.Get_Closest_Adjacent_Tile(loopPos, minoPos, model.Team);

            if (loopPos == targetPos) break;

            var path = pathfinder.Find_Shortest_Path(loopPos, targetPos, 99, model.Team, false);
            if (path == null || path.Count < 2) break;

            Vector2Int nextPos = path[1];
            if (board.Get_Model_At(nextPos.x, nextPos.y) != null) break;

            // Move them
            board.Remove_Model(loopPos.x, loopPos.y);
            board.Place_Model_At(nextPos.x, nextPos.y, model);
            model.Current_X = nextPos.x;
            model.Current_Y = nextPos.y;
            model.Set_Position(board.Get_Unit_Stand_Position(nextPos.x, nextPos.y), false);

            Debug.Log($"[Logic Maze] {model.Stats.Model_Name} was forced to move to ({nextPos.x}, {nextPos.y}).");
        }
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
                if (M.Team == Source.Team) continue; // Must be an enemy

                int Dist = Mathf.Abs(X - Source.Current_X) + Mathf.Abs(Y - Source.Current_Y);
                if (Dist > 0 && Dist <= Maze_Range)
                {
                    Result.Add(new Vector2Int(X, Y));
                }
            }
        }
        return Result;
    }

    // We subscribe to the Turn Start event to handle forced movement
    public override void On_Equip(Model_Standard_Behavior Model)
    {
        Turn_System_Hook_Bridge.On_Turn_Start -= Handle_Logic_Maze_Turn_Start;
        Turn_System_Hook_Bridge.On_Turn_Start += Handle_Logic_Maze_Turn_Start;

        Turn_System_Hook_Bridge.On_Turn_End -= Handle_Logic_Maze_Turn_End;
        Turn_System_Hook_Bridge.On_Turn_End += Handle_Logic_Maze_Turn_End;
    }

    private void Handle_Logic_Maze_Turn_End(Model_Standard_Behavior model, Battle_Board_Behavior board)
    {
        if (model == null || board == null) return;
        if (!model.Has_Status(Status_Type.Logic_Maze)) return;

        // --- FIX: If they didn't move, they become Vulnerable ---
        if (!model.Has_Moved_This_Turn)
        {
            model.Apply_Vulnerability();
            Debug.Log($"[Logic Maze] {model.Stats.Model_Name} stayed still and is now Vulnerable.");

            var CardUI = Object.FindAnyObjectByType<Card_UI_Controller>();
            if (CardUI != null) CardUI.Refresh_Card_For_Model(model);
        }
    }
}