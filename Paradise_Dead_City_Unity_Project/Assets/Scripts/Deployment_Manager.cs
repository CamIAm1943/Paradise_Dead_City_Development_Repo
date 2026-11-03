using System.Collections.Generic;
using UnityEngine;

public class Deployment_Manager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Battle_Board_Behavior Board;
    [SerializeField] private Match_Flow_Manager MatchFlow;
    [SerializeField] private Board_Highlight_Renderer Highlighter;
    [SerializeField] private Army_UI_Controller Army_UI;
    [SerializeField] private Initiative_UI_Controller Initiative_UI;

    [Header("Smooth Motion")]
    [Tooltip("Seconds for a discrete board move. Lower is snappier.")]
    [SerializeField] private float Board_Move_Duration = 0.3f;

    [Header("Spawn Zones (Leave empty to auto-generate)")]
    [SerializeField] private List<Vector2Int> Player_1_Spawn_Zone = new List<Vector2Int>();
    [SerializeField] private List<Vector2Int> Player_2_Spawn_Zone = new List<Vector2Int>();

    [Header("Placement Rules")]
    [SerializeField] private int Placement_Reach_X = 3;
    [SerializeField] private int Placement_Reach_Y = 1;

    [Header("Materials / Prefabs")]
    [SerializeField] private GameObject Spawn_Tile_Prefab;

    private Army_Card_UI Active_Card;

    // Track exactly where the spawn point was clicked
    private Vector2Int Player_1_Spawn_Pos = Battle_Board_Behavior.No_Tile;
    private Vector2Int Player_2_Spawn_Pos = Battle_Board_Behavior.No_Tile;

    // ============================================================
    // PHASE ENTRY POINTS
    // ============================================================

    public void Begin_Player_1_Spawn()
    {
        Generate_Spawn_Zone_If_Empty(1);
        if (Highlighter != null) Highlighter.Show_Spawn_Zone(Player_1_Spawn_Zone);
    }

    public void Begin_Player_2_Spawn()
    {
        Generate_Spawn_Zone_If_Empty(2);
        if (Highlighter != null) Highlighter.Show_Spawn_Zone(Player_2_Spawn_Zone);
    }

    public void Begin_Model_Placement(int Team, List<Model_Type> Units_To_Place)
    {
        if (Highlighter != null) Highlighter.Clear_Spawn_Zone();
        var Tiles = Build_Placement_Tiles(Team);
        Highlighter.Show_Model_Placement_Zone(Tiles);
        if (Army_UI != null) Army_UI.Setup_Deployment_Bar(Team, Units_To_Place);
    }

    private void Generate_Spawn_Zone_If_Empty(int Player)
    {
        if (Board == null) return;

        if (Player == 1 && Player_1_Spawn_Zone.Count == 0)
        {
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < Board.Get_Tile_Count_Y(); y++)
                    Player_1_Spawn_Zone.Add(new Vector2Int(x, y));
        }

        if (Player == 2 && Player_2_Spawn_Zone.Count == 0)
        {
            int maxX = Board.Get_Tile_Count_X();
            for (int x = maxX - 2; x < maxX; x++)
                for (int y = 0; y < Board.Get_Tile_Count_Y(); y++)
                    Player_2_Spawn_Zone.Add(new Vector2Int(x, y));
        }
    }

    // ============================================================
    // CARD INTERACTION
    // ============================================================

    public void Select_Deployment_Card(Army_Card_UI Card)
    {
        Active_Card = Card;
        if (Army_UI != null) Army_UI.Highlight_Deployment_Selection(Card);

        if (Highlighter != null && Card != null)
            Highlighter.Show_Model_Placement_Zone(Build_Placement_Tiles(MatchFlow.Active_Player));
    }

    public void Begin_Deployment_Drag(Army_Card_UI Card)
    {
        Active_Card = Card;
        if (Highlighter != null && Card != null)
            Highlighter.Show_Model_Placement_Zone(Build_Placement_Tiles(MatchFlow.Active_Player));
    }

    public void End_Deployment_Drag()
    {
        // We intentionally leave this empty.
        // Try_Place_At handles the cleanup when successful.
        // If dropped in an invalid spot, this leaves the highlights and card active 
        // so the player can just click a valid tile to place the unit.
    }

    // ============================================================
    // PLACEMENT LOGIC
    // ============================================================

    public bool Try_Place_At(Vector2Int Tile)
    {
        if (Highlighter == null || MatchFlow == null || Board == null) return false;

        // 1. Handle Spawn Point Placement
        if (MatchFlow.Current_Phase == Game_State.Player_1_Place_Spawn || MatchFlow.Current_Phase == Game_State.Player_2_Place_Spawn)
        {
            if (Highlighter.Spawn_Tile_Highlights.Contains(Tile))
            {
                if (MatchFlow.Current_Phase == Game_State.Player_1_Place_Spawn) Player_1_Spawn_Pos = Tile;
                else Player_2_Spawn_Pos = Tile;

                if (Spawn_Tile_Prefab != null)
                    Instantiate(Spawn_Tile_Prefab, Board.Get_Tile_Center(Tile.x, Tile.y), Quaternion.identity, Board.transform);

                Highlighter.Clear_Spawn_Zone();

                if (MatchFlow.Current_Phase == Game_State.Player_1_Place_Spawn)
                {
                    MatchFlow.Current_Phase = Game_State.Player_1_Place_Models;
                    Begin_Model_Placement(1, Get_Roster_For_Team(1));
                }
                else
                {
                    MatchFlow.Current_Phase = Game_State.Player_2_Place_Models;
                    Begin_Model_Placement(2, Get_Roster_For_Team(2));
                }
                return true;
            }
            return false;
        }

        // 2. Handle Model Placement
        if ((MatchFlow.Current_Phase == Game_State.Player_1_Place_Models || MatchFlow.Current_Phase == Game_State.Player_2_Place_Models) && Active_Card != null)
        {
            if (!Highlighter.Model_Placement_Highlights.Contains(Tile)) return false;

            Model_Type Type = Active_Card.Get_Deployment_Type();
            Faction_Data_SO Faction = MatchFlow.Active_Player == 1 ? MatchFlow.Player_1_Faction : MatchFlow.Player_2_Faction;
            if (Faction == null) return false;

            GameObject Prefab = Faction.Get_Prefab_By_Type(Type);
            Model_Stats_SO Stats = Faction.Get_Stats_By_Type(Type);
            if (Prefab == null || Stats == null) return false;

            GameObject Instance = Instantiate(Prefab, Board.Get_Unit_Stand_Position(Tile.x, Tile.y), Quaternion.identity);
            Model_Standard_Behavior Model = Instance.GetComponent<Model_Standard_Behavior>();
            if (Model == null) return false;

            // --- INITIALIZE MODEL DATA ---
            Model.Team = MatchFlow.Active_Player;
            Faction_Data_SO FactionData = MatchFlow.Active_Player == 1 ? MatchFlow.Player_1_Faction : MatchFlow.Player_2_Faction;
            if (FactionData != null && FactionData.Team_Materials.Length > 0)
            {
                Model.Player_1_Mat = MatchFlow.Player_1_Faction.Team_Materials[0];
                Model.Player_2_Mat = MatchFlow.Player_2_Faction.Team_Materials[0];
            }
            Model.Stats = Stats;
            Model.Current_Health = Stats.Health;
            Model.Current_X = Tile.x;
            Model.Current_Y = Tile.y;
            Model.Type = Type;
            Model.Set_Position(Board.Get_Unit_Stand_Position(Tile.x, Tile.y), true);

            // 1. Initialize the Ability
            if (Model.Stats != null && Model.Stats.Ability_Data != null)
            {
                Model.Ability = Model.Stats.Ability_Data;
                Model.Ability_Recharge_Remaining = 0;
                Model.Ability.On_Equip(Model);
            }

            // 2. Initialize the Status Registry
            Model.Set_Status_Registry(Board.Get_Status_Registry());
            Model.Configure_Motion(Board_Move_Duration, 15f, true);

            Board.Place_Model_At(Tile.x, Tile.y, Model);
            MatchFlow.Turn_Order_List.Add(Model);
            if (MatchFlow.Turn_Order_List.Count == 1) MatchFlow.Current_Turn_Index = 0;
            Model.Initiative_Roll = Random.Range(1, 7);

            // 3. Remove the card from the UI
            if (Army_UI != null) Army_UI.Remove_Deployment_Card(Active_Card);

            Highlighter.Clear_Model_Placement_Zone();
            Active_Card = null;

            // 4. Check if the player has placed all their models
            int totalRoster = Get_Roster_For_Team(MatchFlow.Active_Player).Count;
            int placedCount = Count_Placed_Models(MatchFlow.Active_Player);

            if (placedCount >= totalRoster)
            {
                Advance_Phase();
            }
            else
            {
                // Re-show the placement zone for the next unit
                Highlighter.Show_Model_Placement_Zone(Build_Placement_Tiles(MatchFlow.Active_Player));
            }

            return true;
        }

        return false;
    }

    private void Advance_Phase()
    {
        if (MatchFlow.Current_Phase == Game_State.Player_1_Place_Models)
        {
            MatchFlow.Current_Phase = Game_State.Player_2_Place_Spawn;
            MatchFlow.Set_Active_Player(2);
            Begin_Player_2_Spawn();
        }
        else if (MatchFlow.Current_Phase == Game_State.Player_2_Place_Models)
        {
            // 1. Roll D20 and sort the Turn_Order_List
            Roll_Initiative();

            MatchFlow.Current_Phase = Game_State.Gameplay;
            MatchFlow.Current_Turn_Index = 0;

            MatchFlow.Set_Active_Player(MatchFlow.Active_Model.Team);

            if (Highlighter != null) Highlighter.Clear_Model_Placement_Zone();

            // Setup the UI for the winner
            if (Army_UI != null) Army_UI.Setup_Army_Bar(MatchFlow.Active_Model.Team);
            if (Initiative_UI != null) Initiative_UI.Setup_Initiative_Bar();

            // Start the turn
            MatchFlow.Start_Active_Model_Turn();
        }
    }

    private void Roll_Initiative()
    {
        if (MatchFlow == null || MatchFlow.Turn_Order_List.Count == 0) return;

        // 1. Roll a D20 for everyone
        foreach (var unit in MatchFlow.Turn_Order_List)
        {
            unit.Initiative_Roll = Random.Range(1, 21); // D20
        }

        // 2. Sort by highest roll first. 
        // (Using a custom comparison to handle tie-breakers properly)
        MatchFlow.Turn_Order_List.Sort((unitA, unitB) =>
        {
            if (unitA.Initiative_Roll != unitB.Initiative_Roll)
            {
                return unitB.Initiative_Roll.CompareTo(unitA.Initiative_Roll); // Higher goes first
            }

            // --- TIE BREAKER ---
            // If they rolled the same, roll again until the tie is broken
            int rollA = 0;
            int rollB = 0;
            while (rollA == rollB)
            {
                rollA = Random.Range(1, 21);
                rollB = Random.Range(1, 21);
            }

            Debug.Log($"[Initiative Tie Breaker] {unitA.Stats.Model_Name} rolled {rollA} vs {unitB.Stats.Model_Name} rolled {rollB}.");

            if (rollA > rollB)
            {
                // If A wins the tie, A goes first. 
                // (To force the loser right after, we would need more complex sorting logic, 
                // but for now, this ensures A goes before B).
                return -1;
            }
            else
            {
                return 1;
            }
        });

        Debug.Log("=== INITIATIVE ORDER ===");
        foreach (var unit in MatchFlow.Turn_Order_List)
        {
            Debug.Log($"{unit.Stats.Model_Name} (Roll: {unit.Initiative_Roll})");
        }
    }

    private int Count_Placed_Models(int Team)
    {
        int count = 0;
        for (int x = 0; x < Board.Get_Tile_Count_X(); x++)
            for (int y = 0; y < Board.Get_Tile_Count_Y(); y++)
                if (Board.Get_Model_At(x, y) != null && Board.Get_Model_At(x, y).Team == Team) count++;
        return count;
    }

    private List<Model_Type> Get_Roster_For_Team(int Team)
    {
        List<Model_Type> Units = new List<Model_Type>();
        Faction_Data_SO Faction = Team == 1 ? MatchFlow.Player_1_Faction : MatchFlow.Player_2_Faction;
        if (Faction != null && Faction.Army_Composition != null)
        {
            foreach (var Entry in Faction.Army_Composition.Army_Roster)
                for (int i = 0; i < Entry.Count; i++)
                    Units.Add(Entry.Type);
        }
        return Units;
    }

    private List<Vector2Int> Build_Placement_Tiles(int Team)
    {
        List<Vector2Int> Result = new List<Vector2Int>();
        if (Board == null) return Result;

        Vector2Int Spawn = Team == 1 ? Player_1_Spawn_Pos : Player_2_Spawn_Pos;
        if (Spawn == Battle_Board_Behavior.No_Tile) return Result;

        List<Vector2Int> Allowed_Zone = Team == 1 ? Player_1_Spawn_Zone : Player_2_Spawn_Zone;

        for (int X = 0; X < Board.Get_Tile_Count_X(); X++)
            for (int Y = 0; Y < Board.Get_Tile_Count_Y(); Y++)
            {
                Vector2Int Pos = new Vector2Int(X, Y);

                if (!Allowed_Zone.Contains(Pos)) continue;
                if (!Board.Is_Tile_Passable(X, Y) || Board.Get_Model_At(X, Y) != null) continue;

                int dx = Mathf.Abs(X - Spawn.x);
                int dy = Mathf.Abs(Y - Spawn.y);

                if (dx <= Placement_Reach_X && dy <= Placement_Reach_Y)
                {
                    Result.Add(Pos);
                }
            }
        Debug.Log($"[Deployment] Built {Result.Count} placement tiles for Team {Team} around spawn {Spawn}.");

        return Result;
    }
}