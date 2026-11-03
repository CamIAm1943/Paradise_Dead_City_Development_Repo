using System.Collections.Generic;
using UnityEngine;

public class Army_UI_Controller : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Battle_Board_Behavior Board;
    [SerializeField] private Match_Flow_Manager MatchFlow;
    [SerializeField] private Gameplay_Interaction_Handler InteractionHandler;
    [SerializeField] private Transform Army_Container;
    [SerializeField] private GameObject Army_Card_Prefab;

    private List<Army_Card_UI> Spawned_Cards = new List<Army_Card_UI>();

    private void OnEnable()
    {
        if (MatchFlow != null) MatchFlow.OnTurnStarted += Handle_Turn_Started;
    }

    private void OnDisable()
    {
        if (MatchFlow != null) MatchFlow.OnTurnStarted -= Handle_Turn_Started;
    }

    private void Handle_Turn_Started(Model_Standard_Behavior model)
    {
        // Only rebuild the army bar if we are actually in the gameplay phase
        if (MatchFlow != null && MatchFlow.Current_Phase == Game_State.Gameplay)
        {
            Setup_Army_Bar(model.Team);
        }
    }

    public void Setup_Army_Bar(int activeTeam)
    {
        if (Board == null || MatchFlow == null || Army_Container == null || Army_Card_Prefab == null) return;

        Clear_Bar();
        Faction_Data_SO Active_Faction = activeTeam == 1 ? MatchFlow.Player_1_Faction : MatchFlow.Player_2_Faction;
        List<Model_Standard_Behavior> Player_Models = new List<Model_Standard_Behavior>();

        for (int X = 0; X < Board.Get_Tile_Count_X(); X++)
            for (int Y = 0; Y < Board.Get_Tile_Count_Y(); Y++)
            {
                var M = Board.Get_Model_At(X, Y);
                if (M != null && M.gameObject.activeSelf && M.Team == activeTeam)
                    Player_Models.Add(M);
            }

        foreach (var Model in Player_Models)
        {
            GameObject CardGO = Instantiate(Army_Card_Prefab, Army_Container);
            RectTransform rt = CardGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120, 180);

            Army_Card_UI CardScript = CardGO.GetComponent<Army_Card_UI>();
            if (CardScript != null)
            {
                CardScript.Setup(Model, Board, Active_Faction);
                Spawned_Cards.Add(CardScript);
            }
        }

        Update_Selection(InteractionHandler != null ? InteractionHandler.Get_Selected_Model() : null);
    }

    public void Setup_Deployment_Bar(int activeTeam, List<Model_Type> UnitsToPlace)
    {
        if (Board == null || MatchFlow == null || Army_Container == null || Army_Card_Prefab == null) return;

        Clear_Bar();
        Faction_Data_SO Active_Faction = activeTeam == 1 ? MatchFlow.Player_1_Faction : MatchFlow.Player_2_Faction;

        foreach (var Type in UnitsToPlace)
        {
            GameObject CardGO = Instantiate(Army_Card_Prefab, Army_Container);
            RectTransform rt = CardGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120, 180);

            Army_Card_UI CardScript = CardGO.GetComponent<Army_Card_UI>();
            if (CardScript != null)
            {
                CardScript.Setup_Deployment(Type, Board, Active_Faction);
                Spawned_Cards.Add(CardScript);
            }
        }

        Highlight_Deployment_Selection(null);
    }

    public void Highlight_Deployment_Selection(Army_Card_UI selectedCard)
    {
        foreach (var Card in Spawned_Cards)
            Card.Set_Deployment_Highlight(Card == selectedCard);
    }

    public void Remove_Deployment_Card(Army_Card_UI Card)
    {
        if (Spawned_Cards.Contains(Card))
        {
            Spawned_Cards.Remove(Card);
            Destroy(Card.gameObject);
        }
    }

    private void Clear_Bar()
    {
        foreach (Transform child in Army_Container)
            Destroy(child.gameObject);
        Spawned_Cards.Clear();
    }

    public void Update_Selection(Model_Standard_Behavior selectedModel)
    {
        if (MatchFlow == null) return;
        if (MatchFlow.Current_Phase == Game_State.Player_1_Place_Models || MatchFlow.Current_Phase == Game_State.Player_2_Place_Models) return;

        Model_Standard_Behavior focusModel = null;
        if (selectedModel != null)
        {
            if (selectedModel.Team == MatchFlow.Active_Player) focusModel = selectedModel;
        }
        else
        {
            focusModel = MatchFlow.Active_Model;
        }

        foreach (var Card in Spawned_Cards)
            Card.Update_Selected_State(Card.Get_Linked_Model() == focusModel);
    }
}