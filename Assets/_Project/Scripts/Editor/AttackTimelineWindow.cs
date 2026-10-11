using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AttackTimelineWindow : EditorWindow
{
    private Vector2 scroll;
    [MenuItem("Tools/Notebook Dodge/Attack Timeline")]
    private static void Open() => GetWindow<AttackTimelineWindow>("Attack Timeline");
    private static void Field(SerializedProperty cue, string name, string label)
        => EditorGUILayout.PropertyField(cue.FindPropertyRelative(name), new GUIContent(label));
    private void OnInspectorUpdate() => Repaint();
    private void OnGUI()
    {
        var controller = FindObjectOfType<AttackTimelineController>();
        if (controller == null)
        {
            EditorGUILayout.HelpBox("Open Level_test to use the Attack Timeline controller.", MessageType.Info);
            return;
        }
        EditorGUILayout.LabelField("Loop1", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(controller.Status, $"{controller.Elapsed:F1} / {controller.Duration:F1} s");
        EditorGUILayout.HelpBox("Tracks share an emitter template. Each clip owns placement, timing, and attack settings. Clips on one track run in time order; use separate tracks for simultaneous emitters.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Play Loop"))
            {
                if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
                else controller.Begin();
            }
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button(EditorApplication.isPaused ? "Resume" : "Pause")) EditorApplication.isPaused = !EditorApplication.isPaused;
                if (GUILayout.Button("Stop & Clear")) controller.StopAndClear();
                if (GUILayout.Button("Restart"))
                {
                    EditorApplication.isPaused = false;
                    EditorSceneManager.LoadSceneInPlayMode(controller.gameObject.scene.path, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                }
            }
        }
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.LabelField("Active Bounce Bullets", FindObjectsOfType<BounceBullet>().Length.ToString());
            EditorGUILayout.LabelField("Active Trace Bullets", FindObjectsOfType<TraceBullet>().Length.ToString());
            EditorGUILayout.LabelField("Active Lasers", FindObjectsOfType<LaserBeam>().Length.ToString());
        }
        EditorGUILayout.HelpBox("Move keeps the previous emitter on screen and moves it to the next clip. Reenter exits and creates a new instance. Transitions wait for active warnings or lasers; missed clips are skipped, not replayed. First Shot Delay starts after entry.", MessageType.None);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            var serialized = new SerializedObject(controller);
            serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty("player"));
            EditorGUILayout.PropertyField(serialized.FindProperty("duration"));
            EditorGUILayout.PropertyField(serialized.FindProperty("playOnStart"));
            var tracks = serialized.FindProperty("tracks");
            for (int t = 0; t < tracks.arraySize; t++)
            {
                var track = tracks.GetArrayElementAtIndex(t);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    track.isExpanded = EditorGUILayout.Foldout(track.isExpanded, track.FindPropertyRelative("label").stringValue, true);
                    if (!track.isExpanded) continue;
                    Field(track, "label", "Track Name");
                    Field(track, "enabled", "Enabled");
                    Field(track, "emitterTemplate", "Emitter Template");
                    var template = track.FindPropertyRelative("emitterTemplate").objectReferenceValue;
                    if (GUILayout.Button("Select Emitter / Artwork")) Selection.activeObject = template;
                    var clips = track.FindPropertyRelative("clips");
                    for (int c = 0; c < clips.arraySize; c++)
                    {
                        var clip = clips.GetArrayElementAtIndex(c);
                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        {
                            clip.isExpanded = EditorGUILayout.Foldout(clip.isExpanded, clip.FindPropertyRelative("label").stringValue, true);
                            if (!clip.isExpanded) continue;
                            Field(clip, "label", "Clip Name");
                            Field(clip, "enabled", "Enabled");
                            EditorGUILayout.LabelField("Entry & Placement", EditorStyles.boldLabel);
                            Field(clip, "appearAt", "Appear At (s)");
                            Field(clip, "transition", "Transition From Previous");
                            Field(clip, "entrySide", "Entry / Exit Side");
                            Field(clip, "enterDuration", "Entry / Move Duration (s)");
                            Field(clip, "position", "Position");
                            Field(clip, "rotationZ", "Rotation Z");
                            EditorGUILayout.LabelField("Firing Window", EditorStyles.boldLabel);
                            Field(clip, "firstShotDelay", "First Shot Delay (s)");
                            Field(clip, "stopAt", "Stop At (s)");
                            Field(clip, "exitDuration", "Exit Duration (s)");
                            EditorGUILayout.LabelField("Attack Settings", EditorStyles.boldLabel);
                            AttackSettingsGUI.Draw(clip.FindPropertyRelative("attack"), template);
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button("Duplicate Clip"))
                                {
                                    clips.InsertArrayElementAtIndex(c);
                                    var copy = clips.GetArrayElementAtIndex(c + 1);
                                    float start = copy.FindPropertyRelative("appearAt").floatValue;
                                    float stop = copy.FindPropertyRelative("stopAt").floatValue;
                                    copy.FindPropertyRelative("appearAt").floatValue = stop;
                                    copy.FindPropertyRelative("stopAt").floatValue = stop + (stop - start);
                                    copy.FindPropertyRelative("label").stringValue += " Copy";
                                    break;
                                }
                                if (GUILayout.Button("Remove Clip")) { clips.DeleteArrayElementAtIndex(c); break; }
                            }
                        }
                    }
                    if (GUILayout.Button("Add Clip"))
                    {
                        int index = clips.arraySize++;
                        var clip = clips.GetArrayElementAtIndex(index);
                        // Reset copied Unity array data to explicit defaults.
                        clip.FindPropertyRelative("label").stringValue = "New Clip";
                        clip.FindPropertyRelative("enabled").boolValue = true;
                        clip.FindPropertyRelative("appearAt").floatValue = 0;
                        clip.FindPropertyRelative("stopAt").floatValue = controller.Duration;
                        clip.FindPropertyRelative("position").vector3Value = Vector3.zero;
                        clip.FindPropertyRelative("rotationZ").floatValue = 0;
                        clip.FindPropertyRelative("entrySide").enumValueIndex = 0;
                        clip.FindPropertyRelative("transition").enumValueIndex = 0;
                        clip.FindPropertyRelative("enterDuration").floatValue = 0.5f;
                        clip.FindPropertyRelative("exitDuration").floatValue = 0.5f;
                        clip.FindPropertyRelative("firstShotDelay").floatValue = 0;
                        AttackSettingsGUI.Reset(clip.FindPropertyRelative("attack"), template);
                        clip.isExpanded = true;
                    }
                    if (GUILayout.Button("Remove Track")) { tracks.DeleteArrayElementAtIndex(t); break; }
                }
            }
            if (GUILayout.Button("Add Track"))
            {
                int index = tracks.arraySize++;
                var track = tracks.GetArrayElementAtIndex(index);
                track.FindPropertyRelative("label").stringValue = "New Track";
                track.FindPropertyRelative("enabled").boolValue = true;
                track.FindPropertyRelative("emitterTemplate").objectReferenceValue = null;
                track.FindPropertyRelative("clips").ClearArray();
                track.isExpanded = true;
            }
            if (serialized.ApplyModifiedProperties()) EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }
        EditorGUILayout.EndScrollView();
        string error = controller.Validate();
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
    }
}
