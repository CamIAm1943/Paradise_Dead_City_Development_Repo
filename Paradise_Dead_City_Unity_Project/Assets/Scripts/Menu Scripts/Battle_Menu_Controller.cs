using UnityEngine;
using UnityEngine.SceneManagement;

public class Battle_Menu_Controller : MonoBehaviour
{
    [SerializeField] private string Main_Menu_Scene_Name = "Main_Menu";
    [SerializeField] private string Gameplay_Scene_Name = "Game";

    [Header("Match Config")]
    [SerializeField] private Match_Config_SO Match_Config;

    public void On_Back_Button_Pressed()
    {
        SceneManager.LoadScene(Main_Menu_Scene_Name);
    }

    public void On_Play_Button_Pressed()
    {
        // At this point the Army / Map / Player tabs have already written
        // their selections into Match_Config. The Play button just validates
        // and loads.
        if (Match_Config == null)
        {
            Debug.LogError("Battle_Menu_Controller: Match_Config not assigned.");
            return;
        }

        if (Match_Config.Map == null || Match_Config.Player_1_Army == null || Match_Config.Player_2_Army == null)
        {
            Debug.LogWarning("Cannot start match: map or army not selected.");
            return;
        }


        SceneManager.LoadScene(Gameplay_Scene_Name);
    }
}