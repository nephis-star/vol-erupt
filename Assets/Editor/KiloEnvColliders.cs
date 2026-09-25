using UnityEngine;
using UnityEditor;
using System.IO;

public class KiloEnvColliders
{
    public static void CheckEnvColliders()
    {
        string path = "C:/Users/windows-11/vol erupt/kilo_env.txt";
        using (StreamWriter writer = new StreamWriter(path, false))
        {
            writer.WriteLine("=== ENVIRONMENT OBJECTS WITH COLLIDERS ===");
            string[] envObjects = {
                "houses", "houses.002", "houses.003",
                "house", "house6", "house7", "house3",
                "R_BASE_0", "R_01_0", "L_BASE_0", "L_01_0",
                "BASE_BASE_0", "BASE_01_0",
                "RootNode.001", "87ccdf1ecf23405ab49d04bc0512680c.fbx",
                "Rocks_Rocks_0", "Rocks.001_Rocks_0", "Rocks.002_Rocks_0",
                "Rocks.003_Rocks_0", "Rocks.004_Rocks_0", "Rocks.005_Rocks_0",
                "Rocks.006_Rocks_0", "Rocks.007_Rocks_0", "Rocks.008_Rocks_0",
                "Tree_Trunk_01_Tree_Trunk_01_0",
                "Tree_Trunk_01.001_Tree_Trunk_01_0",
                "Tree_Trunk_01.002_Tree_Trunk_01_0",
                "Tree_Trunk_02_Tree_Trunk_02_0",
                "Plane"
            };
            
            foreach (string name in envObjects)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    var colliders = obj.GetComponents<Collider>();
                    var childColliders = obj.GetComponentsInChildren<Collider>(true);
                    
                    writer.WriteLine("");
                    writer.WriteLine(name + ":");
                    writer.WriteLine("  pos: " + obj.transform.position);
                    writer.WriteLine("  localScale: " + obj.transform.localScale);
                    writer.WriteLine("  own colliders: " + colliders.Length);
                    foreach (var c in colliders)
                    {
                        writer.WriteLine("    " + c.GetType().Name + " center=" + (c is BoxCollider bc ? bc.center.ToString() : "N/A") + " size=" + (c is BoxCollider bc2 ? bc2.size.ToString() : "N/A"));
                    }
                    writer.WriteLine("  total colliders (incl children): " + childColliders.Length);
                }
            }
        }
        Debug.Log("Env colliders written to " + path);
    }
}
