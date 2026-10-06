using System;
using System.Linq;
namespace Touchline.Core
{
    public partial class Career
    {
        public void ChangeTacticalFormation(Database db,string formation)
        {
            RequireEmployment();if(life?.managerBanUntil>life?.day)throw new InvalidOperationException("Votre adjoint gère les choix tactiques pendant votre suspension.");
            if(!new[]{"4-3-3","4-4-2","4-2-3-1","3-4-2-1"}.Contains(formation))throw new ArgumentException("Système inconnu.");
            if(match?.finished==true)throw new InvalidOperationException("Le match est terminé.");
            if(tactic.formation==formation)return;
            var next=new Tactic();next.SetFormation(formation);
            var oldSlots=tactic.withoutBall;
            var current=TacticalEleven;
            var locked=new bool[11];locked[0]=true;
            if(match!=null)for(int i=1;i<11;i++)locked[i]=match.actors[i].sentOff;
            var selected=RemapSelectedEleven(db,current,oldSlots,next.withoutBall,locked);
            // Preserve the saved starting eleven for the next match as well;
            // current substitutes are remapped independently on the live pitch.
            var saved=lineup!=null&&lineup.Length==11&&lineup.Distinct().Count()==11&&lineup.All(id=>db.Find(id)?.team==club)?RemapSelectedEleven(db,lineup,oldSlots,next.withoutBall,new[]{true,false,false,false,false,false,false,false,false,false,false}):selected;
            tactic.SetFormation(formation);lineup=saved;
            if(match!=null){var actors=match.actors.Take(11).ToDictionary(a=>a.id);for(int i=0;i<11;i++){var actor=actors[selected[i]];actor.slot=i;match.actors[i]=actor;}}
            BindMatchTactic();
        }
        static string[] RemapSelectedEleven(Database db,string[] ids,Slot[] before,Slot[] after,bool[] locked)
        {
            if(ids==null||ids.Length!=11||ids.Distinct().Count()!=11||ids.Any(id=>db.Find(id)==null))throw new InvalidOperationException("Le onze doit contenir onze joueurs identifiés distincts.");
            var result=(string[])ids.Clone();var slots=Enumerable.Range(0,11).Where(i=>!locked[i]).ToArray();int count=slots.Length,full=(1<<count)-1;
            var scores=new double[count,count];for(int targetIndex=0;targetIndex<count;targetIndex++)for(int candidate=0;candidate<count;candidate++){int target=slots[targetIndex],source=slots[candidate];scores[targetIndex,candidate]=db.Find(ids[source]).Fit(after[target].role)*10000+(before[source].role==after[target].role?10:0)-Math.Abs(before[source].x-after[target].x)*.01-Math.Abs(before[source].y-after[target].y)*.005-Math.Abs(source-target)*.0001;}
            var best=new double[full+1];var ready=new bool[full+1];var choice=new int[full+1];ready[full]=true;
            double Solve(int mask){if(ready[mask])return best[mask];int depth=0;for(int bits=mask;bits!=0;bits>>=1)depth+=bits&1;double score=double.NegativeInfinity;int selected=-1;
                for(int p=0;p<count;p++){if((mask&(1<<p))!=0)continue;double local=scores[depth,p];
                    double value=local+Solve(mask|(1<<p));if(value>score+1e-7){score=value;selected=p;}}
                ready[mask]=true;best[mask]=score;choice[mask]=selected;return score;}
            Solve(0);int used=0;for(int i=0;i<count;i++){int p=choice[used];result[slots[i]]=ids[slots[p]];used|=1<<p;}return result;
        }
    }
}
