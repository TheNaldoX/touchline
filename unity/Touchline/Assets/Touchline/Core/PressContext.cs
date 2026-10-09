using System;
using System.Linq;

namespace Touchline.Core
{
    public sealed class PressContext
    {
        public Fixture fixture;public string phase,question,unavailable;public int result;
        public bool Available=>fixture!=null&&unavailable==null;
    }
    public partial class Career
    {
        public PressContext Conference(string phase)
        {
            var c=new PressContext{phase=phase};
            if(world==null||life==null||phase!="before"&&phase!="after"){c.unavailable="Aucune conférence disponible.";return c;}
            c.fixture=phase=="before"?NextFixture():world.fixtures.Where(f=>f.played&&(f.home==club||f.away==club)).OrderByDescending(f=>f.day).FirstOrDefault();
            if(c.fixture==null){c.unavailable="Aucune rencontre concernée.";return c;}
            var f=c.fixture;int own=f.home==club?f.hg:f.ag,other=f.home==club?f.ag:f.hg;c.result=Math.Sign(own-other);
            c.question=phase=="before"?"Quel message avant cette rencontre ?":c.result>0?"Après cette victoire, comment garder votre groupe mobilisé ?":c.result<0?"Après cette défaite, assumez-vous la responsabilité du résultat ?":"Ce match nul vous satisfait-il ?";
            if(world.managerStatus!="employed")c.unavailable="Vous n’êtes plus en poste.";
            else if(world.press.Any(p=>p.fixture==f.id&&p.phase==phase))c.unavailable="Votre déclaration est déjà enregistrée.";
            else if(phase=="before"&&(f.day<life.day||f.day-life.day>2)||phase=="after"&&(life.day<f.day||life.day-f.day>2))c.unavailable="Conférence disponible dans les deux jours autour de la rencontre.";
            return c;
        }
        float PressMorale(PressContext c,string answer,PlayerLife p)
        {
            if(answer=="calm")return c.phase=="after"&&c.result<0?0:.25f;
            if(answer=="protect")return c.phase=="after"&&c.result<0?2:1;
            // Ambition after a defeat can sound detached from the result to a
            // player who already distrusts the manager; no universal best answer.
            return p.trust>60?1:c.phase=="after"&&c.result<0?-2:-1;
        }
    }
}
