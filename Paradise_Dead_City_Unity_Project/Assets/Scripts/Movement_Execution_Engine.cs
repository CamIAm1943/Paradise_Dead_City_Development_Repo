using System.Collections.Generic;
using UnityEngine;

public class Movement_Execution_Engine : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Battle_Board_Behavior Board;
    [SerializeField] private Match_Flow_Manager MatchFlow;
    [SerializeField] private Board_Highlight_Renderer Highlighter;
    [SerializeField] private Combat_Manager CombatManager;

    [Header("Shove Mechanics")]
    [Tooltip("Damage dealt when a shoved unit hits a wall or another model.")]
    [SerializeField] private int Default_Impact_Damage = 1;

    // ============================================================
    // SPRINT
    // ============================================================

    public bool Begin_Sprint(Model_Standard_Behavior model)
    {
        if (model == null || model.Stats == null) return false;
        if (model.Is_Sprinting_This_Turn) return false;
        if (model.Has_Ended_Turn || model.Has_Attacked_This_Turn) return false;
        if (model.Has_Status(Status_Type.Death_On_Wheels)) return false;
        if (model.Has_Status(Status_Type.Datastream)) return false;
        if (model.Has_Status(Status_Type.Duty_Bound)) return false;

        model.Movement_Before_Sprint = model.Movement_Remaining_This_Turn;

        int Sprint_Total = Mathf.Min(
            model.Movement_Before_Sprint + model.Stats.Sprint_Bonus,
            model.Stats.Get_Sprint_Range());

        model.Movement_Remaining_This_Turn = Sprint_Total;
        model.Is_Sprinting_This_Turn = true;
        model.Has_Moved_Since_Sprint = false;

        model.Has_Used_Sprint_Bonus = false;

        return true;
    }

    public bool Cancel_Sprint(Model_Standard_Behavior model)
    {
        if (model == null || !model.Is_Sprinting_This_Turn) return false;

        if (model.Has_Used_Sprint_Bonus) return false;

        int Sprint_Total = Mathf.Min(model.Movement_Before_Sprint + model.Stats.Sprint_Bonus, model.Stats.Get_Sprint_Range());
        int Spent_Since_Sprint = Sprint_Total - model.Movement_Remaining_This_Turn;

        model.Movement_Remaining_This_Turn = model.Movement_Before_Sprint - Spent_Since_Sprint;
        model.Is_Sprinting_This_Turn = false;
        model.Has_Used_Sprint_Bonus = false;

        if (!model.Has_Moved_Since_Sprint)
        {
            model.Has_Moved_This_Turn = false;
        }

        Debug.Log($"[Cancel Sprint] Successfully reverted {model.Stats.Model_Name}. Movement restored to {model.Movement_Remaining_This_Turn}.");
        return true;
    }

    // ============================================================
    // MOVE
    // ============================================================

    public bool Try_Move_Model(Model_Standard_Behavior model, int x, int y, IReadOnlyList<Vector2Int> Chosen_Path = null)
    {
        if (model == null) return false;

        if (model.Has_Status(Status_Type.Kinshiru))
        {
            model.Remove_Status(Status_Type.Kinshiru);
            model.Apply_Vulnerability();
            bool Survived = model.Take_Damage(1);
            if (!Survived) CombatManager?.Kill_Model(model);
            return false;
        }

        if (model.Has_Status(Status_Type.Death_On_Wheels))
            return Try_Death_On_Wheels_Move(model, x, y, Chosen_Path);

        Vector2Int Prev_Pos = new Vector2Int(model.Current_X, model.Current_Y);
        Vector2Int Target_Pos = new Vector2Int(x, y);

        if (model.Has_Status(Status_Type.Logic_Maze) && model.Logic_Maze_Target_Model != null)
        {
            Vector2Int minoPos = new Vector2Int(model.Logic_Maze_Target_Model.Current_X, model.Logic_Maze_Target_Model.Current_Y);
            var pathfinder = FindAnyObjectByType<Board_Pathfinder>();

            if (pathfinder != null)
            {
                // --- FIX: Pass model.Team here as well ---
                Vector2Int mazeTarget = pathfinder.Get_Closest_Adjacent_Tile(Prev_Pos, minoPos, model.Team);
                var distanceMap = pathfinder.Get_Distance_Map_To_Target(mazeTarget, model.Team);

                int currentDist = distanceMap.ContainsKey(Prev_Pos) ? distanceMap[Prev_Pos] : int.MaxValue;
                int newDist = distanceMap.ContainsKey(Target_Pos) ? distanceMap[Target_Pos] : int.MaxValue;

                if (newDist >= currentDist)
                {
                    Debug.Log("[Logic Maze] Movement blocked: You must move strictly closer to the Minotaur.");
                    return false;
                }
            }
        }

        if (model.Has_Status(Status_Type.Datastream))
        {
            Board_Modifiers Dest_Type = Board.Get_Tile_Type_At(x, y);
            if (Dest_Type == Board_Modifiers.Wall || Dest_Type == Board_Modifiers.Cover)
                return false;
        }

        Model_Standard_Behavior Target_Occupant = Board.Get_Model_At(x, y);
        if (Target_Occupant != null && Target_Occupant.Team != model.Team)
        {
            if (!model.Is_Sprinting_This_Turn || model.Stats == null ||
                !model.Stats.Can_Shove || model.Stats.Shove_Distance <= 0) return false;
            if (!Highlighter.Is_Tile_A_Shove_Target(Target_Pos)) return false;
            return Try_Shove_Model(model, Target_Occupant, Chosen_Path, Target_Pos);
        }

        if (model.Has_Ended_Turn) return false;
        if (model.Has_Status(Status_Type.Duty_Bound)) return false;
        if (model.Movement_Remaining_This_Turn <= 0) return false;
        if (Board.Get_Model_At(x, y) != null) return false;
        if (!Highlighter.Is_Tile_In_Movement_Range(Target_Pos)) return false;

        int Move_Cost = Compute_Move_Cost(Prev_Pos, Target_Pos, Chosen_Path);
        if (Move_Cost > model.Movement_Remaining_This_Turn) return false;

        model.Movement_Remaining_This_Turn -= Move_Cost;
        model.Has_Moved_Since_Sprint = true;
        model.Has_Moved_This_Turn = true;

        Board.Remove_Model(Prev_Pos.x, Prev_Pos.y);
        Board.Place_Model_At(x, y, model);
        model.Current_X = x;
        model.Current_Y = y;
        model.Set_Position(Board.Get_Unit_Stand_Position(x, y), false);
        return true;
    }

    private static int Compute_Move_Cost(Vector2Int From, Vector2Int To, IReadOnlyList<Vector2Int> Chosen_Path)
    {
        if (Chosen_Path != null && Chosen_Path.Count >= 2)
            return Chosen_Path.Count - 1;

        return Mathf.Abs(To.x - From.x) + Mathf.Abs(To.y - From.y);
    }

    // ============================================================
    // SHOVE
    // ============================================================

    public bool Try_Shove_Model(Model_Standard_Behavior Sprinter, Model_Standard_Behavior Defender, IReadOnlyList<Vector2Int> Approach_Path, Vector2Int Defender_Pos)
    {
        if (Sprinter == null || Sprinter.Stats == null || Defender == null) return false;

        Vector2Int Direction = Get_Cardinal_Direction(Approach_Path, Defender_Pos);
        if (Direction == Vector2Int.zero) return false;

        Vector2Int Stop_Pos = Defender_Pos - Direction;
        Vector2Int Sprinter_Start = new Vector2Int(Sprinter.Current_X, Sprinter.Current_Y);

        int Move_Cost = Compute_Move_Cost(Sprinter_Start, Stop_Pos, Approach_Path);
        if (Move_Cost > Sprinter.Movement_Remaining_This_Turn) return false;

        if (Sprinter.Is_Sprinting_This_Turn && !Sprinter.Has_Used_Sprint_Bonus)
        {
            int Sprint_Total = Mathf.Min(Sprinter.Movement_Before_Sprint + Sprinter.Stats.Sprint_Bonus, Sprinter.Stats.Get_Sprint_Range());
            int Spent_Since_Sprint = Sprint_Total - Sprinter.Movement_Remaining_This_Turn;

            if (Spent_Since_Sprint + Move_Cost > Sprinter.Movement_Before_Sprint)
            {
                // They just crossed the threshold into the bonus!
                Sprinter.Has_Used_Sprint_Bonus = true;
                Sprinter.Apply_Vulnerability();
            }
        }

        if (Stop_Pos != Sprinter_Start && Board.Get_Model_At(Stop_Pos.x, Stop_Pos.y) != null)
            return false;

        int Shove_Dist = Sprinter.Stats.Shove_Distance;
        List<Vector2Int> Push_Path = new List<Vector2Int>();
        bool Blocked = false;

        for (int Step = 1; Step <= Shove_Dist; Step++)
        {
            Vector2Int Dest = Defender_Pos + Direction * Step;
            if (!Board.Is_Tile_Passable(Dest.x, Dest.y) ||
                Board.Get_Model_At(Dest.x, Dest.y) != null)
            {
                Blocked = true;
                break;
            }
            Push_Path.Add(Dest);
        }

        if (Stop_Pos != Sprinter_Start)
        {
            Board.Remove_Model(Sprinter_Start.x, Sprinter_Start.y);
            Board.Place_Model_At(Stop_Pos.x, Stop_Pos.y, Sprinter);
            Sprinter.Current_X = Stop_Pos.x;
            Sprinter.Current_Y = Stop_Pos.y;
        }

        Sprinter.Set_Position(Board.Get_Unit_Stand_Position(Sprinter.Current_X, Sprinter.Current_Y), false);
        Sprinter.Movement_Remaining_This_Turn = 0;
        Sprinter.Has_Attacked_This_Turn = true;
        Sprinter.Apply_Vulnerability();

        // If a CardUI is open for the Sprinter, refresh it so the red icon appears instantly
        var Interaction = FindAnyObjectByType<Gameplay_Interaction_Handler>();
        if (Interaction != null)
        {
            Interaction.Select_Model_Public(Sprinter);
        }

        Sprinter.Has_Ended_Turn = true;

        if (Blocked)
        {
            Defender.Apply_Vulnerability();
            bool Survived = Defender.Take_Damage(Default_Impact_Damage);
            if (!Survived) CombatManager?.Kill_Model(Defender);
        }
        else
        {
            Vector2Int Final_Pos = Push_Path[Push_Path.Count - 1];
            Board.Remove_Model(Defender_Pos.x, Defender_Pos.y);
            Board.Place_Model_At(Final_Pos.x, Final_Pos.y, Defender);
            Defender.Current_X = Final_Pos.x;
            Defender.Current_Y = Final_Pos.y;
            Defender.Set_Position(Board.Get_Unit_Stand_Position(Final_Pos.x, Final_Pos.y), false);
        }
        return true;
    }

    private static Vector2Int Get_Cardinal_Direction(IReadOnlyList<Vector2Int> Approach_Path, Vector2Int Defender_Pos)
    {
        // Prefer the last step of the actual chosen path — that's the direction of arrival.
        if (Approach_Path != null && Approach_Path.Count >= 2)
        {
            Vector2Int Step = Approach_Path[Approach_Path.Count - 1] - Approach_Path[Approach_Path.Count - 2];
            if (Step.x != 0 && Step.y == 0) return new Vector2Int(Step.x > 0 ? 1 : -1, 0);
            if (Step.y != 0 && Step.x == 0) return new Vector2Int(0, Step.y > 0 ? 1 : -1);
        }

        // Fallback: cardinal from the defender toward the sprinter's previous position.
        if (Approach_Path != null && Approach_Path.Count >= 1)
        {
            Vector2Int From = Approach_Path[Approach_Path.Count - 1];
            Vector2Int Diff = Defender_Pos - From;
            if (Mathf.Abs(Diff.x) >= Mathf.Abs(Diff.y))
                return new Vector2Int(Diff.x > 0 ? 1 : -1, 0);
            return new Vector2Int(0, Diff.y > 0 ? 1 : -1);
        }

        return Vector2Int.zero;
    }

    // ============================================================
    // DEATH ON WHEELS
    // ============================================================

    private bool Try_Death_On_Wheels_Move(Model_Standard_Behavior model, int x, int y, IReadOnlyList<Vector2Int> Chosen_Path)
    {
        Vector2Int Start = new Vector2Int(model.Current_X, model.Current_Y);
        Vector2Int Target = new Vector2Int(x, y);

        if (Start == Target) return false;
        if (model.Has_Ended_Turn || model.Movement_Remaining_This_Turn <= 0) return false;

        int Push = 2, Damage = 1;
        if (model.Ability is Death_On_Wheels_Ability dow)
        {
            Push = Mathf.Max(1, dow.Shove_Push);
            Damage = Mathf.Max(0, dow.Shove_Damage);
        }

        // Reconstruct the walk. If we have a real path, follow it step by step.
        // Otherwise, do a naive cardinal walk toward the target, one axis at a time.
        List<Vector2Int> Walk = Build_DOW_Walk(Start, Target, Chosen_Path);
        if (Walk == null || Walk.Count < 2) return false;

        Vector2Int Current = Start;

        for (int i = 1; i < Walk.Count; i++)
        {
            Vector2Int Next = Walk[i];
            Vector2Int Dir = Next - Current;

            Board_Modifiers Tile = Board.Get_Tile_Type_At(Next.x, Next.y);
            if (Tile == Board_Modifiers.Wall || Tile == Board_Modifiers.Cover)
                Board.Destroy_Terrain_At(Next.x, Next.y);

            var Victim = Board.Get_Model_At(Next.x, Next.y);
            if (Victim != null && Victim.Team != model.Team)
            {
                bool First_Hit = !model.DOW_Damaged_This_Activation.Contains(Victim);
                Try_Push_Victim_Damaging(Victim, Next, Dir, Push,
                    First_Hit ? Damage : 0, model.DOW_Damaged_This_Activation);
                if (Board.Get_Model_At(Next.x, Next.y) != null) break; // blocked by heavy unit
            }

            model.Movement_Remaining_This_Turn -= 1;
            Current = Next;
        }

        Board.Remove_Model(Start.x, Start.y);
        Board.Place_Model_At(Current.x, Current.y, model);
        model.Current_X = Current.x;
        model.Current_Y = Current.y;
        model.Set_Position(Board.Get_Unit_Stand_Position(Current.x, Current.y), false);
        return true;
    }

    private static List<Vector2Int> Build_DOW_Walk(Vector2Int Start, Vector2Int Target, IReadOnlyList<Vector2Int> Chosen_Path)
    {
        if (Chosen_Path != null && Chosen_Path.Count >= 2 &&
            Chosen_Path[0] == Start && Chosen_Path[Chosen_Path.Count - 1] == Target)
        {
            return new List<Vector2Int>(Chosen_Path);
        }

        // No valid path supplied: walk cardinal, one axis at a time.
        List<Vector2Int> Walk = new List<Vector2Int> { Start };
        Vector2Int Current = Start;
        while (Current.x != Target.x)
        {
            Current = new Vector2Int(Current.x + (Target.x > Current.x ? 1 : -1), Current.y);
            Walk.Add(Current);
        }
        while (Current.y != Target.y)
        {
            Current = new Vector2Int(Current.x, Current.y + (Target.y > Current.y ? 1 : -1));
            Walk.Add(Current);
        }
        return Walk;
    }

    // ============================================================
    // PUSH CHAIN
    // ============================================================

    public int Try_Push_Victim_Damaging(Model_Standard_Behavior Victim, Vector2Int Victim_Pos, Vector2Int Direction, int Max_Dist, int Damage, HashSet<Model_Standard_Behavior> Already_Damaged)
    {
        if (Victim == null || Direction == Vector2Int.zero) return 0;

        bool First_Hit = Already_Damaged == null || !Already_Damaged.Contains(Victim);
        if (First_Hit && Already_Damaged != null) Already_Damaged.Add(Victim);

        if (First_Hit && Damage > 0)
        {
            bool Survived = Victim.Take_Damage(Damage);
            if (!Survived) { CombatManager?.Kill_Model(Victim); return 0; }
        }

        Vector2Int Current = Victim_Pos;
        int Pushed = 0;

        for (int Step = 0; Step < Max_Dist; Step++)
        {
            Vector2Int Next = Current + Direction;
            if (Next.x < 0 || Next.x >= Battle_Board_Behavior.Tile_Count_X ||
                Next.y < 0 || Next.y >= Battle_Board_Behavior.Tile_Count_Y) break;

            Board_Modifiers Tile_Type = Board.Get_Tile_Type_At(Next.x, Next.y);
            if (Tile_Type == Board_Modifiers.Wall || Tile_Type == Board_Modifiers.Cover)
                Board.Destroy_Terrain_At(Next.x, Next.y);
            if (!Board.Is_Tile_Passable(Next.x, Next.y)) break;

            Model_Standard_Behavior Blocker = Board.Get_Model_At(Next.x, Next.y);
            if (Blocker != null)
            {
                int Blocker_Pushed = Try_Push_Victim_Damaging(Blocker, Next, Direction,
                    Max_Dist - Step, Damage, Already_Damaged);
                if (Blocker_Pushed == 0 && Board.Get_Model_At(Next.x, Next.y) != null) break;
                if (Board.Get_Model_At(Next.x, Next.y) != null) break;
            }

            Board.Remove_Model(Current.x, Current.y);
            Board.Place_Model_At(Next.x, Next.y, Victim);
            Victim.Current_X = Next.x;
            Victim.Current_Y = Next.y;
            Current = Next;
            Pushed++;
        }

        if (Pushed > 0) Victim.Set_Position(Board.Get_Unit_Stand_Position(Current.x, Current.y), false);
        if (First_Hit) Victim.Apply_Vulnerability();
        return Pushed;
    }

    // ============================================================
    // HAZARD DAMAGE
    // ============================================================

    public void Apply_Hazard_Damage(Model_Standard_Behavior model, List<Vector2Int> Path)
    {
        if (Path == null || Path.Count == 0) return;

        int Hazards_Crossed = 0;
        for (int i = 1; i < Path.Count; i++)
            if (Board.Get_Tile_Type_At(Path[i].x, Path[i].y) == Board_Modifiers.Hazard)
                Hazards_Crossed++;

        if (Hazards_Crossed > 0)
        {
            bool Survived = model.Take_Damage(Hazards_Crossed);
            if (!Survived) CombatManager?.Kill_Model(model);
        }
    }
}