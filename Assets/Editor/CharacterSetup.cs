using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CharacterSetup
{
    private static readonly Dictionary<string, (string[] keys, string[] exclude)> PlayerStates = new Dictionary<string, (string[], string[])>
    {
        { "Idle", (new[] { "idle", "stand", "breath" }, new[] { "attack", "hit" }) },
        { "Run", (new[] { "run", "sprint", "walk", "move" }, new[] { "back", "left", "right", "attack", "stop", "start" }) },
        { "Jump", (new[] { "jump" }, new string[0]) },
        { "Attack", (new[] { "attack", "slash", "punch", "swing", "kick", "combo" }, new string[0]) },
        { "Hit", (new[] { "gethit", "hit", "damage", "hurt", "impact" }, new[] { "attack" }) },
        { "Dead", (new[] { "die", "death", "dead" }, new string[0]) },
    };

    private static readonly Dictionary<string, (string[] keys, string[] exclude)> MonsterStates = new Dictionary<string, (string[], string[])>
    {
        { "Idle", (new[] { "idle", "stand", "breath" }, new[] { "attack", "hit" }) },
        { "Walk", (new[] { "walk", "run", "move", "crawl" }, new[] { "back", "left", "right", "attack", "stop", "start" }) },
        { "Attack", (new[] { "attack", "bite", "slash", "punch", "claw", "smash" }, new string[0]) },
        { "Hit", (new[] { "gethit", "hit", "damage", "hurt", "impact" }, new[] { "attack" }) },
        { "Dead", (new[] { "die", "death", "dead" }, new string[0]) },
    };

    [MenuItem("TP03/Use selected model as Player", priority = 20)]
    public static void UseAsPlayer() => Setup("Player", TP03Builder.PlayerControllerPath, PlayerStates, 1.8f, "PlayerAnimation");

    [MenuItem("TP03/Use selected model as Monster", priority = 21)]
    public static void UseAsMonster() => Setup("Monster", TP03Builder.MonsterControllerPath, MonsterStates, 2.2f, "MonsterAI");

    private static void Setup(string targetName, string controllerPath, Dictionary<string, (string[] keys, string[] exclude)> stateKeys, float targetHeight, string componentName)
    {
        var model = Selection.activeObject as GameObject;
        if (model == null || !EditorUtility.IsPersistent(model))
        {
            EditorUtility.DisplayDialog("TP03", "Select the character's prefab or FBX in the Project window first, then run this menu again.", "OK");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != TP03Builder.SceneGameplay)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(TP03Builder.SceneGameplay);
        }

        var target = GameObject.Find(targetName);
        if (target == null)
        {
            EditorUtility.DisplayDialog("TP03", $"No '{targetName}' object in the Gameplay scene. Run TP03 > Build everything first.", "OK");
            return;
        }

        var old = target.transform.Find("Model");
        if (old) Object.DestroyImmediate(old.gameObject);
        var placeholder = target.transform.Find("Placeholder");
        if (placeholder) placeholder.gameObject.SetActive(false);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, target.transform);
        inst.name = "Model";
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;

        foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var rb in inst.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;

        FitHeight(inst, target.transform, targetHeight);
        foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = target.layer;

        var animator = inst.GetComponentInChildren<Animator>();
        if (!animator) animator = inst.AddComponent<Animator>();
        if (!animator.avatar)
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(model)).OfType<Avatar>().FirstOrDefault();
            if (avatar) animator.avatar = avatar;
        }
        animator.applyRootMotion = false;

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        animator.runtimeAnimatorController = controller;

        var clips = FindClips(model);
        var report = new StringBuilder();
        var states = controller.layers[0].stateMachine.states;
        foreach (var kv in stateKeys)
        {
            var clip = Pick(clips, kv.Value.keys, kv.Value.exclude);
            var state = states.FirstOrDefault(s => s.state.name == kv.Key).state;
            if (state == null) continue;
            state.motion = clip;
            report.AppendLine($"{kv.Key,-7} : {(clip ? clip.name : "-- not found, drag a clip onto this state in the Animator window")}");
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        var component = target.GetComponent(componentName);
        if (component)
        {
            var so = new SerializedObject(component);
            var prop = so.FindProperty("animator");
            if (prop != null)
            {
                prop.objectReferenceValue = animator;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EditorSceneManager.MarkSceneDirty(target.scene);
        EditorSceneManager.SaveScene(target.scene);
        Selection.activeObject = target;

        EditorUtility.DisplayDialog("TP03", $"'{model.name}' is now the {targetName}.\n\nAnimations found ({clips.Count} clips in the pack):\n\n{report}\nPress Play to test.", "OK");
    }

    private static void FitHeight(GameObject inst, Transform root, float targetHeight)
    {
        var renderers = inst.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        if (b.size.y > 0.01f) inst.transform.localScale *= targetHeight / b.size.y;

        b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        inst.transform.position += Vector3.up * (root.position.y - b.min.y);
    }

    private static List<AnimationClip> FindClips(GameObject model)
    {
        string path = AssetDatabase.GetAssetPath(model);
        var parts = path.Split('/');
        string folder = parts.Length > 2 ? parts[0] + "/" + parts[1] : "Assets";

        var result = new List<AnimationClip>();
        var seen = new HashSet<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!seen.Add(p)) continue;
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>())
                if (!clip.name.StartsWith("__preview__")) result.Add(clip);
        }
        return result;
    }

    private static AnimationClip Pick(List<AnimationClip> clips, string[] keys, string[] exclude)
    {
        foreach (var key in keys)
        {
            var match = clips
                .Where(c =>
                {
                    string n = c.name.ToLowerInvariant();
                    return n.Contains(key) && !exclude.Any(e => n.Contains(e));
                })
                .OrderBy(c => c.name.Length)
                .FirstOrDefault();
            if (match) return match;
        }
        return null;
    }
}
