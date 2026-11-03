using System;
using UnityEngine;

[CreateAssetMenu(fileName = "New Map Data", menuName = "Scriptable Objects/Map Data")]
public class Map_Data_SO : ScriptableObject
{
    // ============================================================
    // BOARD LAYOUT CONSTANTS
    // ============================================================

    // Total board dimensions. Must match Battle_Board_Behavior.Tile_Count_X/Y.
    // These are duplicated here because ScriptableObjects can't read MonoBehaviour constants.
    private const int Board_Width = 8;
    private const int Board_Height = 8;

    // The designer-editable middle section of the board.
    // Rows [Designer_Start_Row, Designer_Start_Row + Designer_Row_Count - 1]
    // are authored in the Inspector. Everything else is generated.
    private const int Designer_Start_Row = 2;
    private const int Designer_Row_Count = 2;
    private const int Designer_End_Row = Designer_Start_Row + Designer_Row_Count - 1;

    // ============================================================
    // SERIALIZED FIELDS
    // ============================================================

    public string Map_Name;

    // NOTE: The header text is hardcoded because attribute arguments must be
    // compile-time constants, and class-level const members don't qualify.
    // Keep this in sync with Designer_Start_Row / Designer_End_Row above.
    [Header("Map Layout (Rows 2-3 Only)")]
    public Map_Row[] Rows = new Map_Row[Designer_Row_Count];

    [Serializable]
    public class Map_Row
    {
        // Must match Map_Data_SO.Board_Height. Duplicated because nested
        // serializable classes can't reference their parent's constants in
        // field initializers.
        public Board_Modifiers[] Tiles = new Board_Modifiers[8];

        // Parallel to Tiles. True = elevated tile. Mirrors the same way
        // Tiles do, so authoring elevation in the designer rows is enough.
        public bool[] Elevated = new bool[8];
    }


    // ============================================================
    // CACHED FULL GRIDS
    // ============================================================

    private Board_Modifiers[,] Full_Grid;
    private bool[,] Elevated_Grid;


    // ============================================================
    // PUBLIC QUERIES
    // ============================================================

    public Board_Modifiers Get_Tile_Type(int X, int Y)
    {
        Ensure_Grid();

        if (X < 0 || X >= Board_Width || Y < 0 || Y >= Board_Height)
            return Board_Modifiers.None;

        return Full_Grid[X, Y];
    }

    public bool Is_Elevated(int X, int Y)
    {
        Ensure_Grid();

        if (X < 0 || X >= Board_Width || Y < 0 || Y >= Board_Height)
            return false;

        return Elevated_Grid[X, Y];
    }

    public bool Is_Tile_Passable(int X, int Y)
    {
        return Get_Tile_Type(X, Y) != Board_Modifiers.Wall;
    }

    public bool Is_Tile_Hazardous(int X, int Y)
    {
        return Get_Tile_Type(X, Y) == Board_Modifiers.Hazard;
    }

    public bool Is_Tile_Cover(int X, int Y)
    {
        return Get_Tile_Type(X, Y) == Board_Modifiers.Cover;
    }

    public bool Is_Tile_Difficult_Terrain(int X, int Y)
    {
        return Get_Tile_Type(X, Y) == Board_Modifiers.Terrain;
    }

    /// <summary>
    /// Forces the cached grid to regenerate the next time a query runs.
    /// Call this after modifying Rows at runtime.
    /// </summary>
    public void Mark_Dirty()
    {
        Full_Grid = null;
        Elevated_Grid = null;
    }

    /// <summary>
    /// Resets the map to its default empty state.
    /// </summary>
    public void Clear_Map()
    {
        Rows = new Map_Row[Designer_Row_Count];
        for (int i = 0; i < Rows.Length; i++)
            Rows[i] = new Map_Row();

        Full_Grid = null;
        Elevated_Grid = null;
    }


    // ============================================================
    // GRID GENERATION
    // ============================================================

    private void Ensure_Grid()
    {
        if (Full_Grid == null)
            Generate_Full_Grid();
    }

    private void Generate_Full_Grid()
    {
        Full_Grid = new Board_Modifiers[Board_Width, Board_Height];
        Elevated_Grid = new bool[Board_Width, Board_Height];

        int Designer_End_Exclusive = Designer_Start_Row + Designer_Row_Count;

        for (int X = 0; X < Board_Width; X++)
        {
            for (int Y = 0; Y < Board_Height; Y++)
            {
                if (Try_Read_Designer_Tile(X, Y, Designer_End_Exclusive,
                                            out Board_Modifiers Type,
                                            out bool Elevated))
                {
                    Full_Grid[X, Y] = Type;
                    Elevated_Grid[X, Y] = Elevated;
                }
                else
                {
                    // Generated zone (spawn rows). Non-elevated empty tiles.
                    Full_Grid[X, Y] = Board_Modifiers.None;
                    Elevated_Grid[X, Y] = false;
                }
            }
        }
    }

    /// <summary>
    /// Resolves a world coordinate to its designer-authored tile, applying
    /// the mirror transform for the lower half of the board. Returns false
    /// for coordinates in the generated zones (spawn rows), where the
    /// caller should apply its own defaults.
    /// </summary>
    private bool Try_Read_Designer_Tile(
        int X, int Y, int Designer_End_Exclusive,
        out Board_Modifiers Type, out bool Elevated)
    {
        Type = Board_Modifiers.None;
        Elevated = false;

        int Designer_Row_Index;

        if (X >= Designer_Start_Row && X < Designer_End_Exclusive)
        {
            // Direct hit on a designer-authored row.
            Designer_Row_Index = X - Designer_Start_Row;
        }
        else
        {
            // Mirror pass. Reflect X across the board center; if the
            // reflected row is a designer row, use it.
            int Mirror_Source_X = Board_Width - 1 - X;

            if (Mirror_Source_X < Designer_Start_Row || Mirror_Source_X >= Designer_End_Exclusive)
                return false;

            Designer_Row_Index = Mirror_Source_X - Designer_Start_Row;
        }

        if (Rows == null || Designer_Row_Index < 0 || Designer_Row_Index >= Rows.Length)
            return false;

        Map_Row Row = Rows[Designer_Row_Index];
        if (Row == null)
            return false;

        if (Row.Tiles != null && Y >= 0 && Y < Row.Tiles.Length)
            Type = Row.Tiles[Y];

        if (Row.Elevated != null && Y >= 0 && Y < Row.Elevated.Length)
            Elevated = Row.Elevated[Y];

        return true;
    }


    // ============================================================
    // EDITOR VALIDATION
    // ============================================================

    private void OnValidate()
    {
        if (Rows == null || Rows.Length != Designer_Row_Count)
            Rows = new Map_Row[Designer_Row_Count];

        for (int i = 0; i < Rows.Length; i++)
        {
            if (Rows[i] == null)
                Rows[i] = new Map_Row();

            if (Rows[i].Tiles == null || Rows[i].Tiles.Length != Board_Height)
                Rows[i].Tiles = new Board_Modifiers[Board_Height];

            if (Rows[i].Elevated == null || Rows[i].Elevated.Length != Board_Height)
            {
                // Resize while preserving any existing values.
                bool[] New = new bool[Board_Height];
                if (Rows[i].Elevated != null)
                {
                    int Copy = Mathf.Min(Rows[i].Elevated.Length, Board_Height);
                    for (int j = 0; j < Copy; j++)
                        New[j] = Rows[i].Elevated[j];
                }
                Rows[i].Elevated = New;
            }
        }

        // Force regeneration on next query after any edit.
        Full_Grid = null;
        Elevated_Grid = null;
    }
}