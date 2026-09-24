using System.Collections.Generic;
using UnityEngine;

public static class AnimatorExtensions
{
    private static readonly Dictionary<RuntimeAnimatorController, HashSet<int>> Cache = new();

    public static bool HasParameter(this Animator animator, int hash)
    {
        if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled) return false;

        var controller = animator.runtimeAnimatorController;
        if (!Cache.TryGetValue(controller, out var set))
        {
            set = new HashSet<int>();
            foreach (var p in animator.parameters) set.Add(p.nameHash);
            if (set.Count == 0) return false;
            Cache[controller] = set;
        }
        return set.Contains(hash);
    }

    public static void TrySetFloat(this Animator animator, int hash, float value)
    {
        if (animator.HasParameter(hash)) animator.SetFloat(hash, value);
    }

    public static void TrySetBool(this Animator animator, int hash, bool value)
    {
        if (animator.HasParameter(hash)) animator.SetBool(hash, value);
    }

    public static void TrySetTrigger(this Animator animator, int hash)
    {
        if (animator.HasParameter(hash)) animator.SetTrigger(hash);
    }
}
