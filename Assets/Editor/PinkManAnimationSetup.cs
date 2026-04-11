using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

public class PinkManAnimationSetup : EditorWindow
{
    [MenuItem("Tools/Setup PinkMan Animations")]
    public static void SetupAnimations()
    {
        string charPath = "Assets/Pixel Adventure 1/Assets/Main Characters/Pink Man";
        string animPath = "Assets/Animations";

        // Ensure Animations folder exists
        if (!AssetDatabase.IsValidFolder(animPath))
            AssetDatabase.CreateFolder("Assets", "Animations");

        // Create animation clips
        AnimationClip idle   = CreateSpriteClip(charPath + "/Idle (32x32).png",        animPath + "/PinkMan_Idle.anim",        11, true);
        AnimationClip run    = CreateSpriteClip(charPath + "/Run (32x32).png",          animPath + "/PinkMan_Run.anim",         12, true);
        AnimationClip jump   = CreateSingleSpriteClip(charPath + "/Jump (32x32).png",   animPath + "/PinkMan_Jump.anim",        false);
        AnimationClip fall   = CreateSingleSpriteClip(charPath + "/Fall (32x32).png",   animPath + "/PinkMan_Fall.anim",        false);
        AnimationClip dblJmp = CreateSpriteClip(charPath + "/Double Jump (32x32).png",  animPath + "/PinkMan_DoubleJump.anim",  12, false);
        AnimationClip hit    = CreateSpriteClip(charPath + "/Hit (32x32).png",          animPath + "/PinkMan_Hit.anim",         7, false);

        // Create/update Animator Controller
        string ctrlPath = animPath + "/PinkMan.controller";
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
        if (ctrl == null)
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);

        // Clear existing states
        var layer = ctrl.layers[0];
        var sm = layer.stateMachine;
        foreach (var s in sm.states) sm.RemoveState(s.state);

        // Add parameters
        ctrl.parameters = new AnimatorControllerParameter[0];
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("VelocityY", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("DoubleJump", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        // Add states
        var stIdle   = sm.AddState("Idle");    stIdle.motion   = idle;
        var stRun    = sm.AddState("Run");     stRun.motion    = run;
        var stJump   = sm.AddState("Jump");    stJump.motion   = jump;
        var stFall   = sm.AddState("Fall");    stFall.motion   = fall;
        var stDblJmp = sm.AddState("DoubleJump"); stDblJmp.motion = dblJmp;
        var stHit    = sm.AddState("Hit");     stHit.motion    = hit;

        sm.defaultState = stIdle;

        // Transitions: Idle <-> Run
        AddTransition(stIdle, stRun,   "Speed",      AnimatorConditionMode.Greater, 0.1f);
        AddTransition(stRun,  stIdle,  "Speed",      AnimatorConditionMode.Less,    0.1f);

        // Transitions: Ground -> Air
        AddTransitionBool(stIdle, stJump, "IsGrounded", false, extraCond: ("VelocityY", AnimatorConditionMode.Greater, 0f));
        AddTransitionBool(stRun,  stJump, "IsGrounded", false, extraCond: ("VelocityY", AnimatorConditionMode.Greater, 0f));
        AddTransitionBool(stIdle, stFall, "IsGrounded", false, extraCond: ("VelocityY", AnimatorConditionMode.Less, 0f));
        AddTransitionBool(stRun,  stFall, "IsGrounded", false, extraCond: ("VelocityY", AnimatorConditionMode.Less, 0f));

        // Jump -> Fall
        AddTransition(stJump, stFall, "VelocityY", AnimatorConditionMode.Less, 0f);

        // Double jump (triggered only by gameplay code)
        AddTransitionTrigger(stJump, stDblJmp, "DoubleJump");
        AddTransitionTrigger(stFall, stDblJmp, "DoubleJump");
        AddTransition(stDblJmp, stFall, "VelocityY", AnimatorConditionMode.Less, 0f);

        // Air -> Ground
        AddTransitionBool(stJump,   stIdle, "IsGrounded", true);
        AddTransitionBool(stFall,   stIdle, "IsGrounded", true);
        AddTransitionBool(stDblJmp, stIdle, "IsGrounded", true);

        // Die trigger (from Any State)
        var dieTrans = sm.AddAnyStateTransition(stHit);
        dieTrans.AddCondition(AnimatorConditionMode.If, 0, "Die");
        dieTrans.hasExitTime = false;
        dieTrans.duration = 0;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PinkManAnimationSetup] Done! All animations and controller created.");
    }

    static AnimationClip CreateSpriteClip(string texturePath, string outputPath, float fps, bool loop)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        var spriteList = new List<Sprite>();
        foreach (var obj in sprites)
            if (obj is Sprite s) spriteList.Add(s);
        spriteList.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

        AnimationClip clip = new AnimationClip();
        clip.frameRate = fps;

        var keyframes = new ObjectReferenceKeyframe[spriteList.Count];
        for (int i = 0; i < spriteList.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / fps,
                value = spriteList[i]
            };
        }

        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        if (loop)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        // Remove existing, save new
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
        if (existing != null) AssetDatabase.DeleteAsset(outputPath);
        AssetDatabase.CreateAsset(clip, outputPath);
        return clip;
    }

    static AnimationClip CreateSingleSpriteClip(string texturePath, string outputPath, bool loop)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        AnimationClip clip = new AnimationClip();
        clip.frameRate = 1;

        var keyframes = new ObjectReferenceKeyframe[]
        {
            new ObjectReferenceKeyframe { time = 0, value = sprite }
        };
        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
        if (existing != null) AssetDatabase.DeleteAsset(outputPath);
        AssetDatabase.CreateAsset(clip, outputPath);
        return clip;
    }

    static void AddTransition(AnimatorState from, AnimatorState to, string param, AnimatorConditionMode mode, float threshold)
    {
        var t = from.AddTransition(to);
        t.AddCondition(mode, threshold, param);
        t.hasExitTime = false;
        t.duration = 0.05f;
    }

    static void AddTransitionBool(AnimatorState from, AnimatorState to, string param, bool value,
        (string name, AnimatorConditionMode mode, float threshold)? extraCond = null)
    {
        var t = from.AddTransition(to);
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
        if (extraCond.HasValue)
            t.AddCondition(extraCond.Value.mode, extraCond.Value.threshold, extraCond.Value.name);
        t.hasExitTime = false;
        t.duration = 0.05f;
    }

    static void AddTransitionTrigger(AnimatorState from, AnimatorState to, string trigger)
    {
        var t = from.AddTransition(to);
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        t.hasExitTime = false;
        t.duration = 0.05f;
    }
}
