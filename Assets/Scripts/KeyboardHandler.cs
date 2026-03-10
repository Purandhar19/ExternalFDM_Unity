// ============================================================
//  KeyboardHandler.cs
//  Runtime keyboard shortcuts for the simulation.
// ============================================================
using UnityEngine;
using System.Reflection;

public class KeyboardHandler : MonoBehaviour
{
    public ArduPilotBridge bridge;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            // Trigger FDM reset via reflection (ResetFDM is private)
            if (bridge != null)
            {
                var method = bridge.GetType().GetMethod("ResetFDM",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                method?.Invoke(bridge, null);
                Debug.Log("[Keyboard] FDM reset triggered by SPACE");
            }
        }
    }
}
