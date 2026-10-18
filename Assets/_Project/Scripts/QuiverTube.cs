using UnityEngine;

/// <summary>
/// Builds the hollow quiver tube from three numbers: open at the top, thin walls, origin on the ground at the centre.
/// Change Height / Inner Radius / Wall Thickness in the Inspector and the mesh and collider rebuild right away.
/// (Always scale this object 1,1,1 - change the numbers instead.)
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class QuiverTube : MonoBehaviour
{
    [SerializeField] float height = 0.6f;
    [SerializeField] float innerRadius = 0.045f;      // 9 cm inside diameter
    [SerializeField] float wallThickness = 0.012f;
    [SerializeField, Range(12, 64)] int segments = 32;

    public float Height => height;
    public float InnerRadius => innerRadius;
    public float OuterRadius => innerRadius + wallThickness;

    Mesh mesh;

    void OnEnable() => Rebuild();

    void OnValidate()
    {
        height = Mathf.Max(0.1f, height);
        innerRadius = Mathf.Max(0.02f, innerRadius);
        wallThickness = Mathf.Max(0.002f, wallThickness);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Rebuild(); };   // not allowed to change components inside OnValidate
#endif
    }

    void OnDestroy()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
    }

    public void Rebuild()
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "Quiver Tube (generated)" };
            mesh.hideFlags = HideFlags.HideAndDontSave;   // always rebuilt from the numbers above, never saved
        }
        mesh.Clear();

        float ri = innerRadius, ro = innerRadius + wallThickness, h = height;
        int n = Mathf.Max(12, segments);
        var v = new System.Collections.Generic.List<Vector3>();
        var nm = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>();
        var t = new System.Collections.Generic.List<int>();

        AddWall(v, nm, uv, t, n, ro, h, true);    // outside
        AddWall(v, nm, uv, t, n, ri, h, false);   // inside
        int b = v.Count;                          // rim
        for (int i = 0; i <= n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(d * ro + Vector3.up * h); nm.Add(Vector3.up); uv.Add(new Vector2((float)i / n, 0f));
            v.Add(d * ri + Vector3.up * h); nm.Add(Vector3.up); uv.Add(new Vector2((float)i / n, 1f));
        }
        for (int i = 0; i < n; i++)
        {
            int a0 = b + i * 2;
            t.Add(a0); t.Add(a0 + 1); t.Add(a0 + 2);
            t.Add(a0 + 2); t.Add(a0 + 1); t.Add(a0 + 3);
        }

        mesh.SetVertices(v);
        mesh.SetNormals(nm);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
        var col = GetComponent<MeshCollider>();
        if (col == null) col = gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = null;
        col.sharedMesh = mesh;
    }

    static void AddWall(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector3> nm,
                        System.Collections.Generic.List<Vector2> uv, System.Collections.Generic.List<int> t,
                        int n, float r, float h, bool outward)
    {
        int b = v.Count;
        for (int i = 0; i <= n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var normal = outward ? d : -d;
            v.Add(d * r); nm.Add(normal); uv.Add(new Vector2((float)i / n, 0f));
            v.Add(d * r + Vector3.up * h); nm.Add(normal); uv.Add(new Vector2((float)i / n, 1f));
        }
        for (int i = 0; i < n; i++)
        {
            int a0 = b + i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
            if (outward) { t.Add(a0); t.Add(a1); t.Add(b0); t.Add(b0); t.Add(a1); t.Add(b1); }
            else         { t.Add(a0); t.Add(b0); t.Add(a1); t.Add(b0); t.Add(b1); t.Add(a1); }
        }
    }
}
