using UnityEngine;

[CreateAssetMenu(fileName = "Datastream", menuName = "Scriptable Objects/Abilities/Datastream")]
public class Datastream_Ability : Ability_Definition_SO
{
    [Header("Datastream Settings")]
    [Tooltip("Maximum number of turns the Specter can stay phased.")]
    [Range(1, 3)]
    public int Max_Duration_Turns = 2;

    public override bool Can_Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        if (Model == null || Board == null) return false;
        if (Model.Has_Ended_Turn) return false;
        if (Model.Has_Attacked_This_Turn) return false;

        // If it's already active, they can ALWAYS click it to turn it off.
        if (Model.Has_Status(Status_Type.Datastream)) return true;

        if (Model.Ability_Recharge_Remaining > 0) return false;
        return true;
    }

    public override void Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        var Interaction = Object.FindAnyObjectByType<Gameplay_Interaction_Handler>();
        var CardUI = Object.FindAnyObjectByType<Card_UI_Controller>();

        if (Model.Has_Status(Status_Type.Datastream))
        {
            // --- TURN OFF (STAGGER) ---
            Model.Remove_Status(Status_Type.Datastream);
            Model.Ability_Recharge_Remaining = Mathf.Max(Recharge_Turns, 1);

            // Stagger: Cannot move, cannot attack. 
            Model.Has_Attacked_This_Turn = true;
            Model.Has_Ended_Turn = true;

            // --- Apply Vulnerability only when manually exiting ---
            Model.Apply_Vulnerability();

            Debug.Log($"[Datastream] {Model.Stats.Model_Name} materialized back into reality. Staggered and Vulnerable.");

            if (Interaction != null) Interaction.Select_Model_Public(Model);
            if (CardUI != null) CardUI.Silent_Refresh_Card(Model);
        }
        else
        {
            // --- TURN ON ---
            Model.Add_Status(Status_Type.Datastream, Max_Duration_Turns);

            if (Interaction != null && Interaction.Get_Highlighter() != null)
                Interaction.Get_Highlighter().Display_Movement_Range(Model);

            Debug.Log($"[Datastream] {Model.Stats.Model_Name} dissolved into data! Immune to attacks, can phase through walls.");

            if (Interaction != null) Interaction.Select_Model_Public(Model);
            if (CardUI != null) CardUI.Silent_Refresh_Card(Model);
        }
    }

    // We subscribe to the Turn Start event to handle expiration cleanup
    public override void On_Equip(Model_Standard_Behavior Model)
    {
        Turn_System_Hook_Bridge.On_Turn_Start -= Handle_Datastream_Expiration;
        Turn_System_Hook_Bridge.On_Turn_Start += Handle_Datastream_Expiration;
    }

    private void Handle_Datastream_Expiration(Model_Standard_Behavior model, Battle_Board_Behavior board)
    {
        if (model == null || board == null) return;

        if (model.Ability != this) return;

        // Check if they were standing in a wall/unit when the status expired
        Board_Modifiers currentTile = board.Get_Tile_Type_At(model.Current_X, model.Current_Y);
        Model_Standard_Behavior occupant = board.Get_Model_At(model.Current_X, model.Current_Y);

        bool isInWall = currentTile == Board_Modifiers.Wall || currentTile == Board_Modifiers.Cover;
        bool isStacked = occupant != null && occupant != model;

        // If they are stuck in a wall/unit, eject them
        if ((isInWall || isStacked) && !model.Has_Status(Status_Type.Datastream))
        {
            Debug.LogWarning($"[Datastream] {model.Stats.Model_Name} was trapped inside a wall/unit when Datastream expired! Ejecting...");
            Eject_To_Safe_Tile(model, board);
        }

        // We track this by checking if the recharge is 0 (meaning it ticked down to 0 this turn)
        if (!model.Has_Status(Status_Type.Datastream) && model.Ability_Recharge_Remaining == 0 && model.Datastream_Just_Expired)
        {
            model.Apply_Vulnerability();
            Debug.Log($"[Datastream] {model.Stats.Model_Name}'s Datastream expired naturally. They are now Vulnerable.");

            var CardUI = Object.FindAnyObjectByType<Card_UI_Controller>();
            if (CardUI != null) CardUI.Refresh_Card_For_Model(model);
        }
    }

    private void Eject_To_Safe_Tile(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        foreach (var Dir in Battle_Board_Behavior.Cardinal_Directions)
        {
            Vector2Int Next = new Vector2Int(Model.Current_X, Model.Current_Y) + Dir;
            if (Next.x < 0 || Next.x >= Board.Get_Tile_Count_X() || Next.y < 0 || Next.y >= Board.Get_Tile_Count_Y()) continue;
            if (!Board.Is_Tile_Passable(Next.x, Next.y)) continue;
            if (Board.Get_Model_At(Next.x, Next.y) != null) continue;

            // We found a valid tile!
            Board.Remove_Model(Model.Current_X, Model.Current_Y);
            Board.Place_Model_At(Next.x, Next.y, Model);
            Model.Current_X = Next.x;
            Model.Current_Y = Next.y;
            Model.Set_Position(Board.Get_Unit_Stand_Position(Next.x, Next.y), false);

            Debug.Log($"[Datastream] {Model.Stats.Model_Name} was ejected to ({Next.x}, {Next.y}).");
            return;
        }

        Debug.LogError($"[Datastream] {Model.Stats.Model_Name} was completely boxed in and could not be ejected!");
    }
}