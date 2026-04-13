using UnityEngine;

public class RevealDotVisual : MonoBehaviour
{
    [Header("Visual")]
    public float noiseStrength = 0.03f;
    public float pulseScale = 0.2f;
    public float pulseSpeed = 8f;

    private ProximitySensor sensor;
    private Vector3 baseScale;
    private Vector3 basePosition;
    private float seed;

    public void Initialize(ProximitySensor proximitySensor)
    {
        sensor = proximitySensor;
        baseScale = transform.localScale;
        basePosition = transform.position;
        seed = Random.Range(0f, 1000f);
    }

    private void Awake()
    {
        baseScale = transform.localScale;
        basePosition = transform.position;
        seed = Random.Range(0f, 1000f);
    }

    private void Update()
    {
        if (sensor == null)
        {
            return;
        }

        float intensity = sensor.currentIntensity;
        float t = Time.time + seed;

        float pulse = 1f + Mathf.Sin(t * pulseSpeed) * pulseScale * intensity;
        transform.localScale = baseScale * pulse;

        Vector3 offset = new Vector3(
            Mathf.PerlinNoise(t, 0f) - 0.5f,
            0f,
            Mathf.PerlinNoise(0f, t) - 0.5f
        ) * noiseStrength * intensity;

        transform.position = basePosition + offset;
    }
}
