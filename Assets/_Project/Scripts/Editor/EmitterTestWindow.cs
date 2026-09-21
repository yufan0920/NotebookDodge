// 放在 Editor 文件夹：仅供 Unity 编辑器测试，不会编译进游戏包。
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class EmitterTestWindow : EditorWindow
{
    private readonly List<MonoBehaviour> emitters = new List<MonoBehaviour>();
    private Vector2 scroll;

    [MenuItem("Tools/Notebook Dodge/Emitter Test Panel")]
    private static void Open()
    {
        GetWindow<EmitterTestWindow>("Emitter Tests");
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.hierarchyChanged += Refresh;
        Undo.undoRedoPerformed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.hierarchyChanged -= Refresh;
        Undo.undoRedoPerformed -= Refresh;
    }

    private void OnPlayModeChanged(PlayModeStateChange state) => Refresh();

    private void Refresh()
    {
        emitters.Clear();
        // 包含停用的对象，但排除 Project 中的 Prefab 资产及未加载的场景。
        foreach (MonoBehaviour component in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (component == null || !component.gameObject.scene.IsValid() ||
                !component.gameObject.scene.isLoaded || EditorUtility.IsPersistent(component)) continue;
            if (component is SingleShotEmitter || component is TraceEmitter)
                emitters.Add(component);
        }
        emitters.Sort((a, b) => string.CompareOrdinal(GetPath(a.transform), GetPath(b.transform)));
        Repaint();
    }

    private static string GetPath(Transform item)
    {
        string path = item.name;
        while (item.parent != null)
        {
            item = item.parent;
            path = item.name + "/" + path;
        }
        return path;
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("可在 Play 前配置开关，Ctrl+S 保存到场景；支持 Ctrl+Z 撤销。\nPlay 中也可切换，但运行中的调整通常不会保留。关闭发射不会删除已有子弹。", MessageType.Info);
        if (GUILayout.Button("刷新列表 / Refresh")) Refresh();

        bool canEdit = !EditorApplication.isCompiling &&
            EditorApplication.isPlaying == EditorApplication.isPlayingOrWillChangePlaymode;
        using (new EditorGUI.DisabledScope(!canEdit))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全部开启")) SetAll(true);
            if (GUILayout.Button("全部关闭")) SetAll(false);
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                if (GUILayout.Button("清除场上子弹和追踪预警")) ClearProjectiles();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (MonoBehaviour emitter in emitters)
            {
                if (emitter == null) continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(GetPath(emitter.transform), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(emitter.GetType().Name);
                EditorGUILayout.BeginHorizontal();
                // 不擅自开启被隐藏的父层级，避免连带启用其他测试对象。
                using (new EditorGUI.DisabledScope(!emitter.gameObject.activeInHierarchy))
                {
                    bool next = EditorGUILayout.ToggleLeft("发射", emitter.enabled);
                    if (next != emitter.enabled) SetEmitterEnabled(emitter, next);
                    if (GUILayout.Button("只测试这个"))
                    {
                        Undo.IncrementCurrentGroup();
                        int undoGroup = Undo.GetCurrentGroup();
                        Undo.SetCurrentGroupName("Solo Test Emitter");
                        foreach (MonoBehaviour item in emitters)
                            SetEmitterEnabled(item, item == emitter);
                        if (EditorApplication.isPlaying) ClearProjectiles();
                        Undo.CollapseUndoOperations(undoGroup);
                    }
                }
                if (GUILayout.Button("选中")) Selection.activeGameObject = emitter.gameObject;
                EditorGUILayout.EndHorizontal();
                if (!emitter.gameObject.activeInHierarchy)
                    EditorGUILayout.LabelField("对象或父对象未启用，请先在 Hierarchy 勾选启用。", EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }
        if (emitters.Count == 0) EditorGUILayout.LabelField("未找到 SingleShotEmitter 或 TraceEmitter。");
    }

    private void SetAll(bool enabled)
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Toggle All Test Emitters");
        foreach (MonoBehaviour emitter in emitters)
            SetEmitterEnabled(emitter, enabled);
        Undo.CollapseUndoOperations(undoGroup);
    }

    private static void SetEmitterEnabled(MonoBehaviour emitter, bool enabled)
    {
        if (emitter == null || emitter.enabled == enabled) return;
        if (!EditorApplication.isPlaying) Undo.RecordObject(emitter, "Toggle Test Emitter");
        emitter.enabled = enabled;
        if (!EditorApplication.isPlaying)
        {
            // 保存场景中的组件状态，同时正确记录 Prefab 实例覆盖。
            if (PrefabUtility.IsPartOfPrefabInstance(emitter))
                PrefabUtility.RecordPrefabInstancePropertyModifications(emitter);
            EditorSceneManager.MarkSceneDirty(emitter.gameObject.scene);
        }
    }

    private static void ClearProjectiles()
    {
        if (!EditorApplication.isPlaying) return;
        // 仅清理场景中的子弹；TraceBullet.OnDestroy 会同时清理自己的预警和材质。
        foreach (MonoBehaviour component in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (component == null || !component.gameObject.scene.IsValid() ||
                !component.gameObject.scene.isLoaded || EditorUtility.IsPersistent(component)) continue;
            if (component is Bullet || component is TraceBullet)
                Object.Destroy(component.gameObject);
        }
    }
}
