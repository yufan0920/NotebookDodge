using System;
using UnityEngine;

// Shared attack parameters used by level tracks and single-emitter tests.
[Serializable]
public class AttackSettings
{
    [Min(1)] public int bulletCount = 1;
    [Range(0, 180)] public float spreadAngle = 40;
    public bool aimAtPlayer;
    [Min(1)] public int burstCount = 1;
    [Min(0.05f)] public float shotInterval = 0.5f;
    [Min(0.05f)] public float cooldown = 2;
    // Zero preserves existing scenes and the Prototype driver's prefab defaults.
    [Min(0)] public float groundLifetime;
    public enum LandingPointSource { WorldCoordinates, ScenePoints }
    public LandingPointSource landingPointSource;
    public Vector2[] landingPoints = new Vector2[0];
    public Transform[] landingPointObjects = new Transform[0];

    public string ValidateLandingPoints(UnityEngine.SceneManagement.Scene scene)
    {
        if (landingPointSource == LandingPointSource.ScenePoints)
        {
            if (landingPointObjects == null || landingPointObjects.Length == 0)
                return "Add at least one Landing Point scene object.";
            for (int i = 0; i < landingPointObjects.Length; i++)
            {
                Transform point = landingPointObjects[i];
                if (point == null) return "Landing Point " + (i + 1) + " is missing. Assign a scene point.";
                if (!point.gameObject.scene.IsValid() || point.gameObject.scene != scene)
                    return "Landing Point " + (i + 1) + " must be an object in this scene, not a prefab asset or another scene.";
                if (!FiniteXY(point.position)) return "Landing Point " + (i + 1) + " has invalid world coordinates.";
            }
        }
        else if (landingPointSource == LandingPointSource.WorldCoordinates)
        {
            if (landingPoints == null || landingPoints.Length == 0) return "Add at least one Landing Point.";
            foreach (Vector2 point in landingPoints)
                if (!FiniteXY(point)) return "Landing Point coordinates must be finite.";
        }
        else return "Choose a valid Landing Point Source.";
        return null;
    }

    // Resolve once when the attack starts. Moving a marker later cannot move an active warning.
    public bool TryGetLandingPoint(int shotIndex, out Vector2 position)
    {
        position = default;
        if (shotIndex < 0) return false;
        if (landingPointSource == LandingPointSource.ScenePoints)
        {
            if (landingPointObjects == null || landingPointObjects.Length == 0) return false;
            Transform point = landingPointObjects[shotIndex % landingPointObjects.Length];
            if (point == null) return false;
            position = point.position;
        }
        else if (landingPointSource == LandingPointSource.WorldCoordinates)
        {
            if (landingPoints == null || landingPoints.Length == 0) return false;
            position = landingPoints[shotIndex % landingPoints.Length];
        }
        else return false;
        return FiniteXY(position);
    }

    private static bool FiniteXY(Vector2 value)
        => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    public AttackSettings Copy()
    {
        var copy = (AttackSettings)MemberwiseClone();
        copy.landingPoints = landingPoints == null ? new Vector2[0] : (Vector2[])landingPoints.Clone();
        copy.landingPointObjects = landingPointObjects == null ? new Transform[0] : (Transform[])landingPointObjects.Clone();
        return copy;
    }
}
