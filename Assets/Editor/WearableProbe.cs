using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WearableProbe
{
    [MenuItem("Tools/Wearable Probe/Probe Sources")]
    public static void Probe()
    {
        Debug.Log(ProbeData());
    }

    public static string ProbeData()
    {
        var outL = new List<string>();
        ProbeMask(outL);
        ProbeSource(outL, "cool_shades_sunglasses", "Plane_Material", null, null);
        ProbeSource(outL, "t-shirts_homme", "", "Obj3d66-516286-2-747_0", new[] { "Object_7", "Object_8" });
        ProbeSource(outL, "hangers_and_pants", "", null, new[] { "Object_4", "Object_5" });
        return string.Join("\n", outL);
    }

    static void ProbeMask(List<string> outL)
    {
        Transform root = FindRoot("mask");
        if (root == null) { outL.Add("=== mask NOT FOUND"); return; }
        outL.Add("=== mask rootWorld=" + root.position.ToString("F3") + " euler=" + root.eulerAngles.ToString("F1") + " lossy=" + root.lossyScale.ToString("F3"));
        var kept = new List<Transform>();
        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            if (IsInChain(mr.transform, "Sketchfab_model.001")) kept.Add(mr.transform);
        }
        PrintCanonical(outL, "mask", root, kept);
    }

    static void ProbeSource(List<string> outL, string name, string keepMeshPrefix, string parentToken, string[] keepNames)
    {
        Transform root = FindRoot(name);
        if (root == null) { outL.Add("=== " + name + " NOT FOUND"); return; }
        outL.Add("=== " + name + " rootWorld=" + root.position.ToString("F3") + " euler=" + root.eulerAngles.ToString("F1") + " lossy=" + root.lossyScale.ToString("F3"));
        var kept = new List<Transform>();
        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            bool keep = string.IsNullOrEmpty(keepMeshPrefix) || mf.sharedMesh.name.StartsWith(keepMeshPrefix);
            bool inChain = string.IsNullOrEmpty(parentToken) || (mr.transform.parent != null && mr.transform.parent.name == parentToken);
            bool named = keepNames == null || System.Array.Exists(keepNames, nn => mr.name == nn);
            if (keep && inChain && named) kept.Add(mr.transform);
        }
        PrintCanonical(outL, name, root, kept);
    }

    static Transform FindRoot(string name)
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            if (go.name == name && go.transform.parent == null) return go.transform;
        return null;
    }

    static bool IsInChain(Transform t, string token)
    {
        while (t != null) { if (t.name == token) return true; t = t.parent; }
        return false;
    }

    // Compute geometry in a frame that removes the root's ROTATION but keeps total scale.
    static void PrintCanonical(List<string> outL, string label, Transform root, List<Transform> leaves)
    {
        Vector3 min = new Vector3(1e9f, 1e9f, 1e9f), max = new Vector3(-1e9f, -1e9f, -1e9f);
        foreach (var leaf in leaves)
        {
            Matrix4x4 world = leaf.localToWorldMatrix;
            Matrix4x4 invRootRot = Matrix4x4.TRS(Vector3.zero, Quaternion.Inverse(root.rotation), Vector3.one);
            Matrix4x4 rel = invRootRot * world;
            Vector3 pos = rel.GetPosition();
            Vector3 right = rel.MultiplyVector(Vector3.right).normalized;
            Vector3 up = rel.MultiplyVector(Vector3.up).normalized;
            Vector3 fwd = rel.MultiplyVector(Vector3.forward).normalized;
            var mr = leaf.GetComponent<MeshRenderer>();
            var mf = mr.GetComponent<MeshFilter>();
            var b = mf.sharedMesh.bounds;
            Vector3[] corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = b.center + new Vector3((i & 1) == 0 ? -b.extents.x : b.extents.x,
                                                   (i & 2) == 0 ? -b.extents.y : b.extents.y,
                                                   (i & 4) == 0 ? -b.extents.z : b.extents.z);
                corners[i] = rel.MultiplyPoint(c);
            }
            foreach (var c in corners) { min = Vector3.Min(min, c); max = Vector3.Max(max, c); }
            outL.Add("  leaf " + leaf.name + " canonPos=" + pos.ToString("F3") + " r=" + right.ToString("F2") + " u=" + up.ToString("F2") + " f=" + fwd.ToString("F2"));
        }
        if (leaves.Count > 0)
            outL.Add("  CANON " + label + " center=" + ((min + max) * 0.5f).ToString("F3") + " size=" + (max - min).ToString("F3"));
        else outL.Add("  CANON " + label + " kept=0");
    }
}