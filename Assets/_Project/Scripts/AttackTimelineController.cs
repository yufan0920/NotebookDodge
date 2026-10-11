using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class AttackTimelineController : MonoBehaviour
{
    public enum EntrySide { Auto, Left, Right, Top, Bottom }

    public enum Transition { Reenter, Move }

    [Serializable]
    public class Cue
    {
        public string label;
        public bool enabled = true;
        public Transition transition;
        public AttackSettings attack = new AttackSettings();
        [Min(0)] public float appearAt;
        [Min(0)] public float stopAt = 60;
        public Vector3 position;
        public float rotationZ;
        public EntrySide entrySide;
        [Min(0)] public float enterDuration = 0.5f;
        [Min(0)] public float firstShotDelay;
        [Min(0)] public float exitDuration = 0.5f;
        public Cue Copy()
        {
            var copy = (Cue)MemberwiseClone();
            copy.attack = attack.Copy();
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

    [SerializeField] private PlayerHealth player;
    [SerializeField, Min(1)] private float duration = 60;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private List<Track> tracks = new List<Track>();
    [SerializeField] private List<GameObject> sceneEmitters = new List<GameObject>();
    private sealed class RunningCue
    {
        public Cue cue;
        public MonoBehaviour template;
        public RunningCue previous, next;
        public AttackSequence sequence;
        public float enteredAt, startAngle;
        public Vector3 entryOrigin;
        public GameObject instance;
        public ITimelineEmitter emitter;
        public float exitStarted = -1;
        public Vector3 exitOrigin, offscreen;
        public bool spawned, finished, emitting;

    }
    private readonly List<RunningCue> running = new List<RunningCue>();
    private bool initialized, ending;
    public float Elapsed { get; private set; }
    public float Duration => duration;
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
        if (player == null || player.IsDead || Camera.main == null)
        {
            Status = "Missing Player / Main Camera, or Player Is Dead";
            return;
        }
        string error = Validate();
        if (error != null) { Status = error; Debug.LogError(error, this); return; }
        foreach (Track track in tracks)
            if (track.emitterTemplate != null && track.emitterTemplate.gameObject.scene == gameObject.scene)
                track.emitterTemplate.gameObject.SetActive(false);
        initialized = true;
        ending = false;
        Elapsed = 0;
        foreach (Track track in tracks)
        {
            if (!track.enabled) continue;
            RunningCue previous = null;
            var ordered = track.clips.FindAll(clip => clip.enabled);
            ordered.Sort((a, b) => a.appearAt.CompareTo(b.appearAt));
            foreach (Cue cue in ordered)
            {
                var state = new RunningCue { cue = cue.Copy(), template = track.emitterTemplate, previous = previous };
                if (previous != null) previous.next = state;
                running.Add(state);
                previous = state;
            }
        }
        IsRunning = true;
        Status = "Loop1 Running";
    }

    public string Validate()
    {
        foreach (Track track in tracks)
        {
            if (!track.enabled) continue;
            if (!(track.emitterTemplate is ITimelineEmitter)) return "Missing or unsupported Emitter Template: " + track.label;
            var ordered = track.clips.FindAll(clip => clip.enabled);
            ordered.Sort((a, b) => a.appearAt.CompareTo(b.appearAt));
            float previousStop = -1;
            foreach (Cue cue in ordered)
            {
                string prefix = track.label + " / " + cue.label + ": ";
                if (cue.enterDuration < 0 || cue.firstShotDelay < 0 || cue.exitDuration < 0) return prefix + "Animation durations and delays cannot be negative.";
                if (cue.appearAt < 0 || cue.appearAt >= duration) return prefix + "Appear At must be within the loop.";
                if (cue.stopAt > duration || cue.stopAt <= cue.appearAt + cue.enterDuration + cue.firstShotDelay)
                    return prefix + "Stop At must be after entry + First Shot Delay and within the loop.";
                if (cue.appearAt < previousStop) return prefix + "Clips on one track cannot overlap. Use another track for simultaneous emitters.";
                if (cue.attack == null || cue.attack.cooldown < 0.05f || cue.attack.shotInterval < 0.05f || cue.attack.burstCount < 1 || cue.attack.bulletCount < 1)
                    return prefix + "Attack counts must be positive; intervals must be at least 0.05 s.";
                if (track.emitterTemplate is ImpactEmitter && (cue.attack.landingPoints == null || cue.attack.landingPoints.Length == 0))
                    return prefix + "Add at least one Landing Point.";
                previousStop = cue.stopAt;
            }
        }
        return null;
    }

    private void Update()
    {
        if (!IsRunning) return;
        if (player == null || player.IsDead) { StopAndClear("Player Dead - Cleared"); return; }
        if (Elapsed >= duration && !ending)
        {
            ending = true;
            Status = "Clearing & Exiting";
            foreach (RunningCue state in running)
            {
                if (state.instance != null) { state.finished = false; BeginExit(state); }
                else state.finished = true;
            }
            ClearAllAttacks();
        }
        foreach (RunningCue state in running) Tick(state);
        if (ending)
        {
            if (running.TrueForAll(state => state.finished)) { IsRunning = false; Status = "Loop1 Complete - Cleared"; }
        }
        else Elapsed = Mathf.Min(duration, Elapsed + Time.deltaTime);
    }

    private void Tick(RunningCue state)
    {
        Cue cue = state.cue;
        if (state.finished || Elapsed < cue.appearAt) return;
        if (!state.spawned)
        {
            if (state.previous != null && !state.previous.finished) return;
            if (Elapsed >= cue.stopAt)
            {
                if (state.previous != null && state.previous.instance != null)
                {
                    state.previous.finished = false;
                    BeginExit(state.previous);
                    return;
                }
                state.finished = true;
                return;
            }
            bool reuse = cue.transition == Transition.Move && state.previous != null && state.previous.instance != null;
            if (reuse)
            {
                state.instance = state.previous.instance;
                state.emitter = state.previous.emitter;
                state.previous.instance = null;
                state.entryOrigin = state.instance.transform.position;
                state.startAngle = state.instance.transform.eulerAngles.z;
            }
            else
            {
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
                state.startAngle = cue.rotationZ;
                state.instance.transform.position = state.entryOrigin;
                state.instance.SetActive(true);
                Destroy(staging);
            }
            state.instance.name = "Timeline " + cue.label;
            state.offscreen = GetOffscreenPosition(state.instance, cue);
            state.enteredAt = Elapsed;
            state.sequence = new AttackSequence();
            state.spawned = true;
        }
        if (state.instance == null) { state.finished = true; return; }
        if (state.exitStarted >= 0)
        {
            float progress = cue.exitDuration <= 0 ? 1 : Mathf.Clamp01((Time.time - state.exitStarted) / cue.exitDuration);
            state.instance.transform.position = Vector3.Lerp(state.exitOrigin, state.offscreen, Mathf.SmoothStep(0, 1, progress));
            if (progress >= 1) { Destroy(state.instance); state.finished = true; }
            return;
        }
        if (Elapsed >= cue.stopAt)
        {
            if (!state.emitter.IsBusy)
            {
                if (state.next != null && !state.next.finished && state.next.cue.transition == Transition.Move) state.finished = true;
                else BeginExit(state);
            }
            return;
        }
        float entry = cue.enterDuration <= 0 ? 1 : Mathf.Clamp01((Elapsed - state.enteredAt) / cue.enterDuration);
        state.instance.transform.position = Vector3.Lerp(state.entryOrigin, cue.position, Mathf.SmoothStep(0, 1, entry));
        if (!state.emitting)
            state.instance.transform.rotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(state.startAngle, cue.rotationZ, Mathf.SmoothStep(0, 1, entry)));
        if (entry >= 1 && Elapsed >= state.enteredAt + cue.enterDuration + cue.firstShotDelay)
        {
            state.emitting = true;
            state.sequence.Tick(Time.deltaTime, state.emitter, cue.attack, player);
            if (state.sequence.Failed)
            {
                Debug.LogError("Emission failed: " + cue.label + ". Check the projectile prefab and attack settings.", this);
                BeginExit(state);
            }
        }
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
        foreach (RunningCue state in running)
            if (state.instance != null) { state.instance.SetActive(false); Destroy(state.instance); }
        running.Clear();
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
