using System;
using System.Collections.Generic;
using Touchline.Core;

namespace Touchline
{
    public struct GoalBallPose
    {
        public Point position; public float height;
        public GoalBallPose(Point position, float height) { this.position = position; this.height = height; }
    }

    // Independent, bounded presentation; no Unity dependency and no writes to
    // MatchState. Sampling uses the match clock, never accumulated frame dt.
    public sealed class GoalNetPresentation
    {
        public const float Front = 52.5f, Back = 54.3f, HalfWidth = 3.66f, TopFront = 2.44f, TopBack = 2.20f;
        const float Gravity = 9.81f, SampleStep = 1f / 240, MaximumSettle = 8, ClothYieldTime = .12f;
        object identity; GoalPresentationContact contact; GoalBallPose[] path; bool active;
        double lastTime = double.NaN;
        float lastStateClock = float.NaN, blockedClock = float.NaN;
        public bool Active => active;
        public float SettleDuration { get; private set; }
        public float ResidualSpeed { get; private set; }
        public int PathSamples => path?.Length ?? 0;
        public void Reset()
        {
            identity = null; contact = default; path = null; active = false;
            lastTime = double.NaN; lastStateClock = blockedClock = float.NaN; SettleDuration = ResidualSpeed = 0;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static bool Finite(Point value) => Finite(value.x) && Finite(value.z);
        static float Roof(float depth) => TopFront + (TopBack - TopFront) * Mathx.Clamp((depth - Front) / (Back - Front), 0, 1) - MatchSimulation.BallRadius;
        static GoalBallPose Lerp(GoalBallPose a, GoalBallPose b, float t) => new GoalBallPose(Point.Lerp(a.position, b.position, t), a.height + (b.height - a.height) * t);
        static bool SafeStart(GoalPresentationContact c)
        {
            float d = Math.Abs(c.impact.x);
            // Core accepts the centre within the goal opening. Do not reject a
            // valid near-post/near-bar goal because the sphere overlaps cloth.
            var release = c.release;
            bool safeRelease = !release.valid || Finite(release.clock) && Finite(release.previous) && Finite(release.previousHeight) &&
                Finite(release.release) && Finite(release.releaseHeight) && Finite(release.end) && Finite(release.endHeight) &&
                Finite(release.flightEnd) && Finite(release.flightEndHeight) && Finite(release.fraction) &&
                release.fraction >= 0 && release.fraction <= 1 && Finite(release.duration) && release.duration >= .1f && Finite(release.loft);
            return safeRelease && Finite(c.clock) && Finite(c.fraction) && Finite(c.previous) && Finite(c.previousHeight) &&
                Finite(c.impact) && Finite(c.height) && Finite(c.velocity) && Finite(c.verticalVelocity) &&
                c.fraction >= 0 && c.fraction <= 1 && d >= Front + MatchSimulation.BallRadius - .0001f &&
                d <= Back && Math.Abs(c.impact.z) < HalfWidth && c.height >= 0 && c.height < TopFront;
        }
        bool BuildPath()
        {
            var result = new List<GoalBallPose>(384);
            var p = contact.impact; var v = contact.velocity;
            float h = contact.height, vy = contact.verticalVelocity, sign = p.x >= 0 ? 1 : -1, r = MatchSimulation.BallRadius;
            result.Add(new GoalBallPose(p, h));
            for (int i = 1; i <= (int)(MaximumSettle / SampleStep); i++)
            {
                p += v * SampleStep; h += vy * SampleStep - Gravity * SampleStep * SampleStep * .5f; vy -= Gravity * SampleStep;
                // Cloth yields temporarily at an accepted overlapping centre,
                // then returns to the ordinary radius-padded net over .12 s.
                float yield = 1 - Mathx.Clamp(i * SampleStep / ClothYieldTime, 0, 1);
                float back = Back - r + Math.Max(0, Math.Abs(contact.impact.x) - (Back - r)) * yield;
                float side = HalfWidth - r + Math.Max(0, Math.Abs(contact.impact.z) - (HalfWidth - r)) * yield;
                float floor = r - Math.Max(0, r - contact.height) * yield;
                float roof = Roof(p.x * sign) + Math.Max(0, contact.height - Roof(Math.Abs(contact.impact.x))) * yield;
                if (p.x * sign > back) { p.x = sign * back; v.x = 0; v.z *= .10f; vy = Math.Min(0, vy * .10f); }
                if (p.x * sign < Front + r) { p.x = sign * (Front + r); v.x = 0; }
                if (Math.Abs(p.z) > side) { p.z = Math.Sign(p.z) * side; v.z = 0; v.x *= .20f; vy = Math.Min(0, vy); }
                if (h > roof) { h = roof; vy = Math.Min(0, vy) * .15f; v *= .40f; }
                if (h < floor)
                {
                    h = floor; vy = 0; float speed = v.Length;
                    v = speed > 3.5f * SampleStep ? v.Normalized * (speed - 3.5f * SampleStep) : new Point();
                }
                if (!Finite(p) || !Finite(h) || !Finite(v) || !Finite(vy)) return false;
                result.Add(new GoalBallPose(p, h));
                if (yield == 0 && h <= r + .00001f && v.Length < .0001f && Math.Abs(vy) < .0001f)
                {
                    path = result.ToArray(); SettleDuration = (path.Length - 1) * SampleStep;
                    ResidualSpeed = v.Length + Math.Abs(vy); return true;
                }
            }
            // Never freeze a moving tail at the budget boundary. Safe fallback
            // if malformed/extreme evidence fails to settle within the cap.
            return false;
        }
        GoalBallPose Net(float age)
        {
            float sample = Math.Max(0, age) / SampleStep;
            if (sample >= path.Length - 1) return path[path.Length - 1];
            int at = (int)sample; return Lerp(path[at], path[at + 1], sample - at);
        }
        static GoalBallPose ReleasePath(BallReleaseContact r, float alpha)
        {
            if (alpha <= 0) return new GoalBallPose(r.previous, r.previousHeight);
            if (alpha >= 1) return new GoalBallPose(r.end, r.endHeight);
            if (alpha < r.fraction) return Lerp(new GoalBallPose(r.previous, r.previousHeight), new GoalBallPose(r.release, r.releaseHeight), alpha / r.fraction);
            float u = Mathx.Clamp((alpha - r.fraction) * MatchSimulation.Step / Math.Max(.1f, r.duration), 0, 1);
            return new GoalBallPose(Point.Lerp(r.release, r.flightEnd, u), r.releaseHeight + (r.flightEndHeight - r.releaseHeight) * u + (float)Math.Sin(Math.PI * u) * r.loft);
        }
        static GoalBallPose Incoming(GoalPresentationContact c, float alpha)
        {
            var r = c.release; var at = r.valid ? ReleasePath(r, c.fraction) : default;
            if (r.valid && r.clock == c.clock && c.fraction >= r.fraction && Point.Distance(at.position, c.impact) < .002f && Math.Abs(at.height - c.height) < .002f)
                return ReleasePath(r, alpha);
            return Lerp(new GoalBallPose(c.previous, c.previousHeight), new GoalBallPose(c.impact, c.height), c.fraction > 0 ? Mathx.Clamp(alpha / c.fraction, 0, 1) : 1);
        }
        public bool TryPosition(object simulationIdentity, MatchState state, GoalPresentationContact fresh, float alpha, out GoalBallPose pose)
        {
            pose = default;
            if (simulationIdentity == null || state == null || !Finite(alpha) || !Finite(state.clock)) { Reset(); return false; }
            if (!ReferenceEquals(identity, simulationIdentity)) { Reset(); identity = simulationIdentity; }
            alpha = Mathx.Clamp(alpha, 0, 1); double time = (double)state.clock - MatchSimulation.Step * (1 - (double)alpha);
            if (Finite(lastStateClock) && state.clock < lastStateClock - .0001f)
            {
                blockedClock = contact.clock; active = false; path = null; lastTime = time; lastStateClock = state.clock; return false;
            }
            // Same-tick interpolation rewinds occur when pause/speed resets the
            // accumulator. Hold the already displayed pose, keep the goal latch.
            if (Finite(lastTime)) time = Math.Max(time, lastTime);
            lastTime = time; lastStateClock = state.clock;
            if (active && !GoalPresentationContact.MatchesRestart(contact, state)) { blockedClock = contact.clock; active = false; path = null; }
            if (fresh.valid && fresh.clock <= state.clock && fresh.clock != blockedClock &&
                GoalPresentationContact.MatchesRestart(fresh, state) && SafeStart(fresh) && (!active || fresh.clock != contact.clock))
            {
                contact = fresh;
                active = BuildPath();
                if (!active) { blockedClock = contact.clock; path = null; }
            }
            if (!active) return false;
            double impactTime = (double)contact.clock - MatchSimulation.Step + (double)contact.fraction * MatchSimulation.Step;
            float effectiveAlpha = Mathx.Clamp((float)((time - ((double)contact.clock - MatchSimulation.Step)) / MatchSimulation.Step), 0, 1);
            pose = state.clock == contact.clock && time < impactTime ? Incoming(contact, effectiveAlpha) : Net((float)Math.Max(0, time - impactTime));
            return true;
        }
    }
}
