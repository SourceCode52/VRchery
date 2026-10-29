using UnityEngine;

/// <summary>
/// The bridge from building to shooting. When the bow has been assembled, the built parts are swapped for the
/// shooting bow in exactly the same position and orientation, so it looks as if nothing changed.
///
/// How the match works: the shooting bow and the built parts use the same meshes, so the shooting bow's riser mesh
/// is lined up with the built riser's mesh. Everything else on the shooting bow then falls onto the matching built
/// part (the socket attach points are set up so the parts fit together exactly as the shooting model does).
///
/// Wire <see cref="Handoff"/> to BowAssembly's On Complete event. BowAssembly hides the built parts at the same
/// moment, so the swap happens within a single frame.
/// </summary>
public class BowHandoff : MonoBehaviour
{
    [Header("The built bow")]
    [Tooltip("The mesh object inside the riser part that is locked into the stand (Riser/riser). The shooting bow is matched to it.")]
    [SerializeField] Transform builtRiserMesh;

    [Header("The shooting bow")]
    [Tooltip("The shooting bow (kept inactive until the bow has been built).")]
    [SerializeField] GameObject shootingBow;

    [Tooltip("The riser mesh inside the shooting bow (FullBow/Model/Aptilla_Riser).")]
    [SerializeField] Transform shootingRiserMesh;

    [Tooltip("Optional. Where the bow waits until it is first picked up (its BowPark mount). It is moved to the built bow's pose.")]
    [SerializeField] Transform parkMount;

    [Tooltip("Optional. Told to hold the bow on its mount until it is picked up.")]
    [SerializeField] BowPark park;

    bool done;

    /// <summary>Swaps the built bow for the shooting bow, in the same place and pose.</summary>
    public void Handoff()
    {
        if (done || shootingBow == null || builtRiserMesh == null || shootingRiserMesh == null) return;
        done = true;

        Transform bow = shootingBow.transform;

        // World pose the shooting bow's root must have for its riser mesh to sit exactly on the built riser mesh.
        Matrix4x4 rootToWorld = builtRiserMesh.localToWorldMatrix
                                * shootingRiserMesh.worldToLocalMatrix
                                * bow.localToWorldMatrix;
        Vector3 position = rootToWorld.GetColumn(3);
        Quaternion rotation = rootToWorld.rotation;

        if (parkMount != null) parkMount.SetPositionAndRotation(position, rotation);
        bow.SetPositionAndRotation(position, rotation);

        var body = shootingBow.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        shootingBow.SetActive(true);
        if (park != null) park.Park();
    }
}
