using UnityEngine;
using UnityEngine.SceneManagement;

public class Main_Menu_Controller : MonoBehaviour
{
    [SerializeField] private string Battle_Menu_Scene_Name = "Battle_Menu";
    [SerializeField] private string Settings_Scene_Name = "Settings";
    [SerializeField] private string Rules_Scene_Name = "Rules";

    public void On_Battle_Button_Pressed()
    {
        SceneManager.LoadScene(Battle_Menu_Scene_Name);
    }

    public void On_Settings_Button_Pressed()
    {
        SceneManager.LoadScene(Settings_Scene_Name);
    }

    public void On_Rules_Button_Pressed()
    {
        SceneManager.LoadScene(Rules_Scene_Name);
    }
}