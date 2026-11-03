using UnityEngine;

public class PlayerSetup : MonoBehaviour
{
    // This script will make sure that players don't control each other or see out of each other's POV.
    // On the player prefab, deactivate the controlling script and the camera. Put this script on the player and it will automatically enable the script and camera for each player so that it's correct.
    public Player player;

    public GameObject cam;

    public void IsLocalPlayer()
    {
        player.enabled = true;
        cam.SetActive(true);
    }
}
