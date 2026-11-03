using UnityEngine;

[CreateAssetMenu(fileName = "New Game Mode", menuName = "Scriptable Objects/Game Mode")]
public class Game_Mode_SO : ScriptableObject
{
    public string Mode_Name;
    public string Description;
    // Add fields as you design the game modes.
}