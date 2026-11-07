using TMPro;
using UnityEngine.UI;
using UnityEngine;
using System.Collections;

public class Card_UI_Controller : MonoBehaviour
{
    private const string Description_Bullet = "• ";
    private const string Default_Name = "---";
    private const string Default_Health = "-/-";
    private const string Default_Stat = "-";
    private const string Default_Description = "Select a model to view details";

    private static readonly int Open_Trigger = Animator.StringToHash("Open");
    private static readonly int Close_Trigger = Animator.StringToHash("Close");

    [Header("Animator")]
    [SerializeField] private Animator Card_Animator;

    [Header("References")]
    [SerializeField] private Gameplay_Interaction_Handler InteractionHandler;
    [SerializeField] private Movement_Execution_Engine MovementEngine;

    [Header("Buttons")]
    [SerializeField] private Button Attack_Button;
    [SerializeField] private Button Sprint_Button;
    [SerializeField] private Button Ability_Button;

    [Header("Status Row")]
    [SerializeField] private Status_Row_UI Status_Row;

    [Header("Text Fields")]
    [SerializeField] private TextMeshProUGUI Name_Text;
    [SerializeField] private TextMeshProUGUI Health_Num;
    [SerializeField] private TextMeshProUGUI Movement_Num;
    [SerializeField] private TextMeshProUGUI Armor_Num;
    [SerializeField] private TextMeshProUGUI Attack_Num;
    [SerializeField] private TextMeshProUGUI Range_Num;
    [SerializeField] private TextMeshProUGUI Damage_Num;
    [SerializeField] private TextMeshProUGUI Description_Text;

    [Header("Image Fields")]
    [SerializeField] private Image Faction_Icon;
    [SerializeField] private Image Model_Icon;

    [Header("Default Placeholders")]
    [SerializeField] private Sprite Default_Faction_Icon;
    [SerializeField] private Sprite Default_Model_Icon;

    [Header("Transition Settings")]
    [SerializeField] private float Close_Animation_Duration = 0.5f;
    [SerializeField] private float Card_Transition_Delay = 0.3f;

    private int Active_Player = 1;
    private bool Is_Card_Open = false;
    private Model_Standard_Behavior Current_Displayed_Model;
    private Faction_Data_SO Current_Faction;
    private Coroutine Current_Transition;
    private Combat_Manager Combat_Mgr;
    private Coroutine Current_Close;
    private Match_Flow_Manager MatchFlow;
    private Battle_Board_Behavior BoardRef;

    private void Awake()
    {
        if (Card_Animator == null)
        {
            Card_Animator = GetComponent<Animator>();
            if (Card_Animator == null) Card_Animator = GetComponentInChildren<Animator>();
        }

        Combat_Mgr = FindAnyObjectByType<Combat_Manager>();
        MatchFlow = FindAnyObjectByType<Match_Flow_Manager>();
        BoardRef = FindAnyObjectByType<Battle_Board_Behavior>();
        if (InteractionHandler == null) InteractionHandler = FindAnyObjectByType<Gameplay_Interaction_Handler>();
        if (MovementEngine == null) MovementEngine = FindAnyObjectByType<Movement_Execution_Engine>();
    }

    private void Start()
    {
        Clear_Card();

        if (Card_Animator != null)
        {
            Card_Animator.ResetTrigger(Open_Trigger);
            Card_Animator.ResetTrigger(Close_Trigger);
        }

        if (Attack_Button != null) Attack_Button.onClick.AddListener(On_Attack_Button_Clicked);
        if (Sprint_Button != null) Sprint_Button.onClick.AddListener(On_Sprint_Button_Clicked);
        if (Ability_Button != null) Ability_Button.onClick.AddListener(On_Ability_Button_Clicked);
    }

    public void Show_Model_Info(Model_Standard_Behavior Model, Faction_Data_SO Faction)
    {
        if (Model == null || Model.Stats == null) return;

        if (Current_Close != null) { StopCoroutine(Current_Close); Current_Close = null; }
        if (Current_Transition != null) { StopCoroutine(Current_Transition); Current_Transition = null; }

        // If it's the exact same model and the card is open, just update silently
        if (Is_Card_Open && Current_Displayed_Model == Model)
        {
            Populate_Card_Data(Model, Faction);
            return;
        }

        // If the card is open but it's a DIFFERENT model, play the transition
        if (Is_Card_Open && Current_Displayed_Model != Model)
        {
            Current_Transition = StartCoroutine(Transition_To_New_Model(Model, Faction));
            return;
        }

        // If the card is closed, just open it with the new model
        Current_Faction = Faction;
        Current_Displayed_Model = Model;
        Populate_Card_Data(Model, Faction);
        Trigger_Open();
    }

    public void Hide_Model_Info()
    {
        if (!Is_Card_Open) return;
        if (Current_Transition != null) { StopCoroutine(Current_Transition); Current_Transition = null; }
        if (Current_Close != null) StopCoroutine(Current_Close);
        Current_Close = StartCoroutine(Close_Card_Sequence());
    }

    public void Set_Active_Player(int Player)
    {
        Active_Player = Player;
        Update_Button_States();
    }

    public Model_Standard_Behavior Get_Displayed_Model() => Current_Displayed_Model;

    private IEnumerator Close_Card_Sequence()
    {
        Trigger_Close();
        Is_Card_Open = false;
        yield return new WaitForSeconds(Close_Animation_Duration);
        Clear_Card();
        Current_Displayed_Model = null;
    }

    private IEnumerator Transition_To_New_Model(Model_Standard_Behavior New_Model, Faction_Data_SO New_Faction)
    {
        Trigger_Close();
        Is_Card_Open = false;
        yield return new WaitForSeconds(Close_Animation_Duration + Card_Transition_Delay);
        Current_Displayed_Model = New_Model;
        Populate_Card_Data(New_Model, New_Faction);
        Trigger_Open();
        Is_Card_Open = true;
        Current_Transition = null;
    }

    private void Trigger_Open()
    {
        if (Card_Animator != null) { Card_Animator.ResetTrigger(Close_Trigger); Card_Animator.SetTrigger(Open_Trigger); }
        Is_Card_Open = true;
    }

    public void Refresh_Card_For_Model(Model_Standard_Behavior Model)
    {
        if (Model == null || Model.Stats == null) return;
        if (Current_Displayed_Model != Model) return;

        // If the card is already open, trigger the close-and-open animation
        if (Is_Card_Open)
        {
            if (Current_Transition != null) StopCoroutine(Current_Transition);
            Current_Transition = StartCoroutine(Transition_To_New_Model(Model, Current_Faction));
            return;
        }

        // Fallback: If the card isn't open, just update the data silently
        Populate_Card_Data(Model, Current_Faction);
    }

    private void Trigger_Close()
    {
        if (Card_Animator != null) { Card_Animator.ResetTrigger(Open_Trigger); Card_Animator.SetTrigger(Close_Trigger); }
    }

    private void Populate_Card_Data(Model_Standard_Behavior Model, Faction_Data_SO Faction)
    {
        Set_Text(Name_Text, Model.Stats.Model_Name);
        Set_Text(Health_Num, $"{Model.Current_Health}/{Model.Stats.Health}");
        Set_Text(Movement_Num, Model.Movement_Remaining_This_Turn.ToString());
        Set_Text(Attack_Num, $"{Model.Stats.Attack_Skill}+");
        Set_Text(Range_Num, Model.Stats.Attack_Range.ToString());
        Set_Text(Damage_Num, Model.Stats.Attack_Damage.ToString());
        Set_Text(Armor_Num, $"{Model.Stats.Armor_Saves}/{Model.Stats.Armor_Target}+");
        Set_Text(Description_Text, Build_Description(Model.Stats));

        Set_Image(Faction_Icon, Faction?.Get_Faction_Icon(), Default_Faction_Icon);
        Set_Image(Model_Icon, Faction?.Get_Art_By_Type(Model.Type)?.Stat_Card_Portrait, Default_Model_Icon);

        if (Status_Row != null) Status_Row.Show_Statuses(Model.Get_Active_Statuses());
        Update_Button_States();
    }

    private void Clear_Card()
    {
        Set_Text(Name_Text, Default_Name);
        Set_Text(Health_Num, Default_Health);
        Set_Text(Movement_Num, Default_Stat);
        Set_Text(Armor_Num, Default_Stat);
        Set_Text(Attack_Num, Default_Stat);
        Set_Text(Range_Num, Default_Stat);
        Set_Text(Damage_Num, Default_Stat);
        Set_Text(Description_Text, Default_Description);

        if (Faction_Icon != null) Faction_Icon.sprite = Default_Faction_Icon;
        if (Model_Icon != null) Model_Icon.sprite = Default_Model_Icon;
        if (Status_Row != null) Status_Row.Clear();

        if (Attack_Button != null) Attack_Button.gameObject.SetActive(false);
        if (Sprint_Button != null) Sprint_Button.gameObject.SetActive(false);
        if (Ability_Button != null) Ability_Button.gameObject.SetActive(false);
    }

    private static string Build_Description(Model_Stats_SO Stats)
    {
        string Description = "";
        if (Stats.Is_Ranged) Description += $"{Description_Bullet}Ranged Attack\n";
        if (Stats.Has_Splash_Damage) Description += $"{Description_Bullet}Splash Damage\n";
        if (Stats.Ability_Data != null)
        {
            string Kind_Label = Stats.Ability_Data.Kind == Ability_Kind.Active ? "Active" : "Passive";
            Description += $"{Description_Bullet}{Kind_Label}: {Stats.Ability_Data.Ability_Name}\n";
        }
        if (string.IsNullOrEmpty(Description)) return "No special abilities";
        return Description.TrimEnd('\n');
    }

    private static void Set_Text(TextMeshProUGUI Field, string Value) { if (Field != null) Field.text = Value; }
    private static void Set_Image(Image Field, Sprite Primary, Sprite Fallback) { if (Field != null) Field.sprite = Primary != null ? Primary : Fallback; }

    private void Update_Button_States()
    {
        Model_Standard_Behavior Displayed = Current_Displayed_Model;
        bool Has_Model = Displayed != null;
        int currentActivePlayer = MatchFlow != null ? MatchFlow.Active_Player : Active_Player;

        bool Is_Mine = Has_Model && Displayed.Team == currentActivePlayer;
        bool Is_Active_Model = Has_Model && MatchFlow != null && Displayed == MatchFlow.Active_Model;

        // Buttons only show if it's the active model's turn
        bool Can_Act = Is_Mine && Is_Active_Model && !Displayed.Has_Attacked_This_Turn && !Displayed.Has_Ended_Turn;

        bool Is_Sprinting = Has_Model && Displayed.Is_Sprinting_This_Turn;
        bool Locked_To_Movement = Has_Model && Displayed.Has_Status(Status_Type.Death_On_Wheels);
        bool Is_Datastream = Has_Model && Displayed.Has_Status(Status_Type.Datastream);

        // Hide buttons if it's not the active model
        if (Attack_Button != null) Attack_Button.gameObject.SetActive(Is_Mine && Is_Active_Model);
        if (Sprint_Button != null) Sprint_Button.gameObject.SetActive(Is_Mine && Is_Active_Model);

        if (Ability_Button != null)
        {
            bool Has_Active_Ability = Is_Mine && Is_Active_Model && Displayed.Stats != null && Displayed.Stats.Get_Ability_Kind() == Ability_Kind.Active;
            Ability_Button.gameObject.SetActive(Has_Active_Ability);
        }

        if (Attack_Button != null) Attack_Button.interactable = Can_Act && !Is_Sprinting && !Locked_To_Movement && !Is_Datastream;
        if (Sprint_Button != null) Sprint_Button.interactable = Can_Act && !Is_Sprinting && !Locked_To_Movement && !Is_Datastream;

        if (Ability_Button != null && Ability_Button.gameObject.activeSelf)
        {
            bool Ability_Ready = Displayed.Ability == null || Displayed.Ability_Is_Ready;

            bool Can_Use_Ability = false;
            if (Ability_Ready && Displayed.Ability != null && BoardRef != null)
            {
                Can_Use_Ability = Displayed.Ability.Can_Activate(Displayed, BoardRef);
            }
            else if (Ability_Ready && Displayed.Ability == null)
            {
                Can_Use_Ability = true;
            }

            Ability_Button.interactable = Can_Act && !Is_Sprinting && Can_Use_Ability;
        }
    }

    public void Refresh_Displayed_Stats(Model_Standard_Behavior Model)
    {
        if (Current_Displayed_Model != Model) return;
        Set_Text(Health_Num, $"{Model.Current_Health}/{Model.Stats.Health}");
        Set_Text(Movement_Num, Model.Movement_Remaining_This_Turn.ToString());
        if (Status_Row != null) Status_Row.Show_Statuses(Model.Get_Active_Statuses());
        Update_Button_States();
    }

    public void Silent_Refresh_Card(Model_Standard_Behavior Model)
    {
        if (Model == null || Model.Stats == null) return;
        if (Current_Displayed_Model != Model) return;

        // Updates text and buttons instantly without playing the animation
        Populate_Card_Data(Model, Current_Faction);
        Update_Button_States();
    }

    private void On_Attack_Button_Clicked()
    {
        if (Combat_Mgr != null) Combat_Mgr.On_Attack_Button_Pressed();
    }

    private void On_Sprint_Button_Clicked()
    {
        if (InteractionHandler == null) return;
        Model_Standard_Behavior Selected = InteractionHandler.Get_Selected_Model();
        if (Selected == null) return;

        // If they are ALREADY sprinting, try to cancel it
        if (Selected.Is_Sprinting_This_Turn)
        {
            if (MovementEngine != null && MovementEngine.Cancel_Sprint(Selected))
            {
                if (InteractionHandler.Get_Highlighter() != null)
                    InteractionHandler.Get_Highlighter().Display_Movement_Range(Selected);

                Silent_Refresh_Card(Selected);
            }
            return;
        }

        // If they are NOT sprinting, start it
        if (MovementEngine != null && MovementEngine.Begin_Sprint(Selected))
        {
            if (InteractionHandler.Get_Highlighter() != null)
                InteractionHandler.Get_Highlighter().Display_Movement_Range(Selected);

            Hide_Model_Info();
        }
    }

    private void On_Ability_Button_Clicked()
    {
        Debug.Log("[Ability Button] Clicked!");

        if (InteractionHandler == null)
        {
            Debug.LogError("InteractionHandler is NULL on Card_UI_Controller!");
            return;
        }

        var Targeting = FindAnyObjectByType<Ability_Targeting_Controller>();
        if (Targeting != null && Targeting.Is_Currently_Targeting())
        {
            Debug.Log("Already in targeting mode.");
            return;
        }

        Model_Standard_Behavior Selected = InteractionHandler.Get_Selected_Model();
        if (Selected == null)
        {
            Debug.LogError("No model is currently selected!");
            return;
        }

        // Prevent using abilities while sprinting
        if (Selected.Is_Sprinting_This_Turn)
        {
            Debug.Log("Cannot use ability while sprinting!");
            return;
        }

        if (Selected.Ability == null)
        {
            Debug.LogError($"Model {Selected.Stats.Model_Name} has no Ability assigned!");
            return;
        }

        var board = FindAnyObjectByType<Battle_Board_Behavior>();
        if (board == null)
        {
            Debug.LogError("Could not find Battle_Board_Behavior in the scene!");
            return;
        }

        if (Selected.Ability.Can_Activate(Selected, board))
        {
            Debug.Log($"[Ability] {Selected.Ability.Ability_Name} is valid. Activating...");
            Selected.Ability.Activate(Selected, board);
            if (Current_Displayed_Model == Selected) Silent_Refresh_Card(Selected);
        }
        else
        {
            Debug.LogWarning($"[Ability] Can_Activate returned FALSE for {Selected.Ability.Ability_Name}. Reason: No targets in range, or the model has already attacked.");
        }
    }
}