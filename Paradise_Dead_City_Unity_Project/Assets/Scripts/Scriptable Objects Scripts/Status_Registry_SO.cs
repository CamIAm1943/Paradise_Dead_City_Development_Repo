using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Status Registry", menuName = "Scriptable Objects/Status Registry")]
public class Status_Registry_SO : ScriptableObject
{
    [Tooltip("One entry per Status_Type. Any type not present here will " +
             "render with a gray fallback dot.")]
    [SerializeField] private Status_Definition_SO[] Definitions;

    private Dictionary<Status_Type, Status_Definition_SO> Cache;

    public Status_Definition_SO Get(Status_Type Type)
    {
        if (Cache == null)
        {
            Cache = new Dictionary<Status_Type, Status_Definition_SO>();
            if (Definitions != null)
            {
                foreach (var D in Definitions)
                {
                    if (D != null)
                        Cache[D.Type] = D;
                }
            }
        }

        Cache.TryGetValue(Type, out var Result);
        return Result;
    }
}