using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using System.IO;
using System.Collections.Generic;

public class EndPointSetup
{
    const string IDLE_PATH   = "Assets/Pixel Adventure 1/Assets/Items/Checkpoints/End/End (Idle).png";
    const string PRESSED_PATH = "Assets/Pixel Adventure 1/Assets/Items/Checkpoints/End/End (Pressed) (64x64).png";
    const int FRAME_SIZE = 64;

    [MenuItem("Tools/Setup EndPoint Sprites & Animation")]
    public static void Run()
    {
        SliceAsMultiple(IDLE_PATH);
        SliceAsMultiple(PRESSED_PATH);
        AssetDatabase.Refresh();

        var idleSprites    = GetSprites(IDLE_PATH);
        var pressedSprites = GetSprites(PRESSED_PATH);

        if (idleSprites.Length == 0) { Debug.LogError("No idle sprites found"); return; }

        // Create animation clips
        AnimationClip idleClip    = CreateClip("EndIdle",    idleSprites,    12f);
        AnimationClip pressedClip = CreateClip("EndPressed", pressedSprites, 12f);

        // Create animator controller
        string controllerPath = "Assets/Scripts/Animations/EndPointController.controller";
        Directory.CreateDirectory(Path.GetDirectoryName(controllerPath));
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        var idleState    = controller.layers[0].stateMachine.AddState("Idle");
        var pressedState = controller.layers[0].stateMachine.AddState("Pressed");

        idleState.motion    = idleClip;
        pressedState.motion = pressedClip;

        controller.layers[0].stateMachine.defaultState = idleState;

        var param = new AnimatorControllerParameter { name = "Pressed", type = AnimatorControllerParameterType.Bool };
        controller.AddParameter(param.name, param.type);

        var toPressed = idleState.AddTransition(pressedState);
        toPressed.AddCondition(AnimatorConditionMode.If, 0, "Pressed");
        toPressed.hasExitTime = false;

        var toIdle = pressedState.AddTransition(idleState);
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Pressed");
        toIdle.hasExitTime = false;

        AssetDatabase.SaveAssets();

        // Apply to EndPoint in scene
        var ep = GameObject.Find("EndPoint");
        if (ep == null) { Debug.LogError("EndPoint not found in scene"); return; }

        var sr = ep.GetComponent<SpriteRenderer>();
        if (sr == null) sr = ep.AddComponent<SpriteRenderer>();
        sr.sprite = idleSprites[0];

        var anim = ep.GetComponent<Animator>();
        if (anim == null) anim = ep.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;

        Debug.Log("EndPoint setup complete!");
    }

    static void SliceAsMultiple(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) return;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.isReadable = true;
        importer.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int cols = tex.width  / FRAME_SIZE;
        int rows = tex.height / FRAME_SIZE;
        string baseName = Path.GetFileNameWithoutExtension(path);

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        var spriteRects = new List<SpriteRect>();
        int idx = 0;
        for (int r = rows - 1; r >= 0; r--)
            for (int c = 0; c < cols; c++)
            {
                spriteRects.Add(new SpriteRect
                {
                    name   = baseName + "_" + idx,
                    rect   = new Rect(c * FRAME_SIZE, r * FRAME_SIZE, FRAME_SIZE, FRAME_SIZE),
                    pivot  = new Vector2(0.5f, 0.5f),
                    alignment = SpriteAlignment.Center,
                    spriteID = GUID.Generate()
                });
                idx++;
            }

        dataProvider.SetSpriteRects(spriteRects.ToArray());
        dataProvider.Apply();

        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    static Sprite[] GetSprites(string path)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        var list = new System.Collections.Generic.List<Sprite>();
        foreach (var a in assets)
            if (a is Sprite s) list.Add(s);
        list.Sort((a, b) => string.Compare(a.name, b.name));
        return list.ToArray();
    }

    static AnimationClip CreateClip(string name, Sprite[] sprites, float fps)
    {
        string dir = "Assets/Scripts/Animations";
        Directory.CreateDirectory(dir);
        string clipPath = dir + "/" + name + ".anim";

        var clip = new AnimationClip { frameRate = fps };
        clip.wrapMode = WrapMode.Loop;

        var binding = new UnityEditor.EditorCurveBinding
        {
            type         = typeof(SpriteRenderer),
            path         = "",
            propertyName = "m_Sprite"
        };

        var keyframes = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            keyframes[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }
}
