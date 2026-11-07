using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class Ability_Confirmation_UI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup CanvasGroup;
    [SerializeField] private TextMeshProUGUI AbilityNameText;
    [SerializeField] private TextMeshProUGUI DescriptionText;
    [SerializeField] private TextMeshProUGUI SelectionText;

    [Header("Buttons")]
    [SerializeField] private Button ConfirmButton;
    [SerializeField] private Button CancelButton;

    [Header("Fading")]
    [Tooltip("How long the fade in/out takes in seconds.")]
    [SerializeField] private float FadeDuration = 0.15f;

    private Ability_Targeting_Controller TargetingController;
    private Coroutine FadeCoroutine;

    public void Setup(Ability_Targeting_Controller controller)
    {
        TargetingController = controller;

        ConfirmButton.onClick.RemoveAllListeners();
        ConfirmButton.onClick.AddListener(() =>
        {
            if (TargetingController != null) TargetingController.Confirm();
        });

        CancelButton.onClick.RemoveAllListeners();
        CancelButton.onClick.AddListener(() =>
        {
            if (TargetingController != null) TargetingController.Cancel();
        });
    }

    public void Show(Model_Standard_Behavior SourceModel, Ability_Definition_SO Ability, int MinSelections)
    {
        gameObject.SetActive(true);

        if (Ability != null)
        {
            AbilityNameText.text = Ability.Ability_Name;
            DescriptionText.text = Ability.Description;
        }
        else
        {
            AbilityNameText.text = "Ability";
            DescriptionText.text = "Select targets.";
        }

        Update_Selection(0, MinSelections, 0);

        if (FadeCoroutine != null) StopCoroutine(FadeCoroutine);
        FadeCoroutine = StartCoroutine(FadeCanvas(1f, true));
    }

    public void Hide()
    {
        if (!gameObject.activeInHierarchy) return;

        if (FadeCoroutine != null) StopCoroutine(FadeCoroutine);
        FadeCoroutine = StartCoroutine(FadeCanvas(0f, false));
    }

    public void Update_Selection(int CurrentSelections, int MinSelections, int MaxSelections)
    {
        string maxStr = MaxSelections == int.MaxValue ? "∞" : MaxSelections.ToString();
        SelectionText.text = $"Selected: {CurrentSelections} / {maxStr}";

        ConfirmButton.interactable = CurrentSelections >= MinSelections;
    }

    private IEnumerator FadeCanvas(float targetAlpha, bool blockRaycasts)
    {
        CanvasGroup.blocksRaycasts = blockRaycasts;
        CanvasGroup.interactable = blockRaycasts;

        float startAlpha = CanvasGroup.alpha;
        float time = 0f;

        while (time < FadeDuration)
        {
            time += Time.unscaledDeltaTime;
            CanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / FadeDuration);
            yield return null;
        }

        CanvasGroup.alpha = targetAlpha;

        if (targetAlpha <= 0f)
        {
            gameObject.SetActive(false);
        }
    }
}