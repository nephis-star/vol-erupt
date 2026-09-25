using UnityEngine;
using UnityEditor;
using System.IO;

public class KiloPlayerDiagnostic
{
    public static void RunDiagnostics()
    {
        string path = "C:/Users/windows-11/vol erupt/kilo_player_diag.txt";
        using (StreamWriter writer = new StreamWriter(path, false))
        {
            var player = GameObject.Find("First Person Controller");
            if (player != null)
            {
                writer.WriteLine("=== PLAYER HIERARCHY ===");
                writer.WriteLine("First Person Controller pos: " + player.transform.position);
                
                writer.WriteLine("");
                writer.WriteLine("=== ALL COMPONENTS ON PLAYER ===");
                var components = player.GetComponents<Component>();
                foreach (var comp in components)
                {
                    writer.WriteLine("  " + comp.GetType().Name);
                }
                
                writer.WriteLine("");
                writer.WriteLine("=== ALL CHILDREN ===");
                foreach (Transform child in player.transform)
                {
                    writer.WriteLine("Child: " + child.name + " | pos=" + child.localPosition + " | worldPos=" + child.position);
                    var childComps = child.GetComponents<Component>();
                    foreach (var comp in childComps)
                    {
                        writer.WriteLine("  " + comp.GetType().Name);
                    }
                    
                    var cc = child.GetComponent<CapsuleCollider>();
                    if (cc != null)
                    {
                        writer.WriteLine("  CapsuleCollider center: " + cc.center + ", height: " + cc.height + ", radius: " + cc.radius);
                        writer.WriteLine("  CapsuleCollider bounds: " + cc.bounds);
                    }
                }
                
                writer.WriteLine("");
                writer.WriteLine("=== RIGIDBODY ===");
                var rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    writer.WriteLine("Rigidbody useGravity: " + rb.useGravity);
                    writer.WriteLine("Rigidbody isKinematic: " + rb.isKinematic);
                    writer.WriteLine("Rigidbody mass: " + rb.mass);
                    writer.WriteLine("Rigidbody velocity: " + rb.linearVelocity);
                }
            }
        }
        Debug.Log("Player diagnostic written to " + path);
    }
}
