using System;
using System.Linq;

namespace Touchline.Core
{
    public partial class Career
    {
        public int integrityReviewUntil=-1;
        public long IntegrityReviewCost=>Math.Max(2500,life.revenue/10000);
        public void ReviewIntegrity()
        {
            OffPitch();if(life.day<integrityReviewUntil)throw new InvalidOperationException("Un contrôle a déjà été financé durant les 90 derniers jours.");
            Charge(IntegrityReviewCost,"Contrôle indépendant d’intégrité");integrityReviewUntil=life.day+90;
            life.suspicion=Math.Max(0,life.suspicion-5);foreach(var p in life.players)p.trust=Math.Min(100,p.trust+1);
            Mail("Référent intégrité • fiction","Contrôle indépendant financé","Les procédures internes sont revues et le groupe bénéficie d’un espace de signalement protégé. Suspicion : −5 points, confiance du vestiaire : +1. Les enquêtes, les restitutions et les sanctions existantes restent applicables. Prochain contrôle dans 90 jours.",null,"integrity");
        }
        public static string SchemeName(string kind)=>kind=="doping"?"Dopage":kind=="fixing"?"Achat de match":kind=="leak"?"Dossier volé":kind=="shadow"?"Avance occulte":kind=="pressure"?"Pression du réseau":"Dossier à risque";
        public long SchemeCost(string kind)=>Math.Max(5000,life.revenue/(kind=="doping"?2000:kind=="fixing"?250:kind=="leak"?3000:1000));
        void StartExtendedScheme(string kind)
        {
            if(!new[]{"leak","shadow","pressure"}.Contains(kind))throw new ArgumentException("Dossier inconnu.");if(world==null)throw new InvalidOperationException("Une carrière complète est nécessaire.");
            long cost=SchemeCost(kind);Charge(cost,"Zone grise • fiction • "+SchemeName(kind));life.schemeCooldown=life.day;var c=new IntegrityCase{kind=kind,opened=life.day,due=life.day+18+(int)(Roll()*12),cost=cost,accepted=Roll()<.5f};life.investigations.Add(c);life.suspicion=Math.Min(100,life.suspicion+30);
            string detail="L’intermédiaire se retire. Les frais sont perdus ; un dossier peut malgré tout être ouvert.";
            if(c.accepted&&kind=="leak"){foreach(var r in world.reports.Where(r=>r.confidence<90))r.due=Math.Max(life.day+2,r.due-2);detail="Les rapports en cours arrivent au plus deux jours plus tôt. L’origine des documents expose le club à une enquête et à une rupture de confiance.";}
            if(c.accepted&&kind=="shadow"){c.benefit=Math.Max(10000,life.revenue/200);Account(c.benefit,"Avance occulte fictive • remboursable");detail="Une avance temporaire entre en trésorerie. Elle devra être rendue intégralement à la clôture du dossier, même sans sanction.";}
            if(c.accepted&&kind=="pressure"){life.boardTrust=Math.Min(100,life.boardTrust+3);foreach(var p in life.players)p.trust=Math.Max(0,p.trust-2);detail="Le conseil accorde un répit limité (+3 confiance). Le vestiaire se méfie de l’ingérence extérieure (-2 confiance).";}
            Mail("Zone grise • fiction",SchemeName(kind),detail+" Les personnes à l’origine de cette intrigue sont fictives.",null,"integrity");
        }
        void IntegrityStoryDay()
        {
            foreach(var c in life.investigations.Where(c=>c.status=="pending"&&!c.alerted&&life.day>=c.opened+3)){c.alerted=true;Mail("Référent intégrité • fiction","Une alerte interne vous concerne","Un membre fictif de l’organisation demande comment protéger son témoignage. Choisissez une réponse dans Zone grise. Sans réponse, le signalement suit son cours normal.",null,"integrity",IntegrityMessageReference(c));}
        }
        public void RespondIntegrity(int opened,string response)
        {
            OffPitch();var c=life.investigations.FirstOrDefault(c=>c.opened==opened&&c.status=="pending"&&c.alerted&&string.IsNullOrEmpty(c.response));if(c==null||!new[]{"protect","cooperate","ignore"}.Contains(response))throw new InvalidOperationException("Cette alerte n’est plus accessible.");
            if(response=="protect")Charge(Math.Max(1000,life.revenue/10000),"Accompagnement indépendant du témoin fictif");c.response=response;
            if(response=="cooperate"){c.due=Math.Min(c.due,life.day+2);if(c.player!=null)Person(c.player).boostUntil=-1;}
            if(response=="ignore")foreach(var p in life.players)p.trust=Math.Max(0,p.trust-2);
            Mail("Référent intégrité • fiction","Réponse enregistrée",response=="protect"?"Un accompagnement indépendant est financé. L’enquête continue, sans garantie d’indulgence.":response=="cooperate"?"Le club reconnaît les faits. Une sanction est certaine, avec une amende réduite de moitié ; les restitutions restent intégrales.":"Le signalement poursuit son cours. Le silence a dégradé la confiance du vestiaire.",null,"integrity");
        }
    }
}
