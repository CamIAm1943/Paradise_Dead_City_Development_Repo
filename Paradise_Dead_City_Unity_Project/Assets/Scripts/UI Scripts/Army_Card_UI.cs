using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class Army_Card_UI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image Portrait_Image;
    [SerializeField] private Image Team_Border_Image;

    private Model_Standard_Behavior Linked_Model;
    private Battle_Board_Behavior Board;
    private Gameplay_Interaction_Handler InteractionHandler;

    private bool Is_Deployment_Card = false;
    private Model_Type Deployment_Type;
    public Model_Type Get_Deployment_Type() => Deployment_Type;

    private Color Base_Team_Color;
    private Coroutine FadeCoroutine;
    private CanvasGroup CanvasGroup;
    private GameObject Drag_Ghost;

    public Model_Standard_Behavior Get_Linked_Model() => Linked_Model;

    private void Awake()
    {
        CanvasGroup = GetComponent<CanvasGroup>();
        if (CanvasGroup == null) CanvasGroup = gameObject.AddComponent<CanvasGroup>();
        InteractionHandler = FindAnyObjectByType<Gameplay_Interaction_Handler>();
    }

    public void Setup(Model_Standard_Behavior model, Battle_Board_Behavior board, Faction_Data_SO faction)
    {
        Linked_Model = model; Board = board; Is_Deployment_Card = false;
        if (Portrait_Image != null && faction != null)
        {
            var Art = faction.Get_Art_By_Type(model.Type);
            if (Art != null) Portrait_Image.sprite = Art.Army_Card_Portrait;
        }
        if (faction != null && faction.Team_Materials.Length > 0) Base_Team_Color = faction.Team_Materials[0].color;
        else Base_Team_Color = Color.gray;
        if (Team_Border_Image != null) Team_Border_Image.color = Base_Team_Color;
    }

    public void Setup_Deployment(Model_Type type, Battle_Board_Behavior board, Faction_Data_SO faction)
    {
        Linked_Model = null; Board = board; Is_Deployment_Card = true; Deployment_Type = type;
        if (Portrait_Image != null && faction != null)
        {
            var Art = faction.Get_Art_By_Type(type);
            if (Art != null) Portrait_Image.sprite = Art.Army_Card_Portrait;
        }
        if (faction != null && faction.Team_Materials.Length > 0) Base_Team_Color = faction.Team_Materials[0].color;
        else Base_Team_Color = Color.gray;
        if (Team_Border_Image != null) Team_Border_Image.color = Base_Team_Color;
    }

    public void Update_Selected_State(bool isSelected)
    {
        if (!gameObject.activeInHierarchy || Is_Deployment_Card) return;
        if (FadeCoroutine != null) StopCoroutine(FadeCoroutine);
        FadeCoroutine = StartCoroutine(Fade_Border(isSelected ? Color.white : Base_Team_Color));
    }

    private IEnumerator Fade_Border(Color targetColor)
    {
        if (Team_Border_Image == null) yield break;
        Color startColor = Team_Border_Image.color;
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.unscaledDeltaTime;
            Team_Border_Image.color = Color.Lerp(startColor, targetColor, elapsed / 0.2f);
            yield return null;
        }
        Team_Border_Image.color = targetColor;
    }

    public void Set_Deployment_Highlight(bool active)
    {
        if (Team_Border_Image == null) return;
        if (active) { Team_Border_Image.color = new Color(1f, 0.85f, 0f); transform.localScale = Vector3.one * 1.15f; }
        else { Team_Border_Image.color = Base_Team_Color; transform.localScale = Vector3.one; }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Is_Deployment_Card)
        {
            // Will be handled by Deployment Manager soon. For now, use Board's bridge.
            if (Board != null) Board.Select_Deployment_Card(this);
        }
        else if (Linked_Model != null && InteractionHandler != null)
        {
            InteractionHandler.Select_Model_Public(Linked_Model);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!Is_Deployment_Card || Board == null) return;
        Board.Begin_Deployment_Drag(this);
        Drag_Ghost = Instantiate(gameObject, transform.root);
        Drag_Ghost.transform.SetAsLastSibling();
        Destroy(Drag_Ghost.GetComponent<Army_Card_UI>());

        CanvasGroup ghostCG = Drag_Ghost.GetComponent<CanvasGroup>();
        if (ghostCG != null) { ghostCG.blocksRaycasts = false; ghostCG.alpha = 0.7f; }
        CanvasGroup.alpha = 0f;
        CanvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Drag_Ghost != null) Drag_Ghost.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!Is_Deployment_Card) return;

        if (Drag_Ghost != null) Destroy(Drag_Ghost);
        CanvasGroup.alpha = 1f;
        CanvasGroup.blocksRaycasts = true;

        if (Board != null)
        {
            if (Camera.main != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(eventData.position);
                if (Physics.Raycast(ray, out RaycastHit hit, 500f))
                {
                    Vector2Int tilePos = Board.Convert_World_To_Tile_Position(hit.point);
                    var deployment = Board.Get_Deployment_Manager();
                    if (deployment != null)
                    {
                        deployment.Try_Place_At(tilePos);
                    }
                }
            }
            // --------------------------------------------------------

            Board.End_Deployment_Drag();
        }
    }
}