[System.Serializable]
public struct Status_Instance
{
    public Status_Type Type;
    public int Turns_Remaining;
    public Status_Definition_SO Definition;

    public Status_Instance(Status_Type type, Status_Definition_SO def, int turns)
    {
        Type = type;
        Definition = def;
        Turns_Remaining = turns;
    }
}