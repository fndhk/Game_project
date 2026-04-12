using UnityEngine;

public class ExitGate : MonoBehaviour
{
    public int requiredItems = 4;
    public GameObject gateVisual;
    public Collider gateBlocker;

    [Header("Read Only")]
    public bool opened;

    private void Update()
    {
        if (!opened && ItemPickup.CollectedCount >= requiredItems)
        {
            OpenGate();
        }
    }

    private void OpenGate()
    {
        opened = true;

        if (gateVisual != null)
        {
            gateVisual.SetActive(false);
        }

        if (gateBlocker != null)
        {
            gateBlocker.enabled = false;
        }
    }
}
