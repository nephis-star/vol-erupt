using UnityEngine;
using UnityEditor;
using System.IO;

public class KiloFinalFix
{
    public static void RunFixes()
    {
        string path = "C:/Users/windows-11/vol erupt/kilo_final_fix.txt";
        using (StreamWriter writer = new StreamWriter(path, false))
        {
            writer.WriteLine("=== FINAL FIXES ===\n");
            
            // 1. Remove duplicate AudioListener from Main Camera
            writer.WriteLine("--- FIXING AUDIO LISTENERS ---");
            var mainCamera = GameObject.Find("Main Camera");
            if (mainCamera != null)
            {
                var al = mainCamera.GetComponent<AudioListener>();
                if (al != null)
                {
                    Object.DestroyImmediate(al);
                    writer.WriteLine("Removed AudioListener from Main Camera");
                }
                else
                {
                    writer.WriteLine("Main Camera has no AudioListener");
                }
            }
            
            // Count remaining AudioListeners
            var allListeners = GameObject.FindObjectsOfType<AudioListener>(true);
            writer.WriteLine("Remaining AudioListeners: " + allListeners.Length);
            foreach (var listener in allListeners)
            {
                writer.WriteLine("  " + listener.gameObject.name);
            }
            
            // 2. Fix GroundCheck distanceThreshold
            writer.WriteLine("\n--- FIXING GROUND CHECK ---");
            var player = GameObject.Find("First Person Controller");
            if (player != null)
            {
                var groundCheck = player.GetComponentInChildren<GroundCheck>();
                if (groundCheck != null)
                {
                    groundCheck.distanceThreshold = 0.5f;
                    writer.WriteLine("GroundCheck distanceThreshold set to 0.5f");
                }
            }
            
            // 3. Verify player spawn
            if (player != null)
            {
                writer.WriteLine("Player position: " + player.transform.position);
                var rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    writer.WriteLine("Player Rigidbody velocity reset");
                }
            }
            
            // 4. Save scene
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            writer.WriteLine("\nScene saved!");
        }
        Debug.Log("Final fix log written to " + path);
    }
}
