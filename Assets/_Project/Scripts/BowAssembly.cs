using System.Collections.Generic;
using UnityEngine;

public class BowAssembly : MonoBehaviour
{
    [Tooltip("How many parts must be snapped in before the swap")]
    public int totalParts = 6;

    [Tooltip("The individual loose parts to hide on completion")]
    public GameObject[] partsToHide;

    [Tooltip("The finished bow model. Leave inactive in the scene.")]
    public GameObject assembledBow;

    private HashSet<object> snapped = new HashSet<object>();

    // Called by each LockingSocket when its part is locked in
    public void RegisterSnapped(object source)
    {
        snapped.Add(source); // a HashSet ignores duplicates
        Debug.Log("Snapped " + snapped.Count + " / " + totalParts);

        if (snapped.Count >= totalParts)
            Complete();
    }

    private void Complete()
    {
        if (assembledBow != null)
        {
            assembledBow.transform.SetPositionAndRotation(
                transform.position, transform.rotation);
            assembledBow.SetActive(true);
        }

        foreach (GameObject part in partsToHide)
        {
            if (part != null) part.SetActive(false);
        }
    }
}