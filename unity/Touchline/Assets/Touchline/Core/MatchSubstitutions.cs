using System;
using System.Linq;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        public static string PendingSubstitutionRestriction(MatchState m,int side,string outgoing,string incoming)
        {
            var pending=m.pendingSubstitutions;
            if(pending==null)return null;
            if(pending.Any(p=>p.incoming==incoming&&p.outgoing!=outgoing))return "Entrée déjà préparée pour un autre joueur.";
            if(m.substitutions[side]+pending.Count(p=>p.side==side&&p.outgoing!=outgoing)>=5)return "Les remplacements restants sont déjà préparés.";
            return null;
        }
        public void RequestSubstitution(int side,int slot,string incoming)
        {
            ValidateSubstitution(side,slot,incoming);
            var outgoing=State.actors[side*11+slot].id;
            var error=PendingSubstitutionRestriction(State,side,outgoing,incoming);if(error!=null)throw new InvalidOperationException(error);
            CancelSubstitution(outgoing);
            State.pendingSubstitutions.Add(new PendingSubstitution{side=side,outgoing=outgoing,incoming=incoming});
            if(State.halfTime||State.restart>0)ApplyPendingSubstitutions();
        }
        public void CancelSubstitution(string outgoing)=>State.pendingSubstitutions.RemoveAll(p=>p.outgoing==outgoing);
        void ApplyPendingSubstitutions()
        {
            if(State.finished||(!State.halfTime&&State.restart<=0)||State.pendingSubstitutions.Count==0)return;
            var requests=State.pendingSubstitutions.ToArray();State.pendingSubstitutions.Clear();
            foreach(var request in requests){
                var actor=State.actors.FirstOrDefault(p=>p.side==request.side&&p.id==request.outgoing);
                if(actor==null||actor.sentOff){Emit("substitution-cancelled",request.side,request.outgoing,"Le changement préparé est annulé : le joueur n’est plus remplaçable.");continue;}
                try{Substitute(request.side,actor.slot,request.incoming);}
                catch(InvalidOperationException){Emit("substitution-cancelled",request.side,request.outgoing,"Le changement préparé ne peut plus être effectué.");}
            }
        }
        void ValidateSubstitution(int side,int slot,string incoming)
        {
            if(side<0||side>1||slot<0||slot>10||State.finished||State.substitutions[side]>=5)throw new InvalidOperationException("Changement indisponible.");
            var p=incoming!=null&&roster.ContainsKey(incoming)?roster[incoming]:null;var windows=side==0?State.homeWindows:State.awayWindows;
            if(p==null||p.unavailableDays>0||p.team!=(side==0?State.home:State.away)||State.used.Contains(incoming))throw new InvalidOperationException("Ce joueur ne peut pas entrer.");
            if(!State.halfTime&&!windows.Contains(State.clock)&&windows.Count>=3)throw new InvalidOperationException("Trois fenêtres de remplacement déjà utilisées.");
            if(State.actors[side*11+slot].sentOff)throw new InvalidOperationException("Un joueur exclu ne peut pas être remplacé.");
        }
    }
}
