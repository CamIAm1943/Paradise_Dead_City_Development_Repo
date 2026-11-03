using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Cloud Dance", menuName = "Scriptable Objects/Abilities/Cloud Dance")]
public class Cloud_Dance_Ability : Ability_Definition_SO
{
    [Header("Cloud Dance")]
    [Range(1, 8)] public int Jump_Range = 3;
    [Range(1, 4)] public int Shove_Distance = 1;
    [Range(0, 3)] public int Wall_Impact_Damage = 1;

    private static readonly Vector2Int[] Card_Dirs = { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) };

    public override bool Can_Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        if (Model == null || Board == null || Model.Stats == null) return false;
        if (Model.Ability_Recharge_Remaining > 0) return false;
        if (Model.Has_Attacked_This_Turn) return false;
        if (Model.Has_Ended_Turn) return false;
        if (Model.Is_Sprinting_This_Turn) return false;
        return true;
    }

    public override void Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        if (Model == null || Board == null) return;
        var Targeting = Object.FindAnyObjectByType<Ability_Targeting_Controller>();
        if (Targeting == null) return;

        List<Vector2Int> Candidates = new List<Vector2Int>();
        int Count_X = Board.Get_Tile_Count_X();
        int Count_Y = Board.Get_Tile_Count_Y();

        for (int X = 0; X < Count_X; X++)
            for (int Y = 0; Y < Count_Y; Y++)
            {
                if (X == Model.Current_X && Y == Model.Current_Y) continue;
                int Dist = Mathf.Abs(X - Model.Current_X) + Mathf.Abs(Y - Model.Current_Y);
                if (Dist > Jump_Range) continue;
                Vector2Int Pos = new Vector2Int(X, Y);
                if (!Is_Valid_Destination(Board, Pos, Model.Team)) continue;
                Candidates.Add(Pos);
            }

        if (Candidates.Count == 0) return;

        Targeting.Begin(Model, Jump_Range, Candidates, 1, 1,
            tiles => { if (tiles != null && tiles.Count > 0) Resolve(Model, Board, tiles[0]); }, null);
    }

    private void Resolve(Model_Standard_Behavior Monk, Battle_Board_Behavior Board, Vector2Int Destination)
    {
        if (Monk == null || Board == null || Monk.Has_Ended_Turn) return;
        if (!Is_Valid_Destination(Board, Destination, Monk.Team)) return;

        Vector2Int Start = new Vector2Int(Monk.Current_X, Monk.Current_Y);
        Model_Standard_Behavior Slam_Target = Board.Get_Model_At(Destination.x, Destination.y);
        if (Slam_Target != null && Slam_Target.Team == Monk.Team) Slam_Target = null;

        int Shock_Range = Random.Range(1, 7);
        int Shock_Damage = Random.Range(1, 7);
        int Melee_Damage = Monk.Stats != null ? Monk.Stats.Attack_Damage : 0;

        Debug.Log($"<color=cyan>=== CLOUD DANCE: {Monk.Stats.Model_Name} SLAMS AT ({Destination.x},{Destination.y}) ===</color>");
        Debug.Log($"<color=cyan>Rolled 2d6: Shockwave Radius = {Shock_Range} | Shockwave Damage = {Shock_Damage}</color>");

        var Shock_Victims = new List<Model_Standard_Behavior>();
        for (int X = 0; X < Board.Get_Tile_Count_X(); X++)
            for (int Y = 0; Y < Board.Get_Tile_Count_Y(); Y++)
            {
                if (X == Destination.x && Y == Destination.y) continue;
                var M = Board.Get_Model_At(X, Y);
                if (M == null || !M.gameObject.activeSelf || M.Team == Monk.Team) continue;
                int Dist = Mathf.Abs(X - Destination.x) + Mathf.Abs(Y - Destination.y);
                if (Dist <= Shock_Range) Shock_Victims.Add(M);
            }

        Shock_Victims.Sort((A, B) => (Mathf.Abs(B.Current_X - Destination.x) + Mathf.Abs(B.Current_Y - Destination.y)).CompareTo(Mathf.Abs(A.Current_X - Destination.x) + Mathf.Abs(A.Current_Y - Destination.y)));

        Board.Remove_Model(Start.x, Start.y);
        var engine = Object.FindAnyObjectByType<Movement_Execution_Engine>();
        var combat = Object.FindAnyObjectByType<Combat_Manager>();

        // 1. Shockwave hits first
        foreach (var Victim in Shock_Victims)
        {
            if (Victim == null || !Victim.gameObject.activeSelf) continue;
            int hpBefore = Victim.Current_Health;
            bool Survived = Victim.Take_Damage(Shock_Damage);
            Debug.Log($"<color=orange>[Cloud Dance - Shockwave]</color> {Victim.Stats.Model_Name} takes {Shock_Damage} damage. (HP: {hpBefore} -> {Victim.Current_Health})");
            if (!Survived) { if (combat != null) combat.Kill_Model(Victim); continue; }

            Vector2Int Dir = Radial_Direction(Destination, new Vector2Int(Victim.Current_X, Victim.Current_Y));
            if (Dir != Vector2Int.zero && engine != null) engine.Try_Push_Victim_Damaging(Victim, new Vector2Int(Victim.Current_X, Victim.Current_Y), Dir, Shove_Distance, 0, new HashSet<Model_Standard_Behavior>());
        }

        // 2. Slam target
        if (Slam_Target != null && Slam_Target.gameObject.activeSelf)
        {
            Board.Remove_Model(Destination.x, Destination.y);
            int Total_Damage = Melee_Damage + Shock_Damage;
            int hpBefore = Slam_Target.Current_Health;
            bool Survived = Slam_Target.Take_Damage(Total_Damage);
            Debug.Log($"<color=red>[Cloud Dance - Direct Slam]</color> {Slam_Target.Stats.Model_Name} takes {Total_Damage} total damage (Melee {Melee_Damage} + Shock {Shock_Damage}). (HP: {hpBefore} -> {Slam_Target.Current_Health})");

            if (!Survived) { if (combat != null) combat.Kill_Model(Slam_Target); }
            else
            {
                Vector2Int randDir = Card_Dirs[Random.Range(0, 4)];
                if (engine != null) engine.Try_Push_Victim_Damaging(Slam_Target, new Vector2Int(Slam_Target.Current_X, Slam_Target.Current_Y), randDir, Shove_Distance, 0, new HashSet<Model_Standard_Behavior>());
            }
        }

        // 3. Place Monk
        Board.Place_Model_At(Destination.x, Destination.y, Monk);
        Monk.Current_X = Destination.x;
        Monk.Current_Y = Destination.y;
        Monk.Set_Position(Board.Get_Unit_Stand_Position(Destination.x, Destination.y), false);

        Monk.Has_Attacked_This_Turn = true;
        Monk.Has_Ended_Turn = true;
        Monk.Ability_Recharge_Remaining = Mathf.Max(Recharge_Turns, 1);

        var interaction = Object.FindAnyObjectByType<Gameplay_Interaction_Handler>();
        if (interaction != null) interaction.Select_Model_Public(Monk);

        Debug.Log($"<color=cyan>=== CLOUD DANCE RESOLVED ===</color>");
    }

    private bool Is_Valid_Destination(Battle_Board_Behavior Board, Vector2Int Dest, int Monk_Team)
    {
        if (Dest.x < 0 || Dest.x >= Board.Get_Tile_Count_X() || Dest.y < 0 || Dest.y >= Board.Get_Tile_Count_Y()) return false;
        if (!Board.Is_Tile_Passable(Dest.x, Dest.y)) return false;
        var Occ = Board.Get_Model_At(Dest.x, Dest.y);
        if (Occ == null) return true;
        if (Occ.Team == Monk_Team) return false;
        return Has_Adjacent_Passable_Tile(Board, Dest);
    }

    private static bool Has_Adjacent_Passable_Tile(Battle_Board_Behavior Board, Vector2Int Pos)
    {
        foreach (var Dir in Card_Dirs)
        {
            Vector2Int Next = Pos + Dir;
            if (Next.x < 0 || Next.x >= Board.Get_Tile_Count_X() || Next.y < 0 || Next.y >= Board.Get_Tile_Count_Y()) continue;
            if (Board.Is_Tile_Passable(Next.x, Next.y)) return true;
        }
        return false;
    }

    private static Vector2Int Radial_Direction(Vector2Int From, Vector2Int To)
    {
        int dx = To.x - From.x;
        int dy = To.y - From.y;
        if (dx == 0 && dy == 0) return Vector2Int.zero;
        if (Mathf.Abs(dx) >= Mathf.Abs(dy)) return new Vector2Int(dx > 0 ? 1 : -1, 0);
        return new Vector2Int(0, dy > 0 ? 1 : -1);
    }
}