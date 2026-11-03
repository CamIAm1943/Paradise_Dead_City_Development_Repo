using System.Collections.Generic;
using UnityEngine;

public enum Game_State { Player_1_Place_Spawn, Player_1_Place_Models, Player_2_Place_Spawn, Player_2_Place_Models, Gameplay }

public class Match_Flow_Manager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Battle_Board_Behavior Board;

    [Header("Factions")]
    [Tooltip("Faction data used by the army bar, initiative bar, and stat card.")]
    [SerializeField] private Faction_Data_SO Player_1_Faction_Ref;
    [SerializeField] private Faction_Data_SO Player_2_Faction_Ref;

    [Header("Turn Timer")]
    [SerializeField] private float Turn_Time_Limit = 40f;

    // Faction lookups used by UI controllers.
    public Faction_Data_SO Player_1_Faction => Player_1_Faction_Ref;
    public Faction_Data_SO Player_2_Faction => Player_2_Faction_Ref;

    public float Get_Time_Remaining() => Mathf.Max(0f, Current_Turn_Time);
    public float Get_Time_Percentage() => Current_Turn_Time / Turn_Time_Limit;
    private float Current_Turn_Time;

    public Game_State Current_Phase = Game_State.Player_1_Place_Spawn;
    public List<Model_Standard_Behavior> Turn_Order_List = new List<Model_Standard_Behavior>();
    public int Current_Turn_Index = 0;
    public int Active_Player { get; private set; } = 1;
    public Model_Standard_Behavior Active_Model => (Turn_Order_List.Count > 0 && Current_Turn_Index < Turn_Order_List.Count) ? Turn_Order_List[Current_Turn_Index] : null;
    public void Set_Active_Player(int New_Player)
    {
        Active_Player = New_Player;
    }

    // EVENT HOOKS for UI Decoupling
    public event System.Action<Model_Standard_Behavior> OnTurnStarted;
    public event System.Action OnTurnEnded;

    private void Start()
    {
        if (Current_Phase == Game_State.Player_1_Place_Spawn)
        {
            var deployment = Board.Get_Deployment_Manager();
            if (deployment != null) deployment.Begin_Player_1_Spawn();
        }
    }

    private void Update()
    {
        if (Current_Phase == Game_State.Gameplay && Active_Model != null)
        {
            Current_Turn_Time -= Time.deltaTime;
            if (Current_Turn_Time <= 0f) { Current_Turn_Time = 0f; End_Turn(); }
        }
    }

    public void Start_Active_Model_Turn()
    {
        if (Active_Model == null) return;
        Current_Turn_Time = Turn_Time_Limit;
        Active_Player = Active_Model.Team;

        Active_Model.Movement_Remaining_This_Turn = Active_Model.Stats.Movement_Range;
        Active_Model.Has_Attacked_This_Turn = false;
        Active_Model.Has_Ended_Turn = false;
        Active_Model.Is_Sprinting_This_Turn = false;
        Active_Model.Has_Moved_Since_Sprint = false;
        Active_Model.Has_Used_Sprint_Bonus = false;
        Active_Model.Has_Moved_This_Turn = false;

        Turn_System_Hook_Bridge.Trigger_Turn_Start(Active_Model, Board);
        OnTurnStarted?.Invoke(Active_Model);
    }

    public void End_Turn()
    {
        if (Current_Phase != Game_State.Gameplay) return;

        OnTurnEnded?.Invoke();
        Turn_System_Hook_Bridge.Trigger_Turn_End(Active_Model, Board);
        Board.Tick_Temporary_Hazards();

        if (Active_Model != null)
        {
            var current = Active_Model;
            Turn_Order_List.RemoveAt(Current_Turn_Index);
            Turn_Order_List.Add(current);
        }

        if (Current_Turn_Index >= Turn_Order_List.Count) Current_Turn_Index = 0;

        int safety = 0;
        while (Active_Model != null && (!Active_Model.gameObject.activeSelf || Active_Model.Current_Health <= 0) && safety < 100)
        {
            Turn_Order_List.RemoveAt(Current_Turn_Index);
            if (Turn_Order_List.Count == 0) break;
            if (Current_Turn_Index >= Turn_Order_List.Count) Current_Turn_Index = 0;
            safety++;
        }

        Start_Active_Model_Turn();
    }

    public void Force_Turn_Insert(Model_Standard_Behavior target)
    {
        // Remove the target from the list if they are already in it (e.g., further down the turn order)
        int index = Turn_Order_List.IndexOf(target);
        if (index != -1) Turn_Order_List.RemoveAt(index);

        // Insert them immediately after the current unit (the Lich)
        int insertIndex = Mathf.Clamp(Current_Turn_Index + 1, 0, Turn_Order_List.Count);
        Turn_Order_List.Insert(insertIndex, target);

        // End the Lich's turn. The board will immediately advance to the Souljacked unit.
        End_Turn();
    }

    public void Force_Souljack_Turn(Model_Standard_Behavior target)
    {
        int index = Turn_Order_List.IndexOf(target);
        if (index != -1) Turn_Order_List.RemoveAt(index);
        int insertIndex = Mathf.Clamp(Current_Turn_Index + 1, 0, Turn_Order_List.Count);
        Turn_Order_List.Insert(insertIndex, target);
        End_Turn();
    }
}