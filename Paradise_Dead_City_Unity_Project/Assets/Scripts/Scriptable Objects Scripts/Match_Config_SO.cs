using UnityEngine;

[CreateAssetMenu(fileName = "Match_Config", menuName = "Scriptable Objects/Match Config")]
public class Match_Config_SO : ScriptableObject
{
    public Map_Data_SO Map;
    public Game_Mode_SO Mode;

    public Faction_Data_SO Player_1_Faction;
    public Faction_Data_SO Player_2_Faction;

    public Army_Composition_SO Player_1_Army;
    public Army_Composition_SO Player_2_Army;

    public int Player_1_Team = 1;
    public int Player_2_Team = 2;
}