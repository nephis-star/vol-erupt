using UnityEngine;
using UnityEditor;
using System.IO;

public class KiloCapsuleCheck
{
    public static void CheckCapsule()
    {
        string path = "C:/Users/windows-11/vol erupt/kilo_capsule.txt";
        using (StreamWriter writer = new StreamWriter(path, false))
        {
            var player = GameObject.Find("First Person Controller");
            if (player != null)
            {
                var cc = player.GetComponent<CapsuleCollider>();
                if (cc != null)
                {
                    writer.WriteLine("CapsuleCollider on: " + cc.gameObject.name);
                    writer.WriteLine("center: " + cc.center);
                    writer.WriteLine("height: " + cc.height);
                    writer.WriteLine("radius: " + cc.radius);
                    writer.WriteLine("isTrigger: " + cc.isTrigger);
                    writer.WriteLine("bounds: " + cc.bounds);
                    writer.WriteLine("world bounds min: " + cc.bounds.min);
                    writer.WriteLine("world bounds max: " + cc.bounds.max);
                    writer.WriteLine("Player transform pos: " + player.transform.position);
                    writer.WriteLine("Player transform local pos: " + player.transform.localPosition);
                }
                else
                {
                    writer.WriteLine("No CapsuleCollider on First Person Controller");
                }
            }
        }
        Debug.Log("Capsule check written to " + path);
    }
}
