using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class Crypt_Blade_Prompt_UI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button Normal_Damage_Button;
    [SerializeField] private Button Roll_Dice_Button;
    [SerializeField] private TextMeshProUGUI Prompt_Text;

    private Action<bool> On_Choice_Made; // True = Roll Dice, False = Normal Damage

    private void Awake()
    {
        // Ensure the panel is hidden at the start of the game
        gameObject.SetActive(false);
    }

    private void Start()
    {
        // Setup the button listeners
        if (Normal_Damage_Button != null)
        {
            Normal_Damage_Button.onClick.RemoveAllListeners();
            Normal_Damage_Button.onClick.AddListener(() => Make_Choice(false));
        }

        if (Roll_Dice_Button != null)
        {
            Roll_Dice_Button.onClick.RemoveAllListeners();
            Roll_Dice_Button.onClick.AddListener(() => Make_Choice(true));
        }
    }

    public void Show_Prompt(Model_Standard_Behavior Attacker, Model_Standard_Behavior Defender, Action<bool> callback)
    {
        On_Choice_Made = callback;
        gameObject.SetActive(true);

        if (Prompt_Text != null)
            Prompt_Text.text = $"{Attacker.Stats.Model_Name} hit {Defender.Stats.Model_Name}!\nUse Crypt Blade to roll for higher damage?";
    }

    private void Make_Choice(bool rollDice)
    {
        gameObject.SetActive(false);
        On_Choice_Made?.Invoke(rollDice);
    }
}