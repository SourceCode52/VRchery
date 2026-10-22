using System.Collections.Generic;
using UnityEngine;

public class BowAssembly : MonoBehaviour
{
    public int totalParts = 6;           // riser, upperLimb, lowerLimb, sight, stabilisers, string
    public GameObject[] partsToHide = [];     // the individual parts
    public GameObject assembledBow;      // inactive in the scene at start

    readonly HashSet<LockingSocket> snapped = new();

    public void RegisterSnapped(LockingSocket socket)
    {
        snapped.Add(socket);   // HashSet prevents double counting
        if (snapped.Count >= totalParts) Complete();
    }

    void Complete()
    {
        assembledBow.transform.SetPositionAndRotation(
            transform.position, transform.rotation);
        foreach (var p in partsToHide) p.SetActive(false);
        assembledBow.SetActive(true);
    }
}