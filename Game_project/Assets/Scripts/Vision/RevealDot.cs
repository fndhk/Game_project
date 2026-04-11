using UnityEngine;

[System.Serializable]
public struct RevealDot
{
    public Vector3 worldPos;
    public float size;

    public RevealDot(Vector3 worldPos, float size)
    {
        this.worldPos = worldPos;
        this.size = size;
    }
}
