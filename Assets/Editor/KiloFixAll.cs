using UnityEngine;
using UnityEditor;
using System.IO;

public class KiloFixAll
{
    public static void FixAll()
    {
        string path = "C:/Users/windows-11/vol erupt/kilo_fix_log.txt";
        using (StreamWriter writer = new StreamWriter(path, false))
        {
            writer.WriteLine("=== FIXING ALL ISSUES ===\n");
            
            // 1. Fix Ground Plane
            writer.WriteLine("--- FIXING GROUND PLANE ---");
            GameObject plane = GameObject.Find("Plane");
            if (plane != null)
            {
                var rend = plane.GetComponent<MeshRenderer>();
                if (rend != null)
                {
                    rend.enabled = true;
                    writer.WriteLine("Plane MeshRenderer enabled: " + rend.enabled);
                }
                
                var pbc = plane.GetComponent<BoxCollider>();
                if (pbc != null)
                {
                    writer.WriteLine("Plane BoxCollider exists: true");
                    writer.WriteLine("Plane BoxCollider center: " + pbc.center);
                    writer.WriteLine("Plane BoxCollider size: " + pbc.size);
                }
                
                writer.WriteLine("Plane position: " + plane.transform.position);
                writer.WriteLine("Plane localScale: " + plane.transform.localScale);
            }
            
            // 2. Fix environment colliders - remove oversized ones
            writer.WriteLine("\n--- FIXING ENVIRONMENT COLLIDERS ---");
            string[] objectsToFix = {
                "houses", "houses.002", "houses.003",
                "house", "house6", "house7", "house3",
                "R_BASE_0", "R_01_0", "L_BASE_0", "L_01_0",
                "BASE_BASE_0", "BASE_01_0",
                "RootNode.001", "87ccdf1ecf23405ab49d04bc0512680c.fbx",
                "Rocks_Rocks_0", "Rocks.001_Rocks_0", "Rocks.002_Rocks_0",
                "Rocks.003_Rocks_0", "Rocks.004_Rocks_0", "Rocks.005_Rocks_0",
                "Rocks.006_Rocks_0", "Rocks.007_Rocks_0", "Rocks.008_Rocks_0",
                "Rocks.009_Rocks_0", "Rocks.010_Rocks_0", "Rocks.011_Rocks_0",
                "Rocks.012_Rocks_0", "Rocks.013_Rocks_0", "Rocks.014_Rocks_0",
                "Rocks.015_Rocks_0", "Rocks.016_Rocks_0", "Rocks.017_Rocks_0",
                "Tree_Trunk_01_Tree_Trunk_01_0",
                "Tree_Trunk_01.001_Tree_Trunk_01_0",
                "Tree_Trunk_01.002_Tree_Trunk_01_0",
                "Tree_Trunk_02_Tree_Trunk_02_0"
            };
            
            foreach (string name in objectsToFix)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    var bc = obj.GetComponent<BoxCollider>();
                    if (bc != null)
                    {
                        var mf = obj.GetComponent<MeshFilter>();
                        if (mf != null && mf.mesh != null)
                        {
                            var bounds = mf.mesh.bounds;
                            bc.size = bounds.size;
                            bc.center = bounds.center;
                            writer.WriteLine("Fixed BoxCollider on " + name + ": size=" + bounds.size + ", center=" + bounds.center);
                        }
                        else
                        {
                            writer.WriteLine("No MeshFilter on " + name + ", removing BoxCollider");
                            Object.DestroyImmediate(bc);
                        }
                    }
                }
            }
            
            // 3. Fix player spawn
            writer.WriteLine("\n--- FIXING PLAYER SPAWN ---");
            var player = GameObject.Find("First Person Controller");
            if (player != null)
            {
                // Spawn player above ground so they fall onto it
                player.transform.position = new Vector3(0.44f, 5.0f, 20.41f);
                writer.WriteLine("Player spawned at: " + player.transform.position);
                
                // Reset rigidbody velocity
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
            writer.WriteLine("\nScene saved successfully!");
        }
        Debug.Log("Fix log written to " + path);
    }
}
