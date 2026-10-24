using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One overview row is one independent emitter track; sorting does not change serialized order.
public class AttackTimelineWindow : EditorWindow
{
    [SerializeField] private int selectedTrack;
    [SerializeField] private int timeView;
    [SerializeField] private bool phasesOpen;
    private float phase2Start, phase3Start, viewStart, viewEnd, fullDuration;
    private string ViewName => timeView == 0 ? "Global" : "P" + timeView;
    [SerializeField] private bool playRequested;
    [SerializeField] private bool rangeOpen;
    [SerializeField] private float testFrom, testTo = 30;
    [SerializeField] private bool requestedRange;
    [SerializeField] private float requestedFrom, requestedTo;
    private string requestedScenePath;
    private Vector2 overviewScroll, detailScroll;
    private bool setupOpen, motionOpen, randomOpen, timingOpen, debugOpen;
    private string pointPreview;
    private struct Row { public int track; public float time; }

    [MenuItem("Tools/Notebook Dodge/Attack Timeline")]
    private static void Open() => GetWindow<AttackTimelineWindow>("Attack Timeline");
    private void OnInspectorUpdate() => Repaint();
    private void OnEnable()
    {
        Undo.undoRedoPerformed += Refresh;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }
    private void OnDisable()
    {
        Undo.undoRedoPerformed -= Refresh;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.delayCall -= ReloadForRequest;
        EditorApplication.delayCall -= BeginLoadedLoop;
    }
    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && playRequested)
        {
            BeginLoadedLoop();
        }
        if (state == PlayModeStateChange.EnteredEditMode) playRequested = false;
    }
    private void BeginLoadedLoop()
    {
        if (!EditorApplication.isPlaying || !playRequested) return;
        playRequested = false;
        var controller = FindObjectOfType<AttackTimelineController>();
        if (controller == null) return;
        if (requestedRange) controller.BeginRange(requestedFrom, requestedTo);
        else controller.Begin();
    }

    private void RequestPlay(AttackTimelineController controller, bool range, float from = 0, float to = 0)
    {
        requestedRange = range; requestedFrom = from; requestedTo = to;
        playRequested = true;
        EditorApplication.isPaused = false;
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        else
        {
            requestedScenePath = controller.gameObject.scene.path;
            // Finish this GUI pass and apply edits before replacing its serialized scene objects.
            EditorApplication.delayCall -= ReloadForRequest;
            EditorApplication.delayCall += ReloadForRequest;
        }
    }

    private void ReloadForRequest()
    {
        if (!EditorApplication.isPlaying || !playRequested) return;
        var controller = FindObjectOfType<AttackTimelineController>();
        if (controller != null) controller.StopAndClear();
        EditorSceneManager.LoadSceneInPlayMode(requestedScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        EditorApplication.delayCall -= BeginLoadedLoop;
        EditorApplication.delayCall += BeginLoadedLoop;
    }

    private void DrawRangeTest(AttackTimelineController controller)
    {
        rangeOpen = EditorGUILayout.Foldout(rangeOpen, "Range Test (Empty Scene)", true);
        if (!rangeOpen) return;
        EditorGUILayout.LabelField("Earlier attacks and hazards are omitted.", EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            float oldWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 78;
            testFrom = TimeValue(testFrom, "Test From (s)", "Inclusive. Earlier attacks are not replayed.");
            testTo = TimeValue(testTo, "Test To (s)", "Exclusive. Attacks are cleared and emitters exit.");
            EditorGUIUtility.labelWidth = oldWidth;
        }
        EditorGUILayout.LabelField("Global range: " + GlobalLabel(testFrom) + " - " + GlobalLabel(testTo), EditorStyles.miniLabel);
        if (timeView != 0 && GUILayout.Button("Use " + ViewName + " Range"))
        { testFrom = viewStart; testTo = viewEnd; GUI.FocusControl(null); }
        string error = AttackTimelineController.RangeError(testFrom, testTo, fullDuration);
        using (new EditorGUI.DisabledScope(error != null))
            if (GUILayout.Button("Play Range")) RequestPlay(controller, true, testFrom, testTo);
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
    }
    private void Refresh() { pointPreview = null; Repaint(); }
    private static SerializedProperty P(SerializedProperty parent, string name) => parent.FindPropertyRelative(name);
    private static void Field(SerializedProperty parent, string name, string label, string tip = null)
        => EditorGUILayout.PropertyField(P(parent, name), new GUIContent(label, tip), true);
    private static string Seconds(float value) => value.ToString("0.##");

    private static string GlobalLabel(float value) => Seconds(value) + "s (" + AttackTimelineTimeView.Clock(value) + ")";
    private string DualTime(float global)
    {
        int phase = timeView == 0 ? AttackTimelineTimeView.PhaseAt(global, phase2Start, phase3Start) : timeView;
        float offset = AttackTimelineTimeView.Offset(phase, phase2Start, phase3Start);
        return "P" + phase + " " + Seconds(global - offset) + "s | Global " + GlobalLabel(global);
    }
    private string TableTime(float global)
        => timeView == 0 ? GlobalLabel(global)
            : Seconds(global - viewStart) + "s | G " + Seconds(global) + "s";

    private void DrawTimeView(SerializedObject data, float elapsed)
    {
        phasesOpen = EditorGUILayout.Foldout(phasesOpen, "Phase Setup", true);
        if (phasesOpen)
        {
            EditorGUILayout.LabelField("Global start seconds. P1 starts at 0. Changing boundaries never moves attacks.", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                EditorGUILayout.PropertyField(data.FindProperty("phase2Start"), new GUIContent("P2 Start (global s)"));
                EditorGUILayout.PropertyField(data.FindProperty("phase3Start"), new GUIContent("P3 Start (global s)"));
            }
        }
        phase2Start = data.FindProperty("phase2Start").floatValue;
        phase3Start = data.FindProperty("phase3Start").floatValue;
        fullDuration = data.FindProperty("duration").floatValue;
        string error = AttackTimelineTimeView.BoundaryError(phase2Start, phase3Start);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Time View", GUILayout.Width(65));
            for (int i = 0; i < 4; i++)
            {
                bool available = i == 0 || (error == null && AttackTimelineTimeView.Offset(i, phase2Start, phase3Start) < fullDuration);
                using (new EditorGUI.DisabledScope(!available))
                    if (GUILayout.Toggle(timeView == i, i == 0 ? "Global" : "P" + i, EditorStyles.toolbarButton) && timeView != i)
                    { timeView = i; GUI.FocusControl(null); overviewScroll = detailScroll = Vector2.zero; }
            }
        }
        if (timeView < 0 || timeView > 3 || error != null || AttackTimelineTimeView.Offset(timeView, phase2Start, phase3Start) >= fullDuration)
            timeView = 0;
        viewStart = AttackTimelineTimeView.Offset(timeView, phase2Start, phase3Start);
        viewEnd = timeView == 1 ? Mathf.Min(phase2Start, fullDuration)
            : timeView == 2 ? Mathf.Min(phase3Start, fullDuration) : fullDuration;
        if (error != null) EditorGUILayout.HelpBox(error + " Using Global view; playback timing is unchanged.", MessageType.Warning);
        if (timeView != 0)
            EditorGUILayout.LabelField(ViewName + " 0s = Global " + GlobalLabel(viewStart)
                + ". Fields use " + ViewName + " seconds; negative values are before this phase.", EditorStyles.wordWrappedMiniLabel);
        if (error == null)
        {
            int phase = AttackTimelineTimeView.PhaseAt(elapsed, phase2Start, phase3Start);
            float offset = AttackTimelineTimeView.Offset(phase, phase2Start, phase3Start);
            EditorGUILayout.LabelField("Playback: P" + phase + " " + Seconds(elapsed - offset) + "s"
                + " | Global " + GlobalLabel(elapsed), EditorStyles.miniLabel);
        }
    }

    private float TimeValue(float global, string label, string tip = null)
    {
        EditorGUI.BeginChangeCheck();
        float local = EditorGUILayout.FloatField(new GUIContent(label, (tip ?? "") + " Input: " + ViewName + " seconds. " + DualTime(global)),
            AttackTimelineTimeView.ToLocal(global, viewStart));
        // Do not round-trip untouched values: switching views must not change stored floats.
        return EditorGUI.EndChangeCheck() ? AttackTimelineTimeView.ToGlobal(local, viewStart) : global;
    }
    private void TimeField(SerializedProperty property, string label, string tip = null)
    {
        float before = property.floatValue;
        float after = TimeValue(before, label, tip);
        if (!before.Equals(after)) property.floatValue = after;
        EditorGUILayout.LabelField(DualTime(after), EditorStyles.miniLabel);
    }

    private void OnGUI()
    {
        var controller = FindObjectOfType<AttackTimelineController>();
        if (controller == null)
        {
            EditorGUILayout.HelpBox("Open Level_test to edit emitter tracks.", MessageType.Info);
            return;
        }
        if (controller.LegacyMigrationPending && !EditorApplication.isPlaying)
        {
            EditorUtility.SetDirty(controller);
            if (PrefabUtility.IsPartOfPrefabInstance(controller))
                PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            controller.AcknowledgeLegacyMigration();
        }
        DrawPlayback(controller);
        using (var data = new SerializedObject(controller))
        {
            data.Update();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Global " + GlobalLabel(controller.Elapsed), GUILayout.MinWidth(145));
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    EditorGUILayout.LabelField(new GUIContent("Duration (s)", "End of the authored pattern. With Endless P3 enabled, playback continues beyond this time."), GUILayout.Width(78));
                    EditorGUILayout.PropertyField(data.FindProperty("duration"), GUIContent.none);
                }
            }
            DrawTimeView(data, controller.Elapsed);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                EditorGUILayout.PropertyField(data.FindProperty("endlessPhase3"), new GUIContent("Endless P3"));
                if (data.FindProperty("endlessPhase3").boolValue)
                {
                    EditorGUILayout.PropertyField(data.FindProperty("loopFrom"), new GUIContent("Loop From (global s)",
                        "Repeat attacks starting here through Duration. Teaching before this time plays only once."));
                    float start = data.FindProperty("loopFrom").floatValue;
                    string loopError = EndlessAttackLoop.Validate(start, fullDuration, phase3Start);
                    EditorGUILayout.HelpBox(loopError ?? $"Repeat {start:0.##}-{fullDuration:0.##}s every {fullDuration - start:0.##}s. Total time keeps increasing; hazards are not cleared. Play Range remains a finite test.",
                        loopError == null ? MessageType.None : MessageType.Warning);
                }
            }
            DrawRangeTest(controller);
            setupOpen = EditorGUILayout.Foldout(setupOpen, "Scene Setup", true);
            if (setupOpen)
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    EditorGUILayout.PropertyField(data.FindProperty("player"));
                    EditorGUILayout.PropertyField(data.FindProperty("pointLibrary"));
                    EditorGUILayout.PropertyField(data.FindProperty("playOnStart"));
                    EditorGUILayout.PropertyField(data.FindProperty("minEmitterSpacing"),
                        new GUIContent("Fixed Point Spacing", "World units. Random tracks use their own Min Spacing; between tracks the larger value applies."));
                }
            var tracks = data.FindProperty("emitterTracks");
            ClampSelection(tracks);
            bool wide = position.width >= 1050;
            if (wide)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width - 370))) DrawOverview(tracks, controller, true);
                    using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true))) DrawDetails(tracks, data, controller);
                }
            }
            else
            {
                DrawOverview(tracks, controller, false);
                DrawDetails(tracks, data, controller);
            }
            if (data.ApplyModifiedProperties())
            {
                pointPreview = null;
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            }
        }
        string error = controller.Validate();
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
        DrawDebug(controller);
    }

    private void DrawPlayback(AttackTimelineController controller)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                if (GUILayout.Button("Play", EditorStyles.toolbarButton))
                {
                    RequestPlay(controller, false);
                }
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button(EditorApplication.isPaused ? "Resume" : "Pause", EditorStyles.toolbarButton))
                    EditorApplication.isPaused = !EditorApplication.isPaused;
                if (GUILayout.Button("Stop & Clear", EditorStyles.toolbarButton)) controller.StopAndClear();
                if (GUILayout.Button(controller.IsRangeTest ? "Restart Range" : "Restart", EditorStyles.toolbarButton))
                    RequestPlay(controller, controller.IsRangeTest, controller.TestFrom, controller.TestTo);
            }
        }
        if (EditorApplication.isPlaying) EditorGUILayout.LabelField(controller.Status, EditorStyles.miniLabel);
    }

    private void ClampSelection(SerializedProperty tracks)
    {
        selectedTrack = tracks.arraySize == 0 ? -1 : Mathf.Clamp(selectedTrack, 0, tracks.arraySize - 1);
    }

    private void DrawOverview(SerializedProperty tracks, AttackTimelineController controller, bool wide)
    {
        EditorGUILayout.LabelField("Emitter Tracks", EditorStyles.boldLabel);
        var rows = new List<Row>();
        for (int t = 0; t < tracks.arraySize; t++)
        {
            var track = tracks.GetArrayElementAtIndex(t);
            float appear = P(track, "appearAt").floatValue;
            if (timeView == 0 || AttackTimelineTimeView.Overlaps(appear, P(track, "stopAt").floatValue, viewStart, viewEnd))
                rows.Add(new Row { track = t, time = appear });
        }
        if (!rows.Exists(row => row.track == selectedTrack)) selectedTrack = rows.Count == 0 ? -1 : rows[0].track;
        if (timeView != 0)
            EditorGUILayout.LabelField("Showing tracks active in " + ViewName + ". Cross-phase tracks keep their full attack lists. G = Global.", EditorStyles.wordWrappedMiniLabel);
        rows.Sort((a, b) => a.time != b.time ? a.time.CompareTo(b.time) : a.track.CompareTo(b.track));
        var sizing = wide ? new[] { GUILayout.MinHeight(120), GUILayout.ExpandHeight(true) }
            : new[] { GUILayout.Height(Mathf.Clamp(position.height * .28f, 135, 260)) };
        overviewScroll = EditorGUILayout.BeginScrollView(overviewScroll, sizing);
        using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(1120)))
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                Cell("On", 25); Cell("Track", 130); Cell("Emitter", 130); Cell("Count", 42);
                Cell("Placement", 125); Cell("Appear", 130); Cell("Attack Times", 250); Cell("Stop", 130);
            }
            foreach (Row row in rows)
            {
                var track = tracks.GetArrayElementAtIndex(row.track);
                Rect rect = EditorGUILayout.GetControlRect(false, 24, GUILayout.MinWidth(1120));
                if (row.track == selectedTrack) EditorGUI.DrawRect(rect, new Color(.20f, .45f, .72f, .3f));
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
                {
                    selectedTrack = row.track;
                    pointPreview = null; detailScroll = Vector2.zero; GUI.FocusControl(null);
                    if (Event.current.mousePosition.x > rect.x + 25) Event.current.Use();
                    Repaint();
                }
                float x = rect.x;
                var enabled = P(track, "enabled");
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    enabled.boolValue = EditorGUI.Toggle(new Rect(x + 3, rect.y + 3, 20, 18), enabled.boolValue);
                x += 29;
                using (new EditorGUI.DisabledScope(!enabled.boolValue))
                {
                    RowCell(ref x, rect, P(track, "label").stringValue, 130);
                    var template = P(track, "emitterTemplate").objectReferenceValue;
                    RowCell(ref x, rect, template != null ? template.name : "Not assigned", 130);
                    RowCell(ref x, rect, P(track, "placementMode").enumValueIndex == 2 ? P(track, "emitterCount").intValue.ToString() : "1", 42);
                    RowCell(ref x, rect, PlacementSummary(track), 125);
                    RowCell(ref x, rect, TableTime(row.time), 130, DualTime(row.time));
                    RowCell(ref x, rect, TimesSummary(P(track, "attackTimes")), 250, TimesSummary(P(track, "attackTimes"), true));
                    RowCell(ref x, rect, TableTime(P(track, "stopAt").floatValue), 130, DualTime(P(track, "stopAt").floatValue));
                }
            }
        }
        EditorGUILayout.EndScrollView();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            if (GUILayout.Button("Add Track")) AddTrack(tracks, controller.Duration);
    }

    private static void Cell(string text, float width) => GUILayout.Label(text, EditorStyles.miniLabel, GUILayout.Width(width));
    private static void RowCell(ref float x, Rect row, string text, float width, string tip = null)
    {
        GUI.Label(new Rect(x, row.y + 3, width, 20), new GUIContent(text, tip ?? text), EditorStyles.label);
        x += width + 4;
    }
    private static string PlacementSummary(SerializedProperty clip)
    {
        int mode = P(clip, "placementMode").enumValueIndex;
        if (mode == 1) return "Fixed: " + P(clip, "pointId").stringValue;
        if (mode == 2) return "Random: " + P(clip, "region").enumDisplayNames[P(clip, "region").enumValueIndex];
        return "Custom";
    }
    private string TimesSummary(SerializedProperty times, bool detailed = false)
    {
        var values = new List<string>();
        for (int i = 0; i < times.arraySize; i++) values.Add(detailed ? DualTime(times.GetArrayElementAtIndex(i).floatValue) : TableTime(times.GetArrayElementAtIndex(i).floatValue));
        return values.Count == 0 ? "-" : string.Join(", ", values);
    }

    private void DrawDetails(SerializedProperty tracks, SerializedObject data, AttackTimelineController controller)
    {
        EditorGUILayout.LabelField("Selected Emitter Track", EditorStyles.boldLabel);
        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
        if (selectedTrack >= 0)
        {
            var track = tracks.GetArrayElementAtIndex(selectedTrack);
            var library = data.FindProperty("pointLibrary").objectReferenceValue as EmitterPointLibrary;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                Field(track, "label", "Name");
                Field(track, "emitterTemplate", "Emitter Template", "Emitter used by this track. Other tracks run independently.");
                var template = P(track, "emitterTemplate").objectReferenceValue;
                Field(track, "placementMode", "Placement");
                int mode = P(track, "placementMode").enumValueIndex;
                if (mode == 1) DrawPointPicker(track, library);
                else if (mode == 2) DrawRandom(track, library);
                else
                {
                    Field(track, "position", "Position");
                    Field(track, "rotationZ", "Rotation Z");
                }
                EditorGUILayout.Space(4);
                Field(track, "reenterPerAttack", "Reenter Per Attack", "Each Attack Time starts one appearance and one burst. Finish the burst, exit, then enter again. Random Points excludes the previous wave's points.");
                bool reenter = P(track, "reenterPerAttack").boolValue;
                if (reenter)
                    EditorGUILayout.HelpBox(mode == 2
                        ? "Each attack: new random points, one burst, then exit. Previous points are excluded. Later entries start before Attack Time by Enter Duration."
                        : "Each attack: enter at the same configured position, one burst, then exit. Use Random Points to change position.", MessageType.None);
                TimeField(P(track, "appearAt"), reenter ? "First Appear At (s)" : "Appear At (s)");
                DrawAttackTimes(track);
                TimeField(P(track, "stopAt"), "Stop At (s)", reenter
                    ? "Track cutoff. Each wave exits after its burst and active preparation / laser finish; it does not wait for Stop At."
                    : "Stop starting new attacks; active preparation / lasers finish before exit.");
                EditorGUILayout.Space(4);
                AttackSettingsGUI.Draw(P(track, "attack"), template, true, false);
                motionOpen = EditorGUILayout.Foldout(motionOpen, "Entry / Exit", true);
                if (motionOpen)
                {
                    if (mode == 0) Field(track, "entrySide", "Entry / Exit Side");
                    Field(track, "enterDuration", "Enter Duration (s)");
                    Field(track, "exitDuration", "Exit Duration (s)");
                }
                timingOpen = EditorGUILayout.Foldout(timingOpen, "Timing Preview / Excel (Global)", true);
                if (timingOpen) AttackTimingPreview.Draw(track, template);
                else if (P(track, "enabled").boolValue) AttackTimingPreview.DrawWarnings(track, template);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Duplicate", "Copy this track, keeping its times and settings. Adjust position and times to stagger attacks.")))
                    {
                        tracks.InsertArrayElementAtIndex(selectedTrack);
                        selectedTrack++;
                        P(tracks.GetArrayElementAtIndex(selectedTrack), "label").stringValue = UniqueName(tracks, selectedTrack,
                            P(tracks.GetArrayElementAtIndex(selectedTrack), "label").stringValue + " Copy");
                        pointPreview = null; GUI.FocusControl(null);
                    }
                    if (GUILayout.Button("Remove"))
                    {
                        tracks.DeleteArrayElementAtIndex(selectedTrack); selectedTrack--;
                        pointPreview = null; GUI.FocusControl(null);
                    }
                }
            }
        }
        else EditorGUILayout.LabelField("Add a track to schedule an emitter.", EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private static string UniqueName(SerializedProperty tracks, int skip, string name)
    {
        var names = new HashSet<string>();
        for (int i = 0; i < tracks.arraySize; i++)
            if (i != skip) names.Add(P(tracks.GetArrayElementAtIndex(i), "label").stringValue);
        string unique = name;
        for (int suffix = 2; names.Contains(unique); suffix++) unique = name + " (" + suffix + ")";
        return unique;
    }

    private static void DrawPointPicker(SerializedProperty clip, EmitterPointLibrary library)
    {
        var id = P(clip, "pointId");
        var ids = new List<string> { id.stringValue };
        var labels = new List<string> { string.IsNullOrEmpty(id.stringValue) ? "Select a point..." : "Missing: " + id.stringValue };
        int selected = 0;
        if (library != null)
            foreach (var point in library.Points)
            {
                if (point == null) continue;
                ids.Add(point.Id); labels.Add(point.Id);
                if (point.Id == id.stringValue) selected = ids.Count - 1;
            }
        if (selected > 0) { ids[0] = ""; labels[0] = "Select a point..."; }
        EditorGUI.BeginChangeCheck();
        int choice = EditorGUILayout.Popup(new GUIContent("Point", "Position, direction and entry edge come from this library point."), selected, labels.ToArray());
        if (EditorGUI.EndChangeCheck()) id.stringValue = ids[choice];
    }

    private void DrawRandom(SerializedProperty clip, EmitterPointLibrary library)
    {
        Field(clip, "emitterCount", "Emitter Count");
        Field(clip, "region", "Region");
        Field(clip, "minSpacing", "Min Spacing", "Minimum distance in world units. Includes other reserved points; the larger spacing applies.");
        randomOpen = EditorGUILayout.Foldout(randomOpen, "Random Options", true);
        if (!randomOpen) return;
        Field(clip, "randomSeed", "Seed", "Same seed and available points reproduce the same selection. Reenter Per Attack changes the seed per wave and excludes previous-wave points.");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("New Seed")) P(clip, "randomSeed").intValue = Guid.NewGuid().GetHashCode();
            using (new EditorGUI.DisabledScope(library == null))
                if (GUILayout.Button(new GUIContent("Preview Points", "First-wave empty-scene preview. Later waves exclude previous points. Runtime occupancy can change the selection.")))
                {
                    try
                    {
                        var result = EmitterPointSelection.Select(library.GetCandidates((EmitterPointLibrary.Region)P(clip, "region").enumValueIndex),
                            P(clip, "emitterCount").intValue, P(clip, "minSpacing").floatValue, P(clip, "randomSeed").intValue);
                        var ids = new List<string>(); foreach (var point in result.Points) ids.Add(point.Id);
                        pointPreview = result.Success ? "Empty scene: " + string.Join(", ", ids) : result.Message;
                    }
                    catch (ArgumentException e) { pointPreview = e.Message; }
                }
        }
        if (!string.IsNullOrEmpty(pointPreview)) EditorGUILayout.HelpBox(pointPreview, MessageType.Info);
    }

    private void DrawAttackTimes(SerializedProperty clip)
    {
        var times = P(clip, "attackTimes");
        EditorGUILayout.LabelField(new GUIContent("Attack Times (s)", "Preparation start in the selected time view, not when bullets leave the emitter. Each time starts one burst. Stored as global seconds."));
        for (int i = 0; i < times.arraySize; i++)
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                    TimeField(times.GetArrayElementAtIndex(i), "Attack " + (i + 1) + " (s)");
                if (GUILayout.Button(new GUIContent("-", "Remove this attack time"), GUILayout.Width(24)))
                { times.DeleteArrayElementAtIndex(i); break; }
            }
        if (GUILayout.Button("+ Attack Time"))
        {
            float next = times.arraySize == 0 ? P(clip, "appearAt").floatValue + P(clip, "enterDuration").floatValue
                : times.GetArrayElementAtIndex(times.arraySize - 1).floatValue + 1;
            if (timeView != 0) next = Mathf.Max(viewStart, next);
            int index = times.arraySize++; times.GetArrayElementAtIndex(index).floatValue = next;
        }
    }

    private void AddTrack(SerializedProperty tracks, float duration)
    {
        selectedTrack = tracks.arraySize++;
        var clip = tracks.GetArrayElementAtIndex(selectedTrack);
        P(clip, "label").stringValue = UniqueName(tracks, selectedTrack, "New Track");
        P(clip, "enabled").boolValue = true;
        P(clip, "reenterPerAttack").boolValue = false;
        P(clip, "emitterTemplate").objectReferenceValue = null;
        float start = viewStart;
        P(clip, "appearAt").floatValue = start; P(clip, "stopAt").floatValue = duration;
        P(clip, "position").vector3Value = Vector3.zero; P(clip, "rotationZ").floatValue = 0;
        P(clip, "entrySide").enumValueIndex = 0;
        P(clip, "placementMode").enumValueIndex = 0; P(clip, "pointId").stringValue = "";
        P(clip, "emitterCount").intValue = 3; P(clip, "region").enumValueIndex = 0;
        P(clip, "minSpacing").floatValue = 2; P(clip, "randomSeed").intValue = 1;
        P(clip, "enterDuration").floatValue = .5f; P(clip, "exitDuration").floatValue = .5f;
        P(clip, "attackTimes").ClearArray(); P(clip, "attackTimes").arraySize = 1;
        P(clip, "attackTimes").GetArrayElementAtIndex(0).floatValue = start + .5f;
        AttackSettingsGUI.Reset(P(clip, "attack"), null);
        pointPreview = null; detailScroll = Vector2.zero;
    }

    private void DrawDebug(AttackTimelineController controller)
    {
        if (!EditorApplication.isPlaying) return;
        debugOpen = EditorGUILayout.Foldout(debugOpen, "Diagnostics", true);
        if (!debugOpen) return;
        EditorGUILayout.LabelField("Log times use Global seconds.", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Reserved Points", controller.ReservedPointCount.ToString());
        for (int i = Mathf.Max(0, controller.AttackLog.Count - 4); i < controller.AttackLog.Count; i++)
            EditorGUILayout.LabelField(controller.AttackLog[i], EditorStyles.wordWrappedMiniLabel);
        if (GUILayout.Button("Copy Log")) EditorGUIUtility.systemCopyBuffer = string.Join("\n", controller.AttackLog);
    }
}

// Pure display conversion. Never writes to tracks, phase boundaries, or runtime clocks.
public static class AttackTimelineTimeView
{
    public static float Offset(int view, float p2, float p3) => view == 2 ? p2 : view == 3 ? p3 : 0;
    public static float ToLocal(float global, float offset) => global - offset;
    public static float ToGlobal(float local, float offset) => local + offset;
    public static int PhaseAt(float global, float p2, float p3) => global >= p3 ? 3 : global >= p2 ? 2 : 1;
    public static bool Overlaps(float appear, float stop, float from, float to) => appear < to && stop > from;
    public static string BoundaryError(float p2, float p3)
        => float.IsNaN(p2) || float.IsInfinity(p2) || float.IsNaN(p3) || float.IsInfinity(p3) || p2 <= 0 || p3 <= p2
            ? "Phase starts must be finite and ordered: 0 < P2 < P3." : null;
    public static string Clock(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds)) return "--:--";
        double rounded = Math.Round(Math.Abs((double)seconds), 2, MidpointRounding.AwayFromZero);
        double minutes = Math.Floor(rounded / 60);
        double remainder = rounded - minutes * 60;
        return (seconds < 0 && rounded > 0 ? "-" : "") + minutes.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            + ":" + remainder.ToString("00.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
