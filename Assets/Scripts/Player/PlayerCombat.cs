using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterStats), typeof(PlayerMovement))]
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private float attackCooldown = 0.8f;
    [SerializeField] private float hitDelay = 0.35f;
    [SerializeField] private float attackRange = 1.6f;
    [SerializeField] private float attackRadius = 1.2f;
    [SerializeField] private float respawnDelay = 3f;

    private CharacterStats stats;
    private PlayerMovement movement;
    private InputAction attack;
    private float nextAttackTime;

    public event Action Attacked;

    private void Awake()
    {
        stats = GetComponent<CharacterStats>();
        movement = GetComponent<PlayerMovement>();
        attack = new InputAction("Attack", InputActionType.Button, "<Keyboard>/f");
        attack.AddBinding("<Gamepad>/buttonWest");
    }

    private void OnEnable()
    {
        attack.Enable();
        stats.Died += OnDied;
    }

    private void OnDisable()
    {
        attack.Disable();
        stats.Died -= OnDied;
    }

    private void OnDestroy()
    {
        attack.Dispose();
    }

    private void Update()
    {
        if (stats.IsDead) return;
        if (attack.WasPressedThisFrame() && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        Attacked?.Invoke();
        yield return new WaitForSeconds(hitDelay);
        if (stats.IsDead) yield break;

        Vector3 center = transform.position + Vector3.up + transform.forward * attackRange;
        foreach (var col in Physics.OverlapSphere(center, attackRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var other = col.GetComponentInParent<CharacterStats>();
            if (other && other != stats && !other.IsDead) other.TakeDamage(stats.AttackPower);
        }
    }

    private void OnDied()
    {
        movement.CanMove = false;
        StartCoroutine(Respawn());
    }

    private IEnumerator Respawn()
    {
        yield return new WaitForSeconds(respawnDelay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up + transform.forward * attackRange, attackRadius);
    }
}
