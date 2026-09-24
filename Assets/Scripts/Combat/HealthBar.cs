using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private CharacterStats stats;
    [SerializeField] private Image fill;
    [SerializeField] private Text label;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.4f, 0f);
    [SerializeField] private Gradient colors;

    private Camera cam;

    private void Start()
    {
        cam = Camera.main;
        if (!stats) stats = GetComponentInParent<CharacterStats>();
        if (!stats) return;
        stats.HealthChanged += Refresh;
        Refresh(stats.CurrentHealth, stats.MaxHealth);
    }

    private void OnDestroy()
    {
        if (stats) stats.HealthChanged -= Refresh;
    }

    private void LateUpdate()
    {
        if (!stats) return;
        transform.position = stats.transform.position + offset;
        if (!cam) cam = Camera.main;
        if (cam) transform.rotation = cam.transform.rotation;
    }

    private void Refresh(int current, int max)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        if (fill)
        {
            fill.fillAmount = ratio;
            if (colors != null) fill.color = colors.Evaluate(ratio);
        }
        if (label) label.text = $"{stats.DisplayName}  {current}/{max}";
    }
}
