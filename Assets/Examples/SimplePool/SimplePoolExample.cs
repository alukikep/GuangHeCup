using System.Collections.Generic;
using ModuleFactory.SimplePooling;
using UnityEngine;

/// <summary>Add to an empty GameObject in a scene with a camera. No prefabs or other modules required.</summary>
public sealed class SimplePoolExample : MonoBehaviour
{
    private SimplePool pool;
    private GameObject template;
    private Mesh mesh;
    private Material material;
    private readonly List<GameObject> moving = new List<GameObject>();
    private void Start()
    {
        pool = gameObject.AddComponent<SimplePool>();
        template = new GameObject("Demo bullet template"); template.SetActive(false);
        mesh = new Mesh();
        mesh.vertices = new[] { new Vector3(-0.1f, -0.1f), new Vector3(0.1f, -0.1f), new Vector3(0.1f, 0.1f), new Vector3(-0.1f, 0.1f) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        material = new Material(Shader.Find("Unlit/Color")) { color = Color.cyan };
        template.AddComponent<MeshFilter>().sharedMesh = mesh; template.AddComponent<MeshRenderer>().sharedMaterial = material;
        pool.Configure(template, 32, 8);
    }
    private void Fire()
    {
        var bullet = pool.Spawn(template, new Vector3(-3, Random.Range(-2f, 2f), 0));
        if (bullet == null) return;
        if (!moving.Contains(bullet)) moving.Add(bullet);
        pool.DespawnAfter(bullet, 2);
    }
    private void Update()
    {
        for (int i = moving.Count - 1; i >= 0; i--)
            if (moving[i] == null || !moving[i].activeSelf) moving.RemoveAt(i);
            else moving[i].transform.position += Vector3.right * (3 * Time.deltaTime);
    }
    private void OnGUI()
    {
        if (pool == null) return;
        GUILayout.BeginArea(new Rect(20, 20, 300, 180), GUI.skin.box);
        GUILayout.Label("Simple Pool: max 32 / return after 2s");
        if (GUILayout.Button("Spawn")) Fire();
        if (GUILayout.Button("Burst 100")) for (int i = 0; i < 100; i++) Fire();
        if (GUILayout.Button("Clear")) { pool.ClearAll(); moving.Clear(); }
        GUILayout.Label($"Total: {pool.CountAll(template)}   In use: {pool.CountActive(template)}");
        GUILayout.EndArea();
    }
    private void OnDestroy()
    {
        if (pool != null) pool.ClearAll();
        Destroy(template); Destroy(mesh); Destroy(material);
    }
}
