using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TriggerZone : MonoBehaviour
{
    [SerializeField] private string targetTag = "Player";

    public Transform Target { get; private set; }
    public event Action<Transform> Entered;
    public event Action<Transform> Exited;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(targetTag)) return;
        Target = other.transform;
        Entered?.Invoke(Target);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(targetTag) || other.transform != Target) return;
        Target = null;
        Exited?.Invoke(other.transform);
    }
}
