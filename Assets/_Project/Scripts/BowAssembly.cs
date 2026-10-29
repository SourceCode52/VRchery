using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BowAssembly : MonoBehaviour
{
    [Tooltip("How many parts must be snapped in before the swap")]
    public int totalParts = 6;

    [Tooltip("The individual loose parts to hide on completion")]
    public GameObject[] partsToHide;

    [Tooltip("Optional. A finished bow model to show on completion. Leave inactive in the scene. " +
             "Not needed when On Complete swaps in the shooting bow instead.")]
    public GameObject assembledBow;

    [Tooltip("Called once, after the loose parts have been hidden (for example to bring in the shooting bow).")]
    public UnityEvent onComplete;

    private HashSet<object> snapped = new HashSet<object>();
    private bool completed;

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
        if (completed) return;
        completed = true;

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

        onComplete?.Invoke();
    }
}
