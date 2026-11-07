using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class Gameplay_Interaction_Handler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Battle_Board_Behavior Board;
    [SerializeField] private Match_Flow_Manager MatchFlow;
    [SerializeField] private Movement_Execution_Engine MovementEngine;
    [SerializeField] private Combat_Manager CombatManager;
    [SerializeField] private Ability_Targeting_Controller AbilityTargeting;
    [SerializeField] private Board_Highlight_Renderer Highlighter;
    [SerializeField] private Board_Pathfinder Pathfinder;
    [SerializeField] private Card_UI_Controller CardUI;

    [Header("Input Settings")]
    [SerializeField] private InputActionAsset InputActions;
    [SerializeField] private float Raycast_Max_Distance = 100f;
    [SerializeField] private LayerMask ClickableLayers;
    [SerializeField] private float Normal_Drag_Offset = 0.5f;
    [SerializeField] private float Altered_Drag_Offset = 1.2f;
    [SerializeField] private float Drag_Hold_Time = 0.2f;
    [SerializeField] private float UI_Block_Duration = 0.1f;

    private InputAction ClickAction, RightClickAction, MousePosAction, SpaceBarAction;
    private Vector2 CurrentMousePos;
    private float UI_Button_Pressed_Time = -1f;
    private PointerEventData UI_Raycast_Data;
    private readonly List<RaycastResult> UI_Raycast_Results = new List<RaycastResult>(8);

    private Model_Standard_Behavior SelectedModel;
    private Model_Standard_Behavior DraggedModel;
    private float CurrentHoldTime = 0f;
    private bool IsHolding = false;

    private List<List<Vector2Int>> AllPaths = new List<List<Vector2Int>>();
    private int CurrentPathIndex = 0;

    private void Awake()
    {
        var map = InputActions.FindActionMap("Battle_Board");
        ClickAction = map.FindAction("Left_Click");
        RightClickAction = map.FindAction("Right_Click");
        MousePosAction = map.FindAction("Mouse_Position");
        SpaceBarAction = map.FindAction("Space_Bar");

        if (MatchFlow != null)
        {
            MatchFlow.OnTurnStarted += Handle_Turn_Started;
            MatchFlow.OnTurnEnded += Handle_Turn_Ended;
        }
    }

    private void OnDestroy()
    {
        if (MatchFlow != null)
        {
            MatchFlow.OnTurnStarted -= Handle_Turn_Started;
            MatchFlow.OnTurnEnded -= Handle_Turn_Ended;
        }
    }

    private void OnEnable() => InputActions.Enable();
    private void OnDisable() => InputActions.Disable();

    private void Handle_Turn_Started(Model_Standard_Behavior model)
    {
        Select_Model_Public(model);
    }

    private void Handle_Turn_Ended()
    {
        Deselect_All();
    }

    private void Update()
    {
        if (MatchFlow.Current_Phase != Game_State.Gameplay &&
            MatchFlow.Current_Phase != Game_State.Player_1_Place_Spawn &&
            MatchFlow.Current_Phase != Game_State.Player_2_Place_Spawn &&
            MatchFlow.Current_Phase != Game_State.Player_1_Place_Models &&
            MatchFlow.Current_Phase != Game_State.Player_2_Place_Models) return;

        CurrentMousePos = MousePosAction.ReadValue<Vector2>();
        Ray ray = Camera.main.ScreenPointToRay(CurrentMousePos);

        if (ClickAction.WasReleasedThisFrame() && DraggedModel != null)
        {
            bool overUI = IsPointerOverUI();
            bool hitBoard = Physics.Raycast(ray, out RaycastHit hitInfo, Raycast_Max_Distance, ClickableLayers);

            if (hitBoard && !overUI)
            {
                Vector2Int tilePos = Board.Convert_World_To_Tile_Position(hitInfo.point);
                Handle_Drop(tilePos); // Tries to move. If invalid, it snaps back.
            }
            else
            {
                // Dropped over UI or off the board: Force snap back
                DraggedModel.Set_Position(Board.Get_Unit_Stand_Position(DraggedModel.Current_X, DraggedModel.Current_Y), false);
                DraggedModel = null;
                AllPaths.Clear();
                if (Highlighter != null) Highlighter.Clear_Path_Highlights();
            }
            return; // Skip normal processing this frame
        }

        if (Physics.Raycast(ray, out RaycastHit boardHit, Raycast_Max_Distance, ClickableLayers))
        {
            Vector2Int tilePos = Board.Convert_World_To_Tile_Position(boardHit.point);
            Process_Input(tilePos, ray);
        }
    }

    private void Process_Input(Vector2Int tilePos, Ray ray)
    {
        if (IsPointerOverUI() || Time.unscaledTime - UI_Button_Pressed_Time < UI_Block_Duration) return;

        if (MatchFlow.Current_Phase != Game_State.Gameplay)
        {
            if (ClickAction.WasPressedThisFrame())
            {
                var deployment = Board.Get_Deployment_Manager();
                if (deployment != null)
                    deployment.Try_Place_At(tilePos);
            }
            return;
        }

        if (AbilityTargeting != null && AbilityTargeting.Is_Currently_Targeting())
        {
            AbilityTargeting.Handle_Tile_Hover(tilePos);
            if (ClickAction.WasPressedThisFrame()) AbilityTargeting.Handle_Tile_Click(tilePos);
            if (RightClickAction.WasPressedThisFrame()) AbilityTargeting.Cancel();
            return;
        }

        if (CombatManager != null && CombatManager.Is_Currently_Selecting_Target())
        {
            if (ClickAction.WasPressedThisFrame()) CombatManager.Try_Select_Target(tilePos);
            if (RightClickAction.WasPressedThisFrame()) CombatManager.Cancel_Attack();
            return;
        }

        if (ClickAction.WasPressedThisFrame()) Handle_Selection(tilePos);
        if (RightClickAction.WasPressedThisFrame()) Deselect_All();
        if (SpaceBarAction.WasPressedThisFrame() && DraggedModel != null) Cycle_Paths();

        Update_Hold_Timer();
        Update_Drag(ray, tilePos);
    }

    private void Handle_Selection(Vector2Int tilePos)
    {
        var model = Board.Get_Model_At(tilePos.x, tilePos.y);
        if (model != null && model == SelectedModel)
        {
            if (model == MatchFlow.Active_Model) Start_Hold_Timer();
            return;
        }

        if (model == null)
        {
            Deselect_All();
        }
        else
        {
            if (SelectedModel != null && MovementEngine != null)
            {
                MovementEngine.Cancel_Sprint(SelectedModel);
            }

            SelectedModel = null;
            DraggedModel = null;
            IsHolding = false;

            if (Highlighter != null)
            {
                Highlighter.Clear_Path_Highlights();
                Highlighter.Clear_Movement_Range_Highlights();
            }

            Select_Model_Public(model);
        }
    }

    public void Select_Model_Public(Model_Standard_Behavior model)
    {
        SelectedModel = model;

        bool showCard = true;
        if (model != null && model.Is_Mid_Sprint())
        {
            showCard = false;
        }

        if (CardUI != null)
        {
            if (showCard)
            {
                Faction_Data_SO faction = null;
                if (model != null && MatchFlow != null)
                {
                    faction = model.Team == 1 ? MatchFlow.Player_1_Faction : MatchFlow.Player_2_Faction;
                }

                // This will trigger the animation if the card is currently open for a different model
                CardUI.Show_Model_Info(model, faction);
            }
            else
            {
                CardUI.Hide_Model_Info();
            }
        }

        if (model != null && model == MatchFlow.Active_Model)
        {
            Highlighter.Display_Movement_Range(model);
        }

        var initUI = FindAnyObjectByType<Initiative_UI_Controller>();
        if (initUI != null) initUI.Update_Selection(SelectedModel);

        var armyUI = FindAnyObjectByType<Army_UI_Controller>();
        if (armyUI != null) armyUI.Update_Selection(SelectedModel);
    }

    public void Deselect_All()
    {
        if (DraggedModel != null)
        {
            DraggedModel.Set_Position(Board.Get_Unit_Stand_Position(DraggedModel.Current_X, DraggedModel.Current_Y), false);
        }

        if (SelectedModel != null && MovementEngine != null)
        {
            MovementEngine.Cancel_Sprint(SelectedModel);
        }

        SelectedModel = null;
        DraggedModel = null;
        IsHolding = false;

        if (Highlighter != null)
        {
            Highlighter.Clear_Path_Highlights();
            Highlighter.Clear_Movement_Range_Highlights();
        }

        if (CardUI != null) CardUI.Hide_Model_Info();

        var initUI = FindAnyObjectByType<Initiative_UI_Controller>();
        if (initUI != null) initUI.Update_Selection(null);

        var armyUI = FindAnyObjectByType<Army_UI_Controller>();
        if (armyUI != null) armyUI.Update_Selection(null);
    }

    private void Start_Hold_Timer()
    {
        IsHolding = true;
        CurrentHoldTime = 0f;
    }

    private void Update_Hold_Timer()
    {
        if (IsHolding && SelectedModel != null && ClickAction.IsPressed())
        {
            CurrentHoldTime += Time.deltaTime;
            if (CurrentHoldTime >= Drag_Hold_Time) DraggedModel = SelectedModel;
        }
    }

    private void Update_Drag(Ray ray, Vector2Int tilePos)
    {
        if (DraggedModel == null || DraggedModel != MatchFlow.Active_Model) return;

        if (!ClickAction.IsPressed())
        {
            DraggedModel.Set_Position(Board.Get_Unit_Stand_Position(DraggedModel.Current_X, DraggedModel.Current_Y), false);
            DraggedModel = null;
            AllPaths.Clear();
            if (Highlighter != null) Highlighter.Clear_Path_Highlights();
            return;
        }

        Plane plane = new Plane(Vector3.up, Vector3.up * 0.2f);
        if (plane.Raycast(ray, out float dist))
        {
            Vector3 mousePos = ray.GetPoint(dist);
            float currentOffset = Normal_Drag_Offset;

            Model_Standard_Behavior hoveredModel = Board.Get_Model_At(tilePos.x, tilePos.y);
            Board_Modifiers tileType = Board.Get_Tile_Type_At(tilePos.x, tilePos.y);

            bool isOverObstacle = (hoveredModel != null && hoveredModel != DraggedModel) ||
                                  (tileType == Board_Modifiers.Wall || tileType == Board_Modifiers.Cover);

            if (isOverObstacle) currentOffset = Altered_Drag_Offset;

            DraggedModel.Follow_Position(mousePos + Vector3.up * currentOffset);
        }

        if (Highlighter.Is_Tile_A_Shove_Target(tilePos))
        {
            if (AllPaths.Count == 0 || AllPaths[0][AllPaths[0].Count - 1] != tilePos)
            {
                Vector2Int start = new Vector2Int(DraggedModel.Current_X, DraggedModel.Current_Y);
                AllPaths = Pathfinder.Find_Shove_Paths(start, tilePos, DraggedModel.Movement_Remaining_This_Turn, DraggedModel.Team);
                CurrentPathIndex = 0;
            }

            Highlighter.Draw_Shove_Paths(AllPaths, CurrentPathIndex, DraggedModel, tilePos);
            return;
        }

        if (Highlighter.Is_Tile_In_Movement_Range(tilePos))
        {
            if (AllPaths.Count == 0 || AllPaths[0][AllPaths[0].Count - 1] != tilePos)
            {
                Vector2Int start = new Vector2Int(DraggedModel.Current_X, DraggedModel.Current_Y);
                bool ignoreWalls = DraggedModel.Has_Status(Status_Type.Death_On_Wheels) || DraggedModel.Has_Status(Status_Type.Datastream);
                AllPaths = Pathfinder.Find_All_Paths(start, tilePos, DraggedModel.Movement_Remaining_This_Turn, DraggedModel.Team, ignoreWalls);
                CurrentPathIndex = 0;
            }
            Highlighter.Draw_Paths(AllPaths, CurrentPathIndex, DraggedModel);
        }
        else
        {
            Highlighter.Clear_Path_Highlights();
        }
    }

    private void Handle_Drop(Vector2Int tilePos)
    {
        if (DraggedModel == null || DraggedModel != MatchFlow.Active_Model) return;

        List<Vector2Int> Chosen_Path = (AllPaths.Count > 0)
            ? new List<Vector2Int>(AllPaths[CurrentPathIndex])
            : new List<Vector2Int>();

        bool Success = MovementEngine.Try_Move_Model(
            DraggedModel, tilePos.x, tilePos.y, Chosen_Path);

        if (Success)
        {
            if (Highlighter != null)
            {
                Highlighter.Clear_Path_Highlights();
                Highlighter.Display_Movement_Range(DraggedModel);
            }

            if (DraggedModel.Is_Sprinting_This_Turn && DraggedModel.Movement_Remaining_This_Turn > 0)
            {
                if (CardUI != null) CardUI.Hide_Model_Info();
            }
            else
            {
                if (CardUI != null) CardUI.Silent_Refresh_Card(DraggedModel);
            }
        }
        else
        {
            // This snaps the unit back to its current exact tile if the move was invalid
            DraggedModel.Set_Position(Board.Get_Unit_Stand_Position(DraggedModel.Current_X, DraggedModel.Current_Y), false);
        }

        DraggedModel = null;
        AllPaths.Clear();
    }

    private void Cycle_Paths()
    {
        if (AllPaths.Count <= 1) return;
        CurrentPathIndex = (CurrentPathIndex + 1) % AllPaths.Count;
        Highlighter.Draw_Paths(AllPaths, CurrentPathIndex, DraggedModel);
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        UI_Raycast_Data ??= new PointerEventData(EventSystem.current);
        UI_Raycast_Data.position = CurrentMousePos;
        UI_Raycast_Results.Clear();
        EventSystem.current.RaycastAll(UI_Raycast_Data, UI_Raycast_Results);
        return UI_Raycast_Results.Count > 0;
    }

    public Model_Standard_Behavior Get_Selected_Model() => SelectedModel;

    public void Cancel_Active_Drag()
    {
        if (DraggedModel != null)
            DraggedModel.Set_Position(Board.Get_Unit_Stand_Position(DraggedModel.Current_X, DraggedModel.Current_Y), false);

        DraggedModel = null;
        IsHolding = false;
    }

    public Board_Highlight_Renderer Get_Highlighter() => Highlighter;
}