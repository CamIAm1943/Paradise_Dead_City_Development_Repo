using System.Collections.Generic;
using UnityEngine;

public class Board_Highlight_Renderer : MonoBehaviour
{
    [Header("Board References")]
    [SerializeField] private Battle_Board_Behavior Board;
    [SerializeField] private Board_Pathfinder Pathfinder;

    [Header("Gameplay Highlight Materials")]
    [SerializeField] private Material Movement_Range_Material;
    [SerializeField] private Material Sprint_Range_Material;
    [SerializeField] private Material Shove_Target_Material;
    [SerializeField] private Material Shove_Destination_Material;
    [SerializeField] private Material Shove_Blocked_Material;
    [SerializeField] private Material Hazard_Path_Material;
    [SerializeField] private Material Death_On_Wheels_Destroy_Material;
    [SerializeField] private Material Death_On_Wheels_Shove_Preview_Material;

    [Header("Setup Phase Materials")]
    [SerializeField] private Material Valid_Spawn_Tile_Locations_Material;
    [SerializeField] private Material Model_Placement_Material;
    [SerializeField] private Material Tile_Material_Fallback;

    [Header("Path Materials (Priority Order)")]
    [SerializeField] private Material[] Path_Materials = new Material[6];
    [Range(1, 8)][SerializeField] private int Path_Slot_Count = 4;

    [Header("Flashing")]
    [SerializeField] private float Flash_Speed = 2.0f;
    [Range(0f, 1f)][SerializeField] private float Flash_Min_Alpha = 0.3f;
    [Range(0f, 1f)][SerializeField] private float Flash_Max_Alpha = 1.0f;

    [Header("Debug Logging")]
    [SerializeField] private bool Debug_Log_Pathfinding = false;
    [SerializeField] private bool Debug_Log_Path_Materials = false;

    // ============================================================
    // HIGHLIGHT STATE
    // ============================================================
    public List<Vector2Int> Spawn_Tile_Highlights { get; private set; } = new List<Vector2Int>();
    public List<Vector2Int> Model_Placement_Highlights { get; private set; } = new List<Vector2Int>();
    public List<Vector2Int> Movement_Range_Highlights { get; private set; } = new List<Vector2Int>();
    public List<Vector2Int> Sprint_Range_Highlights { get; private set; } = new List<Vector2Int>();
    public List<Vector2Int> Shove_Target_Highlights { get; private set; } = new List<Vector2Int>();
    public List<Vector2Int> Shove_Destination_Highlights { get; private set; } = new List<Vector2Int>();
    public List<Vector2Int> Path_Highlight_Tiles { get; private set; } = new List<Vector2Int>();
    public List<Vector2Int> DOW_Shove_Preview_Tiles { get; private set; } = new List<Vector2Int>();

    private bool Shove_Highlights_Are_Blocked = false;

    // ============================================================
    // UNITY LIFECYCLE
    // ============================================================
    private void Update()
    {
        Update_Flashing_Effect();
    }

    // ============================================================
    // PUBLIC QUERIES
    // ============================================================
    public bool Is_Tile_In_Movement_Range(Vector2Int Tile) => Movement_Range_Highlights.Contains(Tile);
    public bool Is_Tile_A_Shove_Target(Vector2Int Tile) => Shove_Target_Highlights.Contains(Tile);
    public bool Is_Tile_A_Shove_Destination(Vector2Int Tile) => Shove_Destination_Highlights.Contains(Tile);
    public bool Is_Tile_In_Path(Vector2Int Tile) => Path_Highlight_Tiles.Contains(Tile);

    public Material Get_Movement_Range_Material() => Movement_Range_Material;
    public Material Get_Sprint_Range_Material() => Sprint_Range_Material;
    public Material Get_Shove_Target_Material() => Shove_Target_Material;
    public Material Get_Shove_Destination_Material() => Shove_Destination_Material;
    public Material Get_Shove_Blocked_Material() => Shove_Blocked_Material;
    public Material Get_DOW_Destroy_Material() => Death_On_Wheels_Destroy_Material;
    public int Get_Path_Slot_Count() => Path_Slot_Count;

    public Material Get_Base_Tile_Material(int X, int Y)
    {
        Material BaseMat = Board.Get_Base_Tile_Material(X, Y);
        return BaseMat != null ? BaseMat : Tile_Material_Fallback;
    }

    // ============================================================
    // SETUP PHASES (Spawn & Placement)
    // ============================================================
    public void Show_Spawn_Zone(List<Vector2Int> Tiles)
    {
        Clear_Spawn_Zone();
        Spawn_Tile_Highlights.AddRange(Tiles);
        foreach (var Tile in Tiles)
            Paint_Tile(Tile, Valid_Spawn_Tile_Locations_Material);
    }

    public void Clear_Spawn_Zone()
    {
        foreach (var Tile in Spawn_Tile_Highlights) Paint_Base(Tile);
        Spawn_Tile_Highlights.Clear();
    }

    public void Show_Model_Placement_Zone(List<Vector2Int> Tiles)
    {
        Clear_Model_Placement_Zone();
        Model_Placement_Highlights.AddRange(Tiles);
        if (Model_Placement_Material == null)
        {
            Debug.LogError("[Highlighter] Model_Placement_Material is NULL! Please assign it in the Inspector.");
        }

        foreach (var Tile in Tiles)
            Paint_Tile(Tile, Model_Placement_Material);
    }

    public void Clear_Model_Placement_Zone()
    {
        foreach (var Tile in Model_Placement_Highlights) Paint_Base(Tile);
        Model_Placement_Highlights.Clear();
    }

    // ============================================================
    // MOVEMENT RANGE & PATHING
    // ============================================================
    public void Display_Movement_Range(Model_Standard_Behavior Model, int? Range_Override = null)
    {
        Clear_Movement_Range_Highlights();

        if (Model.Stats == null || Model.Has_Ended_Turn) return;

        int Movement_Range = Range_Override ?? Model.Movement_Remaining_This_Turn;
        if (Movement_Range <= 0) return;

        int Start_X = Model.Current_X;
        int Start_Y = Model.Current_Y;

        Vector2Int Start = new Vector2Int(Start_X, Start_Y);

        bool Show_Sprint_Band = Model.Is_Sprinting_This_Turn && Model.Stats != null;
        int Base_Remaining_For_Sprint = -1;

        if (Show_Sprint_Band)
        {
            int Sprint_Total = Mathf.Min(Model.Movement_Before_Sprint + Model.Stats.Sprint_Bonus, Model.Stats.Get_Sprint_Range());
            int Spent_Since_Sprint = Sprint_Total - Model.Movement_Remaining_This_Turn;
            Base_Remaining_For_Sprint = Model.Movement_Before_Sprint - Spent_Since_Sprint;
        }

        bool Death_On_Wheels = Model.Has_Status(Status_Type.Death_On_Wheels);
        bool Is_Datastream = Model.Has_Status(Status_Type.Datastream);
        bool Ignore_Walls = Death_On_Wheels || Is_Datastream;

        // --- Calculate distance map ONCE before the loop ---
        Dictionary<Vector2Int, int> mazeDistanceMap = null;
        if (Model.Has_Status(Status_Type.Logic_Maze))
        {
            if (Model.Logic_Maze_Target_Model == null)
            {
                for (int x = 0; x < Board.Get_Tile_Count_X(); x++)
                {
                    for (int y = 0; y < Board.Get_Tile_Count_Y(); y++)
                    {
                        var m = Board.Get_Model_At(x, y);
                        if (m != null && m.Ability is Logic_Maze_Ability)
                        {
                            Model.Logic_Maze_Target_Model = m;
                            break;
                        }
                    }
                }
            }

            if (Model.Logic_Maze_Target_Model != null)
            {
                Vector2Int minoPos = new Vector2Int(Model.Logic_Maze_Target_Model.Current_X, Model.Logic_Maze_Target_Model.Current_Y);
                Vector2Int mazeTarget = Pathfinder.Get_Closest_Adjacent_Tile(Start, minoPos, Model.Team);
                mazeDistanceMap = Pathfinder.Get_Distance_Map_To_Target(mazeTarget, Model.Team);

                Debug.Log($"[Logic Maze Debug] Minotaur is at {minoPos}. Target tile is {mazeTarget}. Start is {Start}. Distance map contains {mazeDistanceMap?.Count ?? 0} tiles.");
            }
        }
        // --------------------------------------------------------

        bool[,] Visited = new bool[Board.Get_Tile_Count_X(), Board.Get_Tile_Count_Y()];
        Queue<Vector2Int> To_Explore = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> Distance_Map = new Dictionary<Vector2Int, int>();

        To_Explore.Enqueue(Start);
        Visited[Start_X, Start_Y] = true;
        Distance_Map[Start] = 0;

        bool Can_Shove = Model.Is_Sprinting_This_Turn && Model.Stats.Can_Shove && Model.Stats.Shove_Distance > 0;

        while (To_Explore.Count > 0)
        {
            Vector2Int Current = To_Explore.Dequeue();
            int Current_Distance = Distance_Map[Current];

            if (Current_Distance >= Movement_Range) continue;

            foreach (var Dir in Battle_Board_Behavior.Cardinal_Directions)
            {
                Vector2Int Neighbor = Current + Dir;

                if (Neighbor.x < 0 || Neighbor.x >= Board.Get_Tile_Count_X() ||
                    Neighbor.y < 0 || Neighbor.y >= Board.Get_Tile_Count_Y())
                    continue;

                if (Visited[Neighbor.x, Neighbor.y]) continue;

                bool Passable = Board.Is_Tile_Passable(Neighbor.x, Neighbor.y) || Ignore_Walls;
                if (!Passable) continue;

                Model_Standard_Behavior Occupant = Board.Get_Model_At(Neighbor.x, Neighbor.y);
                Board_Modifiers Neighbor_Type = Board.Get_Tile_Type_At(Neighbor.x, Neighbor.y);
                bool Is_Wall = Neighbor_Type == Board_Modifiers.Wall || Neighbor_Type == Board_Modifiers.Cover;

                int Step_Cost = 1;
                if (Death_On_Wheels && Is_Wall) Step_Cost += 1;

                int New_Distance = Current_Distance + Step_Cost;

                if (Occupant != null)
                {
                    if (Occupant.Team == Model.Team)
                    {
                        Visited[Neighbor.x, Neighbor.y] = true;
                        Distance_Map[Neighbor] = New_Distance;
                        if (New_Distance < Movement_Range) To_Explore.Enqueue(Neighbor);
                        continue;
                    }

                    if (Death_On_Wheels)
                    {
                        Visited[Neighbor.x, Neighbor.y] = true;
                        Distance_Map[Neighbor] = New_Distance;

                        if (New_Distance <= Movement_Range)
                        {
                            Movement_Range_Highlights.Add(Neighbor);
                            Material Mat = Shove_Target_Material != null ? Shove_Target_Material : Movement_Range_Material;
                            Paint_Tile(Neighbor, Mat);
                        }

                        if (New_Distance < Movement_Range) To_Explore.Enqueue(Neighbor);
                        continue;
                    }

                    if (!Can_Shove) continue;

                    Visited[Neighbor.x, Neighbor.y] = true;
                    Shove_Target_Highlights.Add(Neighbor);
                    Paint_Tile(Neighbor, Shove_Target_Material);
                    continue;
                }

                Visited[Neighbor.x, Neighbor.y] = true;
                Distance_Map[Neighbor] = New_Distance;

                if (New_Distance <= Movement_Range)
                {
                    bool isValidTile = true;

                    // --- FIX: Compare against Start instead of Current ---
                    if (mazeDistanceMap != null)
                    {
                        int startDist = mazeDistanceMap.ContainsKey(Start) ? mazeDistanceMap[Start] : int.MaxValue;
                        int newDist = mazeDistanceMap.ContainsKey(Neighbor) ? mazeDistanceMap[Neighbor] : int.MaxValue;

                        if (newDist >= startDist)
                        {
                            Debug.Log($"[Logic Maze Debug] Rejecting tile {Neighbor}. Start tile {Start} is {startDist} steps away. Neighbor is {newDist} steps away.");
                            isValidTile = false;
                        }
                        else
                        {
                            Debug.Log($"[Logic Maze Debug] ACCEPTING tile {Neighbor}. Start tile {Start} is {startDist} steps away. Neighbor is {newDist} steps away.");
                        }
                    }
                    // -----------------------------------------------------

                    if (!isValidTile)
                    {
                        if (New_Distance < Movement_Range) To_Explore.Enqueue(Neighbor);
                        continue;
                    }

                    if (Is_Datastream && Is_Wall)
                    {
                        // Do nothing visually, but keep exploring the path
                    }
                    else
                    {
                        Movement_Range_Highlights.Add(Neighbor);

                        Material DOW_Override = null;
                        if (Death_On_Wheels && Is_Wall)
                        {
                            DOW_Override = Death_On_Wheels_Destroy_Material != null ? Death_On_Wheels_Destroy_Material : Movement_Range_Material;
                        }

                        if (DOW_Override != null)
                        {
                            Paint_Tile(Neighbor, DOW_Override);
                        }
                        else
                        {
                            bool In_Sprint_Range = Show_Sprint_Band && New_Distance > Base_Remaining_For_Sprint;

                            if (In_Sprint_Range)
                            {
                                Sprint_Range_Highlights.Add(Neighbor);
                                Material Mat = Sprint_Range_Material != null ? Sprint_Range_Material : Movement_Range_Material;
                                Paint_Tile(Neighbor, Mat);
                            }
                            else
                            {
                                Paint_Tile(Neighbor, Movement_Range_Material);
                            }
                        }
                    }
                }

                if (New_Distance < Movement_Range) To_Explore.Enqueue(Neighbor);
            }
        }

        if (Death_On_Wheels) Prune_DOW_Unreachable_Highlights(Model);
    }

    public void Draw_Paths(List<List<Vector2Int>> All_Paths, int Current_Path_Index, Model_Standard_Behavior Dragged_Model)
    {
        Clear_Shove_Destination_Highlights();

        foreach (var Tile in Path_Highlight_Tiles)
        {
            if (Movement_Range_Highlights.Contains(Tile) && Movement_Range_Material != null)
                Paint_Tile(Tile, Movement_Range_Material);
            else
                Paint_Base(Tile);
        }
        Path_Highlight_Tiles.Clear();

        if (All_Paths == null || All_Paths.Count == 0) return;

        int Levels = Mathf.Min(Path_Slot_Count, All_Paths.Count);
        int[] Priority_Order = new int[All_Paths.Count];

        for (int i = 0; i < All_Paths.Count; i++)
        {
            int Order = (i - Current_Path_Index + All_Paths.Count) % All_Paths.Count;
            if (Order >= Levels) Order = Levels - 1;
            Priority_Order[i] = Order;
        }

        for (int Priority_Level = Levels - 1; Priority_Level >= 0; Priority_Level--)
        {
            for (int i = 0; i < All_Paths.Count; i++)
            {
                if (Priority_Order[i] == Priority_Level)
                    Show_Path_Highlights(All_Paths[i], Priority_Order[i], Dragged_Model);
            }
        }

        Update_DOW_Shove_Preview(Dragged_Model, All_Paths, Current_Path_Index);

        if (Debug_Log_Pathfinding)
            Debug.Log($"[Path] Drawn. Current Index: {Current_Path_Index}, Total Paths: {All_Paths.Count}");
    }

    public void Draw_Shove_Paths(List<List<Vector2Int>> All_Paths, int Current_Path_Index, Model_Standard_Behavior Dragged_Model, Vector2Int Enemy_Position)
    {
        Draw_Paths(All_Paths, Current_Path_Index, Dragged_Model);
        Update_Shove_Preview_From_Path(All_Paths, Current_Path_Index, Dragged_Model, Enemy_Position);
        Repaint_Shove_Highlights();
    }

    public void Update_Shove_Preview_From_Path(List<List<Vector2Int>> All_Paths, int Current_Path_Index, Model_Standard_Behavior Dragged_Model, Vector2Int Enemy_Position)
    {
        Clear_Shove_Destination_Highlights();
        if (Dragged_Model == null || Dragged_Model.Stats == null || All_Paths == null || All_Paths.Count == 0) return;

        int Index = Mathf.Clamp(Current_Path_Index, 0, All_Paths.Count - 1);
        List<Vector2Int> Path = All_Paths[Index];
        if (Path.Count < 2) return;

        Vector2Int Stop_Tile = Path[Path.Count - 2];
        Vector2Int Direction = Enemy_Position - Stop_Tile;
        if (Direction == Vector2Int.zero) return;

        int Shove_Distance = Dragged_Model.Stats.Shove_Distance;
        bool All_Blocked = false;

        for (int Step = 1; Step <= Shove_Distance; Step++)
        {
            Vector2Int Dest = Enemy_Position + Direction * Step;

            if (!Board.Is_Tile_Passable(Dest.x, Dest.y) || Board.Get_Model_At(Dest.x, Dest.y) != null)
            {
                All_Blocked = true;
                break;
            }

            Shove_Destination_Highlights.Add(Dest);
            Paint_Tile(Dest, Shove_Destination_Material);
        }

        Shove_Highlights_Are_Blocked = All_Blocked;
        if (All_Blocked && Shove_Blocked_Material != null)
            Paint_Tile(Enemy_Position, Shove_Blocked_Material);
    }

    // ============================================================
    // CLEARING HIGHLIGHTS
    // ============================================================
    public void Clear_Movement_Range_Highlights()
    {
        // Repaint every single tile that might have been highlighted
        foreach (Vector2Int Tile in Movement_Range_Highlights) Paint_Base(Tile);
        foreach (Vector2Int Tile in Sprint_Range_Highlights) Paint_Base(Tile);
        foreach (Vector2Int Tile in Shove_Target_Highlights) Paint_Base(Tile);
        foreach (Vector2Int Tile in Shove_Destination_Highlights) Paint_Base(Tile);

        // Clear all the tracking lists
        Movement_Range_Highlights.Clear();
        Sprint_Range_Highlights.Clear();
        Shove_Target_Highlights.Clear();
        Shove_Destination_Highlights.Clear();
        Shove_Highlights_Are_Blocked = false;
    }

    public void Clear_Path_Highlights()
    {
        Clear_DOW_Shove_Preview();

        Clear_Shove_Destination_Highlights();

        foreach (Vector2Int Tile in Path_Highlight_Tiles)
        {
            if (Shove_Target_Highlights.Contains(Tile) && Shove_Target_Material != null)
                Paint_Tile(Tile, Shove_Target_Material);
            else if (Movement_Range_Highlights.Contains(Tile) && Movement_Range_Material != null)
                Paint_Tile(Tile, Movement_Range_Material);
            else
                Paint_Base(Tile);
        }
        Path_Highlight_Tiles.Clear();
    }

    public void Clear_Shove_Destination_Highlights()
    {
        foreach (Vector2Int Tile in Shove_Destination_Highlights)
        {
            if (Shove_Target_Highlights.Contains(Tile)) continue;

            if (Path_Highlight_Tiles.Contains(Tile))
                Paint_Tile(Tile, Get_Path_Material(0));
            else if (Movement_Range_Highlights.Contains(Tile) && Movement_Range_Material != null)
                Paint_Tile(Tile, Movement_Range_Material);
            else
                Paint_Base(Tile);
        }
        Shove_Destination_Highlights.Clear();

        if (Shove_Highlights_Are_Blocked)
        {
            foreach (Vector2Int Tile in Shove_Target_Highlights)
            {
                if (Shove_Target_Material != null) Paint_Tile(Tile, Shove_Target_Material);
            }
            Shove_Highlights_Are_Blocked = false;
        }
    }

    public void Clear_Shove_Target_Highlights()
    {
        foreach (Vector2Int Tile in Shove_Target_Highlights) Paint_Base(Tile);

        Shove_Target_Highlights.Clear();
        Clear_Shove_Destination_Highlights();
        Shove_Highlights_Are_Blocked = false;
    }

    // ============================================================
    // DOW PREVIEW & LOGIC
    // ============================================================
    public void Update_DOW_Shove_Preview(Model_Standard_Behavior Dragged_Model, List<List<Vector2Int>> All_Paths, int Current_Path_Index)
    {
        Clear_DOW_Shove_Preview();

        if (Dragged_Model == null || !Dragged_Model.Has_Status(Status_Type.Death_On_Wheels) || All_Paths == null || All_Paths.Count == 0) return;

        int Index = Mathf.Clamp(Current_Path_Index, 0, All_Paths.Count - 1);
        List<Vector2Int> Path = All_Paths[Index];
        if (Path == null || Path.Count < 2) return;

        int Push = 2;
        if (Dragged_Model.Ability is Death_On_Wheels_Ability Dow) Push = Mathf.Max(1, Dow.Shove_Push);

        int Count_X = Board.Get_Tile_Count_X();
        int Count_Y = Board.Get_Tile_Count_Y();

        var Shadow_Models = new Dictionary<Vector2Int, Model_Standard_Behavior>();
        for (int X = 0; X < Count_X; X++)
            for (int Y = 0; Y < Count_Y; Y++)
            {
                var M = Board.Get_Model_At(X, Y);
                if (M != null && M != Dragged_Model) Shadow_Models[new Vector2Int(X, Y)] = M;
            }

        var Shadow_Tiles = new Board_Modifiers[Count_X, Count_Y];
        for (int X = 0; X < Count_X; X++)
            for (int Y = 0; Y < Count_Y; Y++) Shadow_Tiles[X, Y] = Board.Get_Tile_Type_At(X, Y);

        var Final_Positions = new Dictionary<Model_Standard_Behavior, Vector2Int>();
        Vector2Int Engine = new Vector2Int(Dragged_Model.Current_X, Dragged_Model.Current_Y);

        for (int i = 1; i < Path.Count; i++)
        {
            Vector2Int Next = Path[i];
            Vector2Int Dir = Next - Engine;

            if (Shadow_Tiles[Next.x, Next.y] == Board_Modifiers.Wall || Shadow_Tiles[Next.x, Next.y] == Board_Modifiers.Cover)
                Shadow_Tiles[Next.x, Next.y] = Board_Modifiers.None;

            if (Shadow_Models.TryGetValue(Next, out var Victim) && Victim.Team != Dragged_Model.Team)
                Shadow_Push_Chain(Shadow_Tiles, Victim, Next, Dir, Push, Shadow_Models, Final_Positions);

            Engine = Next;
        }

        if (Final_Positions.Count == 0) return;

        Material Mat = Death_On_Wheels_Shove_Preview_Material != null ? Death_On_Wheels_Shove_Preview_Material : Shove_Destination_Material;
        if (Mat == null) return;

        foreach (var kvp in Final_Positions)
        {
            Vector2Int Dest = kvp.Value;
            if (Dest.x < 0 || Dest.x >= Count_X || Dest.y < 0 || Dest.y >= Count_Y) continue;
            if (Dest == Path[Path.Count - 1]) continue;

            DOW_Shove_Preview_Tiles.Add(Dest);
            Paint_Tile(Dest, Mat);
        }
    }

    public void Clear_DOW_Shove_Preview()
    {
        foreach (var Tile in DOW_Shove_Preview_Tiles)
        {
            if (Path_Highlight_Tiles.Contains(Tile)) continue;
            if (Movement_Range_Highlights.Contains(Tile) && Movement_Range_Material != null)
                Paint_Tile(Tile, Movement_Range_Material);
            else
                Paint_Base(Tile);
        }
        DOW_Shove_Preview_Tiles.Clear();
    }

    private void Prune_DOW_Unreachable_Highlights(Model_Standard_Behavior Model)
    {
        if (Model == null || !Model.Has_Status(Status_Type.Death_On_Wheels) || Movement_Range_Highlights.Count == 0) return;

        int Push = 2;
        if (Model.Ability is Death_On_Wheels_Ability Dow) Push = Mathf.Max(1, Dow.Shove_Push);

        Vector2Int Start = new Vector2Int(Model.Current_X, Model.Current_Y);
        int Budget = Model.Movement_Remaining_This_Turn;
        var To_Remove = new List<Vector2Int>();

        foreach (var Tile in Movement_Range_Highlights)
        {
            var Route = Pathfinder.Find_DOW_Route(Start, Tile, Budget);
            if (Route == null || Route.Count < 2 || !Simulate_DOW_Walk(Model, Route, Push))
                To_Remove.Add(Tile);
        }

        foreach (var Tile in To_Remove)
        {
            Movement_Range_Highlights.Remove(Tile);
            Paint_Base(Tile);
        }
    }

    private bool Simulate_DOW_Walk(Model_Standard_Behavior Model, List<Vector2Int> Path, int Push_Distance)
    {
        if (Path == null || Path.Count < 2) return false;

        var Shadow_Models = new Dictionary<Vector2Int, Model_Standard_Behavior>();
        for (int X = 0; X < Board.Get_Tile_Count_X(); X++)
            for (int Y = 0; Y < Board.Get_Tile_Count_Y(); Y++)
                if (Board.Get_Model_At(X, Y) != null && Board.Get_Model_At(X, Y) != Model)
                    Shadow_Models[new Vector2Int(X, Y)] = Board.Get_Model_At(X, Y);

        var Shadow_Tiles = new Board_Modifiers[Board.Get_Tile_Count_X(), Board.Get_Tile_Count_Y()];
        for (int X = 0; X < Board.Get_Tile_Count_X(); X++)
            for (int Y = 0; Y < Board.Get_Tile_Count_Y(); Y++)
                Shadow_Tiles[X, Y] = Board.Get_Tile_Type_At(X, Y);

        Vector2Int Current = Path[0];

        for (int i = 1; i < Path.Count; i++)
        {
            Vector2Int Next = Path[i];

            if (Shadow_Tiles[Next.x, Next.y] == Board_Modifiers.Wall || Shadow_Tiles[Next.x, Next.y] == Board_Modifiers.Cover)
                Shadow_Tiles[Next.x, Next.y] = Board_Modifiers.None;

            if (Shadow_Models.TryGetValue(Next, out var Victim) && Victim.Team != Model.Team)
            {
                Shadow_Push_Chain(Shadow_Tiles, Victim, Next, Next - Current, Push_Distance, Shadow_Models, null);
                if (Shadow_Models.ContainsKey(Next)) return false;
            }
            Current = Next;
        }
        return true;
    }

    private void Shadow_Push_Chain(Board_Modifiers[,] Shadow_Tiles, Model_Standard_Behavior Victim, Vector2Int Victim_Pos, Vector2Int Direction, int Max_Distance, Dictionary<Vector2Int, Model_Standard_Behavior> Shadow_Models, Dictionary<Model_Standard_Behavior, Vector2Int> Final_Positions)
    {
        Vector2Int Current = Victim_Pos;
        int Moved = 0;

        for (int Step = 0; Step < Max_Distance; Step++)
        {
            Vector2Int Next = Current + Direction;
            if (Next.x < 0 || Next.x >= Board.Get_Tile_Count_X() || Next.y < 0 || Next.y >= Board.Get_Tile_Count_Y()) break;

            if (Shadow_Tiles[Next.x, Next.y] == Board_Modifiers.Wall || Shadow_Tiles[Next.x, Next.y] == Board_Modifiers.Cover)
                Shadow_Tiles[Next.x, Next.y] = Board_Modifiers.None;

            Board_Modifiers Next_Type = Shadow_Tiles[Next.x, Next.y];
            if (Next_Type == Board_Modifiers.Wall || Next_Type == Board_Modifiers.Cover) break;

            if (Shadow_Models.TryGetValue(Next, out var Blocker))
            {
                Shadow_Push_Chain(Shadow_Tiles, Blocker, Next, Direction, Max_Distance - Step, Shadow_Models, Final_Positions);
                if (Shadow_Models.ContainsKey(Next)) break;
            }

            Shadow_Models.Remove(Current);
            Shadow_Models[Next] = Victim;
            Current = Next;
            Moved++;
        }

        if (Moved > 0 && Final_Positions != null) Final_Positions[Victim] = Current;
    }

    // ============================================================
    // TILE PAINTING & UTILS
    // ============================================================
    public void Paint_Tile(Vector2Int Tile, Material Mat)
    {
        if (Mat == null) return;
        var Renderer = Get_Renderer(Tile);
        if (Renderer == null) return;

        Renderer.material = Mat;
        Color C = Renderer.material.color;
        C.a = 1f;
        Renderer.material.color = C;
    }

    public void Paint_Base(Vector2Int Tile)
    {
        var Renderer = Get_Renderer(Tile);
        if (Renderer == null) return;

        Renderer.material = Get_Base_Tile_Material(Tile.x, Tile.y);
        Color C = Renderer.material.color;
        C.a = 1f;
        Renderer.material.color = C;
    }

    private void Update_Flashing_Effect()
    {
        // Now uses the exact Inspector values for EVERYTHING
        float Alpha = Mathf.Lerp(Flash_Min_Alpha, Flash_Max_Alpha, (Mathf.Sin(Time.time * Flash_Speed) + 1.0f) * 0.5f);

        Apply_Alpha_To_Highlight_Group(Spawn_Tile_Highlights, Alpha);
        Apply_Alpha_To_Highlight_Group(Model_Placement_Highlights, Alpha);

        Apply_Alpha_To_Highlight_Group(Movement_Range_Highlights, Alpha);
        Apply_Alpha_To_Highlight_Group(Shove_Target_Highlights, Alpha);
        Apply_Alpha_To_Highlight_Group(Shove_Destination_Highlights, Alpha);
        Apply_Alpha_To_Highlight_Group(DOW_Shove_Preview_Tiles, Alpha);
        Apply_Alpha_To_Path_Tiles(Alpha);
    }

    public void Apply_Alpha_To_Highlight_Group(List<Vector2Int> Highlight_Group, float Alpha)
    {
        foreach (Vector2Int Tile in Highlight_Group)
        {
            var Renderer = Get_Renderer(Tile);
            if (Renderer != null && Renderer.material != null)
            {
                Color Color = Renderer.material.color;
                Color.a = Alpha;
                Renderer.material.color = Color;
            }
        }
    }

    private void Apply_Alpha_To_Path_Tiles(float Alpha)
    {
        foreach (Vector2Int Tile in Path_Highlight_Tiles)
        {
            if (Movement_Range_Highlights.Contains(Tile) || Shove_Target_Highlights.Contains(Tile) || Shove_Destination_Highlights.Contains(Tile) || DOW_Shove_Preview_Tiles.Contains(Tile)) continue;

            var Renderer = Get_Renderer(Tile);
            if (Renderer == null || Renderer.material == null) continue;

            Color Color = Renderer.material.color;
            Color.a = Alpha;
            Renderer.material.color = Color;
        }
    }

    private MeshRenderer Get_Renderer(Vector2Int Tile)
    {
        var Tiles = Board.Get_Tiles();
        if (Tiles == null || Tile.x < 0 || Tile.x >= Tiles.GetLength(0) || Tile.y < 0 || Tile.y >= Tiles.GetLength(1)) return null;
        var GO = Tiles[Tile.x, Tile.y];
        return GO != null ? GO.GetComponent<MeshRenderer>() : null;
    }

    private Material Get_Path_Material(int Priority)
    {
        int Slot_Count = Mathf.Min(Path_Slot_Count, Path_Materials.Length);
        if (Slot_Count <= 0)
        {
            if (Debug_Log_Path_Materials) Debug.LogWarning($"[PathMat] Get_Path_Material({Priority}): Slot_Count=0.");
            return null;
        }

        if (Priority < 0) Priority = 0;
        if (Priority >= Slot_Count) Priority = Slot_Count - 1;

        Material Mat = Path_Materials[Priority];
        if (Mat != null)
        {
            if (Debug_Log_Path_Materials) Debug.Log($"[PathMat] Get_Path_Material({Priority}) -> '{Mat.name}'");
            return Mat;
        }

        for (int i = Priority - 1; i >= 0; i--)
        {
            if (Path_Materials[i] != null)
            {
                if (Debug_Log_Path_Materials) Debug.LogWarning($"[PathMat] slot {Priority} NULL, fell back to slot {i} ('{Path_Materials[i].name}')");
                return Path_Materials[i];
            }
        }

        if (Debug_Log_Path_Materials) Debug.LogWarning($"[PathMat] Get_Path_Material({Priority}) -> NULL.");
        return null;
    }

    private void Show_Path_Highlights(List<Vector2Int> Path, int Priority, Model_Standard_Behavior Dragged_Model)
    {
        bool Is_Primary = (Priority == 0);
        Material Regular_Path_Material = Get_Path_Material(Priority);

        foreach (Vector2Int Tile in Path)
        {
            if (!Path_Highlight_Tiles.Contains(Tile)) Path_Highlight_Tiles.Add(Tile);

            Material DOW = Get_DOW_Tile_Override(Dragged_Model, Tile.x, Tile.y);
            if (DOW != null)
            {
                Paint_Tile(Tile, DOW);
                continue;
            }

            if (Is_Primary && Board.Get_Tile_Type_At(Tile.x, Tile.y) == Board_Modifiers.Hazard && Hazard_Path_Material != null)
                Paint_Tile(Tile, Hazard_Path_Material);
            else if (Regular_Path_Material != null)
                Paint_Tile(Tile, Regular_Path_Material);
        }
    }

    public Material Get_DOW_Tile_Override(Model_Standard_Behavior Dragged_Model, int X, int Y)
    {
        if (Dragged_Model == null || !Dragged_Model.Has_Status(Status_Type.Death_On_Wheels)) return null;

        Board_Modifiers Type = Board.Get_Tile_Type_At(X, Y);
        if (Type == Board_Modifiers.Wall || Type == Board_Modifiers.Cover)
        {
            if (Death_On_Wheels_Destroy_Material != null) return Death_On_Wheels_Destroy_Material;
            if (Sprint_Range_Material != null) return Sprint_Range_Material;
            return Movement_Range_Material;
        }

        Model_Standard_Behavior Occupant = Board.Get_Model_At(X, Y);
        if (Occupant != null && Occupant.Team != Dragged_Model.Team) return Shove_Target_Material;

        return null;
    }

    private void Repaint_Shove_Highlights()
    {
        if (Shove_Target_Material != null)
        {
            foreach (Vector2Int Tile in Shove_Target_Highlights)
            {
                Material Mat = (Shove_Highlights_Are_Blocked && Shove_Blocked_Material != null) ? Shove_Blocked_Material : Shove_Target_Material;
                Paint_Tile(Tile, Mat);
            }
        }

        if (Shove_Destination_Material != null)
        {
            foreach (Vector2Int Tile in Shove_Destination_Highlights)
                Paint_Tile(Tile, Shove_Destination_Material);
        }
    }
}