using System.Diagnostics;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

public static class GameLogger
{
    [Conditional(conditionString: "ENABLE_LOG")]
    public static void Log(object message)
    {
        Debug.Log(message);
    }

    public static void Logwarning(object message)
    {
        Debug.LogWarning(message);
    }

    public static void Logerror(object message)
    {
        Debug.LogError(message);
    }
}