using UnityEngine;

public class RoomListButton : MonoBehaviour
    // This script should go on the prefab of the button that goes on the JoinRoom list. Make sure the button's OnClick() event calls OnButtonPressed.
{
    public string roomName;
    public RoomManager roomManager;

    public void OnButtonPressed()
    {
        roomManager.JoinRoomByName(roomName);
    }
}
