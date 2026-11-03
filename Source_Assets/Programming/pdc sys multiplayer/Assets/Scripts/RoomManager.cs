using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using TMPro;

public class RoomManager : MonoBehaviourPunCallbacks
    // THIS SCRIPT ENCOMPASSES FUNCTIONS AND BEHAVIORS REGARDING CONNECTING TO THE SERVER, HENCE MONOBEHAVIORPUNCALLBACKS
{
    public GameObject player; // This is the prefab for the player. The player should be able to handle being instantiated. IMPORTANT: THE PLAYER PREFAB MUST GO IN A FOLDER CALLED "Resources" EXACTLY.
    [Space]
    public Transform spawnPos; // The spawn point of the player

    [Space]
    public GameObject roomCam; // This is the base camera that exists in the scene. This lets the buttons appear before the player enters a room, and is destroyed when the player (with the camera prefab) enters.
    [Space]
    public GameObject buttonMenu; // The menu that holds all the base buttons
    public GameObject connectingScreen; // The screen that says "Connecting...", signalling to the player that they're connecting
    [Space]
    public TMP_InputField createRoomName; // This is the inputfield tied to the create room button, which gives the created room its name.

    [Space]
    public Transform roomListParent; // This is the content in viewport inside the scroll view that holds the list of rooms.
    public GameObject roomListItemPrefab; // This is the prefab for the button that is used to connect to rooms. The button must have the RoomListbutton script. See the prefab for more info.
    public GameObject roomSelectMenu; // This is the gameobject that holds the menu with the join room scroll view.

    private List<RoomInfo> cachedRoomList = new List<RoomInfo>();
    private string connectType;


    public void Connect() // Call this function after DefineConnectType to get the player into a room
    {
        buttonMenu.SetActive(false); // These lines disable all of the buttons and bring up the screen that says "Connecting..."
        connectingScreen.SetActive(true);

        PhotonNetwork.ConnectUsingSettings();
    }

    public void DefineConnectType(string conType) // Call this function before "Connect" via other scripts or clickable buttons and pass through the string that corresponds to the button.
    {
        conType = conType.ToLower();
        if (conType != "quick match" && conType != "create room" && conType != "join room") // The potential connect types (not case sensitive)
        {
            Debug.LogWarning("The connect type was not properly defined. Starting quick match...");
            return;
        }

        connectType = conType;
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Server");

        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("We're connected");
        if (connectType == "create room")
            PhotonNetwork.CreateRoom(createRoomName.text, SetRoomOptions(), null); // SetRoomOptions can be seen below
        else if (connectType == "join room")
        {
            roomSelectMenu.SetActive(true);
            connectingScreen.SetActive(false);
        }
        else
            PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("We're connected and in a room now");

        GameObject _player = PhotonNetwork.Instantiate(player.name, spawnPos.position, Quaternion.identity);
        _player.GetComponent<PlayerSetup>().IsLocalPlayer(); // Ensure that the PlayerSetup script is on the player

        roomCam.SetActive(false);
    }
    public override void OnJoinRoomFailed(short returnCode, string message) // If you tried to join random and there are no rooms, create a new room with a random name.
                                                                            // If you tried to join a specific room that was full, you're kicked back to the main menu.
    {
        if (returnCode == 32760)
        {
            PhotonNetwork.CreateRoom($"Room{UnityEngine.Random.Range(0, 10000):D5}", SetRoomOptions(), null);
            roomCam.SetActive(false);
        }
        else if (returnCode == 32765)
        {
            Debug.LogError("Room is full.");
            buttonMenu.SetActive(true);
            connectingScreen.SetActive(false);
            roomSelectMenu.SetActive(false);
        }
        else
        {
            Debug.LogError("Something went wrong.");
            buttonMenu.SetActive(true);
            connectingScreen.SetActive(false);
        }
    }
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (returnCode == 32760)
        {
            PhotonNetwork.CreateRoom($"Room{UnityEngine.Random.Range(0, 10000):D4}", SetRoomOptions(), null);
            roomCam.SetActive(false);
        }
        else if (returnCode == 32765)
        {
            Debug.LogError("Room is full.");
            buttonMenu.SetActive(true);
            connectingScreen.SetActive(false);
        }
        else
        {
            Debug.LogError("Something went wrong.");
            buttonMenu.SetActive(true);
            connectingScreen.SetActive(false);
        }
    }

    public RoomOptions SetRoomOptions() // Defines the room settings (currently just max players)
    {
        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = 2; // Max players is 2
        return roomOptions;
    }


    public override void OnRoomListUpdate(List<RoomInfo> roomList) // Dynamically updates the list of rooms on the Join Room menu
    {
        if (!roomSelectMenu.activeInHierarchy)
            return;

        if (cachedRoomList.Count <= 0)
        {
            cachedRoomList = roomList;
        }
        else
        {
            foreach (var room in roomList)
            {
                for (int i = 0; i < cachedRoomList.Count; i++)
                {
                    if (cachedRoomList[i].Name == room.Name)
                    {
                        List<RoomInfo> newList = cachedRoomList;

                        if (room.RemovedFromList)
                        {
                            newList.Remove(newList[i]);
                        }
                        else
                        {
                            newList[i] = room;
                        }

                        cachedRoomList = newList;
                    }
                }
            }
        }
        UpdateUI();
    }

    void UpdateUI() // Updates the room list UI
    {
        foreach (Transform roomItem in roomListParent)
        {
            Destroy(roomItem.gameObject);
        }

        foreach (var room in cachedRoomList)
        {
            if (room.PlayerCount != 1) // The menu will not display full rooms (Delete these two lines if you want to display rooms that are full)
                return;

            GameObject roomItem = Instantiate(roomListItemPrefab, roomListParent);

            roomItem.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = room.Name;

            RoomListButton buttonScript = roomItem.GetComponent<RoomListButton>();

            buttonScript.roomName = room.Name;
            buttonScript.roomManager = this;
        }
    }

    public void JoinRoomByName(string _name) // RoomListButton uses this to join the room that the player clicks on
    {
        PhotonNetwork.JoinRoom(_name);
    }

    public void LeaveRoomList() // Call this on the button that takes you from the room list back to main.
    {
        PhotonNetwork.Disconnect();
    }
}
