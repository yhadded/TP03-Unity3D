using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class TP03Builder
{
    public const int PlayerLayer = 6;
    public const string SceneGameplay = "Assets/Scenes/Gameplay.unity";
    public const string SceneBezier = "Assets/Scenes/Bezier.unity";
    public const string SceneForest = "Assets/Scenes/Forest.unity";
    public const string PlayerControllerPath = "Assets/Generated/Animations/PlayerAnimator.controller";
    public const string MonsterControllerPath = "Assets/Generated/Animations/MonsterAnimator.controller";

    private const string Gen = "Assets/Generated";

    private static Font font;
    private static Sprite whiteSprite;
    private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

    static TP03Builder()
    {
        EditorApplication.delayCall += TryAutoBuild;
    }

    private static void TryAutoBuild()
    {
        if (File.Exists(SceneGameplay) || SessionState.GetBool("TP03_AutoBuildDone", false)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryAutoBuild;
            return;
        }
        SessionState.SetBool("TP03_AutoBuildDone", true);
        BuildAll();
    }

    [MenuItem("TP03/Build everything", priority = 0)]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        bool restart = false;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Progress("Project settings", 0.05f);
            restart = SetupProjectSettings();

            Progress("Cleaning", 0.1f);
            AssetDatabase.DeleteAsset(Gen);
            AssetDatabase.DeleteAsset("Assets/Scenes");
            foreach (var f in new[] { "Materials", "Textures", "Animations", "Meshes", "Prefabs", "Terrain" })
                EnsureFolder(Gen + "/" + f);
            EnsureFolder("Assets/Scenes");
            Mats.Clear();

            Progress("Textures and materials", 0.2f);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateTextures();
            CreateMaterials();

            Progress("Animator controllers", 0.3f);
            CreatePlayerController();
            CreateMonsterController();

            Progress("Scene: Gameplay (Ex 1-5, 8)", 0.4f);
            BuildGameplay();

            Progress("Scene: Bezier (Ex 6, 9)", 0.6f);
            BuildBezier();

            Progress("Scene: Forest (Ex 7)", 0.75f);
            BuildForest();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneGameplay, true),
                new EditorBuildSettingsScene(SceneBezier, true),
                new EditorBuildSettingsScene(SceneForest, true),
            };

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(SceneGameplay);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("TP03", "Build failed:\n" + e.Message + "\n\nSee the Console, then run TP03 > Build everything again.", "OK");
            return;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (restart)
        {
            if (EditorUtility.DisplayDialog("TP03", "The 3 scenes are built.\n\nUnity must restart once to enable the new Input System.", "Restart now", "Later"))
                EditorApplication.OpenProject(Directory.GetCurrentDirectory());
        }
        else
        {
            EditorUtility.DisplayDialog("TP03", "The 3 scenes are built (Gameplay, Bezier, Forest).\n\nGameplay is open: press Play.", "OK");
        }
    }

    private static void Progress(string info, float p) => EditorUtility.DisplayProgressBar("TP03 - building", info, p);

    private static bool SetupProjectSettings()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        tagManager.FindProperty("layers").GetArrayElementAtIndex(PlayerLayer).stringValue = "Player";
        tagManager.ApplyModifiedPropertiesWithoutUndo();

        bool restart = false;
        var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (ps.Length > 0)
        {
            var so = new SerializedObject(ps[0]);
            var handler = so.FindProperty("activeInputHandler");
            if (handler != null && handler.intValue == 0)
            {
                handler.intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
                restart = true;
            }
        }
        AssetDatabase.SaveAssets();
        return restart;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void Set(UnityEngine.Object target, Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SerializedProperty P(this SerializedObject so, string name)
    {
        var p = so.FindProperty(name);
        if (p == null) throw new Exception($"Field '{name}' not found on {so.targetObject.GetType().Name}");
        return p;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }

    private static Texture2D SaveTexture(string name, int w, int h, Func<int, int, Color> pixel, bool alpha, bool asSprite = false)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, pixel(x, y));
        tex.Apply();
        string path = $"{Gen}/Textures/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (asSprite)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
        }
        imp.alphaIsTransparency = alpha;
        imp.wrapMode = alpha ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Texture2D Noise(string name, Color a, Color b, float scale, int seed)
    {
        var rng = new System.Random(seed);
        float ox = rng.Next(1000), oy = rng.Next(1000);
        return SaveTexture(name, 128, 128, (x, y) =>
        {
            float n = Mathf.PerlinNoise(ox + x * scale, oy + y * scale) * 0.7f + (float)rng.NextDouble() * 0.3f;
            return Color.Lerp(a, b, n);
        }, false);
    }

    private static Texture2D grassTex, dirtTex, rockTex, bladeTex;

    private static void CreateTextures()
    {
        grassTex = Noise("Grass", new Color(0.22f, 0.42f, 0.14f), new Color(0.38f, 0.6f, 0.22f), 0.08f, 1);
        dirtTex = Noise("Dirt", new Color(0.36f, 0.26f, 0.16f), new Color(0.52f, 0.4f, 0.26f), 0.1f, 2);
        rockTex = Noise("Rock", new Color(0.35f, 0.35f, 0.36f), new Color(0.6f, 0.6f, 0.58f), 0.12f, 3);

        var rng = new System.Random(4);
        var blades = new List<(int x, int h, float lean)>();
        for (int i = 0; i < 9; i++) blades.Add((4 + rng.Next(56), 30 + rng.Next(32), (float)(rng.NextDouble() - 0.5) * 0.5f));
        bladeTex = SaveTexture("GrassBlade", 64, 64, (x, y) =>
        {
            foreach (var bl in blades)
            {
                if (y >= bl.h) continue;
                float cx = bl.x + bl.lean * y;
                float width = 2.5f * (1f - (float)y / bl.h) + 0.5f;
                if (Mathf.Abs(x - cx) < width)
                    return Color.Lerp(new Color(0.25f, 0.5f, 0.15f), new Color(0.65f, 0.9f, 0.4f), (float)y / bl.h);
            }
            return new Color(0f, 0f, 0f, 0f);
        }, true);

        var ui = SaveTexture("UIWhite", 4, 4, (x, y) => Color.white, true, true);
        whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(ui));
    }

    private static Material Mat(string name, Color color, string shader = "Standard")
    {
        var m = new Material(Shader.Find(shader)) { color = color };
        AssetDatabase.CreateAsset(m, $"{Gen}/Materials/{name}.mat");
        Mats[name] = m;
        return m;
    }

    private static void CreateMaterials()
    {
        var ground = Mat("Ground", Color.white);
        ground.mainTexture = grassTex;
        ground.mainTextureScale = new Vector2(20f, 20f);
        Mat("Player", new Color(0.2f, 0.45f, 0.95f));
        Mat("Monster", new Color(0.85f, 0.2f, 0.2f));
        Mat("Eyes", Color.white);
        Mat("Obstacle", new Color(0.55f, 0.55f, 0.58f));
        Mat("Bark", new Color(0.4f, 0.27f, 0.15f));
        Mat("Leaves", new Color(0.2f, 0.5f, 0.2f));
        Mat("Pine", new Color(0.12f, 0.35f, 0.18f));
        Mat("BezierGround", new Color(0.75f, 0.77f, 0.8f));
        Mat("PointQuadratic", new Color(1f, 0.55f, 0.1f));
        Mat("PointCubic", new Color(0.1f, 0.75f, 0.9f));
        Mat("PointRecursive", new Color(0.85f, 0.25f, 0.85f));
        Mat("Line", Color.white, "Sprites/Default");
        Mat("Zone", new Color(1f, 0.2f, 0.2f, 0.12f), "Sprites/Default");
    }

    private static AnimatorState AddState(AnimatorStateMachine sm, string name, Vector2 pos)
    {
        return sm.AddState(name, pos);
    }

    private static AnimatorStateTransition Trans(AnimatorState from, AnimatorState to, bool exitTime)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = exitTime;
        t.exitTime = 0.9f;
        t.duration = 0.1f;
        return t;
    }

    private static AnimatorStateTransition Any(AnimatorStateMachine sm, AnimatorState to)
    {
        var t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false;
        t.duration = 0.1f;
        t.canTransitionToSelf = false;
        return t;
    }

    private static void CreatePlayerController()
    {
        var c = AnimatorController.CreateAnimatorControllerAtPath(PlayerControllerPath);
        c.AddParameter("Speed", AnimatorControllerParameterType.Float);
        c.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        c.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Dead", AnimatorControllerParameterType.Bool);

        var sm = c.layers[0].stateMachine;
        var idle = AddState(sm, "Idle", new Vector2(300, 0));
        var run = AddState(sm, "Run", new Vector2(550, 0));
        var jump = AddState(sm, "Jump", new Vector2(300, 120));
        var attack = AddState(sm, "Attack", new Vector2(300, -120));
        var hit = AddState(sm, "Hit", new Vector2(550, -120));
        var dead = AddState(sm, "Dead", new Vector2(0, 240));
        sm.defaultState = idle;

        Trans(idle, run, false).AddCondition(AnimatorConditionMode.Greater, 0.2f, "Speed");
        Trans(run, idle, false).AddCondition(AnimatorConditionMode.Less, 0.2f, "Speed");
        Trans(jump, idle, true).AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        Trans(attack, idle, true);
        Trans(hit, idle, true);

        Any(sm, dead).AddCondition(AnimatorConditionMode.If, 0f, "Dead");
        foreach (var (state, trigger) in new[] { (jump, "Jump"), (attack, "Attack"), (hit, "Hit") })
        {
            var t = Any(sm, state);
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        }
    }

    private static void CreateMonsterController()
    {
        var c = AnimatorController.CreateAnimatorControllerAtPath(MonsterControllerPath);
        c.AddParameter("Speed", AnimatorControllerParameterType.Float);
        c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Dead", AnimatorControllerParameterType.Bool);

        var sm = c.layers[0].stateMachine;
        var idle = AddState(sm, "Idle", new Vector2(300, 0));
        var walk = AddState(sm, "Walk", new Vector2(550, 0));
        var attack = AddState(sm, "Attack", new Vector2(300, -120));
        var hit = AddState(sm, "Hit", new Vector2(550, -120));
        var dead = AddState(sm, "Dead", new Vector2(0, 240));
        sm.defaultState = idle;

        Trans(idle, walk, false).AddCondition(AnimatorConditionMode.Greater, 0.2f, "Speed");
        Trans(walk, idle, false).AddCondition(AnimatorConditionMode.Less, 0.2f, "Speed");
        Trans(attack, idle, true);
        Trans(hit, idle, true);

        Any(sm, dead).AddCondition(AnimatorConditionMode.If, 0f, "Dead");
        foreach (var (state, trigger) in new[] { (attack, "Attack"), (hit, "Hit") })
        {
            var t = Any(sm, state);
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        }
    }

    private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, string mat, bool keepCollider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (parent) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = Mats[mat];
        if (!keepCollider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static Canvas OverlayCanvas(out Text info, out Text help, string infoText, string helpText)
    {
        var go = new GameObject("UI", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        info = UIText(go.transform, "Info", infoText, 34, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(30f, -24f));
        help = UIText(go.transform, "Help", helpText, 24, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(30f, 24f));
        return canvas;
    }

    private static Text UIText(Transform parent, string name, string content, int size, TextAnchor align, Vector2 anchor, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(1800f, 200f);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var o = go.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.9f);
        o.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    private static void SceneInfoObject(Canvas canvas, Text info, Text help, DayNightCycle dayNight, BezierPathFollower follower)
    {
        var go = new GameObject("SceneInfo");
        var si = go.AddComponent<SceneInfo>();
        Set(si, so =>
        {
            so.P("infoText").objectReferenceValue = info;
            so.P("helpText").objectReferenceValue = help;
            so.P("dayNight").objectReferenceValue = dayNight;
            so.P("follower").objectReferenceValue = follower;
            so.P("uiRoot").objectReferenceValue = canvas.gameObject;
        });
    }

    private static void HealthBarFor(GameObject owner, CharacterStats stats, float height)
    {
        var go = new GameObject("HealthBar", typeof(RectTransform));
        go.transform.SetParent(owner.transform, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);
        go.transform.localScale = Vector3.one * 0.01f;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(160f, 22f);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

        var bg = new GameObject("Background", typeof(RectTransform));
        bg.transform.SetParent(go.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = whiteSprite;
        bgImg.color = new Color(0f, 0f, 0f, 0.75f);
        Stretch((RectTransform)bg.transform, 0f);

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(bg.transform, false);
        var fill = fillGo.AddComponent<Image>();
        fill.sprite = whiteSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        Stretch((RectTransform)fillGo.transform, 3f);

        var label = UIText(go.transform, "Label", stats.DisplayName, 18, TextAnchor.LowerCenter, new Vector2(0.5f, 1f), new Vector2(0f, 4f));
        label.rectTransform.pivot = new Vector2(0.5f, 0f);
        label.rectTransform.sizeDelta = new Vector2(300f, 30f);

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.9f, 0.15f, 0.15f), 0f), new GradientColorKey(new Color(0.95f, 0.8f, 0.2f), 0.5f), new GradientColorKey(new Color(0.25f, 0.85f, 0.3f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        var bar = go.AddComponent<HealthBar>();
        Set(bar, so =>
        {
            so.P("stats").objectReferenceValue = stats;
            so.P("fill").objectReferenceValue = fill;
            so.P("label").objectReferenceValue = label;
            so.P("offset").vector3Value = new Vector3(0f, height, 0f);
            so.P("colors").gradientValue = gradient;
        });
    }

    private static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    private static void BuildGameplay()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var cam = Camera.main;
        var sunGo = GameObject.Find("Directional Light");
        sunGo.name = "Sun";
        var sun = sunGo.GetComponent<Light>();
        sun.shadows = LightShadows.Soft;

        var ground = Prim(PrimitiveType.Plane, "Ground", null, Vector3.zero, new Vector3(8f, 1f, 8f), "Ground", true);

        var obstacles = new GameObject("Obstacles").transform;
        Prim(PrimitiveType.Cube, "Wall_A", obstacles, new Vector3(0f, 1.5f, -7f), new Vector3(10f, 3f, 0.6f), "Obstacle", true);
        Prim(PrimitiveType.Cube, "Wall_B", obstacles, new Vector3(-8f, 1.5f, 2f), new Vector3(0.6f, 3f, 8f), "Obstacle", true);
        Prim(PrimitiveType.Cube, "Wall_C", obstacles, new Vector3(16f, 1.5f, 4f), new Vector3(0.6f, 3f, 6f), "Obstacle", true);
        Prim(PrimitiveType.Cube, "Pillar_A", obstacles, new Vector3(6f, 2f, -2f), new Vector3(1.2f, 4f, 1.2f), "Obstacle", true);
        Prim(PrimitiveType.Cube, "Pillar_B", obstacles, new Vector3(-3f, 2f, 6f), new Vector3(1.2f, 4f, 1.2f), "Obstacle", true);
        Prim(PrimitiveType.Cube, "Crate", obstacles, new Vector3(4f, 1f, 6f), new Vector3(3f, 2f, 3f), "Obstacle", true);
        foreach (var p in new[] { new Vector3(-12f, 0f, -8f), new Vector3(-14f, 0f, -2f), new Vector3(-6f, 0f, -13f), new Vector3(10f, 0f, -10f) })
        {
            var tree = new GameObject("Tree").transform;
            tree.SetParent(obstacles, false);
            tree.position = p;
            Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 1.5f, 0.5f), "Bark", true);
            Prim(PrimitiveType.Sphere, "Leaves", tree, new Vector3(0f, 4f, 0f), new Vector3(3f, 3f, 3f), "Leaves", true);
        }

        var playerCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerControllerPath);
        var player = new GameObject("Player") { tag = "Player" };
        var cc = player.AddComponent<CharacterController>();
        cc.center = new Vector3(0f, 1f, 0f);
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.stepOffset = 0.3f;
        var placeholder = Prim(PrimitiveType.Capsule, "Placeholder", player.transform, new Vector3(0f, 1f, 0f), Vector3.one, "Player", false);
        Prim(PrimitiveType.Cube, "Nose", placeholder.transform, new Vector3(0f, 0.5f, 0.45f), new Vector3(0.25f, 0.15f, 0.3f), "Eyes", false);
        var playerAnimator = placeholder.AddComponent<Animator>();
        playerAnimator.runtimeAnimatorController = playerCtrl;
        var playerStats = player.AddComponent<CharacterStats>();
        Set(playerStats, so =>
        {
            so.P("displayName").stringValue = "Joueur";
            so.P("maxHealth").intValue = 100;
            so.P("attackPower").intValue = 25;
        });
        player.AddComponent<PlayerMovement>();
        player.AddComponent<PlayerCombat>();
        var anim = player.AddComponent<PlayerAnimation>();
        Set(anim, so => so.P("animator").objectReferenceValue = playerAnimator);
        HealthBarFor(player, playerStats, 2.4f);
        SetLayerRecursive(player, PlayerLayer);

        var monsterCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MonsterControllerPath);
        var monster = new GameObject("Monster");
        monster.transform.position = new Vector3(12f, 0f, 12f);
        monster.transform.rotation = Quaternion.Euler(0f, 225f, 0f);
        var mcc = monster.AddComponent<CharacterController>();
        mcc.center = new Vector3(0f, 1.1f, 0f);
        mcc.height = 2.2f;
        mcc.radius = 0.6f;
        var mBody = Prim(PrimitiveType.Capsule, "Placeholder", monster.transform, new Vector3(0f, 1.1f, 0f), new Vector3(1.2f, 1.1f, 1.2f), "Monster", false);
        Prim(PrimitiveType.Cube, "EyeL", mBody.transform, new Vector3(-0.2f, 0.45f, 0.42f), new Vector3(0.15f, 0.12f, 0.1f), "Eyes", false);
        Prim(PrimitiveType.Cube, "EyeR", mBody.transform, new Vector3(0.2f, 0.45f, 0.42f), new Vector3(0.15f, 0.12f, 0.1f), "Eyes", false);
        var monsterAnimator = mBody.AddComponent<Animator>();
        monsterAnimator.runtimeAnimatorController = monsterCtrl;

        var zoneGo = new GameObject("WatchZone");
        zoneGo.transform.SetParent(monster.transform, false);
        var sphere = zoneGo.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 9f;
        var zrb = zoneGo.AddComponent<Rigidbody>();
        zrb.isKinematic = true;
        zrb.useGravity = false;
        var zone = zoneGo.AddComponent<TriggerZone>();
        Prim(PrimitiveType.Cylinder, "ZoneVisual", zoneGo.transform, new Vector3(0f, 0.02f, 0f), new Vector3(18f, 0.01f, 18f), "Zone", false);

        var monsterStats = monster.AddComponent<CharacterStats>();
        Set(monsterStats, so =>
        {
            so.P("displayName").stringValue = "Monstre";
            so.P("maxHealth").intValue = 100;
            so.P("attackPower").intValue = 10;
        });
        var ai = monster.AddComponent<MonsterAI>();
        Set(ai, so =>
        {
            so.P("watchZone").objectReferenceValue = zone;
            so.P("animator").objectReferenceValue = monsterAnimator;
        });
        HealthBarFor(monster, monsterStats, 2.9f);

        cam.transform.position = new Vector3(0f, 3.5f, -6f);
        cam.farClipPlane = 300f;
        var orbit = cam.gameObject.AddComponent<OrbitCamera>();
        Set(orbit, so =>
        {
            so.P("target").objectReferenceValue = player.transform;
            so.P("obstacleMask").intValue = ~((1 << PlayerLayer) | (1 << 2));
        });

        var cycle = sunGo.AddComponent<DayNightCycle>();
        Set(cycle, so => so.P("sun").objectReferenceValue = sun);
        RenderSettings.sun = sun;

        var canvas = OverlayCanvas(out var info, out var help, "08:00",
            "ZQSD / WASD : se deplacer    ESPACE : sauter    F : attaquer\n" +
            "Clic droit : tourner camera + joueur    Clic gauche : tourner camera seule    Molette : zoom\n" +
            "T : accelerer le temps (24h = 2 min)    1 / 2 / 3 : changer de scene    H : cacher l'interface");
        SceneInfoObject(canvas, info, help, cycle, null);

        EditorSceneManager.SaveScene(scene, SceneGameplay);
    }

    private static GameObject pointPrefab;

    private static BezierCurve MakeCurve(string name, BezierMode mode, Vector3[] pts, string pointMat, Color color, string title, Vector3 labelPos)
    {
        var go = new GameObject(name);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = Mats["Line"];
        line.startColor = line.endColor = color;
        line.startWidth = line.endWidth = 0.12f;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;

        var polyGo = new GameObject("ControlPolygon");
        polyGo.transform.SetParent(go.transform, false);
        var poly = polyGo.AddComponent<LineRenderer>();
        poly.sharedMaterial = Mats["Line"];
        poly.startColor = poly.endColor = new Color(0.3f, 0.3f, 0.35f, 0.8f);
        poly.startWidth = poly.endWidth = 0.04f;

        var curve = go.AddComponent<BezierCurve>();
        var points = new List<Transform>();
        for (int i = 0; i < pts.Length; i++)
        {
            var p = (GameObject)PrefabUtility.InstantiatePrefab(pointPrefab);
            p.name = "P" + i;
            p.transform.SetParent(go.transform, false);
            p.transform.position = pts[i];
            p.GetComponent<Renderer>().sharedMaterial = Mats[pointMat];
            points.Add(p.transform);
        }

        Set(curve, so =>
        {
            so.P("mode").enumValueIndex = (int)mode;
            so.P("polygonLine").objectReferenceValue = poly;
            var arr = so.P("controlPoints");
            arr.arraySize = points.Count;
            for (int i = 0; i < points.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
        });

        var label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        label.transform.position = labelPos;
        label.transform.rotation = Quaternion.Euler(42f, 0f, 0f);
        var tm = label.AddComponent<TextMesh>();
        tm.text = title;
        tm.font = font;
        tm.fontSize = 48;
        tm.characterSize = 0.12f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        label.GetComponent<MeshRenderer>().sharedMaterial = font.material;

        curve.Redraw();
        return curve;
    }

    private static void BuildBezier()
    {
        var pointGo = Prim(PrimitiveType.Sphere, "ControlPoint", null, Vector3.zero, Vector3.one * 0.6f, "PointRecursive", true);
        pointGo.AddComponent<ControlPointHandle>();
        pointPrefab = PrefabUtility.SaveAsPrefabAsset(pointGo, $"{Gen}/Prefabs/ControlPoint.prefab");
        UnityEngine.Object.DestroyImmediate(pointGo);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var cam = Camera.main;
        cam.transform.position = new Vector3(0.5f, 15f, -15f);
        cam.transform.rotation = Quaternion.Euler(42f, 0f, 0f);

        Prim(PrimitiveType.Plane, "Ground", null, new Vector3(0.5f, 0f, 1f), new Vector3(4f, 1f, 2.5f), "BezierGround", true);

        const float y = 0.3f;
        MakeCurve("Quadratic", BezierMode.Quadratic,
            new[] { new Vector3(-13f, y, -2f), new Vector3(-10f, y, 6f), new Vector3(-6f, y, -2f) },
            "PointQuadratic", new Color(1f, 0.55f, 0.1f), "Quadratique\n(3 points)", new Vector3(-9.5f, 1.5f, -5f));

        MakeCurve("Cubic", BezierMode.Cubic,
            new[] { new Vector3(-3.5f, y, -2f), new Vector3(-2.5f, y, 6f), new Vector3(2f, y, -3f), new Vector3(3.5f, y, 5f) },
            "PointCubic", new Color(0.1f, 0.75f, 0.9f), "Cubique\n(4 points)", new Vector3(0f, 1.5f, -5f));

        var recursive = MakeCurve("Recursive", BezierMode.Recursive,
            new[] { new Vector3(6f, y, -2f), new Vector3(7f, y, 5f), new Vector3(9f, y, -3f), new Vector3(11f, y, 6f), new Vector3(13f, y, -1f), new Vector3(15f, y, 3f) },
            "PointRecursive", new Color(0.85f, 0.25f, 0.85f), "Recursive\n(n points)", new Vector3(10.5f, 1.5f, -5f));

        var dragger = cam.gameObject.AddComponent<ControlPointDragger>();
        Set(dragger, so =>
        {
            so.P("editableCurve").objectReferenceValue = recursive;
            so.P("pointPrefab").objectReferenceValue = pointPrefab;
        });

        var canvas = OverlayCanvas(out var info, out var help, "Courbes de Bezier : quadratique, cubique, recursive (De Casteljau, n points)",
            "Clic gauche + glisser : deplacer un point de controle (la courbe se met a jour en temps reel)\n" +
            "N : ajouter un point a la courbe recursive    Retour arriere : retirer le dernier point\n" +
            "1 / 2 / 3 : changer de scene    H : cacher l'interface");
        SceneInfoObject(canvas, info, help, null, null);

        EditorSceneManager.SaveScene(scene, SceneBezier);
    }

    private static Mesh CombineTree(string name, (Mesh mesh, Vector3 pos, Vector3 scale)[] trunk, (Mesh mesh, Vector3 pos, Vector3 scale)[] foliage)
    {
        CombineInstance[] Build((Mesh mesh, Vector3 pos, Vector3 scale)[] parts)
        {
            var arr = new CombineInstance[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                arr[i] = new CombineInstance { mesh = parts[i].mesh, transform = Matrix4x4.TRS(parts[i].pos, Quaternion.identity, parts[i].scale) };
            return arr;
        }

        var trunkMesh = new Mesh();
        trunkMesh.CombineMeshes(Build(trunk), true, true);
        var foliageMesh = new Mesh();
        foliageMesh.CombineMeshes(Build(foliage), true, true);

        var result = new Mesh { name = name };
        result.CombineMeshes(new[]
        {
            new CombineInstance { mesh = trunkMesh, transform = Matrix4x4.identity },
            new CombineInstance { mesh = foliageMesh, transform = Matrix4x4.identity }
        }, false, false);
        result.RecalculateBounds();
        AssetDatabase.CreateAsset(result, $"{Gen}/Meshes/{name}.asset");
        return result;
    }

    private static GameObject TreePrefab(string name, Mesh mesh, Material foliage)
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { Mats["Bark"], foliage };
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Gen}/Prefabs/{name}.prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    private static Mesh PrimitiveMesh(PrimitiveType type)
    {
        var go = GameObject.CreatePrimitive(type);
        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(go);
        return mesh;
    }

    private static void BuildForest()
    {
        const float size = 240f;
        const float height = 35f;
        const int res = 257;
        var center = new Vector2(size * 0.5f, size * 0.5f);

        float RingRadius(float theta) => 75f + 12f * Mathf.Sin(3f * theta);
        float RingDistance(float x, float z)
        {
            float dx = x - center.x, dz = z - center.y;
            float theta = Mathf.Atan2(dz, dx);
            return Mathf.Abs(Mathf.Sqrt(dx * dx + dz * dz) - RingRadius(theta));
        }

        var td = new TerrainData { heightmapResolution = res };
        td.size = new Vector3(size, height, size);
        AssetDatabase.CreateAsset(td, $"{Gen}/Terrain/ForestTerrain.asset");

        var heights = new float[res, res];
        for (int zi = 0; zi < res; zi++)
        {
            for (int xi = 0; xi < res; xi++)
            {
                float x = xi / (float)(res - 1) * size;
                float z = zi / (float)(res - 1) * size;
                float h = Mathf.PerlinNoise(x * 0.012f + 10f, z * 0.012f + 20f) * 0.6f
                        + Mathf.PerlinNoise(x * 0.035f + 30f, z * 0.035f + 40f) * 0.25f
                        + Mathf.PerlinNoise(x * 0.09f + 50f, z * 0.09f + 60f) * 0.08f;
                float valley = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 22f, RingDistance(x, z)));
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(95f, 118f, Vector2.Distance(new Vector2(x, z), center)));
                heights[zi, xi] = Mathf.Lerp(0.12f, h, valley) + edge * 0.35f;
            }
        }
        td.SetHeights(0, 0, heights);

        TerrainLayer Layer(string n, Texture2D tex, float tile)
        {
            var l = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(tile, tile) };
            AssetDatabase.CreateAsset(l, $"{Gen}/Terrain/{n}.terrainlayer");
            return l;
        }
        td.terrainLayers = new[] { Layer("Grass", grassTex, 8f), Layer("Dirt", dirtTex, 6f), Layer("Rock", rockTex, 10f) };

        const int alphaRes = 256;
        td.alphamapResolution = alphaRes;
        var alpha = new float[alphaRes, alphaRes, 3];
        for (int zi = 0; zi < alphaRes; zi++)
        {
            for (int xi = 0; xi < alphaRes; xi++)
            {
                float nx = xi / (float)(alphaRes - 1), nz = zi / (float)(alphaRes - 1);
                float steep = td.GetSteepness(nx, nz);
                float rock = Mathf.InverseLerp(22f, 38f, steep);
                float dirt = (1f - Mathf.InverseLerp(3f, 8f, RingDistance(nx * size, nz * size))) * (1f - rock);
                float grass = Mathf.Max(0f, 1f - rock - dirt);
                alpha[zi, xi, 0] = grass;
                alpha[zi, xi, 1] = dirt;
                alpha[zi, xi, 2] = rock;
            }
        }
        td.SetAlphamaps(0, 0, alpha);

        var cyl = PrimitiveMesh(PrimitiveType.Cylinder);
        var sph = PrimitiveMesh(PrimitiveType.Sphere);
        var pine = TreePrefab("PineTree", CombineTree("PineTree",
            new[] { (cyl, new Vector3(0f, 2f, 0f), new Vector3(0.35f, 2f, 0.35f)) },
            new[] { (sph, new Vector3(0f, 3.8f, 0f), new Vector3(3f, 2.4f, 3f)), (sph, new Vector3(0f, 5.4f, 0f), new Vector3(2.2f, 2f, 2.2f)), (sph, new Vector3(0f, 6.8f, 0f), new Vector3(1.3f, 1.6f, 1.3f)) }),
            Mats["Pine"]);
        var oak = TreePrefab("OakTree", CombineTree("OakTree",
            new[] { (cyl, new Vector3(0f, 1.6f, 0f), new Vector3(0.45f, 1.6f, 0.45f)) },
            new[] { (sph, new Vector3(0f, 4.4f, 0f), new Vector3(4.2f, 3.4f, 4.2f)), (sph, new Vector3(1.2f, 3.8f, 0.6f), new Vector3(2.6f, 2.2f, 2.6f)), (sph, new Vector3(-1f, 4f, -0.8f), new Vector3(2.4f, 2f, 2.4f)) }),
            Mats["Leaves"]);
        td.treePrototypes = new[] { new TreePrototype { prefab = pine }, new TreePrototype { prefab = oak } };

        var rng = new System.Random(7);
        var trees = new List<TreeInstance>();
        for (int i = 0; i < 4000 && trees.Count < 1800; i++)
        {
            float nx = (float)rng.NextDouble(), nz = (float)rng.NextDouble();
            float x = nx * size, z = nz * size;
            if (RingDistance(x, z) < 6f || td.GetSteepness(nx, nz) > 28f) continue;
            float s = 0.8f + (float)rng.NextDouble() * 0.6f;
            trees.Add(new TreeInstance
            {
                position = new Vector3(nx, 0f, nz),
                prototypeIndex = rng.NextDouble() < 0.6 ? 0 : 1,
                widthScale = s,
                heightScale = s * (0.9f + (float)rng.NextDouble() * 0.3f),
                rotation = (float)(rng.NextDouble() * Math.PI * 2.0),
                color = Color.white,
                lightmapColor = Color.white
            });
        }
        td.SetTreeInstances(trees.ToArray(), true);

        td.detailPrototypes = new[]
        {
            new DetailPrototype
            {
                prototypeTexture = bladeTex,
                renderMode = DetailRenderMode.GrassBillboard,
                usePrototypeMesh = false,
                minWidth = 0.8f, maxWidth = 1.4f,
                minHeight = 0.5f, maxHeight = 1.1f,
                healthyColor = new Color(0.6f, 0.9f, 0.4f),
                dryColor = new Color(0.8f, 0.8f, 0.4f)
            }
        };
        const int detailRes = 256;
        td.SetDetailResolution(detailRes, 16);
        var detail = new int[detailRes, detailRes];
        for (int zi = 0; zi < detailRes; zi++)
        {
            for (int xi = 0; xi < detailRes; xi++)
            {
                float nx = xi / (float)(detailRes - 1), nz = zi / (float)(detailRes - 1);
                if (RingDistance(nx * size, nz * size) < 4f || td.GetSteepness(nx, nz) > 25f) continue;
                detail[zi, xi] = rng.Next(0, 4);
            }
        }
        td.SetDetailLayer(0, 0, 0, detail);
        EditorUtility.SetDirty(td);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var terrainGo = Terrain.CreateTerrainGameObject(td);
        terrainGo.name = "ForestTerrain";
        var terrain = terrainGo.GetComponent<Terrain>();
        terrain.treeDistance = 2000f;
        terrain.treeBillboardDistance = 2000f;
        terrain.detailObjectDistance = 120f;
        terrain.detailObjectDensity = 1f;

        var sunGo = new GameObject("Sun");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sun.shadows = LightShadows.Soft;
        sun.color = new Color(1f, 0.95f, 0.85f);
        sunGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        RenderSettings.sun = sun;
        RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.006f;
        RenderSettings.fogColor = new Color(0.68f, 0.78f, 0.85f);

        var pathGo = new GameObject("CameraPath");
        var pathLine = pathGo.AddComponent<LineRenderer>();
        pathLine.sharedMaterial = Mats["Line"];
        pathLine.startColor = pathLine.endColor = new Color(1f, 0.9f, 0.2f);
        pathLine.startWidth = pathLine.endWidth = 0.3f;
        pathLine.enabled = false;
        var path = pathGo.AddComponent<BezierCurve>();

        const int segments = 8;
        Vector3 Anchor(float theta)
        {
            float r = RingRadius(theta);
            float x = center.x + r * Mathf.Cos(theta);
            float z = center.y + r * Mathf.Sin(theta);
            return new Vector3(x, td.GetInterpolatedHeight(x / size, z / size) + 4f, z);
        }

        var anchors = new Vector3[segments + 1];
        var tangents = new Vector3[segments + 1];
        for (int k = 0; k <= segments; k++)
        {
            float theta = 2f * Mathf.PI * k / segments;
            anchors[k] = Anchor(theta);
            var d = Anchor(theta + 0.01f) - Anchor(theta - 0.01f);
            d.y = 0f;
            tangents[k] = d.normalized;
        }
        anchors[segments] = anchors[0];
        tangents[segments] = tangents[0];

        float handle = 2f * Mathf.PI * 75f / segments / 3f;
        var controlPositions = new List<Vector3>();
        for (int k = 0; k < segments; k++)
        {
            controlPositions.Add(anchors[k]);
            controlPositions.Add(anchors[k] + tangents[k] * handle);
            controlPositions.Add(anchors[k + 1] - tangents[k + 1] * handle);
        }
        controlPositions.Add(anchors[segments]);

        var pointTransforms = new List<Transform>();
        for (int i = 0; i < controlPositions.Count; i++)
        {
            var p = new GameObject("P" + i).transform;
            p.SetParent(pathGo.transform, false);
            var pos = controlPositions[i];
            pos.y = Mathf.Max(pos.y, td.GetInterpolatedHeight(pos.x / size, pos.z / size) + 3f);
            p.position = pos;
            pointTransforms.Add(p);
        }

        Set(path, so =>
        {
            so.P("mode").enumValueIndex = (int)BezierMode.CubicChain;
            so.P("resolution").intValue = 400;
            var arr = so.P("controlPoints");
            arr.arraySize = pointTransforms.Count;
            for (int i = 0; i < pointTransforms.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = pointTransforms[i];
        });
        path.Redraw();

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.farClipPlane = 600f;
        cam.fieldOfView = 65f;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = controlPositions[0];
        var follower = camGo.AddComponent<BezierPathFollower>();
        Set(follower, so =>
        {
            so.P("path").objectReferenceValue = path;
            so.P("speed").floatValue = 12f;
            so.P("lookAhead").floatValue = 8f;
        });

        var canvas = OverlayCanvas(out var info, out var help, "",
            "Fleches haut / bas : vitesse de la camera    R : recommencer\n" +
            "H : cacher l'interface (pour l'enregistrement video)    1 / 2 / 3 : changer de scene");
        SceneInfoObject(canvas, info, help, null, follower);

        EditorSceneManager.SaveScene(scene, SceneForest);
    }
}
