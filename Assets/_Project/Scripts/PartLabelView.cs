using TMPro;
using UnityEngine;

/// <summary>
/// The look of a part's name tag: a ring on the part, a line leading away from it, and a black tag with the name on it.
/// This sits on the PartLabel prefab; <see cref="PartLabel"/> creates it, fills in the name and keeps it facing the player.
/// The ring is at this object's origin; the tag hangs up and to the right of it.
/// </summary>
public class PartLabelView : MonoBehaviour
{
    [Header("Pieces (all built into the prefab)")]
    [SerializeField] TMP_Text text;
    [SerializeField] Transform backing;           // a quad, black
    [SerializeField] LineRenderer leader;         // from the ring up to the tag and along its top edge
    [SerializeField] LineRenderer ringOutline;
    [SerializeField] MeshFilter dot;              // the white middle of the ring (its mesh is made here)

    [Header("Layout (metres)")]
    [SerializeField] Vector2 offset = new Vector2(0.07f, 0.07f);      // top-left corner of the tag, from the ring
    [SerializeField] float ringRadius = 0.009f;
    [SerializeField] Vector2 padding = new Vector2(0.02f, 0.012f);    // space around the text
    [SerializeField] float minWidth = 0.12f;

    void Awake()
    {
        // the ring
        const int n = 24;
        ringOutline.useWorldSpace = false;
        ringOutline.loop = true;
        ringOutline.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            ringOutline.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ringRadius);
        }

        // the white disc inside it
        var mesh = new Mesh { name = "LabelDot" };
        var verts = new Vector3[n + 1];
        var tris = new int[n * 3];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ringRadius;
            // wound so that it faces the same way as the tag (visible when looking along +z)
            tris[i * 3] = 0; tris[i * 3 + 1] = 1 + (i + 1) % n; tris[i * 3 + 2] = 1 + i;
        }
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        dot.sharedMesh = mesh;
    }

    /// <summary>Writes the name and sizes the tag around it.</summary>
    public void SetText(string label)
    {
        text.text = label;
        text.ForceMeshUpdate();
        float w = Mathf.Max(minWidth, text.preferredWidth + padding.x * 2f);
        float h = text.preferredHeight + padding.y * 2f;

        backing.localPosition = new Vector3(offset.x + w * 0.5f, offset.y - h * 0.5f, 0.0005f);
        backing.localScale = new Vector3(w, h, 1f);

        var rect = text.rectTransform;
        rect.localPosition = new Vector3(offset.x + w * 0.5f, offset.y - h * 0.5f, 0f);
        rect.sizeDelta = new Vector2(w, h);

        // from the ring, diagonally up to the tag's top-left corner, then along its top edge
        leader.useWorldSpace = false;
        leader.positionCount = 3;
        Vector3 start = new Vector3(0.7071f, 0.7071f, 0f) * ringRadius;
        leader.SetPosition(0, start);
        leader.SetPosition(1, new Vector3(offset.x, offset.y, 0f));
        leader.SetPosition(2, new Vector3(offset.x + w, offset.y, 0f));
    }
}
