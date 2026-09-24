using UnityEngine;
using UnityEngine.InputSystem;

public class DayNightCycle : MonoBehaviour
{
    [SerializeField] private Light sun;
    [SerializeField, Range(0f, 24f)] private float startHour = 8f;
    [SerializeField] private float realSecondsPerDay = 1440f;
    [SerializeField] private float fastSecondsPerDay = 120f;
    [SerializeField] private bool fastMode = false;
    [SerializeField] private float sunYaw = 170f;

    [Header("Light")]
    [SerializeField] private float maxIntensity = 1.3f;
    [SerializeField] private float nightIntensity = 0.05f;
    [SerializeField] private Color noonColor = new Color(1f, 0.96f, 0.88f);
    [SerializeField] private Color horizonColor = new Color(1f, 0.55f, 0.25f);
    [SerializeField] private float dayAmbient = 1f;
    [SerializeField] private float nightAmbient = 0.15f;

    private float environmentTimer;

    public float Hour { get; private set; }
    public bool FastMode => fastMode;
    public float SecondsPerDay => fastMode ? fastSecondsPerDay : realSecondsPerDay;

    private void Start()
    {
        Hour = startHour;
        if (!sun) sun = GetComponent<Light>();
        RenderSettings.sun = sun;
        Apply();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.tKey.wasPressedThisFrame) fastMode = !fastMode;

        Hour = (Hour + 24f * Time.deltaTime / Mathf.Max(1f, SecondsPerDay)) % 24f;
        Apply();

        environmentTimer -= Time.deltaTime;
        if (environmentTimer <= 0f)
        {
            environmentTimer = 0.5f;
            DynamicGI.UpdateEnvironment();
        }
    }

    private void Apply()
    {
        if (!sun) return;

        float angle = (Hour / 24f) * 360f - 90f;
        sun.transform.rotation = Quaternion.Euler(angle, sunYaw, 0f);

        float daylight = Mathf.Clamp01(Mathf.Sin((Hour - 6f) / 12f * Mathf.PI));
        sun.intensity = Mathf.Lerp(nightIntensity, maxIntensity, daylight);
        sun.color = Color.Lerp(horizonColor, noonColor, Mathf.Sqrt(daylight));
        RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbient, dayAmbient, daylight);
    }
}
