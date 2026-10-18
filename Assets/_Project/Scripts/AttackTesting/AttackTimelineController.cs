using System;
using System.Collections.Generic;
using UnityEngine;

//执行整段编排：管理track、clip、发射器入场、移动、退场和清场
[DefaultExecutionOrder(-200)]
public class AttackTimelineController : MonoBehaviour, ISerializationCallbackReceiver
{
    public enum EntrySide { Auto, Left, Right, Top, Bottom }

    public enum PlacementMode { CustomPosition, FixedPoint, RandomPoints }

    [Serializable]
    public class Cue
    {
        public string label;
        public bool enabled = true;
        public PlacementMode placementMode;
        public string pointId;
        [Range(1, 120)] public int emitterCount = 3;
        public EmitterPointLibrary.Region region = EmitterPointLibrary.Region.All;
        [Min(0)] public float minSpacing = 2;
        public int randomSeed = 1;
        public AttackSettings attack = new AttackSettings();
        [Min(0)] public float appearAt;
        [Min(0)] public float stopAt = 60;
        public Vector3 position;
        public float rotationZ;
        public EntrySide entrySide;
        [Min(0)] public float enterDuration = 0.5f;
        public List<float> attackTimes = new List<float>();
        [Min(0)] public float exitDuration = 0.5f;
        public Cue Copy()
        {
            var copy = (Cue)MemberwiseClone();
            copy.attack = attack.Copy();
            copy.attackTimes = new List<float>(attackTimes);
            return copy;
        }
    }

    [Serializable]
    public class Track
    {
        public string label = "New Track";
        public bool enabled = true;
        public MonoBehaviour emitterTemplate;
        public List<Cue> clips = new List<Cue>();
    }

    // Flat authoring model. Track / clips above are retained only to read older scenes.
    [Serializable]
    public class EmitterTrack : Cue
    {
        public MonoBehaviour emitterTemplate;

        public static EmitterTrack FromLegacy(Track owner, Cue cue)
        {
            return new EmitterTrack
            {
                label = string.IsNullOrWhiteSpace(cue.label) ? owner.label : cue.label,
                enabled = owner.enabled && cue.enabled,
                emitterTemplate = owner.emitterTemplate,
                placementMode = cue.placementMode, pointId = cue.pointId,
                emitterCount = cue.emitterCount, region = cue.region,
                minSpacing = cue.minSpacing, randomSeed = cue.randomSeed,
                attack = cue.attack == null ? null : cue.attack.Copy(),
                appearAt = cue.appearAt, stopAt = cue.stopAt,
                position = cue.position, rotationZ = cue.rotationZ, entrySide = cue.entrySide,
                enterDuration = cue.enterDuration, exitDuration = cue.exitDuration,
                attackTimes = cue.attackTimes == null ? null : new List<float>(cue.attackTimes)
            };
        }
    }

    [SerializeField] private List<EmitterTrack> emitterTracks = new List<EmitterTrack>();
    public bool LegacyMigrationPending { get; private set; }
    public void AcknowledgeLegacyMigration() => LegacyMigrationPending = false;
    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize() => UpgradeLegacyTracks();

    // No Unity API calls: deserialization can run off the main thread.
    public bool UpgradeLegacyTracks()
    {
        if (tracks == null || tracks.Count == 0) return false;
        if (emitterTracks == null) emitterTracks = new List<EmitterTrack>();
        foreach (Track owner in tracks)
        {
            if (owner == null) continue;
            if (owner.clips == null || owner.clips.Count == 0)
                emitterTracks.Add(new EmitterTrack { label = owner.label, enabled = owner.enabled,
                    emitterTemplate = owner.emitterTemplate });
            else foreach (Cue cue in owner.clips)
                if (cue != null) emitterTracks.Add(EmitterTrack.FromLegacy(owner, cue));
        }
        // Stable unique names keep duplicated Laser entries distinguishable.
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (EmitterTrack track in emitterTracks)
        {
            string name = string.IsNullOrWhiteSpace(track.label) ? "Emitter Track" : track.label;
            string unique = name;
            for (int suffix = 2; !names.Add(unique); suffix++) unique = name + " (" + suffix + ")";
            track.label = unique;
        }
        tracks.Clear();
        LegacyMigrationPending = true;
        return true;
    }

    [SerializeField] private PlayerHealth player;
    [SerializeField] private EmitterPointLibrary pointLibrary = null;
    [SerializeField, Min(0)] private float minEmitterSpacing = 2;
    [SerializeField, Min(1)] private float duration = 60;
    [SerializeField] private bool playOnStart = true;
    [SerializeField, HideInInspector] private List<Track> tracks = new List<Track>();
    [SerializeField] private List<GameObject> sceneEmitters = new List<GameObject>();
    private sealed class RunningCue
    {
        public Cue cue;
        public MonoBehaviour template;
        public AbsoluteAttackSchedule sequence;
        public bool ready;
        public float enteredAt;
        public Vector3 entryOrigin;
        public GameObject instance;
        public ITimelineEmitter emitter;
        public float exitStarted = -1;
        public Vector3 exitOrigin, offscreen;
        public bool spawned, finished, emitting;
        public bool restoreEntry;
        public EmitterPointSelection.Reservation reservation;
        public string placementFailure;
        public List<RunningCue> members;

    }
    private readonly List<RunningCue> running = new List<RunningCue>();
    private readonly EmitterPointSelection.Occupancy pointOccupancy = new EmitterPointSelection.Occupancy();
    public EmitterPointLibrary PointLibrary => pointLibrary;
    public int ReservedPointCount => pointOccupancy.PointCount;
    private bool initialized, ending;
    public readonly List<string> AttackLog = new List<string>();
    public float Elapsed { get; private set; }
    public bool HasStarted => initialized;
    public float Duration => duration;
    public bool IsRangeTest { get; private set; }
    public float TestFrom { get; private set; }
    public float TestTo { get; private set; }
    public bool IsRunning { get; private set; }
    public string Status { get; private set; } = "Not Started";

    private void Awake()
    {
        foreach (GameObject source in sceneEmitters)
            if (source != null) source.SetActive(false);
    }
    private void Start() { if (playOnStart) Begin(); }

    public void Begin()
    {
        if (!Application.isPlaying || initialized) return;
        BeginSession(0, duration, false);
    }

    public void BeginRange(float from, float to)
    {
        if (!Application.isPlaying) return;
        string error = RangeError(from, to, duration);
        if (error != null) { Status = error; Debug.LogError(error, this); return; }
        // Also cancels any automatic Play On Start activity before the editor's request arrives.
        StopAndClear();
        initialized = false;
        BeginSession(from, to, true);
    }

    public static string RangeError(float from, float to, float fullDuration)
    {
        if (!Finite(from) || !Finite(to) || !Finite(fullDuration) || from < 0 || to <= from || to > fullDuration)
            return "Use 0 <= Test From < Test To <= Duration.";
        return null;
    }

    private static bool OverlapsRange(Cue cue, float from, float to)
        => cue.stopAt > from && cue.appearAt < to;

    private static float EntryStart(float appearAt, float now, bool restore)
        => restore || now - appearAt <= AbsoluteAttackSchedule.MaxFrameLateness ? appearAt : now;

    private AbsoluteAttackSchedule CreateSchedule(Cue cue)
        => IsRangeTest
            ? new AbsoluteAttackSchedule(cue.attackTimes, cue.attack.burstCount, cue.attack.shotInterval, TestFrom, TestTo)
            : new AbsoluteAttackSchedule(cue.attackTimes, cue.attack.burstCount, cue.attack.shotInterval);

    private void BeginSession(float from, float to, bool rangeTest)
    {
        if (player == null || player.IsDead || Camera.main == null)
        {
            Status = "Missing Player / Main Camera, or Player Is Dead";
            return;
        }
        string error = Validate();
        if (error != null) { Status = error; Debug.LogError(error, this); return; }
        foreach (EmitterTrack track in emitterTracks)
            if (track.emitterTemplate != null && track.emitterTemplate.gameObject.scene == gameObject.scene)
                track.emitterTemplate.gameObject.SetActive(false);
        initialized = true;
        ending = false;
        IsRangeTest = rangeTest;
        TestFrom = from;
        TestTo = to;
        Elapsed = from;
        AttackLog.Clear();
        if (rangeTest) AttackLog.Add($"EMPTY RANGE [{from:0.##}, {to:0.##}) s | Earlier attacks / hazards omitted; random points selected from current availability.");
        foreach (EmitterTrack track in emitterTracks)
        {
            if (!track.enabled || (rangeTest && !OverlapsRange(track, from, to))) continue;
            var state = new RunningCue { cue = track.Copy(), template = track.emitterTemplate,
                restoreEntry = rangeTest && track.appearAt < from };
            if (track.placementMode == PlacementMode.FixedPoint)
            {
                if (!TryGetFixedPlacement(track.pointId, track.emitterTemplate, out state.cue.position,
                    out state.cue.rotationZ, out state.cue.entrySide, out string placementError))
                { StopAndClear(placementError); return; }
            }
            state.sequence = CreateSchedule(track);
            running.Add(state);
        }
        IsRunning = true;
        Status = rangeTest ? $"Empty Range {from:0.##}-{to:0.##} s" : "Loop1 Running";
        // Placement only: never fire or reconstruct historical projectiles during setup.
        if (rangeTest) foreach (RunningCue state in running) TickPlacement(state);
    }

    public string Validate()
    {
        if (!Finite(minEmitterSpacing) || minEmitterSpacing < 0) return "Fixed Point Spacing must be finite and non-negative.";
        foreach (EmitterTrack track in emitterTracks)
        {
            if (!track.enabled) continue;
            if (!(track.emitterTemplate is ITimelineEmitter)) return "Missing or unsupported Emitter Template: " + track.label;
            Cue cue = track;
            string prefix = track.label + ": ";
            if (cue.placementMode == PlacementMode.FixedPoint &&
                !TryGetFixedPlacement(cue.pointId, track.emitterTemplate, out _, out _, out _, out string placementError))
                return prefix + placementError;
            if (cue.placementMode == PlacementMode.RandomPoints &&
                !TryGetRandomCandidates(cue, track.emitterTemplate, out _, out string randomError))
                return prefix + randomError;
            if (cue.enterDuration < 0 || cue.exitDuration < 0) return prefix + "Animation durations and delays cannot be negative.";
            if (cue.appearAt < 0 || cue.appearAt >= duration) return prefix + "Appear At must be within the loop.";
            if (cue.stopAt > duration || cue.stopAt <= cue.appearAt + cue.enterDuration)
                return prefix + "Stop At must be after entry and within the loop.";
            if (cue.attack == null || cue.attack.shotInterval < 0.05f || cue.attack.burstCount < 1 || cue.attack.bulletCount < 1)
                return prefix + "Attack counts must be positive; intervals must be at least 0.05 s.";
            if (track.emitterTemplate is ImpactEmitter && (cue.attack.landingPoints == null || cue.attack.landingPoints.Length == 0))
                return prefix + "Add at least one Landing Point.";
            if (cue.attackTimes == null || cue.attackTimes.Count == 0) return prefix + "Add at least one Attack Time (loop seconds).";
            if ((long)cue.attackTimes.Count * cue.attack.burstCount * (cue.placementMode == PlacementMode.RandomPoints ? cue.emitterCount : 1) > 10000) return prefix + "Too many scheduled shots (maximum 10000 per track).";
            var shots = AbsoluteAttackSchedule.Expand(cue.attackTimes, cue.attack.burstCount, cue.attack.shotInterval);
            float lastShot = -1;
            foreach (var shot in shots)
            {
                if (float.IsNaN(shot.time) || float.IsInfinity(shot.time) || shot.time < cue.appearAt + cue.enterDuration || shot.time >= cue.stopAt)
                    return prefix + "Every shot, including burst shots, must be after entry and before Stop At.";
                if (shot.time - lastShot < 0.0001f) return prefix + "Duplicate shot time: " + shot.time.ToString("F2") + " s.";
                lastShot = shot.time;
            }
        }
        return null;
    }

    private void Update()
    {
        if (!IsRunning) return;
        if (player == null || player.IsDead) { StopAndClear("Player Dead - Cleared"); return; }
        if (Elapsed >= TestTo && !ending)
        {
            ending = true;
            Status = "Clearing & Exiting";
            foreach (RunningCue state in RuntimeInstances())
            {
                if (state.instance != null) { state.finished = false; BeginExit(state); }
                else { ReleasePoint(state); state.finished = true; }
            }
            ClearAllAttacks();
        }
        // Finish exits first so reservation release does not depend on track ordering.
        foreach (RunningCue state in RuntimeInstances())
            if (!state.finished && (state.exitStarted >= 0 || (state.spawned && state.instance == null))) TickPlacement(state);
        foreach (RunningCue state in running) Tick(state);
        if (ending)
        {
            if (running.TrueForAll(state => state.finished)) { IsRunning = false; Status = IsRangeTest ? "Range Complete - Cleared" : "Loop1 Complete - Cleared"; }
        }
        else Elapsed = Mathf.Min(TestTo, Elapsed + Time.deltaTime);
    }

    private void Tick(RunningCue state)
    {
        state.ready = false;
        TickPlacement(state);
        if (ending) return;
        if (state.cue.placementMode == PlacementMode.RandomPoints)
        {
            if (state.members != null)
                foreach (RunningCue member in state.members) TickSequence(member);
        }
        else TickSequence(state);
    }

    private void TickSequence(RunningCue state)
    {
        state.sequence.Tick(Elapsed, index =>
        {
            if (state.placementFailure != null) return state.placementFailure;
            if (!state.ready || state.instance == null) return "Emitter not in position / track ended";
            if (!state.emitter.CanFire) return "Previous attack still active";
            if (!state.emitter.Fire(state.cue.attack, player, index)) return "Emitter busy or projectile setup invalid";
            state.emitting = true;
            return null;
        }, (planned, actual, reason) =>
        {
            string record = state.cue.label + " | planned " + planned.ToString("F2") + " s | "
                + (reason == null ? "fired " + actual.ToString("F2") + " s" : "SKIPPED: " + reason);
            AttackLog.Add(record);
            if (AttackLog.Count > 200) AttackLog.RemoveAt(0);
            if (reason != null) Debug.LogWarning(record, this);
        });
    }

    private void TickPlacement(RunningCue state)
    {
        Cue cue = state.cue;
        if (cue.placementMode == PlacementMode.RandomPoints) { TickRandomPlacement(state); return; }
        if (state.finished || Elapsed < cue.appearAt) return;
        if (!state.spawned)
        {
            if (Elapsed >= cue.stopAt)
            {
                state.finished = true;
                return;
            }
            if (cue.placementMode == PlacementMode.FixedPoint && state.reservation == null)
            {
                var candidate = new EmitterPointSelection.Candidate(cue.pointId, cue.position);
                if (!pointOccupancy.TryReserve(new[] { candidate }, 1, minEmitterSpacing, 0,
                    out state.reservation, out _))
                {
                    state.placementFailure = "Fixed point " + cue.pointId + " is occupied or too close to another reserved point; track skipped.";
                    state.finished = true;
                    RecordPlacement(state, state.placementFailure, true);
                    return;
                }
                RecordPlacement(state, "Reserved " + cue.pointId + " (entry / attack / exit)", false);
            }
            GameObject staging = new GameObject("Timeline Staging");
            staging.SetActive(false);
            state.instance = Instantiate(state.template.gameObject, staging.transform);
            foreach (EmitterTestDriver test in state.instance.GetComponentsInChildren<EmitterTestDriver>(true)) test.enabled = false;
            var component = (MonoBehaviour)state.instance.GetComponent(state.template.GetType());
            state.emitter = (ITimelineEmitter)component;
            component.enabled = true;
            state.instance.transform.SetParent(transform, true);
            state.instance.transform.position = cue.position;
            state.instance.transform.rotation = Quaternion.Euler(0, 0, cue.rotationZ);
            state.entryOrigin = GetOffscreenPosition(state.instance, cue);
            state.instance.transform.position = state.entryOrigin;
            state.instance.SetActive(true);
            Destroy(staging);
            state.instance.name = "Timeline " + cue.label;
            state.offscreen = GetOffscreenPosition(state.instance, cue);
            state.enteredAt = EntryStart(cue.appearAt, Elapsed, state.restoreEntry);
            state.spawned = true;
        }
        if (state.instance == null) { ReleasePoint(state); state.finished = true; return; }
        if (state.exitStarted >= 0)
        {
            float progress = cue.exitDuration <= 0 ? 1 : Mathf.Clamp01((Time.time - state.exitStarted) / cue.exitDuration);
            state.instance.transform.position = Vector3.Lerp(state.exitOrigin, state.offscreen, Mathf.SmoothStep(0, 1, progress));
            if (progress >= 1)
            {
                state.instance.SetActive(false);
                Destroy(state.instance);
                state.instance = null;
                ReleasePoint(state);
                state.finished = true;
            }
            return;
        }
        if (Elapsed >= cue.stopAt)
        {
            if (!state.emitter.IsBusy) BeginExit(state);
            return;
        }
        float entry = cue.enterDuration <= 0 ? 1 : Mathf.Clamp01((Elapsed - state.enteredAt) / cue.enterDuration);
        state.instance.transform.position = Vector3.Lerp(state.entryOrigin, cue.position, Mathf.SmoothStep(0, 1, entry));
        if (!state.emitting)
            state.instance.transform.rotation = Quaternion.Euler(0, 0, cue.rotationZ);
        state.ready = Elapsed + 0.0001f >= state.enteredAt + cue.enterDuration;
    }

    // Random tracks spawn independent children, each with its own animation and attack schedule.
    private IEnumerable<RunningCue> RuntimeInstances()
    {
        foreach (var state in running)
        {
            if (state.members == null) yield return state;
            else foreach (var member in state.members) yield return member;
        }
    }

    private void TickRandomPlacement(RunningCue state)
    {
        if (state.finished || Elapsed < state.cue.appearAt) return;
        if (!state.spawned)
        {
            if (Elapsed >= state.cue.stopAt) { SkipRandomGroup(state, "Entry window has ended."); return; }
            if (!TryGetRandomCandidates(state.cue, state.template, out var candidates, out string error))
            { SkipRandomGroup(state, error); return; }
            if (!pointOccupancy.TryReserve(candidates, state.cue.emitterCount, state.cue.minSpacing,
                state.cue.randomSeed, out var reservation, out var selection))
            { SkipRandomGroup(state, selection.Message); return; }

            // Resolve every member before spawning any. A failed group never partially appears.
            var members = new List<RunningCue>();
            var ids = new List<string>();
            foreach (var point in selection.Points)
            {
                var cue = state.cue.Copy();
                cue.placementMode = PlacementMode.FixedPoint;
                cue.pointId = point.Id;
                cue.label += " [" + point.Id + "]";
                if (!TryGetFixedPlacement(point.Id, state.template, out cue.position, out cue.rotationZ,
                    out cue.entrySide, out error))
                {
                    pointOccupancy.Release(reservation);
                    SkipRandomGroup(state, error);
                    return;
                }
                members.Add(new RunningCue { cue = cue, template = state.template, reservation = reservation,
                    restoreEntry = state.restoreEntry, sequence = CreateSchedule(cue) });
                ids.Add(point.Id);
            }
            state.members = members;
            state.spawned = true;
            RecordPlacement(state, "Random seed " + state.cue.randomSeed + " | Reserved " + string.Join(", ", ids), false);
        }
        foreach (var member in state.members)
        {
            member.ready = false;
            TickPlacement(member);
        }
        state.finished = state.members.TrueForAll(member => member.finished);
    }

    private void SkipRandomGroup(RunningCue state, string reason)
    {
        state.placementFailure = "Random track SKIPPED: " + reason;
        state.finished = true;
        RecordPlacement(state, state.placementFailure, true);
    }

    public bool TryGetRandomCandidates(Cue cue, MonoBehaviour template,
        out List<EmitterPointSelection.Candidate> candidates, out string error)
    {
        candidates = null; error = null;
        if (cue.emitterCount < 1 || cue.emitterCount > 120)
        { error = "Emitter Count must be between 1 and 120."; return false; }
        if (!Finite(cue.minSpacing) || cue.minSpacing < 0)
        { error = "Min Spacing must be finite and non-negative."; return false; }
        if (!Enum.IsDefined(typeof(EmitterPointLibrary.Region), cue.region))
        { error = "Choose a valid Region."; return false; }
        if (pointLibrary == null || pointLibrary.gameObject.scene != gameObject.scene)
        { error = "Assign a Point Library instance from this scene."; return false; }
        if (!(template is IEmitterOrientation))
        { error = "This emitter does not support point direction. Use Custom Position (Impact uses Landing Points)."; return false; }
        candidates = pointLibrary.GetCandidates(cue.region);
        foreach (var point in candidates)
            if (!TryGetFixedPlacement(point.Id, template, out _, out _, out _, out error)) return false;
        return true;
    }

    private void ReleasePoint(RunningCue state)
    {
        if (state.reservation == null) return;
        pointOccupancy.ReleasePoint(state.reservation, state.cue.pointId);
        state.reservation = null;
        RecordPlacement(state, "Released " + state.cue.pointId, false);
    }

    private void RecordPlacement(RunningCue state, string message, bool warning)
    {
        string record = state.cue.label + " | " + Elapsed.ToString("F2") + " s | " + message;
        AttackLog.Add(record);
        if (AttackLog.Count > 200) AttackLog.RemoveAt(0);
        if (warning) Debug.LogWarning(record, this);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static float PlacementRotation(Vector2 localForward, Vector2 worldDirection)
        => (float)((Math.Atan2(worldDirection.y, worldDirection.x)
            - Math.Atan2(localForward.y, localForward.x)) * 180 / Math.PI);

    public bool TryGetFixedPlacement(string pointId, MonoBehaviour template, out Vector3 position,
        out float rotation, out EntrySide side, out string error)
    {
        position = default; rotation = 0; side = EntrySide.Auto; error = null;
        if (pointLibrary == null || pointLibrary.gameObject.scene != gameObject.scene)
        { error = "Assign a Point Library instance from this scene."; return false; }
        if (!(template is IEmitterOrientation orientation))
        { error = "This emitter does not support point direction. Use Custom Position (Impact uses Landing Points)."; return false; }
        if (string.IsNullOrWhiteSpace(pointId)) { error = "Select a Fixed Point."; return false; }
        EmitterPointLibrary.Point found = null;
        foreach (var point in pointLibrary.Points)
        {
            if (point == null || point.Id != pointId) continue;
            if (found != null) { error = "Duplicate point ID: " + pointId; return false; }
            found = point;
        }
        if (found == null) { error = "Point not found: " + pointId; return false; }
        position = pointLibrary.GetWorldPosition(found);
        Vector2 direction = pointLibrary.GetWorldDirection(found);
        Vector2 local = orientation.LocalForwardDirection;
        if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)
            || !Finite(direction.x) || !Finite(direction.y) || direction.sqrMagnitude < 0.0001f
            || !Finite(local.x) || !Finite(local.y) || local.sqrMagnitude < 0.0001f)
        { error = "Point position and direction must be finite, with a non-zero direction."; return false; }
        rotation = PlacementRotation(local, direction);
        switch (found.EntrySide)
        {
            case EmitterPointLibrary.Side.Top: side = EntrySide.Top; break;
            case EmitterPointLibrary.Side.Bottom: side = EntrySide.Bottom; break;
            case EmitterPointLibrary.Side.Left: side = EntrySide.Left; break;
            case EmitterPointLibrary.Side.Right: side = EntrySide.Right; break;
        }
        return true;
    }

    private void BeginExit(RunningCue state)
    {
        if (state.exitStarted >= 0 || state.finished) return;
        state.exitStarted = Time.time;
        state.exitOrigin = state.instance.transform.position;
    }

    private Vector3 GetOffscreenPosition(GameObject instance, Cue cue)
    {
        Camera camera = Camera.main;
        Vector3 viewport = camera.WorldToViewportPoint(cue.position);
        EntrySide side = cue.entrySide;
        if (side == EntrySide.Auto)
        {
            float nearest = viewport.x;
            side = EntrySide.Left;
            if (1 - viewport.x < nearest) { nearest = 1 - viewport.x; side = EntrySide.Right; }
            if (viewport.y < nearest) { nearest = viewport.y; side = EntrySide.Bottom; }
            if (1 - viewport.y < nearest) side = EntrySide.Top;
        }
        // A bounding radius keeps rotated artwork fully outside the camera.
        float radius = 0.25f;
        foreach (SpriteRenderer sprite in instance.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sprite.sprite == null) continue;
            Bounds bounds = sprite.sprite.bounds;
            Vector3 center = sprite.transform.TransformPoint(bounds.center);
            float extent = Vector3.Scale(bounds.extents, sprite.transform.lossyScale).magnitude;
            radius = Mathf.Max(radius, Vector3.Distance(center, instance.transform.position) + extent + 0.1f);
        }
        Vector3 direction;
        switch (side)
        {
            case EntrySide.Left: viewport.x = 0; direction = -camera.transform.right; break;
            case EntrySide.Right: viewport.x = 1; direction = camera.transform.right; break;
            case EntrySide.Top: viewport.y = 1; direction = camera.transform.up; break;
            default: viewport.y = 0; direction = -camera.transform.up; break;
        }
        return camera.ViewportToWorldPoint(viewport) + direction * radius;
    }

    public void StopAndClear() => StopAndClear("Stopped & Cleared");
    private void StopAndClear(string status)
    {
        IsRunning = false;
        foreach (RunningCue state in RuntimeInstances())
            if (state.instance != null) { state.instance.SetActive(false); Destroy(state.instance); }
        running.Clear();
        pointOccupancy.Clear();
        ClearAllAttacks();
        Status = status;
    }
    private void ClearAllAttacks()
    {
        ClearAttacks<Bullet>();
        ClearAttacks<PatternBullet>();
        ClearAttacks<BounceBullet>();
        ClearAttacks<TraceBullet>();
        ClearAttacks<LaserBeam>();
        ClearAttacks<ImpactBullet>();
        ClearAttacks<GroundArea>();
    }
    private void ClearAttacks<T>() where T : Component
    {
        foreach (T attack in FindObjectsOfType<T>(true))
            if (attack.gameObject.scene == gameObject.scene)
            {
                attack.gameObject.SetActive(false);
                Destroy(attack.gameObject);
            }
    }
    private void OnDisable() { if (Application.isPlaying && initialized) StopAndClear(); }
}
