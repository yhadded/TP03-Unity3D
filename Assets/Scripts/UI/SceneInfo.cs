using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneInfo : MonoBehaviour
{
    [SerializeField] private Text infoText;
    [SerializeField] private Text helpText;
    [SerializeField] private DayNightCycle dayNight;
    [SerializeField] private BezierPathFollower follower;
    [SerializeField] private GameObject uiRoot;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) Load(0);
            if (kb.digit2Key.wasPressedThisFrame) Load(1);
            if (kb.digit3Key.wasPressedThisFrame) Load(2);
            if (kb.hKey.wasPressedThisFrame && uiRoot) uiRoot.SetActive(!uiRoot.activeSelf);
        }

        if (!infoText) return;
        if (dayNight)
        {
            int h = Mathf.FloorToInt(dayNight.Hour);
            int m = Mathf.FloorToInt((dayNight.Hour - h) * 60f);
            infoText.text = $"{h:00}:{m:00}   {(dayNight.FastMode ? "ACCELERE (24h = " + dayNight.SecondsPerDay + " s)" : "normal (24h = " + dayNight.SecondsPerDay / 60f + " min)")}";
        }
        else if (follower)
        {
            infoText.text = $"Vitesse camera : {follower.Speed:0.0} m/s   Progression : {follower.Progress * 100f:0}%";
        }
    }

    private static void Load(int index)
    {
        if (index < SceneManager.sceneCountInBuildSettings && index != SceneManager.GetActiveScene().buildIndex)
            SceneManager.LoadScene(index);
    }
}
