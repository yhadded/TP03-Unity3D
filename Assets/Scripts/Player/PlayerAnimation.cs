using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    private PlayerMovement movement;
    private PlayerCombat combat;
    private CharacterStats stats;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        stats = GetComponent<CharacterStats>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) animator.applyRootMotion = false;
    }

    private void OnEnable()
    {
        movement.Jumped += OnJumped;
        if (combat) combat.Attacked += OnAttacked;
        if (stats)
        {
            stats.Damaged += OnDamaged;
            stats.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        movement.Jumped -= OnJumped;
        if (combat) combat.Attacked -= OnAttacked;
        if (stats)
        {
            stats.Damaged -= OnDamaged;
            stats.Died -= OnDied;
        }
    }

    private void Update()
    {
        animator.TrySetFloat(SpeedHash, movement.HorizontalSpeed);
        animator.TrySetBool(GroundedHash, movement.IsGrounded);
    }

    private void OnJumped() => animator.TrySetTrigger(JumpHash);
    private void OnAttacked() => animator.TrySetTrigger(AttackHash);
    private void OnDamaged() => animator.TrySetTrigger(HitHash);
    private void OnDied() => animator.TrySetBool(DeadHash, true);
}
