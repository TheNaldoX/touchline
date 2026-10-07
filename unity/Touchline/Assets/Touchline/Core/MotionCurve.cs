using System;

namespace Touchline.Core
{
    // Rendu fluide entre deux pas de simulation (10 par seconde).
    // Une interpolation linéaire relie les positions par des segments : la
    // vitesse change d'un coup à chaque pas, ce qui se voit comme une saccade
    // dans les virages et les accélérations. Cette courbe d'Hermite passe par
    // les mêmes positions aux mêmes instants (aucun retard, contacts avec le
    // ballon inchangés) mais sa vitesse est continue d'un pas à l'autre : la
    // tangente de fin d'un segment est la tangente de début du suivant.
    public sealed class MotionCurve
    {
        // Au-delà, le déplacement est une téléportation (remise en place,
        // changement de joueur) : on revient à l'interpolation linéaire.
        public const float MaxPlausibleSpeed=12f; // m/s
        Point lastPrevious,lastPosition,chordPrevious,chordCurrent;
        bool hasCurrent,hasPrevious;

        public void Reset(){hasCurrent=hasPrevious=false;}

        // À appeler à chaque image avec les positions du pas en cours.
        public void Observe(Point previous,Point position,float step)
        {
            if(hasCurrent&&previous.x==lastPrevious.x&&previous.z==lastPrevious.z&&position.x==lastPosition.x&&position.z==lastPosition.z)return;
            var chord=(position-previous)/step;
            bool continuous=hasCurrent&&Point.Distance(previous,lastPosition)<.001f;
            if(chord.Length>MaxPlausibleSpeed){hasCurrent=hasPrevious=false;lastPrevious=previous;lastPosition=position;return;}
            chordPrevious=continuous?chordCurrent:chord;hasPrevious=continuous||hasCurrent;
            chordCurrent=chord;hasCurrent=true;lastPrevious=previous;lastPosition=position;
        }

        bool Smooth=>hasCurrent&&hasPrevious;

        public Point Position(Point previous,Point position,float step,float t)
        {
            t=Mathx.Clamp(t,0,1);if(!Smooth)return Point.Lerp(previous,position,t);
            float t2=t*t,t3=t2*t;
            float h00=2*t3-3*t2+1,h10=t3-2*t2+t,h01=-2*t3+3*t2,h11=t3-t2;
            return previous*h00+chordPrevious*(h10*step)+position*h01+chordCurrent*(h11*step);
        }

        // Vitesse (m/s) de la courbe à l'instant t, continue d'un pas à l'autre.
        public Point Velocity(Point previous,Point position,float step,float t)
        {
            t=Mathx.Clamp(t,0,1);if(!Smooth)return (position-previous)/step;
            float t2=t*t;
            float d00=6*t2-6*t,d10=3*t2-4*t+1,d01=-6*t2+6*t,d11=3*t2-2*t;
            return (previous*d00+position*d01)/step+chordPrevious*d10+chordCurrent*d11;
        }
    }
}
