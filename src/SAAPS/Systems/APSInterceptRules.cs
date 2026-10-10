namespace SprocketAdvancedAPS.Systems
{
    internal static class APSInterceptRules
    {
        internal static bool Ready(bool hardKill, bool enabled, bool operational, bool destroyed, float health, int ammo, float cooldown, bool friendly) =>
            hardKill && enabled && operational && !destroyed && float.IsFinite(health) && health > 0f &&
            ammo > 0 && float.IsFinite(cooldown) && cooldown <= 0f && !friendly;

        // Jamming needs the same live module, ammo and cooldown state; only the hard-kill veto is dropped.
        internal static bool JamReady(bool canJam, bool enabled, bool operational, bool destroyed, float health, int ammo, float cooldown, bool friendly) =>
            canJam && enabled && operational && !destroyed && float.IsFinite(health) && health > 0f &&
            ammo > 0 && float.IsFinite(cooldown) && cooldown <= 0f && !friendly;

        internal const float JamHorizontalFOV = 30f;
        internal const float JamVerticalFOV = 20f;

        internal static bool InCone(float x, float y, float z, float horizontalFOV, float verticalFOV)
        {
            if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z) ||
                !float.IsFinite(horizontalFOV) || !float.IsFinite(verticalFOV) ||
                horizontalFOV <= 0f || horizontalFOV > 360f || verticalFOV <= 0f || verticalFOV > 180f) return false;
            double horizontal = Math.Sqrt((double)x * x + (double)z * z);
            if (horizontal == 0d && y == 0f) return false;
            return Math.Abs(Math.Atan2(x, z) * 180d / Math.PI) <= horizontalFOV * .5d + 1e-5d &&
                Math.Abs(Math.Atan2(y, horizontal) * 180d / Math.PI) <= verticalFOV * .5d + 1e-5d;
        }

        // A held jam requires a live emitter, not another round of ammo/cooldown.
        internal static bool JamHeld(bool enabled, bool operational, bool destroyed, float health, bool friendly) =>
            enabled && operational && !destroyed && float.IsFinite(health) && health > 0f && !friendly;

        // How fast a jammed round is walked off its aim line; a stronger jam turns it harder.
        internal static float DeflectionRate(float strength) => 1.2f + 2.5f * Math.Clamp(strength, 0f, 1f);

        // Slerp weight for one tick of guided deflection, clamped so a dropped frame
        // cannot teleport a round or spin it backwards.
        internal static float DeflectionWeight(float rate, float deltaTime)
        {
            if (!float.IsFinite(rate) || !float.IsFinite(deltaTime) || rate <= 0f || deltaTime <= 0f) return 0f;
            return Math.Clamp(rate * deltaTime, 0f, 1f);
        }

        internal static bool Eligible(float speed, float minimum, float maximum, bool classSupported, bool speedBased) =>
            float.IsFinite(speed) && float.IsFinite(minimum) && float.IsFinite(maximum) &&
            minimum >= 0f && maximum >= minimum && speed > 0f && speed >= minimum && speed <= maximum &&
            (speedBased || classSupported);

        internal static bool Approaching(float distanceSquared, float closingDot, float relativeSpeedSquared, float radius) =>
            float.IsFinite(distanceSquared) && float.IsFinite(closingDot) && float.IsFinite(relativeSpeedSquared) &&
            float.IsFinite(radius) && radius > 0f && distanceSquared >= 0f && closingDot > 0f && relativeSpeedSquared > 0f &&
            distanceSquared - closingDot * closingDot / relativeSpeedSquared <= radius * radius;

        // A round already inside the module's minimum stand-off is the most dangerous one there
    // is; the sector range test rejects it, but nothing is gained by waiting it out.
    internal static bool InsideMinRange(float distanceSquared, float minRange) =>
        float.IsFinite(distanceSquared) && float.IsFinite(minRange) && minRange > 0f &&
        distanceSquared >= 0f && distanceSquared < minRange * minRange;

    internal static bool ReactionReady(float now, float detectedAt, float delay) =>
            float.IsFinite(now) && float.IsFinite(detectedAt) && float.IsFinite(delay) && delay >= 0f && now - detectedAt >= delay;

        // A target that reaches the protected vehicle before the tracker can finish reacting
        // is engaged immediately, otherwise short windows would never produce a launch.
        internal static bool ReactionSatisfied(float now, float detectedAt, float delay, float timeToImpact) =>
            ReactionReady(now, detectedAt, delay) ||
            (float.IsFinite(timeToImpact) && timeToImpact >= 0f && timeToImpact <= delay);

        internal static float Probability(float configured, float fallback, bool speedBased) =>
            Math.Clamp(float.IsFinite(configured) ? (speedBased && configured <= 0f ? fallback : configured) : 0f, 0f, 1f);

        // Native projectile flags: bit 0 active, bits 1-3 mark released/held slots.
        internal static bool IsLiveNative(int flags) => (flags & 1) != 0 && (flags & 14) == 0;
    }
}
