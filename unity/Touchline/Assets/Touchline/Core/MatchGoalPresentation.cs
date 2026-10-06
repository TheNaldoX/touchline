using System;

namespace Touchline.Core
{
    // Descriptive evidence only: never serialized into authoritative state.
    public struct GoalPresentationContact
    {
        public bool valid;
        public float clock, fraction, previousHeight, height, verticalVelocity, restartDuration;
        public Point previous, impact, velocity, eventEnd;
        public int scoringSide, restartSide, homeScore, awayScore, eventIndex;
        public string home, away, goalPlayer;
        public BallReleaseContact release;
        [NonSerialized] public MatchState stateIdentity;
        [NonSerialized] public BallState restartBallIdentity;

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Point value) => Finite(value.x) && Finite(value.z);
        public static GoalPresentationContact Capture(MatchState state, float fraction,
            Point impact, float height, int scoringSide, BallReleaseContact release = default,
            float? impactVerticalVelocity = null)
        {
            if (state == null || state.ball == null || state.events == null || state.score == null ||
                state.score.Length != 2 || scoringSide < 0 || scoringSide > 1 || !Finite(state.clock) ||
                !Finite(fraction) || fraction < 0 || fraction > 1 || !Finite(impact) || !Finite(height)) return default;
            var b = state.ball;
            float vertical = impactVerticalVelocity ?? b.verticalVelocity;
            if (!Finite(b.previous) || !Finite(b.previousHeight) || !Finite(b.velocity) || !Finite(vertical)) return default;
            int index = state.events.Count - 1;
            if (index < 0) return default;
            var e = state.events[index];
            if (e == null || e.kind != "goal" || e.time != state.clock || e.side != scoringSide || !Finite(e.position)) return default;
            bool currentRelease = release.valid && release.clock == state.clock && release.kind == b.kind &&
                release.source == b.from && b.elapsed >= 0 && b.elapsed <= MatchSimulation.Step + .000001f &&
                Finite(release.previous) && Finite(release.previousHeight) && Finite(release.release) &&
                Finite(release.releaseHeight) && Finite(release.end) && Finite(release.endHeight) &&
                Finite(release.flightEnd) && Finite(release.flightEndHeight) && Finite(release.fraction) &&
                release.fraction >= 0 && release.fraction <= fraction && Finite(release.duration) && release.duration >= .1f &&
                Finite(release.loft) && Point.Distance(release.end, b.position) < .00001f && Math.Abs(release.endHeight - b.height) < .00001f;
            return new GoalPresentationContact {
                valid = true, clock = state.clock, fraction = fraction, previous = b.previous,
                previousHeight = b.previousHeight, impact = impact, height = height,
                velocity = b.velocity, verticalVelocity = vertical, scoringSide = scoringSide,
                restartSide = 1 - scoringSide, homeScore = state.score[0], awayScore = state.score[1],
                home = state.home, away = state.away, restartDuration = state.HalfDuration == 2700 ? 45 : 3,
                release = currentRelease ? release : default, eventIndex = index, eventEnd = e.position,
                goalPlayer = e.player, stateIdentity = state
            };
        }

        static bool MatchesRestartFields(GoalPresentationContact c, MatchState m)
        {
            if (!c.valid || m == null || m.ball == null || m.events == null || m.score == null || m.score.Length != 2 ||
                !Finite(m.clock) || !Finite(c.clock) || !Finite(c.impact) || !Finite(c.eventEnd) ||
                c.eventIndex < 0 || c.eventIndex >= m.events.Count) return false;
            var e = m.events[c.eventIndex];
            float sign = c.impact.x >= 0 ? 1 : -1;
            return e != null && ReferenceEquals(c.stateIdentity, m) && m.phase == "goal" && m.restart > 0 &&
                m.restartSide == c.restartSide && m.home == c.home && m.away == c.away &&
                m.score[0] == c.homeScore && m.score[1] == c.awayScore &&
                m.ball.owner == null && m.ball.kind == "none" &&
                Point.Distance(m.ball.position, new Point(sign * 53, c.impact.z)) < .0001f &&
                Math.Abs(m.ball.height - MatchSimulation.BallRadius) < .0001f && m.clock >= c.clock &&
                m.clock - c.clock <= c.restartDuration + .15f && e.kind == "goal" && e.time == c.clock &&
                e.side == c.scoringSide && e.player == c.goalPlayer && Point.Distance(e.position, c.eventEnd) < .0001f;
        }
        public static bool MatchesRestart(GoalPresentationContact c, MatchState m) =>
            MatchesRestartFields(c, m) && ReferenceEquals(c.restartBallIdentity, m.ball);
        public static bool BindRestartBall(ref GoalPresentationContact c, MatchState m)
        {
            if (!MatchesRestartFields(c, m)) return false;
            if (c.restartBallIdentity == null && m.clock == c.clock) c.restartBallIdentity = m.ball;
            return ReferenceEquals(c.restartBallIdentity, m.ball);
        }
    }

    public sealed partial class MatchSimulation
    {
        GoalPresentationContact goalContact;
        public GoalPresentationContact GoalContact =>
            GoalPresentationContact.MatchesRestart(goalContact, State) ? goalContact : default;
        // After Emit(goal)/score update, before Restart replaces ball. Fraction
        // is GLOBAL within the fixed step, including preparation before flight.
        void RecordGoalContact(float fraction, Point impact, float height, int scoringSide)
        {
            float vertical = State.ball.verticalVelocity;
            if (CurrentFirstReleasedFlight(out var release)) vertical = ReleasedVerticalVelocity(release, fraction);
            goalContact = GoalPresentationContact.Capture(State, fraction, impact, height, scoringSide, ReleaseContact, vertical);
        }
        // End of TickCore; do NOT reset at the beginning of the next tick.
        void MaintainGoalContact()
        {
            if (!GoalPresentationContact.BindRestartBall(ref goalContact, State)) goalContact = default;
        }
    }
}
