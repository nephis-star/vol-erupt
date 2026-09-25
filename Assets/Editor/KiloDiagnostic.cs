using UnityEngine;
using UnityEditor;
using System.IO;

public class KiloDiagnostic
{
    public static void RunDiagnostics()
    {
        string path = "C:/Users/windows-11/vol erupt/kilo_diag.txt";
        using (StreamWriter writer = new StreamWriter(path, false))
        {
            writer.WriteLine("=== AUDIO LISTENERS ===");
            var listeners = GameObject.FindObjectsOfType<AudioListener>(true);
            writer.WriteLine("Total AudioListeners: " + listeners.Length);
            foreach (var al in listeners)
            {
                writer.WriteLine("  " + al.gameObject.name + " | enabled=" + al.enabled + " | active=" + al.gameObject.activeInHierarchy);
            }
            
            writer.WriteLine("");
            writer.WriteLine("=== CAMERAS ===");
            var cameras = GameObject.FindObjectsOfType<Camera>(true);
            writer.WriteLine("Total Cameras: " + cameras.Length);
            foreach (var cam in cameras)
            {
                var al = cam.GetComponent<AudioListener>();
                writer.WriteLine("  " + cam.gameObject.name + " | enabled=" + cam.enabled + " | AudioListener=" + (al != null ? "yes" : "no"));
            }
            
            writer.WriteLine("");
            writer.WriteLine("=== PLAYER STATE ===");
            var player = GameObject.Find("First Person Controller");
            if (player != null)
            {
                var rb = player.GetComponent<Rigidbody>();
                var cc = player.GetComponent<CapsuleCollider>();
                writer.WriteLine("Player pos: " + player.transform.position);
                writer.WriteLine("Rigidbody velocity: " + (rb != null ? rb.linearVelocity.ToString() : "null"));
                writer.WriteLine("Rigidbody useGravity: " + (rb != null ? rb.useGravity.ToString() : "null"));
                if (cc != null)
                {
                    writer.WriteLine("Capsule bounds min: " + cc.bounds.min);
                    writer.WriteLine("Capsule bounds max: " + cc.bounds.max);
                }
            }
            
            writer.WriteLine("");
            writer.WriteLine("=== GROUND CHECK ===");
            if (player != null)
            {
                var groundCheck = player.GetComponentInChildren<GroundCheck>();
                if (groundCheck != null)
                {
                    writer.WriteLine("GroundCheck distanceThreshold: " + groundCheck.distanceThreshold);
                    writer.WriteLine("GroundCheck isGrounded: " + groundCheck.isGrounded);
                    writer.WriteLine("GroundCheck position: " + groundCheck.transform.position);
                }
                
                // Raycast down from player
                Vector3 origin = player.transform.position + Vector3.down * 1.0f;
                RaycastHit hit;
                bool hitSomething = Physics.Raycast(origin, Vector3.down, out hit, 20f);
                writer.WriteLine("Raycast from player down: " + hitSomething);
                if (hitSomething)
                {
                    writer.WriteLine("  Hit: " + hit.collider.gameObject.name + " at " + hit.point + " dist=" + hit.distance);
                }
            }
            
            writer.WriteLine("");
            writer.WriteLine("=== PLANE STATE ===");
            var plane = GameObject.Find("Plane");
            if (plane != null)
            {
                var rend = plane.GetComponent<MeshRenderer>();
                var pbc = plane.GetComponent<BoxCollider>();
                writer.WriteLine("Plane MeshRenderer enabled: " + (rend != null ? rend.enabled.ToString() : "null"));
                writer.WriteLine("Plane BoxCollider exists: " + (pbc != null ? "yes" : "no"));
                if (pbc != null)
                {
                    writer.WriteLine("Plane BoxCollider center: " + pbc.center);
                    writer.WriteLine("Plane BoxCollider size: " + pbc.size);
                    writer.WriteLine("Plane BoxCollider bounds min: " + pbc.bounds.min);
                    writer.WriteLine("Plane BoxCollider bounds max: " + pbc.bounds.max);
                }
            }
        }
        Debug.Log("Diagnostic written to " + path);
    }
}
