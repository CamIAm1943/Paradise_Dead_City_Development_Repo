using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrapper : MonoBehaviour
{
    [SerializeField] private string First_Scene_Name = "Main_Menu";

    private void Start()
    {
        // 1. Initialize persistent systems here. For now, none.
        //    Example:
        //    Instantiate(Audio_Manager_Prefab);
        //    Save_System.Load();

        // 2. Load the next scene.
        SceneManager.LoadScene(First_Scene_Name);
    }
}