using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure pathfinding queries for the board. Given a start, a target, and
/// a set of constraints, returns paths. Does not mutate board state,
/// does not paint tiles, does not apply effects.
///
/// Sidecar to Battle_Board_Behavior: lives on the same GameObject (or a
/// sibling), takes a reference to the board, and reads board state
/// through the board's public getters.
/// </summary>
public class Board_Pathfinder : MonoBehaviour
{
    // ============================================================
    // CONSTANTS
    // ============================================================

    private static readonly Vector2Int[] Cardinal_Directions =
    {
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0)
    };


    // ============================================================
    // SERIALIZED FIELDS
    // ============================================================

    [Header("Board Reference")]
    [Tooltip("The board whose state this pathfinder queries.")]
    [SerializeField] private Battle_Board_Behavior Board;

    [Header("Shove Path Generation")]
    [Tooltip("Maximum number of shove paths shown to the player. Generation " +
             "may produce more candidates; the curator trims to this many.")]
    [Range(1, 12)]
    [SerializeField] private int Max_Shove_Paths_Shown = 8;

    [Tooltip("If true, keep every shortest route to each stop tile, so a " +
             "single approach direction can appear multiple times (once per " +
             "route). If false, keep only one route per approach direction.")]
    [SerializeField] private bool Show_Alternate_Routes_Per_Direction = true;

    [Tooltip("Within a direction, prefer routes with fewer hazard tiles " +
             "when choosing which routes survive the cap.")]
    [SerializeField] private bool Prefer_Hazard_Free_Routes = true;

    [Header("Debug Logging")]
    [SerializeField] private bool Debug_Log_Pathfinding = false;


    // ============================================================
    // MOVEMENT PATHFINDING
    // ============================================================

    /// <summary>
    /// Multi-parent BFS returning every shortest path from Start to
    /// Target within Max_Distance. Only equal-distance parents are
    /// recorded, so the parent graph is a DAG and cannot cycle.
    ///
    /// Moving_Team controls who can pass through whom: same-team models
    /// are traversable, others block (except the target itself).
    /// Death_On_Wheels opens all tiles and all models.
    /// </summary>
    public List<List<Vector2Int>> Find_All_Paths(
        Vector2Int Start,
        Vector2Int Target,
        int Max_Distance,
        int Moving_Team,
        bool Death_On_Wheels)
    {
        List<List<Vector2Int>> All_Paths = new List<List<Vector2Int>>();

        if (Start == Target) return All_Paths;

        int Count_X = Board.Get_Tile_Count_X();
        int Count_Y = Board.Get_Tile_Count_Y();

        Queue<Vector2Int> Queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> Distance = new Dictionary<Vector2Int, int>();
        Dictionary<Vector2Int, List<Vector2Int>> Parents = new Dictionary<Vector2Int, List<Vector2Int>>();

        Queue.Enqueue(Start);
        Distance[Start] = 0;
        Parents[Start] = new List<Vector2Int>();

        int Shortest_Path_Length = int.MaxValue;

        while (Queue.Count > 0)
        {
            Vector2Int Current = Queue.Dequeue();
            int Current_Dist = Distance[Current];

            if (Current_Dist >= Shortest_Path_Length) continue;
            if (Current_Dist >= Max_Distance) continue;

            foreach (Vector2Int Dir in Cardinal_Directions)
            {
                Vector2Int Neighbor = Current + Dir;

                if (Neighbor.x < 0 || Neighbor.x >= Count_X ||
                    Neighbor.y < 0 || Neighbor.y >= Count_Y)
                    continue;

                bool Passable = Board.Is_Tile_Passable(Neighbor.x, Neighbor.y) || Death_On_Wheels;
                if (!Passable) continue;

                bool Is_Target = (Neighbor == Target);

                Model_Standard_Behavior Occupant = Board.Get_Model_At(Neighbor.x, Neighbor.y);
                if (Occupant != null)
                {
                    if (Occupant.Team == Moving_Team) { }
                    else if (Death_On_Wheels) { }
                    else if (!Is_Target) continue;
                }

                int New_Dist = Current_Dist + 1;

                if (!Distance.ContainsKey(Neighbor))
                {
                    Distance[Neighbor] = New_Dist;
                    Parents[Neighbor] = new List<Vector2Int> { Current };
                    Queue.Enqueue(Neighbor);

                    if (Is_Target && New_Dist < Shortest_Path_Length)
                        Shortest_Path_Length = New_Dist;
                }
                else if (Distance[Neighbor] == New_Dist)
                {
                    Parents[Neighbor].Add(Current);
                }
            }
        }

        if (Parents.ContainsKey(Target))
            Reconstruct_Paths(Target, Parents, new List<Vector2Int>(), All_Paths);

        return All_Paths;
    }

    /// <summary>
    /// Single-path BFS. Allies pass through; enemies block except the
    /// target tile itself. Returns the path including both endpoints, or
    /// an empty list if no path exists.
    /// </summary>
    public List<Vector2Int> Find_Shortest_Path(
        Vector2Int Start,
        Vector2Int Target,
        int Max_Distance,
        int Moving_Team,
        bool Death_On_Wheels)
    {
        if (Start == Target) return new List<Vector2Int> { Start };
        if (Max_Distance < 1) return new List<Vector2Int>();

        int Count_X = Board.Get_Tile_Count_X();
        int Count_Y = Board.Get_Tile_Count_Y();

        var Came_From = new Dictionary<Vector2Int, Vector2Int>();
        var Distance = new Dictionary<Vector2Int, int> { [Start] = 0 };
        var Queue = new Queue<Vector2Int>();
        Queue.Enqueue(Start);

        while (Queue.Count > 0)
        {
            Vector2Int Current = Queue.Dequeue();
            int Current_Dist = Distance[Current];

            if (Current_Dist >= Max_Distance) continue;

            foreach (Vector2Int Dir in Cardinal_Directions)
            {
                Vector2Int Neighbor = Current + Dir;

                if (Neighbor.x < 0 || Neighbor.x >= Count_X ||
                    Neighbor.y < 0 || Neighbor.y >= Count_Y)
                    continue;

                bool Passable = Board.Is_Tile_Passable(Neighbor.x, Neighbor.y) || Death_On_Wheels;
                if (!Passable) continue;

                bool Is_Target = (Neighbor == Target);

                Model_Standard_Behavior Occupant = Board.Get_Model_At(Neighbor.x, Neighbor.y);
                if (Occupant != null)
                {
                    if (Occupant.Team == Moving_Team) { }
                    else if (Death_On_Wheels) { }
                    else if (!Is_Target) continue;
                }

                if (Distance.ContainsKey(Neighbor)) continue;

                Distance[Neighbor] = Current_Dist + 1;
                Came_From[Neighbor] = Current;
                Queue.Enqueue(Neighbor);

                if (Is_Target)
                    return Reconstruct_Single_Path(Came_From, Start, Target);
            }
        }

        return new List<Vector2Int>();
    }

    /// <summary>
    /// BFS route for Death on Wheels. Treats every tile as passable,
    /// since walls get destroyed and enemies get shoved. Returns null
    /// if no route exists within the budget.
    /// </summary>
    public List<Vector2Int> Find_DOW_Route(
        Vector2Int Start,
        Vector2Int Target,
        int Max_Distance)
    {
        if (Start == Target) return new List<Vector2Int> { Start };

        int Count_X = Board.Get_Tile_Count_X();
        int Count_Y = Board.Get_Tile_Count_Y();

        var Came_From = new Dictionary<Vector2Int, Vector2Int>();
        var Dist = new Dictionary<Vector2Int, int> { [Start] = 0 };
        var Queue = new Queue<Vector2Int>();
        Queue.Enqueue(Start);

        while (Queue.Count > 0)
        {
            var Cur = Queue.Dequeue();
            int D = Dist[Cur];
            if (D >= Max_Distance) continue;

            foreach (var Dir in Cardinal_Directions)
            {
                var Nb = Cur + Dir;
                if (Nb.x < 0 || Nb.x >= Count_X) continue;
                if (Nb.y < 0 || Nb.y >= Count_Y) continue;
                if (Dist.ContainsKey(Nb)) continue;

                Dist[Nb] = D + 1;
                Came_From[Nb] = Cur;
                Queue.Enqueue(Nb);

                if (Nb == Target)
                    return Reconstruct_Single_Path(Came_From, Start, Target);
            }
        }

        return null;
    }


    // ============================================================
    // SHOVE PATHFINDING
    // ============================================================

    /// <summary>
    /// Generates the shove path candidates for a given approach and
    /// curates them down to a manageable set. Called by the board when
    /// previewing a sprint-shove.
    /// </summary>
    public List<List<Vector2Int>> Find_Shove_Paths(
        Vector2Int Start,
        Vector2Int Enemy_Position,
        int Max_Distance,
        int Moving_Team)
    {
        List<List<Vector2Int>> Candidates = Generate_Shove_Path_Candidates(
            Start, Enemy_Position, Max_Distance, Moving_Team);

        return Curate_Shove_Paths(Candidates, Enemy_Position);
    }


    // ============================================================
    // DIAGNOSTIC STRINGS
    // ============================================================

    public string Analyze_Path(List<Vector2Int> Path)
    {
        int Hazard_Count = 0;
        int Terrain_Count = 0;

        foreach (Vector2Int Tile in Path)
        {
            Board_Modifiers Modifier = Board.Get_Tile_Type_At(Tile.x, Tile.y);
            if (Modifier == Board_Modifiers.Hazard) Hazard_Count++;
            else if (Modifier == Board_Modifiers.Terrain) Terrain_Count++;
        }

        string Message = $"len={Path.Count}";
        if (Hazard_Count > 0) Message += $", Haz={Hazard_Count}";
        if (Terrain_Count > 0) Message += $", Ter={Terrain_Count}";

        return Message;
    }

    public string Describe_Path(List<Vector2Int> Path)
    {
        if (Path == null || Path.Count == 0)
            return "(empty)";

        string Result = "[";
        for (int i = 0; i < Path.Count; i++)
        {
            Result += $"({Path[i].x},{Path[i].y})";
            if (i < Path.Count - 1) Result += " -> ";
        }
        Result += "]";
        return Result;
    }


    // ============================================================
    // PRIVATE HELPERS
    // ============================================================

    private List<List<Vector2Int>> Generate_Shove_Path_Candidates(
        Vector2Int Start,
        Vector2Int Enemy_Position,
        int Max_Distance,
        int Moving_Team)
    {
        var Result = new List<List<Vector2Int>>();

        if (Start == Enemy_Position || Max_Distance < 0)
            return Result;

        int Count_X = Board.Get_Tile_Count_X();
        int Count_Y = Board.Get_Tile_Count_Y();

        foreach (Vector2Int Dir in Cardinal_Directions)
        {
            Vector2Int Stop_Tile = Enemy_Position - Dir;

            if (Stop_Tile.x < 0 || Stop_Tile.x >= Count_X ||
                Stop_Tile.y < 0 || Stop_Tile.y >= Count_Y)
                continue;

            if (!Board.Is_Tile_Passable(Stop_Tile.x, Stop_Tile.y))
                continue;

            Model_Standard_Behavior Occupant = Board.Get_Model_At(Stop_Tile.x, Stop_Tile.y);
            if (Occupant != null && Stop_Tile != Start)
                continue;

            List<List<Vector2Int>> Sub_Paths;

            if (Stop_Tile == Start)
            {
                Sub_Paths = new List<List<Vector2Int>>
                {
                    new List<Vector2Int> { Start }
                };
            }
            else if (Show_Alternate_Routes_Per_Direction)
            {
                Sub_Paths = Find_All_Paths(Start, Stop_Tile, Max_Distance,
                                            Moving_Team, Death_On_Wheels: false);
            }
            else
            {
                Sub_Paths = new List<List<Vector2Int>>();
                List<Vector2Int> Single = Find_Shortest_Path(
                    Start, Stop_Tile, Max_Distance, Moving_Team, Death_On_Wheels: false);
                if (Single.Count > 0)
                    Sub_Paths.Add(Single);
            }

            if (Debug_Log_Pathfinding)
                Debug.Log($"[ShoveGen] Dir={Dir}, Start={Start}, StopTile={Stop_Tile}, " +
                          $"reachable={Sub_Paths.Count}");

            foreach (var Sub_Path in Sub_Paths)
            {
                var Full_Path = new List<Vector2Int>(Sub_Path) { Enemy_Position };
                Result.Add(Full_Path);
            }
        }

        return Result;
    }

    private List<List<Vector2Int>> Curate_Shove_Paths(
        List<List<Vector2Int>> Candidates,
        Vector2Int Enemy_Position)
    {
        if (Candidates.Count == 0)
            return Candidates;

        var By_Direction = new Dictionary<Vector2Int, List<List<Vector2Int>>>();
        foreach (var Path in Candidates)
        {
            if (Path.Count < 2) continue;
            Vector2Int Dir = Path[Path.Count - 1] - Path[Path.Count - 2];
            if (!By_Direction.TryGetValue(Dir, out var List))
            {
                List = new List<List<Vector2Int>>();
                By_Direction[Dir] = List;
            }
            List.Add(Path);
        }

        foreach (var Entry in By_Direction)
        {
            Entry.Value.Sort((A, B) =>
            {
                int Length_Compare = A.Count.CompareTo(B.Count);
                if (Length_Compare != 0) return Length_Compare;

                if (Prefer_Hazard_Free_Routes)
                {
                    int Haz_A = Count_Hazards(A);
                    int Haz_B = Count_Hazards(B);
                    int Haz_Compare = Haz_A.CompareTo(Haz_B);
                    if (Haz_Compare != 0) return Haz_Compare;
                }

                return 0;
            });
        }

        var Chosen = new List<List<Vector2Int>>();
        var Remainders = new List<List<Vector2Int>>();

        foreach (var Entry in By_Direction)
        {
            Chosen.Add(Entry.Value[0]);
            for (int i = 1; i < Entry.Value.Count; i++)
                Remainders.Add(Entry.Value[i]);
        }

        if (Remainders.Count > 0 && Chosen.Count < Max_Shove_Paths_Shown)
        {
            Remainders.Sort((A, B) =>
            {
                int Length_Compare = A.Count.CompareTo(B.Count);
                if (Length_Compare != 0) return Length_Compare;

                if (Prefer_Hazard_Free_Routes)
                {
                    int Haz_A = Count_Hazards(A);
                    int Haz_B = Count_Hazards(B);
                    int Haz_Compare = Haz_A.CompareTo(Haz_B);
                    if (Haz_Compare != 0) return Haz_Compare;
                }

                return 0;
            });

            foreach (var Extra in Remainders)
            {
                if (Chosen.Count >= Max_Shove_Paths_Shown) break;
                Chosen.Add(Extra);
            }
        }

        Chosen.Sort((A, B) => A.Count.CompareTo(B.Count));

        if (Chosen.Count > Max_Shove_Paths_Shown)
            Chosen.RemoveRange(Max_Shove_Paths_Shown, Chosen.Count - Max_Shove_Paths_Shown);

        return Chosen;
    }

    private int Count_Hazards(List<Vector2Int> Path)
    {
        int Count = 0;
        foreach (Vector2Int Tile in Path)
            if (Board.Get_Tile_Type_At(Tile.x, Tile.y) == Board_Modifiers.Hazard)
                Count++;
        return Count;
    }

    private void Reconstruct_Paths(
        Vector2Int Current,
        Dictionary<Vector2Int, List<Vector2Int>> Parents,
        List<Vector2Int> Current_Path,
        List<List<Vector2Int>> All_Paths)
    {
        if (Parents[Current].Count > 0)
            Current_Path.Insert(0, Current);

        if (Parents[Current].Count == 0)
        {
            Current_Path.Insert(0, Current);
            All_Paths.Add(new List<Vector2Int>(Current_Path));
            return;
        }

        foreach (Vector2Int Parent in Parents[Current])
            Reconstruct_Paths(Parent, Parents, new List<Vector2Int>(Current_Path), All_Paths);
    }

    private static List<Vector2Int> Reconstruct_Single_Path(
        Dictionary<Vector2Int, Vector2Int> Came_From,
        Vector2Int Start,
        Vector2Int Target)
    {
        var Path = new List<Vector2Int> { Target };
        Vector2Int Current = Target;

        while (Current != Start)
        {
            Current = Came_From[Current];
            Path.Add(Current);
        }

        Path.Reverse();
        return Path;
    }

    public Dictionary<Vector2Int, int> Get_Distance_Map_To_Target(Vector2Int Target, int Moving_Team)
    {
        var Map = new Dictionary<Vector2Int, int>();
        var Queue = new Queue<Vector2Int>();

        Map[Target] = 0;
        Queue.Enqueue(Target);

        while (Queue.Count > 0)
        {
            var Current = Queue.Dequeue();
            int Dist = Map[Current];

            foreach (var Dir in Cardinal_Directions)
            {
                var Neighbor = Current + Dir;
                if (Neighbor.x < 0 || Neighbor.x >= Board.Get_Tile_Count_X() || Neighbor.y < 0 || Neighbor.y >= Board.Get_Tile_Count_Y()) continue;
                if (Map.ContainsKey(Neighbor)) continue;
                if (!Board.Is_Tile_Passable(Neighbor.x, Neighbor.y)) continue;

                // Do NOT check for units here. We just want the pure distance to the Minotaur.
                Map[Neighbor] = Dist + 1;
                Queue.Enqueue(Neighbor);
            }
        }
        return Map;
    }

    public Vector2Int Get_Closest_Adjacent_Tile(Vector2Int sourcePos, Vector2Int minoPos, int Moving_Team)
    {
        Vector2Int bestTarget = minoPos; // Fallback to Minotaur's tile
        int bestPathLength = int.MaxValue;

        foreach (var dir in Cardinal_Directions)
        {
            Vector2Int adj = minoPos + dir;
            if (adj.x >= 0 && adj.x < Board.Get_Tile_Count_X() && adj.y >= 0 && adj.y < Board.Get_Tile_Count_Y())
            {
                // Tile must be passable and not occupied (unless it's the unit itself)
                if (Board.Is_Tile_Passable(adj.x, adj.y) && (Board.Get_Model_At(adj.x, adj.y) == null || adj == sourcePos))
                {
                    // Find the actual path to this adjacent tile using the correct team
                    var path = Find_Shortest_Path(sourcePos, adj, 99, Moving_Team, false);
                    if (path != null && path.Count > 0)
                    {
                        if (path.Count < bestPathLength)
                        {
                            bestPathLength = path.Count;
                            bestTarget = adj;
                        }
                    }
                }
            }
        }
        return bestTarget;
    }
}