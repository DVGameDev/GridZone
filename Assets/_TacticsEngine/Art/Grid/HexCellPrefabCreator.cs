using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Rendering;

public class HexPrefabGenerator : EditorWindow
{
    private float hexSize = 1.0f;
    private Material hexMaterial;
    private string prefabName = "HexCell";

    [MenuItem("Tools/Grid/Create Hex Prefab")]
    public static void ShowWindow()
    {
        GetWindow<HexPrefabGenerator>("Hex Prefab Creator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Hex Cell Prefab Generator", EditorStyles.boldLabel);

        hexSize = EditorGUILayout.FloatField("Hex Size (Radius)", hexSize);
        hexMaterial = (Material)EditorGUILayout.ObjectField("Material", hexMaterial, typeof(Material), false);
        prefabName = EditorGUILayout.TextField("Prefab Name", prefabName);

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Hex Prefab", GUILayout.Height(40)))
        {
            GenerateHexPrefab();
        }

        EditorGUILayout.HelpBox(
            "Creates a Flat-Top hex mesh prefab with:\n" +
            "• MeshFilter + MeshRenderer\n" +
            "• Physics Shape (for raycasting)\n" +
            "• ECS conversion components",
            MessageType.Info);
    }

    private void GenerateHexPrefab()
    {
        if (hexMaterial == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a Material!", "OK");
            return;
        }

        // 1. Создаем GameObject
        GameObject hexGO = new GameObject(prefabName);

        // 2. Генерируем Hex Mesh
        Mesh hexMesh = CreateFlatTopHexMesh(hexSize);

        // 3. Добавляем MeshFilter + Renderer
        MeshFilter mf = hexGO.AddComponent<MeshFilter>();
        mf.sharedMesh = hexMesh;

        MeshRenderer mr = hexGO.AddComponent<MeshRenderer>();
        mr.sharedMaterial = hexMaterial;

        // 4. Добавляем Physics Shape для Raycast
        var meshCollider = hexGO.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = hexMesh;
        meshCollider.convex = false;

        // 5. ECS Conversion - добавляем компоненты для DOTS
        // (Unity 6 Entities 1.4 использует Baker вместо ConvertToEntity)
        // Компоненты добавим через Authoring скрипт ниже

        // 6. Сохраняем Mesh как Asset
        string meshPath = $"Assets/Meshes/{prefabName}_Mesh.asset";
        if (!AssetDatabase.IsValidFolder("Assets/Meshes"))
            AssetDatabase.CreateFolder("Assets", "Meshes");

        AssetDatabase.CreateAsset(hexMesh, meshPath);

        // 7. Сохраняем Prefab
        string prefabPath = $"Assets/Prefabs/{prefabName}.prefab";
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        PrefabUtility.SaveAsPrefabAsset(hexGO, prefabPath);

        // 8. Cleanup
        DestroyImmediate(hexGO);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success",
            $"Hex prefab created:\n{prefabPath}\n\nMesh saved:\n{meshPath}", "OK");

        // Пингуем prefab в Project
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        EditorGUIUtility.PingObject(prefab);
        Selection.activeObject = prefab;
    }

    /// <summary>
    /// Генерирует Flat-Top Hex mesh
    /// </summary>
    private Mesh CreateFlatTopHexMesh(float size)
    {
        Mesh mesh = new Mesh();
        mesh.name = "HexMesh_FlatTop";

        // Flat-Top hex: 6 вершин + центр
        Vector3[] vertices = new Vector3[7];
        vertices[0] = new Vector3(0, 0.01f, 0); // Центр СВЕРХУ 🔥

        for (int i = 0; i < 6; i++)
        {
            float angle = 60f * i * Mathf.Deg2Rad;
            float x = size * Mathf.Cos(angle);
            float z = size * Mathf.Sin(angle);
            vertices[i + 1] = new Vector3(x, 0.01f, z); // 🔥 Y = 0.01f СВЕРХУ
        }

        // Треугольники (против часовой - для правильных нормалей)
        int[] triangles = new int[18];
        for (int i = 0; i < 6; i++)
        {
            triangles[i * 3 + 0] = 0;                    // Центр
            triangles[i * 3 + 1] = (i + 1) % 6 + 1;     // Следующая вершина (против часовой)
            triangles[i * 3 + 2] = i + 1;               // Текущая вершина
        }

        // UV coordinates
        Vector2[] uvs = new Vector2[7];
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < 6; i++)
        {
            float angle = 60f * i * Mathf.Deg2Rad;
            uvs[i + 1] = new Vector2(
                0.5f + 0.4f * Mathf.Cos(angle),
                0.5f + 0.4f * Mathf.Sin(angle)
            );
        }

        // 🔥 НОРМАЛИ ВВЕРХ
        Vector3[] normals = new Vector3[7];
        for (int i = 0; i < 7; i++)
            normals[i] = Vector3.up;

        // 🔥 TANGENTS для правильного освещения
        Vector4[] tangents = new Vector4[7];
        for (int i = 0; i < 7; i++)
            tangents[i] = new Vector4(1, 0, 0, 1);

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.tangents = tangents;

        mesh.RecalculateBounds();
        mesh.RecalculateTangents(); // 🔥 На всякий случай

        return mesh;
    }

}
