public static class Turn_System_Hook_Bridge
{
    public static event System.Action<Model_Standard_Behavior, Battle_Board_Behavior> On_Turn_Start;
    public static event System.Action<Model_Standard_Behavior, Battle_Board_Behavior> On_Turn_End;

    public static void Trigger_Turn_Start(Model_Standard_Behavior model, Battle_Board_Behavior board)
    {
        if (model == null) return;
        model.Tick_Statuses();
        model.Tick_Ability_Recharge();
        On_Turn_Start?.Invoke(model, board);
    }

    public static void Trigger_Turn_End(Model_Standard_Behavior model, Battle_Board_Behavior board)
    {
        if (model == null) return;
        On_Turn_End?.Invoke(model, board);
    }

    // IMPORTANT: Call this in your Bootstrapper or when changing scenes!
    public static void Clear_All_Listeners()
    {
        On_Turn_Start = null;
        On_Turn_End = null;
    }
}