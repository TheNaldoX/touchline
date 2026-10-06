using System;
using System.Linq;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public void Caution(string id)
        {
            var p=Find(id);if(p==null||p.sentOff)throw new InvalidOperationException("Joueur absent du terrain.");p.yellows++;Emit("yellow",p.side,p.id,Data(p).name+" reçoit un avertissement.");if(p.yellows>=2)SendOff(id);
        }
        public void SendOff(string id)
        {
            var p=Find(id);if(p==null||p.sentOff)throw new InvalidOperationException("Joueur déjà exclu.");p.sentOff=true;p.velocity=new Point();Emit("red",p.side,p.id,Data(p).name+" est exclu. Son équipe joue en infériorité numérique.");
            if(State.ball.owner==id){State.ball.owner=null;State.ball.held=false;State.ball.kind="loose";}if(State.ball.to==id)State.ball.to=null;
            if(p.slot==0){var emergency=State.actors.LastOrDefault(a=>a.side==p.side&&!a.sentOff);if(emergency!=null){int at=p.side*11+emergency.slot;int slot=emergency.slot;State.actors[p.side*11]=emergency;State.actors[at]=p;emergency.slot=0;p.slot=slot;}}
            if(State.actors.Count(a=>a.side==p.side&&!a.sentOff)<7){State.finished=true;State.score[p.side]=0;State.score[1-p.side]=Math.Max(3,State.score[1-p.side]);Emit("abandoned",p.side,null,"Rencontre arrêtée : moins de sept joueurs. Résultat administratif appliqué.");}
        }
        void MedicalDuringMatch()
        {
            if(!State.professionalRules||State.restart>0||State.clock<State.nextMedicalCheck)return;State.nextMedicalCheck=State.clock+State.SecondsPerMinute;
            foreach(var p in State.actors){if(p.sentOff||p.injured||p.velocity.Length<2)continue;float risk=.00014f*(1+(100-p.fitness)/25)*(Tactic(p.side).pressing>.75f?1.25f:1);if(Random()>=risk)continue;p.injured=true;p.fitness=Math.Max(20,p.fitness-18);p.action="hurt";p.actionTime=2;Emit("injury",p.side,p.id,Data(p).name+" ressent une douleur et demande l’intervention du staff médical.");}
        }
    }
}
