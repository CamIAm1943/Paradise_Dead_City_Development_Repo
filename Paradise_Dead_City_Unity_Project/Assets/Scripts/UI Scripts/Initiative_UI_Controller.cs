using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Initiative_UI_Controller : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Match_Flow_Manager MatchFlow;
    [SerializeField] private Gameplay_Interaction_Handler InteractionHandler;
    [SerializeField] private Transform Initiative_Container;
    [SerializeField] private GameObject Head_Icon_Prefab;

    private Dictionary<Model_Standard_Behavior, GameObject> Icon_Objects = new Dictionary<Model_Standard_Behavior, GameObject>();
    private Dictionary<Model_Standard_Behavior, Image> Icon_Borders = new Dictionary<Model_Standard_Behavior, Image>();
    private Dictionary<Model_Standard_Behavior, Color> Base_Colors = new Dictionary<Model_Standard_Behavior, Color>();

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
        // Rebuild the bar so the active unit is visually at the front
        Setup_Initiative_Bar();
    }

    public void Setup_Initiative_Bar()
    {
        if (MatchFlow == null || Initiative_Container == null || Head_Icon_Prefab == null) return;
        StopAllCoroutines();

        foreach (Transform child in Initiative_Container) Destroy(child.gameObject);

        Icon_Objects.Clear(); Icon_Borders.Clear(); Base_Colors.Clear();

        if (MatchFlow.Turn_Order_List.Count == 0) return;

        foreach (var Model in MatchFlow.Turn_Order_List)
        {
            if (Model == null || !Model.gameObject.activeSelf) continue;

            GameObject IconGO = Instantiate(Head_Icon_Prefab, Initiative_Container);
            RectTransform rt = IconGO.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(64, 64);

            Image BorderImage = IconGO.GetComponent<Image>();
            Image PortraitImage = IconGO.transform.GetChild(0).GetComponent<Image>();

            Faction_Data_SO Faction = Model.Team == 1 ? MatchFlow.Player_1_Faction : MatchFlow.Player_2_Faction;

            Color baseColor = (Faction != null && Faction.Team_Materials.Length > 0) ? Faction.Team_Materials[0].color : Color.gray;
            Base_Colors[Model] = baseColor;

            if (BorderImage != null) BorderImage.color = baseColor;

            if (PortraitImage != null && Faction != null)
            {
                var Art = Faction.Get_Art_By_Type(Model.Type);
                if (Art != null && Art.Initiative_Icon != null) PortraitImage.sprite = Art.Initiative_Icon;
            }

            Button iconButton = IconGO.GetComponent<Button>();
            if (iconButton == null) iconButton = IconGO.AddComponent<Button>();

            Model_Standard_Behavior capturedModel = Model;
            iconButton.onClick.AddListener(() => {
                if (InteractionHandler != null) InteractionHandler.Select_Model_Public(capturedModel);
            });

            Icon_Objects[Model] = IconGO;
            Icon_Borders[Model] = BorderImage;
        }

        Update_Selection(InteractionHandler != null ? InteractionHandler.Get_Selected_Model() : null);
    }

    public void Update_Selection(Model_Standard_Behavior selectedModel)
    {
        foreach (var kvp in Icon_Objects)
        {
            Model_Standard_Behavior model = kvp.Key;
            Image border = Icon_Borders[model];

            if (border != null)
            {
                Color targetColor = Base_Colors[model];

                if (selectedModel != null)
                {
                    // If a unit is selected, IT gets the white highlight
                    if (model == selectedModel) targetColor = Color.white;
                }
                else
                {
                    // If nothing is selected, the Active Model gets the white highlight
                    if (model == MatchFlow.Active_Model) targetColor = Color.white;
                }

                StartCoroutine(Fade_Border(border, targetColor, 0.2f));
            }
        }
    }

    private IEnumerator Fade_Border(Image img, Color target, float duration)
    {
        if (img == null) yield break;
        Color start = img.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (img == null) yield break;
            img.color = Color.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        if (img != null) img.color = target;
    }

    public void Refresh_Team_Colors()
    {
        if (MatchFlow == null || Icon_Objects == null) return;

        foreach (var kvp in Icon_Objects)
        {
            Model_Standard_Behavior model = kvp.Key;
            if (model == null) continue;

            // Grab the faction based on the model's NEW team
            Faction_Data_SO Faction = model.Team == 1 ? MatchFlow.Player_1_Faction : MatchFlow.Player_2_Faction;
            Color baseColor = (Faction != null && Faction.Team_Materials.Length > 0) ? Faction.Team_Materials[0].color : Color.gray;

            // Update the cached color
            Base_Colors[model] = baseColor;
        }

        // Trigger the fade animation to the new colors
        Update_Selection(InteractionHandler != null ? InteractionHandler.Get_Selected_Model() : null);
    }
}