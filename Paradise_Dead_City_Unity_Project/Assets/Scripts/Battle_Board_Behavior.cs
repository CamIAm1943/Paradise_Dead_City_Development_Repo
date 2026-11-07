using System.Collections.Generic;
using UnityEngine;

public enum Board_Modifiers { None, Cover, Wall, Terrain, Hazard, Spawn }

public class Battle_Board_Behavior : MonoBehaviour
{
    public const int Tile_Count_X = 8;
    public const int Tile_Count_Y = 8;
    public static readonly Vector2Int No_Tile = new Vector2Int(-1, -1);
    public static readonly Vector2Int[] Cardinal_Directions = { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) };

    [Header("Assets")]
    [SerializeField] private Material Tile_Material_A;
    [SerializeField] private Material Tile_Material_B;
    [SerializeField] private float Tile_Size = 1.0f;
    [SerializeField] private float Y_Offset = 0.2f;
    [SerializeField] private float Terrain_Y_Offset = 0.1f;
    [SerializeField] private Vector3 Board_Center = Vector3.zero;

    [Header("Map Data")]
    [SerializeField] private Map_Data_SO Current_Map;
    [SerializeField] private GameObject Cover_Tile_Prefab;
    [SerializeField] private GameObject Wall_Tile_Prefab;
    [SerializeField] private GameObject Terrain_Tile_Prefab;
    [SerializeField] private GameObject Hazard_Tile_Prefab;
    [SerializeField] private float Elevation_Unit_Height = 0.5f;

    [Header("Status")]
    [Tooltip("Central registry of Status_Definition_SO assets. Passed to every model at spawn so statuses can look up their visuals.")]
    [SerializeField] private Status_Registry_SO Status_Registry;

    [Header("Scene Bridges")]
    [Tooltip("Wired in the Game scene. Every SO ability and UI script reaches " +
         "scene services through the board, not FindAnyObjectByType.")]
    [SerializeField] private Match_Flow_Manager Match_Flow_Ref;
    [SerializeField] private Combat_Manager Combat_Ref;
    [SerializeField] private Ability_Targeting_Controller Ability_Targeting_Ref;
    [SerializeField] private Movement_Execution_Engine Movement_Engine_Ref;
    [SerializeField] private Gameplay_Interaction_Handler Interaction_Handler_Ref;
    [SerializeField] private Board_Highlight_Renderer Highlighter_Ref;
    [SerializeField] private Board_Pathfinder Pathfinder_Ref;
    [SerializeField] private Deployment_Manager Deployment_Ref;

    private GameObject[,] Tiles;
    private Vector3 Bounds;
    private Vector3 Board_Origin;
    private Board_Modifiers[,] Map_Tiles;
    private GameObject[,] Terrain_Objects;
    private Model_Standard_Behavior[,] Models;

    private Dictionary<Vector2Int, int> Temporary_Hazards = new Dictionary<Vector2Int, int>();
    private Dictionary<Vector2Int, GameObject> Temp_Hazard_Objects = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, Board_Modifiers> Original_Tile_Types = new Dictionary<Vector2Int, Board_Modifiers>();

    private void Awake()
    {
        Initialize_Board();
    }

    public void Initialize_Board()
    {
        Generate_All_Tiles(Tile_Size);
        if (Current_Map != null) Generate_Map_Terrain();
        Models = new Model_Standard_Behavior[Tile_Count_X, Tile_Count_Y];
    }

    private void Generate_All_Tiles(float size)
    {
        float absYOffset = Y_Offset + transform.position.y;
        Bounds = new Vector3((Tile_Count_X / 2.0f) * size, 0, (Tile_Count_Y / 2.0f) * size) + Board_Center;
        Board_Origin = new Vector3(0, absYOffset, 0) - Bounds;

        Tiles = new GameObject[Tile_Count_X, Tile_Count_Y];
        for (int x = 0; x < Tile_Count_X; x++)
            for (int y = 0; y < Tile_Count_Y; y++)
                Tiles[x, y] = Create_Single_Tile(size, x, y, absYOffset);
    }

    private GameObject Create_Single_Tile(float Tile_Size, int x, int y, float Absolute_Y_Offset)
    {
        GameObject Tile_Object = new GameObject($"X:{x}, Y:{y}");
        Tile_Object.transform.parent = transform;

        Mesh Mesh = new Mesh();
        Tile_Object.AddComponent<MeshFilter>().mesh = Mesh;

        Material BaseMat = Get_Base_Tile_Material(x, y);
        Tile_Object.AddComponent<MeshRenderer>().material = BaseMat;

        Vector3[] Vertices = new Vector3[4];
        Vertices[0] = new Vector3(x * Tile_Size, Absolute_Y_Offset, y * Tile_Size) - Bounds;
        Vertices[1] = new Vector3(x * Tile_Size, Absolute_Y_Offset, (y + 1) * Tile_Size) - Bounds;
        Vertices[2] = new Vector3((x + 1) * Tile_Size, Absolute_Y_Offset, y * Tile_Size) - Bounds;
        Vertices[3] = new Vector3((x + 1) * Tile_Size, Absolute_Y_Offset, (y + 1) * Tile_Size) - Bounds;

        int[] Tris = new int[] { 0, 1, 2, 1, 3, 2 };

        Mesh.vertices = Vertices;
        Mesh.triangles = Tris;
        Mesh.RecalculateNormals();

        MeshCollider Collider = Tile_Object.AddComponent<MeshCollider>();
        Collider.sharedMesh = Mesh;

        Tile_Object.layer = LayerMask.NameToLayer("Default");
        return Tile_Object;
    }

    public void Destroy_Terrain_At(int x, int y)
    {
        if (x < 0 || x >= Tile_Count_X || y < 0 || y >= Tile_Count_Y) return;
        Map_Tiles[x, y] = Board_Modifiers.None;

        if (Terrain_Objects != null && Terrain_Objects[x, y] != null) { Destroy(Terrain_Objects[x, y]); Terrain_Objects[x, y] = null; }

        if (Tiles[x, y] != null && Tiles[x, y].TryGetComponent<MeshRenderer>(out var r))
        {
            r.material = Get_Base_Tile_Material(x, y);
        }
    }

    public void Create_Temporary_Hazard(int x, int y, int durationTurns)
    {
        if (x < 0 || x >= Tile_Count_X || y < 0 || y >= Tile_Count_Y) return;
        Vector2Int pos = new Vector2Int(x, y);

        if (Temporary_Hazards.ContainsKey(pos)) { Temporary_Hazards[pos] = durationTurns; return; }

        Original_Tile_Types[pos] = Map_Tiles[x, y];
        Map_Tiles[x, y] = Board_Modifiers.Hazard;

        GameObject hazardObj = Spawn_Terrain_Object(Board_Modifiers.Hazard, x, y);
        if (hazardObj != null) { hazardObj.transform.position = Get_Unit_Stand_Position(x, y) + Vector3.up * 0.02f; Temp_Hazard_Objects[pos] = hazardObj; }
        Temporary_Hazards[pos] = durationTurns;
    }

    public void Tick_Temporary_Hazards()
    {
        List<Vector2Int> expired = new List<Vector2Int>();
        List<Vector2Int> keys = new List<Vector2Int>(Temporary_Hazards.Keys);

        foreach (var key in keys)
        {
            Temporary_Hazards[key]--;
            if (Temporary_Hazards[key] <= 0) expired.Add(key);
        }

        foreach (var pos in expired)
        {
            if (Original_Tile_Types.TryGetValue(pos, out Board_Modifiers originalType)) { Map_Tiles[pos.x, pos.y] = originalType; Original_Tile_Types.Remove(pos); }
            else { Map_Tiles[pos.x, pos.y] = Board_Modifiers.None; }

            if (Temp_Hazard_Objects.TryGetValue(pos, out GameObject obj) && obj != null) { Destroy(obj); Temp_Hazard_Objects.Remove(pos); }
            Temporary_Hazards.Remove(pos);
        }
    }

    private void Generate_Map_Terrain()
    {
        Map_Tiles = new Board_Modifiers[Tile_Count_X, Tile_Count_Y];
        Terrain_Objects = new GameObject[Tile_Count_X, Tile_Count_Y];
        for (int x = 0; x < Tile_Count_X; x++)
            for (int y = 0; y < Tile_Count_Y; y++)
            {
                Board_Modifiers type = Current_Map.Get_Tile_Type(x, y);
                Map_Tiles[x, y] = type;
                GameObject obj = Spawn_Terrain_Object(type, x, y);
                if (obj != null) Terrain_Objects[x, y] = obj;
            }
    }

    private GameObject Spawn_Terrain_Object(Board_Modifiers type, int x, int y)
    {
        GameObject prefab = type switch
        {
            Board_Modifiers.Cover => Cover_Tile_Prefab,
            Board_Modifiers.Wall => Wall_Tile_Prefab,
            Board_Modifiers.Terrain => Terrain_Tile_Prefab,
            Board_Modifiers.Hazard => Hazard_Tile_Prefab,
            _ => null
        };
        if (prefab == null) return null;

        Vector3 spawnPos = Get_Tile_Center(x, y);
        spawnPos.y += Terrain_Y_Offset;

        return Instantiate(prefab, spawnPos, Quaternion.identity, transform);
    }

    public Model_Standard_Behavior Get_Model_At(int x, int y) => (x < 0 || x >= Tile_Count_X || y < 0 || y >= Tile_Count_Y) ? null : Models[x, y];
    public void Remove_Model(int x, int y) { if (x >= 0 && x < Tile_Count_X && y >= 0 && y < Tile_Count_Y) Models[x, y] = null; }
    public void Place_Model_At(int x, int y, Model_Standard_Behavior model) { if (x >= 0 && x < Tile_Count_X && y >= 0 && y < Tile_Count_Y) Models[x, y] = model; }

    public Board_Modifiers Get_Tile_Type_At(int x, int y) => (x < 0 || x >= Tile_Count_X || y < 0 || y >= Tile_Count_Y) ? Board_Modifiers.None : Map_Tiles[x, y];
    public bool Is_Tile_Passable(int x, int y) => Get_Tile_Type_At(x, y) != Board_Modifiers.Wall && Get_Tile_Type_At(x, y) != Board_Modifiers.Cover;

    public Vector3 Get_Tile_Center(int x, int y) => new Vector3(x * Tile_Size, Y_Offset + transform.position.y, y * Tile_Size) - Bounds + new Vector3(Tile_Size / 2, 0, Tile_Size / 2);
    public Vector3 Get_Unit_Stand_Position(int x, int y) => Get_Tile_Center(x, y) + Vector3.up * (Is_Tile_Elevated(x, y) ? Elevation_Unit_Height : 0f);
    public bool Is_Tile_Elevated(int x, int y) => Current_Map != null && Current_Map.Is_Elevated(x, y);

    public Vector2Int Convert_World_To_Tile_Position(Vector3 worldPos)
    {
        Vector3 rel = worldPos - Board_Origin;
        return new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt(rel.x / Tile_Size), 0, Tile_Count_X - 1),
            Mathf.Clamp(Mathf.FloorToInt(rel.z / Tile_Size), 0, Tile_Count_Y - 1)
        );
    }

    // ============================================================
    // SCENE BRIDGES
    // ============================================================

    public Match_Flow_Manager Get_Match_Flow() => Match_Flow_Ref;
    public Combat_Manager Get_Combat_Manager() => Combat_Ref;
    public Ability_Targeting_Controller Get_Ability_Targeting_Controller() => Ability_Targeting_Ref;
    public Movement_Execution_Engine Get_Movement_Engine() => Movement_Engine_Ref;
    public Gameplay_Interaction_Handler Get_Interaction_Handler() => Interaction_Handler_Ref;
    public Board_Highlight_Renderer Get_Highlighter() => Highlighter_Ref;
    public Board_Pathfinder Get_Pathfinder() => Pathfinder_Ref;
    public Deployment_Manager Get_Deployment_Manager() => Deployment_Ref;
    public Status_Registry_SO Get_Status_Registry() => Status_Registry;

    public void Select_Deployment_Card(Army_Card_UI Card)
    {
        if (Deployment_Ref != null) Deployment_Ref.Select_Deployment_Card(Card);
    }

    public void Begin_Deployment_Drag(Army_Card_UI Card)
    {
        if (Deployment_Ref != null) Deployment_Ref.Begin_Deployment_Drag(Card);
    }

    public void End_Deployment_Drag()
    {
        if (Deployment_Ref != null) Deployment_Ref.End_Deployment_Drag();
    }

    public GameObject[,] Get_Tiles() => Tiles;
    public Material Get_Base_Tile_Material(int x, int y) => (x + y) % 2 == 0 ? Tile_Material_A : Tile_Material_B;
    public int Get_Tile_Count_X() => Tile_Count_X;
    public int Get_Tile_Count_Y() => Tile_Count_Y;
}