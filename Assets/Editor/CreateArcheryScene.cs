#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;                 // XRInteractionManager
using UnityEngine.XR.Interaction.Toolkit.Interactables;   // XRGrabInteractable
using UnityEngine.XR.Interaction.Toolkit.Interactors;     // XRSocketInteractor

// Put this file in Assets/Editor/, then run: Archery > Create Blank Scene
public static class CreateArcheryScene
{
    // Optional: drag your imported prefabs' asset paths in here to use them instead of placeholders.
    const string RiserPrefabPath = "";   // e.g. "Assets/Models/Riser.prefab"
    const string LimbPrefabPath  = "";   // e.g. "Assets/Models/Limb.prefab"

    [MenuItem("Archery/Create Blank Scene")]
    static void Create()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lighting
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Floor
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(2, 1, 2);

        // Assembly table so parts have somewhere to rest
        var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "Assembly Table";
        table.transform.position = new Vector3(0, 0.45f, 1f);
        table.transform.localScale = new Vector3(1.6f, 0.9f, 0.8f);

        // Player: XR Origin (VR) via the toolkit's own menu item
        EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (VR)");
        if (Object.FindFirstObjectByType<XRInteractionManager>() == null)
            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();

        // Riser
        GameObject riser = MakePart("Riser", RiserPrefabPath, new Vector3(-0.15f, 1.0f, 1f),
                                    new Vector3(0.04f, 0.45f, 0.06f), new Color(0.35f, 0.2f, 0.1f));

        // Socket on the riser where the limb attaches (top end)
        var socketGO = new GameObject("Limb Socket");
        socketGO.transform.SetParent(riser.transform, false);
        socketGO.transform.localPosition = new Vector3(0, 0.25f, 0);
        var trigger = socketGO.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.06f;
        socketGO.AddComponent<XRSocketInteractor>();

        // Limb
        MakePart("Limb", LimbPrefabPath, new Vector3(0.15f, 1.0f, 1f),
                 new Vector3(0.03f, 0.6f, 0.04f), new Color(0.15f, 0.15f, 0.15f));

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ArcheryVR.unity");
        Debug.Log("Archery scene created: Assets/Scenes/ArcheryVR.unity");
    }

    static GameObject MakePart(string name, string prefabPath, Vector3 pos, Vector3 size, Color color)
    {
        GameObject root = new GameObject(name);
        root.transform.position = pos;
        root.AddComponent<Rigidbody>();

        GameObject prefab = string.IsNullOrEmpty(prefabPath)
            ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab != null)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.transform.SetParent(root.transform, false);
        }
        else
        {
            // Placeholder mesh until you swap in your imported model
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Placeholder Mesh";
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localScale = size;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard")) { color = color };
            mesh.GetComponent<Renderer>().sharedMaterial = mat;
        }

        root.AddComponent<XRGrabInteractable>();
        return root;
    }
}
#endif
