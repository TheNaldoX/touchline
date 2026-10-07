using System;

namespace Touchline.Core
{
    // Formule de vol scripté du ballon (passe, centre, tir, dégagement…),
    // identique à MatchBall.UpdateBall : sert au rendu entre deux pas.
    public static class BallFlight
    {
        public struct Sample { public Point position; public float height; }

        // Le pas précédent était déjà sur la même trajectoire : on peut
        // évaluer la formule entre les deux pas sans créer de saut.
        // Both ends of the step are checked against the formula, so any contact
        // or restart that moved the ball falls back to the plain interpolation.
        public static bool InScriptedFlight(BallState b,float step)
        {
            if(b==null||!string.IsNullOrEmpty(b.owner)||b.held||b.kind=="loose"||b.kind=="none"||b.elapsed-step<.0001f||b.elapsed>Math.Max(.1f,b.duration))return false;
            var now=At(b,b.elapsed);var before=At(b,b.elapsed-step);
            return Point.Distance(now.position,b.position)<Tolerance&&Math.Abs(now.height-b.height)<Tolerance
                &&Point.Distance(before.position,b.previous)<Tolerance&&Math.Abs(before.height-b.previousHeight)<Tolerance;
        }
        const float Tolerance=.02f; // m

        public static Sample At(BallState b,float elapsed)
        {
            float u=Mathx.Clamp(elapsed/Math.Max(.1f,b.duration),0,1);
            return new Sample{position=Point.Lerp(b.start,b.end,u),height=b.startHeight+(b.endHeight-b.startHeight)*u+(float)Math.Sin(Math.PI*u)*b.loft};
        }
    }
}
