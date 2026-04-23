#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class AnimatorJsonConverterWindowV2 : EditorWindow
{
    private UnityEngine.Object exportSourceObject;
    private TextAsset importJsonAsset;
    private DefaultAsset importOutputFolder;
    private Animator assignImportedToAnimator;

    private Vector2 _scroll;

    [MenuItem("NatorTools/Animation/Animator JSON Converter")]
    public static void Open()
    {
        GetWindow<AnimatorJsonConverterWindowV2>("Animator JSON");
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Animator <-> JSON Converter V2", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Exporta un AnimatorController a JSON o reconstruye un .controller desde JSON.\n" +
            "V2 incluye StateMachineBehaviours, validación de clips/masks/tipos faltantes y reporte de warnings.",
            MessageType.Info);

        EditorGUILayout.Space(10);
        DrawExportSection();

        EditorGUILayout.Space(16);
        DrawImportSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawExportSection()
    {
        EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);
        exportSourceObject = EditorGUILayout.ObjectField(
            new GUIContent("Source Animator / Controller"),
            exportSourceObject,
            typeof(UnityEngine.Object),
            true);

        using (new EditorGUI.DisabledScope(ResolveController(exportSourceObject) == null))
        {
            if (GUILayout.Button("Export To JSON", GUILayout.Height(32)))
            {
                ExportSelected();
            }
        }

        if (exportSourceObject != null && ResolveController(exportSourceObject) == null)
        {
            EditorGUILayout.HelpBox(
                "Selecciona un AnimatorController del Project, un Animator de escena o un GameObject con Animator.",
                MessageType.Warning);
        }
    }

    private void DrawImportSection()
    {
        EditorGUILayout.LabelField("Import", EditorStyles.boldLabel);

        importJsonAsset = (TextAsset)EditorGUILayout.ObjectField(
            new GUIContent("JSON Asset"),
            importJsonAsset,
            typeof(TextAsset),
            false);

        importOutputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            new GUIContent("Output Folder"),
            importOutputFolder,
            typeof(DefaultAsset),
            false);

        assignImportedToAnimator = (Animator)EditorGUILayout.ObjectField(
            new GUIContent("Assign Imported Controller To (optional)"),
            assignImportedToAnimator,
            typeof(Animator),
            true);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Load JSON From Disk", GUILayout.Height(28)))
        {
            string path = EditorUtility.OpenFilePanel("Select Animator JSON", "", "json");
            if (!string.IsNullOrEmpty(path))
            {
                string projectRoot = Application.dataPath.Replace("/Assets", "");
                if (path.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    string relative = "Assets" + path.Substring(projectRoot.Length).Replace("\\", "/");
                    importJsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(relative);
                }
                else
                {
                    string folder = GetOutputFolderPath();
                    ImportFromJson(File.ReadAllText(path), folder, assignImportedToAnimator);
                }
            }
        }

        using (new EditorGUI.DisabledScope(importJsonAsset == null || string.IsNullOrEmpty(GetOutputFolderPath())))
        {
            if (GUILayout.Button("Import JSON Asset", GUILayout.Height(28)))
            {
                ImportFromJson(importJsonAsset.text, GetOutputFolderPath(), assignImportedToAnimator);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private AnimatorController ResolveController(UnityEngine.Object obj)
    {
        if (obj == null)
            return null;

        if (obj is AnimatorController controller)
            return controller;

        if (obj is Animator animator)
            return animator.runtimeAnimatorController as AnimatorController;

        if (obj is GameObject go)
        {
            var scopeAnimator = go.GetComponent<Animator>();
            return scopeAnimator != null ? scopeAnimator.runtimeAnimatorController as AnimatorController : null;
        }

        return null;
    }

    private void ExportSelected()
    {
        AnimatorController controller = ResolveController(exportSourceObject);
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Export failed", "No se pudo resolver un AnimatorController válido.", "OK");
            return;
        }

        AnimatorImportReport report = new AnimatorImportReport();
        AnimatorControllerJsonV2 data = AnimatorControllerJsonUtilityV2.Export(controller, report);
        string json = JsonUtility.ToJson(data, true);

        string savePath = EditorUtility.SaveFilePanel(
            "Save Animator JSON",
            "",
            controller.name + ".json",
            "json");

        if (string.IsNullOrEmpty(savePath))
            return;

        File.WriteAllText(savePath, json);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Export complete",
            $"JSON exportado.\n\nWarnings: {report.warnings.Count}\nErrors: {report.errors.Count}",
            "OK");

        if (report.warnings.Count > 0 || report.errors.Count > 0)
        {
            Debug.Log(report.BuildLog("Animator JSON Export Report"));
        }
    }

    private string GetOutputFolderPath()
    {
        if (importOutputFolder == null)
            return "Assets";

        string path = AssetDatabase.GetAssetPath(importOutputFolder);
        return AssetDatabase.IsValidFolder(path) ? path : "Assets";
    }

    private void ImportFromJson(string json, string outputFolder, Animator assignTarget)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            EditorUtility.DisplayDialog("Import failed", "El JSON está vacío.", "OK");
            return;
        }

        AnimatorControllerJsonV2 data;
        try
        {
            data = JsonUtility.FromJson<AnimatorControllerJsonV2>(json);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Import failed", "No se pudo parsear el JSON.", "OK");
            return;
        }

        if (data == null || string.IsNullOrWhiteSpace(data.name))
        {
            EditorUtility.DisplayDialog("Import failed", "JSON inválido o sin nombre de controller.", "OK");
            return;
        }

        string safeName = MakeSafeFileName(data.name);
        string controllerPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{safeName}.controller");

        AnimatorImportReport report = new AnimatorImportReport();

        try
        {
            AnimatorController controller = AnimatorControllerJsonUtilityV2.Import(data, controllerPath, report);

            if (assignTarget != null)
            {
                Undo.RecordObject(assignTarget, "Assign Imported Animator Controller");
                assignTarget.runtimeAnimatorController = controller;
                EditorUtility.SetDirty(assignTarget);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(controller);

            string msg =
                $"AnimatorController creado en:\n{controllerPath}\n\n" +
                $"Warnings: {report.warnings.Count}\n" +
                $"Errors: {report.errors.Count}";

            EditorUtility.DisplayDialog("Import complete", msg, "OK");

            if (report.warnings.Count > 0 || report.errors.Count > 0)
            {
                Debug.Log(report.BuildLog("Animator JSON Import Report"));
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Import failed", $"Error al crear el AnimatorController:\n{e.Message}", "OK");
        }
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return string.IsNullOrWhiteSpace(name) ? "AnimatorController_FromJson" : name;
    }
}

#region DATA

[Serializable]
public class AnimatorControllerJsonV2
{
    public string version = "2.0";
    public string name;
    public List<AnimatorParameterJsonV2> parameters = new();
    public List<AnimatorLayerJsonV2> layers = new();
}

[Serializable]
public class AnimatorParameterJsonV2
{
    public string name;
    public int type;
    public float defaultFloat;
    public int defaultInt;
    public bool defaultBool;
}

[Serializable]
public class AnimatorLayerJsonV2
{
    public string name;
    public float defaultWeight;
    public int blendingMode;
    public bool iKPass;
    public string syncedLayerAffectsTiming = "";
    public string avatarMaskAssetPath;
    public AnimatorStateMachineJsonV2 stateMachine;
}

[Serializable]
public class AnimatorStateMachineJsonV2
{
    public string path;
    public string name;
    public string defaultStatePath;

    public Vector2 anyStatePosition;
    public Vector2 entryPosition;
    public Vector2 exitPosition;
    public Vector2 parentStateMachinePosition;

    public List<BehaviourJsonV2> behaviours = new();
    public List<AnimatorStateJsonV2> states = new();
    public List<AnimatorStateMachineJsonV2> stateMachines = new();
    public List<AnimatorTransitionJsonV2> anyStateTransitions = new();
    public List<AnimatorEntryTransitionJsonV2> entryTransitions = new();
}

[Serializable]
public class AnimatorStateJsonV2
{
    public string path;
    public string name;
    public Vector2 position;

    public float speed = 1f;
    public float cycleOffset = 0f;
    public bool iKOnFeet = false;
    public bool mirror = false;
    public string tag = "";
    public bool writeDefaultValues = true;

    public MotionJsonV2 motion;
    public List<BehaviourJsonV2> behaviours = new();
    public List<AnimatorTransitionJsonV2> transitions = new();
}

[Serializable]
public class AnimatorTransitionJsonV2
{
    public string destinationStatePath;
    public string destinationStateMachinePath;
    public bool isExit;

    public bool hasExitTime;
    public float exitTime;
    public bool hasFixedDuration;
    public float duration;
    public float offset;
    public bool mute;
    public bool solo;
    public bool canTransitionToSelf;
    public int interruptionSource;
    public bool orderedInterruption;

    public List<AnimatorConditionJsonV2> conditions = new();
}

[Serializable]
public class AnimatorEntryTransitionJsonV2
{
    public string destinationStatePath;
    public string destinationStateMachinePath;
    public bool mute;
    public bool solo;
    public List<AnimatorConditionJsonV2> conditions = new();
}

[Serializable]
public class AnimatorConditionJsonV2
{
    public int mode;
    public string parameter;
    public float threshold;
}

[Serializable]
public class MotionJsonV2
{
    public string motionType; // None / AnimationClip / BlendTree

    public string assetPath;
    public BlendTreeJsonV2 blendTree;
}

[Serializable]
public class BlendTreeJsonV2
{
    public string name;
    public int blendType;
    public string blendParameter;
    public string blendParameterY;
    public bool useAutomaticThresholds;
    public float minThreshold;
    public float maxThreshold;

    public List<BlendTreeChildJsonV2> children = new();
}

[Serializable]
public class BlendTreeChildJsonV2
{
    public MotionJsonV2 motion;
    public float threshold;
    public Vector2 position;
    public float timeScale = 1f;
    public float cycleOffset = 0f;
    public string directBlendParameter;
    public bool mirror;
}

[Serializable]
public class BehaviourJsonV2
{
    public string typeName;
    public string assemblyQualifiedTypeName;
    public string editorJson;
}

#endregion

#region REPORT

[Serializable]
public class AnimatorImportReport
{
    public List<string> warnings = new();
    public List<string> errors = new();

    public void Warn(string message) => warnings.Add(message);
    public void Error(string message) => errors.Add(message);

    public string BuildLog(string title)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== {title} ===");

        if (warnings.Count > 0)
        {
            sb.AppendLine("-- Warnings --");
            foreach (var w in warnings)
                sb.AppendLine(w);
        }

        if (errors.Count > 0)
        {
            sb.AppendLine("-- Errors --");
            foreach (var e in errors)
                sb.AppendLine(e);
        }

        if (warnings.Count == 0 && errors.Count == 0)
        {
            sb.AppendLine("No issues.");
        }

        return sb.ToString();
    }
}

#endregion

public static class AnimatorControllerJsonUtilityV2
{
    private class ExportContext
    {
        public readonly Dictionary<AnimatorState, string> statePaths = new();
        public readonly Dictionary<AnimatorStateMachine, string> stateMachinePaths = new();
    }

    private class ImportContext
    {
        public AnimatorController controller;
        public AnimatorImportReport report;

        public readonly Dictionary<string, AnimatorState> stateMap = new();
        public readonly Dictionary<string, AnimatorStateMachine> stateMachineMap = new();
    }

    public static AnimatorControllerJsonV2 Export(AnimatorController controller, AnimatorImportReport report)
    {
        AnimatorControllerJsonV2 data = new AnimatorControllerJsonV2
        {
            name = controller.name
        };

        foreach (var p in controller.parameters)
        {
            data.parameters.Add(new AnimatorParameterJsonV2
            {
                name = p.name,
                type = (int)p.type,
                defaultFloat = p.defaultFloat,
                defaultInt = p.defaultInt,
                defaultBool = p.defaultBool
            });
        }

        foreach (var layer in controller.layers)
        {
            ExportContext ctx = new ExportContext();
            RegisterStateMachineRecursive(layer.stateMachine, $"Layer:{layer.name}", ctx);

            AnimatorLayerJsonV2 layerJson = new AnimatorLayerJsonV2
            {
                name = layer.name,
                defaultWeight = layer.defaultWeight,
                blendingMode = (int)layer.blendingMode,
                iKPass = layer.iKPass,
                avatarMaskAssetPath = layer.avatarMask != null ? AssetDatabase.GetAssetPath(layer.avatarMask) : "",
                stateMachine = ExportStateMachineRecursive(layer.stateMachine, ctx, report)
            };

            data.layers.Add(layerJson);
        }

        return data;
    }

    public static AnimatorController Import(AnimatorControllerJsonV2 data, string controllerPath, AnimatorImportReport report)
    {
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        foreach (var p in controller.parameters.ToArray())
            controller.RemoveParameter(p);

        while (controller.layers.Length > 0)
            controller.RemoveLayer(0);

        ImportContext ctx = new ImportContext
        {
            controller = controller,
            report = report
        };

        // parameters
        foreach (var p in data.parameters)
        {
            try
            {
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = p.name,
                    type = (AnimatorControllerParameterType)p.type,
                    defaultFloat = p.defaultFloat,
                    defaultInt = p.defaultInt,
                    defaultBool = p.defaultBool
                });
            }
            catch (Exception e)
            {
                report.Error($"No se pudo agregar parámetro '{p.name}': {e.Message}");
            }
        }

        // layers + base graph
        foreach (var layerJson in data.layers)
        {
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = string.IsNullOrWhiteSpace(layerJson.name) ? "Layer" : layerJson.name,
                defaultWeight = layerJson.defaultWeight,
                blendingMode = (AnimatorLayerBlendingMode)layerJson.blendingMode,
                iKPass = layerJson.iKPass,
                stateMachine = new AnimatorStateMachine()
            };

            AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
            layer.stateMachine.name = string.IsNullOrWhiteSpace(layerJson.stateMachine?.name)
                ? $"{layer.name}_StateMachine"
                : layerJson.stateMachine.name;

            if (!string.IsNullOrEmpty(layerJson.avatarMaskAssetPath))
            {
                layer.avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(layerJson.avatarMaskAssetPath);
                if (layer.avatarMask == null)
                    report.Warn($"AvatarMask faltante en layer '{layer.name}': {layerJson.avatarMaskAssetPath}");
            }

            controller.AddLayer(layer);

            if (layerJson.stateMachine != null)
            {
                ctx.stateMachineMap[layerJson.stateMachine.path] = layer.stateMachine;
                BuildStateMachineRecursive(layer.stateMachine, layerJson.stateMachine, ctx);
            }
        }

        // finalize transitions / defaults
        foreach (var layerJson in data.layers)
        {
            if (layerJson.stateMachine != null &&
                ctx.stateMachineMap.TryGetValue(layerJson.stateMachine.path, out var rootSm))
            {
                FinalizeStateMachineRecursive(rootSm, layerJson.stateMachine, ctx);
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    #region EXPORT

    private static void RegisterStateMachineRecursive(AnimatorStateMachine sm, string currentPath, ExportContext ctx)
    {
        ctx.stateMachinePaths[sm] = currentPath;

        ChildAnimatorState[] states = sm.states;
        for (int i = 0; i < states.Length; i++)
        {
            ctx.statePaths[states[i].state] = $"{currentPath}/S:{states[i].state.name}#{i}";
        }

        ChildAnimatorStateMachine[] sms = sm.stateMachines;
        for (int i = 0; i < sms.Length; i++)
        {
            string subPath = $"{currentPath}/SM:{sms[i].stateMachine.name}#{i}";
            RegisterStateMachineRecursive(sms[i].stateMachine, subPath, ctx);
        }
    }

    private static AnimatorStateMachineJsonV2 ExportStateMachineRecursive(
        AnimatorStateMachine sm,
        ExportContext ctx,
        AnimatorImportReport report)
    {
        AnimatorStateMachineJsonV2 json = new AnimatorStateMachineJsonV2
        {
            path = ctx.stateMachinePaths.TryGetValue(sm, out var path) ? path : sm.name,
            name = sm.name,
            anyStatePosition = sm.anyStatePosition,
            entryPosition = sm.entryPosition,
            exitPosition = sm.exitPosition,
            parentStateMachinePosition = sm.parentStateMachinePosition,
            behaviours = ExportBehaviours(sm.behaviours, report, $"StateMachine '{sm.name}'")
        };

        if (sm.defaultState != null && ctx.statePaths.TryGetValue(sm.defaultState, out string defaultStatePath))
            json.defaultStatePath = defaultStatePath;

        foreach (var child in sm.states)
        {
            AnimatorState state = child.state;
            AnimatorStateJsonV2 stateJson = new AnimatorStateJsonV2
            {
                path = ctx.statePaths.TryGetValue(state, out var statePath) ? statePath : state.name,
                name = state.name,
                position = child.position,
                speed = state.speed,
                cycleOffset = state.cycleOffset,
                iKOnFeet = state.iKOnFeet,
                mirror = state.mirror,
                tag = state.tag,
                writeDefaultValues = state.writeDefaultValues,
                motion = ExportMotion(state.motion, report, $"State '{state.name}'"),
                behaviours = ExportBehaviours(state.behaviours, report, $"State '{state.name}'")
            };

            foreach (var t in state.transitions)
                stateJson.transitions.Add(ExportTransition(t, ctx));

            json.states.Add(stateJson);
        }

        foreach (var childSm in sm.stateMachines)
        {
            var subJson = ExportStateMachineRecursive(childSm.stateMachine, ctx, report);
            subJson.parentStateMachinePosition = childSm.position;
            json.stateMachines.Add(subJson);
        }

        foreach (var t in sm.anyStateTransitions)
            json.anyStateTransitions.Add(ExportTransition(t, ctx));

        foreach (var t in sm.entryTransitions)
            json.entryTransitions.Add(ExportEntryTransition(t, ctx));

        return json;
    }

    private static List<BehaviourJsonV2> ExportBehaviours(
        StateMachineBehaviour[] behaviours,
        AnimatorImportReport report,
        string ownerLabel)
    {
        List<BehaviourJsonV2> list = new List<BehaviourJsonV2>();

        if (behaviours == null)
            return list;

        foreach (var b in behaviours)
        {
            if (b == null)
                continue;

            try
            {
                list.Add(new BehaviourJsonV2
                {
                    typeName = b.GetType().FullName,
                    assemblyQualifiedTypeName = b.GetType().AssemblyQualifiedName,
                    editorJson = EditorJsonUtility.ToJson(b, true)
                });
            }
            catch (Exception e)
            {
                report.Warn($"No se pudo exportar behaviour '{b.GetType().FullName}' en {ownerLabel}: {e.Message}");
            }
        }

        return list;
    }

    private static MotionJsonV2 ExportMotion(Motion motion, AnimatorImportReport report, string ownerLabel)
    {
        if (motion == null)
            return new MotionJsonV2 { motionType = "None" };

        if (motion is AnimationClip clip)
        {
            string assetPath = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(assetPath))
            {
                report.Warn($"Motion clip sin asset path en {ownerLabel}: '{clip.name}'");
            }

            return new MotionJsonV2
            {
                motionType = "AnimationClip",
                assetPath = assetPath
            };
        }

        if (motion is BlendTree bt)
        {
            return new MotionJsonV2
            {
                motionType = "BlendTree",
                blendTree = ExportBlendTree(bt, report, ownerLabel)
            };
        }

        report.Warn($"Motion desconocido/no soportado en {ownerLabel}: {motion.GetType().FullName}");
        return new MotionJsonV2 { motionType = "None" };
    }

    private static BlendTreeJsonV2 ExportBlendTree(BlendTree bt, AnimatorImportReport report, string ownerLabel)
    {
        BlendTreeJsonV2 json = new BlendTreeJsonV2
        {
            name = bt.name,
            blendType = (int)bt.blendType,
            blendParameter = bt.blendParameter,
            blendParameterY = bt.blendParameterY,
            useAutomaticThresholds = bt.useAutomaticThresholds,
            minThreshold = bt.minThreshold,
            maxThreshold = bt.maxThreshold
        };

        foreach (var child in bt.children)
        {
            json.children.Add(new BlendTreeChildJsonV2
            {
                motion = ExportMotion(child.motion, report, $"{ownerLabel} -> BlendTree '{bt.name}'"),
                threshold = child.threshold,
                position = child.position,
                timeScale = child.timeScale,
                cycleOffset = child.cycleOffset,
                directBlendParameter = child.directBlendParameter,
                mirror = child.mirror
            });
        }

        return json;
    }

    private static AnimatorTransitionJsonV2 ExportTransition(AnimatorStateTransition t, ExportContext ctx)
    {
        AnimatorTransitionJsonV2 json = new AnimatorTransitionJsonV2
        {
            isExit = t.isExit,
            hasExitTime = t.hasExitTime,
            exitTime = t.exitTime,
            hasFixedDuration = t.hasFixedDuration,
            duration = t.duration,
            offset = t.offset,
            mute = t.mute,
            solo = t.solo,
            canTransitionToSelf = t.canTransitionToSelf,
            interruptionSource = (int)t.interruptionSource,
            orderedInterruption = t.orderedInterruption
        };

        if (t.destinationState != null && ctx.statePaths.TryGetValue(t.destinationState, out string stPath))
            json.destinationStatePath = stPath;

        if (t.destinationStateMachine != null && ctx.stateMachinePaths.TryGetValue(t.destinationStateMachine, out string smPath))
            json.destinationStateMachinePath = smPath;

        foreach (var c in t.conditions)
        {
            json.conditions.Add(new AnimatorConditionJsonV2
            {
                mode = (int)c.mode,
                parameter = c.parameter,
                threshold = c.threshold
            });
        }

        return json;
    }

    private static AnimatorEntryTransitionJsonV2 ExportEntryTransition(AnimatorTransition t, ExportContext ctx)
    {
        AnimatorEntryTransitionJsonV2 json = new AnimatorEntryTransitionJsonV2
        {
            mute = t.mute,
            solo = t.solo
        };

        if (t.destinationState != null && ctx.statePaths.TryGetValue(t.destinationState, out string stPath))
            json.destinationStatePath = stPath;

        if (t.destinationStateMachine != null && ctx.stateMachinePaths.TryGetValue(t.destinationStateMachine, out string smPath))
            json.destinationStateMachinePath = smPath;

        foreach (var c in t.conditions)
        {
            json.conditions.Add(new AnimatorConditionJsonV2
            {
                mode = (int)c.mode,
                parameter = c.parameter,
                threshold = c.threshold
            });
        }

        return json;
    }

    #endregion

    #region IMPORT

    private static void BuildStateMachineRecursive(
        AnimatorStateMachine sm,
        AnimatorStateMachineJsonV2 json,
        ImportContext ctx)
    {
        sm.name = string.IsNullOrWhiteSpace(json.name) ? sm.name : json.name;
        sm.anyStatePosition = json.anyStatePosition;
        sm.entryPosition = json.entryPosition;
        sm.exitPosition = json.exitPosition;
        sm.parentStateMachinePosition = json.parentStateMachinePosition;

        ImportBehavioursToStateMachine(sm, json.behaviours, ctx);

        foreach (var stateJson in json.states)
        {
            AnimatorState state = sm.AddState(
                string.IsNullOrWhiteSpace(stateJson.name) ? "State" : stateJson.name,
                stateJson.position);

            state.speed = stateJson.speed;
            state.cycleOffset = stateJson.cycleOffset;
            state.iKOnFeet = stateJson.iKOnFeet;
            state.mirror = stateJson.mirror;
            state.tag = stateJson.tag ?? string.Empty;
            state.writeDefaultValues = stateJson.writeDefaultValues;
            state.motion = ImportMotion(stateJson.motion, ctx, $"State '{stateJson.name}'");

            ImportBehavioursToState(state, stateJson.behaviours, ctx);

            ctx.stateMap[stateJson.path] = state;
        }

        foreach (var subJson in json.stateMachines)
        {
            AnimatorStateMachine childSm = sm.AddStateMachine(
                string.IsNullOrWhiteSpace(subJson.name) ? "StateMachine" : subJson.name,
                subJson.parentStateMachinePosition);

            childSm.anyStatePosition = subJson.anyStatePosition;
            childSm.entryPosition = subJson.entryPosition;
            childSm.exitPosition = subJson.exitPosition;
            childSm.parentStateMachinePosition = subJson.parentStateMachinePosition;

            ctx.stateMachineMap[subJson.path] = childSm;
            BuildStateMachineRecursive(childSm, subJson, ctx);
        }
    }

    private static void FinalizeStateMachineRecursive(
        AnimatorStateMachine sm,
        AnimatorStateMachineJsonV2 json,
        ImportContext ctx)
    {
        if (!string.IsNullOrEmpty(json.defaultStatePath) &&
            ctx.stateMap.TryGetValue(json.defaultStatePath, out var defaultState))
        {
            sm.defaultState = defaultState;
        }

        foreach (var tJson in json.anyStateTransitions)
        {
            AnimatorStateTransition t = null;

            if (!string.IsNullOrEmpty(tJson.destinationStatePath) &&
                ctx.stateMap.TryGetValue(tJson.destinationStatePath, out var dstState))
            {
                t = sm.AddAnyStateTransition(dstState);
            }
            else if (!string.IsNullOrEmpty(tJson.destinationStateMachinePath) &&
                     ctx.stateMachineMap.TryGetValue(tJson.destinationStateMachinePath, out var dstSm))
            {
                t = sm.AddAnyStateTransition(dstSm);
            }
            else
            {
                ctx.report.Warn($"AnyState transition sin destino válido en state machine '{json.name}'.");
            }

            if (t != null)
                ApplyStateTransitionData(t, tJson);
        }

        foreach (var tJson in json.entryTransitions)
        {
            AnimatorTransition t = null;

            if (!string.IsNullOrEmpty(tJson.destinationStatePath) &&
                ctx.stateMap.TryGetValue(tJson.destinationStatePath, out var dstState))
            {
                t = sm.AddEntryTransition(dstState);
            }
            else if (!string.IsNullOrEmpty(tJson.destinationStateMachinePath) &&
                     ctx.stateMachineMap.TryGetValue(tJson.destinationStateMachinePath, out var dstSm))
            {
                t = sm.AddEntryTransition(dstSm);
            }
            else
            {
                ctx.report.Warn($"Entry transition sin destino válido en state machine '{json.name}'.");
            }

            if (t != null)
            {
                t.mute = tJson.mute;
                t.solo = tJson.solo;

                foreach (var c in tJson.conditions)
                {
                    t.AddCondition((AnimatorConditionMode)c.mode, c.threshold, c.parameter);
                }
            }
        }

        foreach (var stateJson in json.states)
        {
            if (!ctx.stateMap.TryGetValue(stateJson.path, out var state))
                continue;

            foreach (var tJson in stateJson.transitions)
            {
                AnimatorStateTransition t = null;

                if (tJson.isExit)
                {
                    t = state.AddExitTransition();
                }
                else if (!string.IsNullOrEmpty(tJson.destinationStatePath) &&
                         ctx.stateMap.TryGetValue(tJson.destinationStatePath, out var dstState))
                {
                    t = state.AddTransition(dstState);
                }
                else
                {
                    ctx.report.Warn($"Transition sin destino válido desde state '{stateJson.name}'.");
                }

                if (t != null)
                    ApplyStateTransitionData(t, tJson);
            }
        }

        foreach (var subJson in json.stateMachines)
        {
            if (ctx.stateMachineMap.TryGetValue(subJson.path, out var childSm))
                FinalizeStateMachineRecursive(childSm, subJson, ctx);
        }
    }

    private static void ImportBehavioursToState(AnimatorState state, List<BehaviourJsonV2> behaviours, ImportContext ctx)
    {
        if (behaviours == null)
            return;

        foreach (var b in behaviours)
        {
            try
            {
                Type type = ResolveType(b);
                if (type == null)
                {
                    ctx.report.Warn($"No se encontró type para behaviour '{b.typeName}' en state '{state.name}'.");
                    continue;
                }

                if (!typeof(StateMachineBehaviour).IsAssignableFrom(type))
                {
                    ctx.report.Warn($"El type '{type.FullName}' no hereda de StateMachineBehaviour.");
                    continue;
                }

                StateMachineBehaviour instance = state.AddStateMachineBehaviour(type);
                if (instance == null)
                {
                    ctx.report.Warn($"No se pudo instanciar behaviour '{type.FullName}' en state '{state.name}'.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(b.editorJson))
                {
                    EditorJsonUtility.FromJsonOverwrite(b.editorJson, instance);
                }

                EditorUtility.SetDirty(instance);
            }
            catch (Exception e)
            {
                ctx.report.Warn($"Error importando behaviour '{b.typeName}' en state '{state.name}': {e.Message}");
            }
        }
    }

    private static void ImportBehavioursToStateMachine(AnimatorStateMachine sm, List<BehaviourJsonV2> behaviours, ImportContext ctx)
    {
        if (behaviours == null)
            return;

        foreach (var b in behaviours)
        {
            try
            {
                Type type = ResolveType(b);
                if (type == null)
                {
                    ctx.report.Warn($"No se encontró type para behaviour '{b.typeName}' en state machine '{sm.name}'.");
                    continue;
                }

                if (!typeof(StateMachineBehaviour).IsAssignableFrom(type))
                {
                    ctx.report.Warn($"El type '{type.FullName}' no hereda de StateMachineBehaviour.");
                    continue;
                }

                StateMachineBehaviour instance = sm.AddStateMachineBehaviour(type);
                if (instance == null)
                {
                    ctx.report.Warn($"No se pudo instanciar behaviour '{type.FullName}' en state machine '{sm.name}'.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(b.editorJson))
                {
                    EditorJsonUtility.FromJsonOverwrite(b.editorJson, instance);
                }

                EditorUtility.SetDirty(instance);
            }
            catch (Exception e)
            {
                ctx.report.Warn($"Error importando behaviour '{b.typeName}' en state machine '{sm.name}': {e.Message}");
            }
        }
    }

    private static Type ResolveType(BehaviourJsonV2 b)
    {
        if (!string.IsNullOrWhiteSpace(b.assemblyQualifiedTypeName))
        {
            Type t = Type.GetType(b.assemblyQualifiedTypeName);
            if (t != null)
                return t;
        }

        if (!string.IsNullOrWhiteSpace(b.typeName))
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(b.typeName);
                if (t != null)
                    return t;
            }
        }

        return null;
    }

    private static Motion ImportMotion(MotionJsonV2 motionJson, ImportContext ctx, string ownerLabel)
    {
        if (motionJson == null || string.IsNullOrEmpty(motionJson.motionType) || motionJson.motionType == "None")
            return null;

        if (motionJson.motionType == "AnimationClip")
        {
            if (string.IsNullOrEmpty(motionJson.assetPath))
            {
                ctx.report.Warn($"Clip sin path en {ownerLabel}.");
                return null;
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(motionJson.assetPath);
            if (clip == null)
            {
                ctx.report.Warn($"Clip faltante en {ownerLabel}: {motionJson.assetPath}");
                return null;
            }

            return clip;
        }

        if (motionJson.motionType == "BlendTree")
        {
            return ImportBlendTree(motionJson.blendTree, ctx, ownerLabel);
        }

        ctx.report.Warn($"Motion type no soportado '{motionJson.motionType}' en {ownerLabel}.");
        return null;
    }

    private static BlendTree ImportBlendTree(BlendTreeJsonV2 json, ImportContext ctx, string ownerLabel)
    {
        if (json == null)
            return null;

        BlendTree blendTree = new BlendTree
        {
            name = string.IsNullOrWhiteSpace(json.name) ? "BlendTree" : json.name,
            blendType = (BlendTreeType)json.blendType,
            blendParameter = json.blendParameter ?? string.Empty,
            blendParameterY = json.blendParameterY ?? string.Empty,
            useAutomaticThresholds = json.useAutomaticThresholds,
            minThreshold = json.minThreshold,
            maxThreshold = json.maxThreshold
        };

        AssetDatabase.AddObjectToAsset(blendTree, ctx.controller);

        foreach (var childJson in json.children)
        {
            Motion childMotion = ImportMotion(childJson.motion, ctx, $"{ownerLabel} -> BlendTree '{blendTree.name}'");
            if (childMotion == null)
                continue;

            blendTree.AddChild(childMotion, childJson.threshold);

            ChildMotion[] children = blendTree.children;
            int index = children.Length - 1;

            ChildMotion child = children[index];
            child.threshold = childJson.threshold;
            child.position = childJson.position;
            child.timeScale = childJson.timeScale;
            child.cycleOffset = childJson.cycleOffset;
            child.directBlendParameter = childJson.directBlendParameter ?? string.Empty;
            child.mirror = childJson.mirror;

            children[index] = child;
            blendTree.children = children;
        }

        EditorUtility.SetDirty(blendTree);
        return blendTree;
    }

    private static void ApplyStateTransitionData(AnimatorStateTransition t, AnimatorTransitionJsonV2 json)
    {
        t.hasExitTime = json.hasExitTime;
        t.exitTime = json.exitTime;
        t.hasFixedDuration = json.hasFixedDuration;
        t.duration = json.duration;
        t.offset = json.offset;
        t.mute = json.mute;
        t.solo = json.solo;
        t.canTransitionToSelf = json.canTransitionToSelf;
        t.interruptionSource = (TransitionInterruptionSource)json.interruptionSource;
        t.orderedInterruption = json.orderedInterruption;

        foreach (var c in json.conditions)
        {
            t.AddCondition((AnimatorConditionMode)c.mode, c.threshold, c.parameter);
        }
    }

    #endregion
}
#endif