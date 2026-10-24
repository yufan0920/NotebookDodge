using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EmitterPointLibrary))]
public class EmitterPointLibraryEditor : Editor
{
    private EmitterPointLibrary Library => (EmitterPointLibrary)target;
    private int selectedIndex;
    private bool showGeneration;
    private string generationError;
    private static GUIStyle pointLabel;
    private bool editPoints = true;
    private bool hidTools, previousToolsHidden;
    private bool showSideAdjustment;
    private EmitterPointLibrary.Side adjustSide = EmitterPointLibrary.Side.Bottom;
    private Vector2 sideOffset = new Vector2(0, -0.3f);
    private PlayerMovementArea movementArea;
    private float edgeGap = 0.5f;
    private float edgeInset = 0.3f;
    private string adjustmentError;
    private bool showRandom = true;
    private int previewCount = 3, previewSeed = 1;
    private float previewSpacing = 2;
    private int geometryHash;
    private EmitterPointSelection.Result randomPreview;
    private string randomError;
    private readonly EmitterPointSelection.Occupancy testOccupancy = new EmitterPointSelection.Occupancy();

    private void OnEnable()
    {
        showGeneration = Library.Points.Count == 0;
        Undo.undoRedoPerformed += Refresh;
    }

    private void OnDisable()
    {
        RestoreTools();
        Undo.undoRedoPerformed -= Refresh;
        ClearRandomPreview();
        SceneView.RepaintAll();
    }
    private void Refresh() { ClearRandomPreview(); Repaint(); SceneView.RepaintAll(); }

    private void ClearRandomPreview()
    {
        randomPreview = null;
        randomError = null;
        testOccupancy.Clear();
    }

    [MenuItem("Tools/Notebook Dodge/Placement/Select Point Library")]
    private static void SelectLibrary()
    {
        var libraries = FindObjectsOfType<EmitterPointLibrary>();
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var library in libraries)
        {
            if (library.gameObject.scene != activeScene) continue;
            Selection.activeGameObject = library.gameObject;
            EditorGUIUtility.PingObject(library);
            return;
        }
        Debug.Log("Select EmitterPointLibrary in PlacementSetup and add the Emitter Point Library component.");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        EditorGUILayout.HelpBox("Select this library in Hierarchy, then enable Edit Points. Click a point and drag its yellow center; drag the arrow tip to aim. You can also edit Position (World XY) below. Ctrl+Z undoes changes.", MessageType.Info);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            editPoints = EditorGUILayout.Toggle("Edit Points in Scene", editPoints);
            UpdateTools();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("previewRegion"), new GUIContent("Preview Region"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("showLabels"), new GUIContent("Show Point IDs"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("arrowLength"), new GUIContent("Arrow Display Length"));
            Apply();
            DrawPointSelection();
            DrawSideAdjustment();
            DrawRandomPlacement();

            EditorGUILayout.Space();
            showGeneration = EditorGUILayout.Foldout(showGeneration, "Generate Layout", true);
            if (showGeneration)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("horizontalPointsPerSide"), new GUIContent("Top / Bottom Points"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("verticalPointsPerSide"), new GUIContent("Left / Right Points"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("layoutCenter"), new GUIContent("Center (Library XY)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("layoutSize"), new GUIContent("Size (Width / Height)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cornerInset"));
                Apply();
                int horizontal = serializedObject.FindProperty("horizontalPointsPerSide").intValue;
                int vertical = serializedObject.FindProperty("verticalPointsPerSide").intValue;
                Vector2 size = serializedObject.FindProperty("layoutSize").vector2Value;
                float inset = serializedObject.FindProperty("cornerInset").floatValue;
                if (horizontal > 1 && vertical > 1)
                    EditorGUILayout.LabelField("Generated Spacing (H / V)",
                        $"{(size.x - 2 * inset) / (horizontal - 1):F2} / {(size.y - 2 * inset) / (vertical - 1):F2}");
                if (GUILayout.Button("Match Horizontal Spacing (Count Only)"))
                {
                    try
                    {
                        serializedObject.FindProperty("horizontalPointsPerSide").intValue =
                            EmitterPointLibrary.RecommendHorizontalCount(vertical, size, inset);
                        Apply();
                        generationError = null;
                    }
                    catch (ArgumentException exception) { generationError = exception.Message; }
                }
                if (Library.Points.Count > 0)
                {
                    EditorGUILayout.HelpBox("Add Midpoints preserves existing positions and directions, interpolates new arrows, and renumbers top/bottom IDs in spatial order. Left/right points stay unchanged.", MessageType.None);
                    if (GUILayout.Button("Add Horizontal Midpoints (Keep Edits)")) AddMidpoints();
                }
                EditorGUILayout.HelpBox("Generation replaces all points and resets their directions. Editing these fields alone does not move existing points. You can Undo generation.", MessageType.None);
                if (GUILayout.Button(Library.Points.Count == 0 ? "Generate Points" : "Regenerate Points (Reset Layout)"))
                    Generate();
                if (!string.IsNullOrEmpty(generationError)) EditorGUILayout.HelpBox(generationError, MessageType.Warning);
            }

            if (!EditorUtility.IsPersistent(Library) && !PrefabUtility.IsPartOfPrefabInstance(Library) &&
                UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() == null)
            {
                EditorGUILayout.Space();
                using (new EditorGUI.DisabledScope(Library.Points.Count == 0))
                    if (GUILayout.Button("Save as Library Prefab...")) SavePrefab();
            }
            else if (PrefabUtility.IsPartOfPrefabInstance(Library))
                EditorGUILayout.HelpBox("Point edits are local prefab overrides. Apply to the prefab only if other scenes should share this layout.", MessageType.None);
        }
        if (Vector3.Dot(Library.transform.forward, Vector3.forward) < 0.999f)
            EditorGUILayout.HelpBox("Use a 2D XY layout: keep Rotation X and Y at zero.", MessageType.Warning);
        if ((Library.transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
            EditorGUILayout.HelpBox("Keep the library and its parents at Scale (1, 1, 1) so spacing stays consistent between scenes.", MessageType.Warning);
    }

    private void Apply()
    {
        if (serializedObject.ApplyModifiedProperties())
        {
            randomPreview = null;
            randomError = null;
            SceneView.RepaintAll();
        }
    }

    private void UpdateTools()
    {
        bool editing = editPoints && !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorUtility.IsPersistent(Library) && Selection.activeGameObject == Library.gameObject;
        if (editing && !hidTools)
        {
            previousToolsHidden = Tools.hidden;
            Tools.hidden = true;
            hidTools = true;
        }
        else if (!editing) RestoreTools();
    }

    private void RestoreTools()
    {
        if (!hidTools) return;
        Tools.hidden = previousToolsHidden;
        hidTools = false;
    }

    private void DrawSideAdjustment()
    {
        showSideAdjustment = EditorGUILayout.Foldout(showSideAdjustment, "Adjust One Side", true);
        if (!showSideAdjustment) return;
        adjustSide = (EmitterPointLibrary.Side)EditorGUILayout.EnumPopup("Side", adjustSide);
        sideOffset = EditorGUILayout.Vector2Field("Move Offset (World XY)", sideOffset);
        if (GUILayout.Button("Move Side")) AdjustSide(false);
        movementArea = (PlayerMovementArea)EditorGUILayout.ObjectField("Movement Area", movementArea,
            typeof(PlayerMovementArea), true);
        edgeGap = Mathf.Max(0, EditorGUILayout.FloatField("Gap Outside Edge", edgeGap));
        edgeInset = Mathf.Max(0, EditorGUILayout.FloatField("Corner Inset", edgeInset));
        if (GUILayout.Button("Align Side to Movement Area")) AdjustSide(true);
        EditorGUILayout.HelpBox("Move Side adds the offset once. Align Side spaces the existing points along the selected boundary edge. IDs, directions and other sides are preserved. Ctrl+Z to undo.", MessageType.None);
        if (adjustmentError != null) EditorGUILayout.HelpBox(adjustmentError, MessageType.Warning);
    }

    private void AdjustSide(bool align)
    {
        adjustmentError = null;
        var indices = new List<int>();
        for (int i = 0; i < Library.Points.Count; i++)
            if (Library.Points[i].EntrySide == adjustSide) indices.Add(i);
        if (indices.Count == 0) { adjustmentError = "No points on this side."; return; }
        if (!IsFinite(sideOffset.x) || !IsFinite(sideOffset.y) || !IsFinite(edgeGap) || !IsFinite(edgeInset))
        { adjustmentError = "Use finite offsets and distances."; return; }

        Vector2 start = default, end = default;
        if (align)
        {
            var corners = new Vector2[4];
            if (movementArea == null || movementArea.gameObject.scene != Library.gameObject.scene ||
                !movementArea.TryCopyBoundary(corners))
            { adjustmentError = "Assign a valid Movement Area from this scene."; return; }
            switch (adjustSide)
            {
                case EmitterPointLibrary.Side.Top: start = corners[3]; end = corners[2]; break;
                case EmitterPointLibrary.Side.Bottom: start = corners[0]; end = corners[1]; break;
                case EmitterPointLibrary.Side.Left: start = corners[0]; end = corners[3]; break;
                default: start = corners[1]; end = corners[2]; break;
            }
            Vector2 tangent = (end - start).normalized;
            if (edgeInset * 2 >= Vector2.Distance(start, end))
            { adjustmentError = "Corner Inset must be less than half the edge length."; return; }
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            Vector2 center = (corners[0] + corners[1] + corners[2] + corners[3]) / 4;
            if (Vector2.Dot(normal, (start + end) / 2 - center) < 0) normal = -normal;
            start += tangent * edgeInset + normal * edgeGap;
            end += -tangent * edgeInset + normal * edgeGap;
            // IDs retain the authored order: horizontal left-to-right, vertical bottom-to-top.
            indices.Sort((a, b) => string.CompareOrdinal(Library.Points[a].Id, Library.Points[b].Id));
        }
        serializedObject.Update();
        var points = serializedObject.FindProperty("points");
        for (int n = 0; n < indices.Count; n++)
        {
            int index = indices[n];
            Vector3 world = Library.GetWorldPosition(Library.Points[index]);
            Vector2 next = align ? Vector2.Lerp(start, end, indices.Count == 1 ? 0.5f : (float)n / (indices.Count - 1))
                : (Vector2)world + sideOffset;
            world.x = next.x; world.y = next.y;
            points.GetArrayElementAtIndex(index).FindPropertyRelative("position").vector2Value =
                Library.transform.InverseTransformPoint(world);
        }
        Apply();
        ClearRandomPreview();
        SceneView.RepaintAll();
    }

    private void DrawRandomPlacement()
    {
        int hash = 17;
        unchecked
        {
            foreach (var point in Library.Points)
            {
                hash = hash * 31 + point.Id.GetHashCode();
                hash = hash * 31 + Library.GetWorldPosition(point).GetHashCode();
            }
        }
        if (hash != geometryHash) { ClearRandomPreview(); geometryHash = hash; SceneView.RepaintAll(); }
        EditorGUILayout.Space();
        showRandom = EditorGUILayout.Foldout(showRandom, "Random Placement Preview", true);
        if (!showRandom) return;
        EditorGUILayout.HelpBox("Uses Preview Region. Green rings = chosen; red crosses = test occupancy. Reservations simulate entry, firing and exit until cleared. Preview only: no emitters are spawned. Point edits, Undo or closing this inspector clear test occupancy.", MessageType.None);
        EditorGUI.BeginChangeCheck();
        previewCount = Mathf.Clamp(EditorGUILayout.IntField("Emitter Count", previewCount), 1, 120);
        previewSpacing = EditorGUILayout.FloatField("Min Emitter Spacing", previewSpacing);
        previewSeed = EditorGUILayout.IntField("Random Seed", previewSeed);
        if (EditorGUI.EndChangeCheck()) { randomPreview = null; randomError = null; SceneView.RepaintAll(); }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview Random")) RunRandomPreview(false);
            if (GUILayout.Button("New Seed")) { previewSeed = unchecked(previewSeed + 1); RunRandomPreview(false); }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reserve Group (Test)")) RunRandomPreview(true);
            if (GUILayout.Button("Clear Test Occupancy")) { ClearRandomPreview(); SceneView.RepaintAll(); }
        }
        int occupiedCount = 0;
        foreach (var reservation in testOccupancy.Reservations) occupiedCount += reservation.Points.Count;
        EditorGUILayout.LabelField("Test Occupied Points", occupiedCount.ToString());
        // Release one group to test reuse only after an exit has completed.
        for (int i = 0; i < testOccupancy.Reservations.Count; i++)
        {
            var reservation = testOccupancy.Reservations[i];
            if (GUILayout.Button($"Complete Exit: Test Group {i + 1} ({reservation.Points.Count} points)"))
            {
                testOccupancy.Release(reservation);
                randomPreview = null;
                randomError = null;
                SceneView.RepaintAll();
                break;
            }
        }
        if (randomPreview != null)
        {
            EditorGUILayout.HelpBox(randomPreview.Message + (randomPreview.Success ? "" : " No points were reserved for this failed request."),
                randomPreview.Success ? MessageType.Info : MessageType.Warning);
            var ids = new List<string>();
            foreach (var point in randomPreview.Points) ids.Add(point.Id);
            EditorGUILayout.LabelField(string.Join(", ", ids), EditorStyles.wordWrappedMiniLabel);
        }
        if (randomError != null) EditorGUILayout.HelpBox(randomError, MessageType.Warning);
    }

    private void RunRandomPreview(bool reserve)
    {
        try
        {
            var candidates = Library.GetCandidates(Library.PreviewRegion);
            if (reserve)
            {
                EmitterPointSelection.Reservation reservation;
                testOccupancy.TryReserve(candidates, previewCount, previewSpacing, previewSeed, out reservation, out randomPreview);
            }
            else randomPreview = testOccupancy.Preview(candidates, previewCount, previewSpacing, previewSeed);
            randomError = null;
        }
        catch (ArgumentException exception) { randomPreview = null; randomError = exception.Message; }
        SceneView.RepaintAll();
    }

    private void DrawPointSelection()
    {
        var indices = new List<int>();
        var names = new List<string>();
        for (int i = 0; i < Library.Points.Count; i++)
        {
            var point = Library.Points[i];
            if (!Library.IsInRegion(point, Library.PreviewRegion)) continue;
            indices.Add(i);
            names.Add(point.Id);
        }
        EditorGUILayout.LabelField("Visible Points", indices.Count + " / " + Library.Points.Count);
        if (indices.Count == 0)
        {
            EditorGUILayout.HelpBox("No points in this region. Choose All or generate a layout.", MessageType.Info);
            return;
        }
        int option = indices.IndexOf(selectedIndex);
        if (option < 0) option = 0;
        int newIndex = indices[EditorGUILayout.Popup("Selected Point", option, names.ToArray())];
        if (newIndex != selectedIndex) { selectedIndex = newIndex; SceneView.RepaintAll(); }
        var selected = Library.Points[selectedIndex];

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.EnumPopup("Entry / Exit Side", selected.EntrySide);
        Vector3 world = Library.GetWorldPosition(selected);
        EditorGUI.BeginChangeCheck();
        Vector2 position = EditorGUILayout.Vector2Field("Position (World XY)", world);
        if (EditorGUI.EndChangeCheck())
            SetPosition(new Vector3(position.x, position.y, world.z));

        Vector2 direction = Library.GetWorldDirection(selected);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        EditorGUI.BeginChangeCheck();
        float newAngle = EditorGUILayout.FloatField("Direction Angle (World)", angle);
        if (EditorGUI.EndChangeCheck() && IsFinite(newAngle))
        {
            float radians = newAngle * Mathf.Deg2Rad;
            SetDirection(new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)));
        }
        EditorGUILayout.LabelField("0 = Right, 90 = Up, -90 = Down, 180 = Left", EditorStyles.wordWrappedMiniLabel);
        if (GUILayout.Button("Reset Direction to Side Default"))
        {
            var property = PointProperty().FindPropertyRelative("direction");
            property.vector2Value = EmitterPointLibrary.DefaultDirection(selected.EntrySide);
            Apply();
        }
    }

    private SerializedProperty PointProperty() => serializedObject.FindProperty("points").GetArrayElementAtIndex(selectedIndex);

    private void SetPosition(Vector3 world)
    {
        if (!IsFinite(world.x) || !IsFinite(world.y)) return;
        serializedObject.Update();
        PointProperty().FindPropertyRelative("position").vector2Value = Library.transform.InverseTransformPoint(world);
        Apply();
    }

    private void SetDirection(Vector2 worldDirection)
    {
        if (!IsFinite(worldDirection.x) || !IsFinite(worldDirection.y) || worldDirection.sqrMagnitude < 0.0001f) return;
        serializedObject.Update();
        Vector2 local = Library.transform.InverseTransformDirection(worldDirection.normalized);
        PointProperty().FindPropertyRelative("direction").vector2Value = local.normalized;
        Apply();
    }

    private void Generate()
    {
        serializedObject.Update();
        try
        {
            Vector2 center = serializedObject.FindProperty("layoutCenter").vector2Value;
            var generated = Library.CreateConfiguredLayout();
            WritePoints(generated);
            serializedObject.FindProperty("regionCenter").vector2Value = center;
            serializedObject.FindProperty("previewRegion").enumValueIndex = 0;
            Apply();
            selectedIndex = 0;
            generationError = null;
        }
        catch (ArgumentException exception) { generationError = exception.Message; }
    }

    private void AddMidpoints()
    {
        serializedObject.Update();
        try
        {
            var generated = EmitterPointLibrary.AddHorizontalMidpoints(Library.Points);
            WritePoints(generated);
            serializedObject.FindProperty("horizontalPointsPerSide").intValue =
                generated.FindAll(point => point.EntrySide == EmitterPointLibrary.Side.Top).Count;
            serializedObject.FindProperty("verticalPointsPerSide").intValue =
                generated.FindAll(point => point.EntrySide == EmitterPointLibrary.Side.Left).Count;
            Apply();
            selectedIndex = 0;
            generationError = null;
        }
        catch (ArgumentException exception) { generationError = exception.Message; }
    }

    private void WritePoints(List<EmitterPointLibrary.Point> generated)
    {
        var points = serializedObject.FindProperty("points");
        points.arraySize = generated.Count;
        for (int i = 0; i < generated.Count; i++)
        {
            var point = points.GetArrayElementAtIndex(i);
            point.FindPropertyRelative("id").stringValue = generated[i].Id;
            point.FindPropertyRelative("side").enumValueIndex = (int)generated[i].EntrySide;
            point.FindPropertyRelative("position").vector2Value = generated[i].LocalPosition;
            point.FindPropertyRelative("direction").vector2Value = generated[i].LocalDirection;
        }
    }

    private void SavePrefab()
    {
        string path = EditorUtility.SaveFilePanelInProject("Save Point Library", "EmitterPointLibrary", "prefab",
            "Choose a prefab asset for this point library.", "Assets/_Project/Prefabs");
        if (string.IsNullOrEmpty(path)) return;
        PrefabUtility.SaveAsPrefabAssetAndConnect(Library.gameObject, path, InteractionMode.UserAction);
        GUIUtility.ExitGUI();
    }

    private void OnSceneGUI()
    {
        UpdateTools();
        if (!editPoints || EditorApplication.isPlayingOrWillChangePlaymode || Library.Points.Count == 0) return;
        // Background sprites must not steal clicks while editing points.
        if (!Event.current.alt)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Library.Points.Count - 1);
        Color oldColor = Handles.color;
        if (randomPreview != null)
        {
            Handles.color = randomPreview.Success ? Color.green : Color.yellow;
            foreach (var point in randomPreview.Points)
                Handles.DrawWireDisc(new Vector3(point.Position.x, point.Position.y, Library.transform.position.z), Vector3.forward, 0.18f);
        }
        Handles.color = Color.red;
        foreach (var reservation in testOccupancy.Reservations)
            foreach (var point in reservation.Points)
            {
                Vector3 position = new Vector3(point.Position.x, point.Position.y, Library.transform.position.z);
                var offset = new Vector3(0.14f, 0.14f, 0);
                Handles.DrawLine(position - offset, position + offset);
                offset.y = -offset.y;
                Handles.DrawLine(position - offset, position + offset);
            }
        for (int i = 0; i < Library.Points.Count; i++)
        {
            var point = Library.Points[i];
            if (!Library.IsInRegion(point, Library.PreviewRegion) || i == selectedIndex) continue;
            Vector3 position = Library.GetWorldPosition(point);
            float size = HandleUtility.GetHandleSize(position) * 0.055f;
            Handles.color = SideColor(point.EntrySide);
            if (Handles.Button(position, Quaternion.identity, size, size, Handles.DotHandleCap))
            {
                selectedIndex = i;
                Repaint();
            }
        }
        var selected = Library.Points[selectedIndex];
        if (Library.IsInRegion(selected, Library.PreviewRegion))
        {
            Vector3 position = Library.GetWorldPosition(selected);
            float size = HandleUtility.GetHandleSize(position) * 0.075f;
            Handles.color = Color.yellow;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider2D(position, Library.transform.forward,
                Library.transform.right, Library.transform.up, size, Handles.DotHandleCap, Vector2.zero);
            if (EditorGUI.EndChangeCheck()) SetPosition(moved);

            Vector3 tip = moved + (Vector3)Library.GetWorldDirection(selected) * Library.ArrowLength;
            EditorGUI.BeginChangeCheck();
            Vector3 aimed = Handles.Slider2D(tip, Library.transform.forward,
                Library.transform.right, Library.transform.up, size * 0.7f, Handles.RectangleHandleCap, Vector2.zero);
            if (EditorGUI.EndChangeCheck()) SetDirection(aimed - moved);
        }
        Handles.color = oldColor;
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawPoints(EmitterPointLibrary library, GizmoType gizmoType)
    {
        if (pointLabel == null) pointLabel = new GUIStyle(EditorStyles.miniBoldLabel);
        Color oldGizmo = Gizmos.color;
        Color oldHandle = Handles.color;
        foreach (var point in library.Points)
        {
            if (point == null) continue;
            bool visible = library.IsInRegion(point, library.PreviewRegion);
            Color color = visible ? SideColor(point.EntrySide) : new Color(0.5f, 0.5f, 0.5f, 0.2f);
            Gizmos.color = Handles.color = color;
            Vector3 origin = library.GetWorldPosition(point);
            Vector3 direction = library.GetWorldDirection(point);
            Vector3 end = origin + direction * library.ArrowLength;
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0);
            float head = library.ArrowLength * 0.22f;
            Gizmos.DrawSphere(origin, 0.055f);
            Gizmos.DrawLine(origin, end);
            Gizmos.DrawLine(end, end - direction * head + perpendicular * head * 0.5f);
            Gizmos.DrawLine(end, end - direction * head - perpendicular * head * 0.5f);
            if (visible && library.ShowLabels)
            {
                pointLabel.normal.textColor = color;
                Handles.Label(origin + new Vector3(0.1f, 0.13f, 0), point.Id, pointLabel);
            }
        }
        Gizmos.color = oldGizmo;
        Handles.color = oldHandle;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static Color SideColor(EmitterPointLibrary.Side side)
    {
        switch (side)
        {
            case EmitterPointLibrary.Side.Top: return new Color(0.3f, 0.8f, 1);
            case EmitterPointLibrary.Side.Bottom: return new Color(1, 0.7f, 0.3f);
            case EmitterPointLibrary.Side.Left: return new Color(0.65f, 0.95f, 0.45f);
            default: return new Color(1, 0.5f, 0.7f);
        }
    }
}
