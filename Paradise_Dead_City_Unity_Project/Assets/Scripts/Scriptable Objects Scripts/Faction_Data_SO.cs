using UnityEngine;

[CreateAssetMenu(fileName = "New Faction", menuName = "Scriptable Objects/Faction Data")]
public class Faction_Data_SO : ScriptableObject
{
    [System.Serializable]
    public class Model_Art_Assets
    {
        public Model_Type Type;

        [Header("Initiative Bar")]
        public Sprite Initiative_Icon;

        [Header("Stat Card (Left Side)")]
        public Sprite Stat_Card_Portrait;

        [Header("Army Row (Bottom)")]
        public Sprite Army_Card_Portrait;

        [Header("On the Board")]
        public Sprite Board_Sprite;
    }

    // ============================================================
    // SERIALIZED FIELDS
    // ============================================================

    public string Faction_Name;

    [Header("Model Prefabs")]
    public GameObject Chaff_Prefab;
    public GameObject Specialist_A_Prefab;
    public GameObject Specialist_B_Prefab;
    public GameObject Axillary_Prefab;
    public GameObject Death_Head_Prefab;

    [Header("Model Stats")]
    public Model_Stats_SO Chaff_Stats;
    public Model_Stats_SO Specialist_A_Stats;
    public Model_Stats_SO Specialist_B_Stats;
    public Model_Stats_SO Axillary_Stats;
    public Model_Stats_SO Death_Head_Stats;

    [Header("Model Materials")]
    public Material[] Team_Materials;

    [Header("Army Composition")]
    public Army_Composition_SO Army_Composition;

    [Header("UI Icons & Artwork")]
    public Sprite Faction_Icon;

    [Tooltip("Add one entry for each Model_Type (Chaff, Specialist, etc.)")]
    public Model_Art_Assets[] Model_Art;


    // ============================================================
    // LOOKUPS
    // ============================================================

    public GameObject Get_Prefab_By_Type(Model_Type Type)
    {
        return Type switch
        {
            Model_Type.Chaff => Chaff_Prefab,
            Model_Type.Specialist_A => Specialist_A_Prefab,
            Model_Type.Specialist_B => Specialist_B_Prefab,
            Model_Type.Axillary => Axillary_Prefab,
            Model_Type.DeathHead => Death_Head_Prefab,
            _ => null
        };
    }

    public Model_Stats_SO Get_Stats_By_Type(Model_Type Type)
    {
        return Type switch
        {
            Model_Type.Chaff => Chaff_Stats,
            Model_Type.Specialist_A => Specialist_A_Stats,
            Model_Type.Specialist_B => Specialist_B_Stats,
            Model_Type.Axillary => Axillary_Stats,
            Model_Type.DeathHead => Death_Head_Stats,
            _ => null
        };
    }

    public Model_Art_Assets Get_Art_By_Type(Model_Type Type)
    {
        if (Model_Art == null) return null;
        foreach (var Art in Model_Art)
        {
            if (Art.Type == Type) return Art;
        }
        return null;
    }

    public Sprite Get_Faction_Icon()
    {
        return Faction_Icon;
    }
}