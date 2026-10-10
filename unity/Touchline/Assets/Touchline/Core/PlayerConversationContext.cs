using System;
using System.Linq;

namespace Touchline.Core
{
    public partial class Career
    {
        public string CareerPersonality(string id)=>new[]{"Ambitieux","Collectif","Réservé","Exigeant"}[StableIdentity(id)%4];
        public string PlayerSituation(Database db,string id)
        {
            var p=Person(id);var injury=Injury(id);if(injury!=null)return "Rééducation · "+injury.remaining+" jours estimés";
            if(p.restUntil>life.day)return "Repos convenu jusqu’au "+Epoch.AddDays(p.restUntil).ToString("dd/MM");
            if(p.promiseUntil>=0)return "Promesse de temps de jeu · "+Math.Max(0,p.appearances-p.promiseStarts)+" / 2 apparitions";
            if(p.fitness<78)return "Fatigue élevée · envisager récupération et rotation";
            if(p.morale<50||p.trust<45)return "Relation à reconstruire · écouter avant de demander davantage";
            var contract=world?.contracts.FirstOrDefault(c=>c.player==id);if(contract?.joined>0&&life.day-contract.joined<60)return "Arrivée récente · intégration au collectif";
            if(p.discussionFocus=="development")return "Parcours à concrétiser · charge, sélections et options de développement";
            if(contract?.playingTime!=null)return PlayingTimeRoles.Label(contract.role)+" · "+PlayingTimeProgress(id);
            return p.appearances<Math.Max(1,life.matches/3)?"Temps de jeu limité · clarifier le rôle":"Situation stable dans le groupe";
        }
        public string PlayerDevelopmentAdvice(Database db,string id)
        {
            var player=db.Find(id);var person=life.players.FirstOrDefault(p=>p.id==id);if(player==null||person==null)return "Le parcours de ce joueur doit être étudié par son club actuel.";
            if(Injury(id)!=null)return "Priorité au dossier médical et à une reprise progressive. Un prêt ou une charge plus forte ne résout pas une blessure.";
            if(person.restUntil>life.day||person.fitness<78)return "Stabilisez d’abord la récupération et respectez le repos convenu. Réévaluez ensuite les sélections et le travail.";
            if(player.age>=24)return "Pour ce joueur expérimenté, privilégiez un rôle clair, la fraîcheur et des sélections cohérentes. Un parcours de centre de formation ne convient pas à son âge.";
            var coach=Staff("youth");if(coach.wage<=0||coach.coaching<9)return "L’encadrement de formation est trop limité pour un avis technique précis. Évaluez d’abord le staff, la charge et les occasions de jouer.";
            var contract=world?.contracts.FirstOrDefault(c=>c.player==id);var usage=contract?.playingTime;
            if(usage!=null&&!string.IsNullOrEmpty(usage.club)&&usage.club!=club)return "Le suivi disponible vient d’un autre club. Observez ses premières rencontres ici avant de juger son utilisation dans votre groupe.";
            if(usage!=null&&(usage.games==null||usage.games.Count<4))return "Le suivi est encore trop court pour un bilan de temps de jeu : attendez quatre rencontres disponibles observées ici. Fixez entre-temps un rôle et une charge adaptés.";
            bool roleConcern=PlayingTimeConcern(id);
            bool lowMinutes=usage!=null?roleConcern||usage.games.Count(g=>g.minutes>=15)<usage.games.Count/3f:person.appearances<Math.Max(1,life.matches/3);
            string role=PlayingTimeRoles.Normalize(contract?.role);
            if((role=="key"||role=="starter")&&(usage!=null?roleConcern:lowMinutes))return "Le temps de jeu est faible au regard de son statut contractuel. Donnez des sélections concrètes ou discutez de son rôle avant de proposer un prêt.";
            if(lowMinutes)return player.age<18?"Prévoyez une intégration progressive et un encadrement adapté. Les transferts de mineurs sont limités dans cette simulation.":"Il manque surtout des matchs. Regardez vos sélections et un prêt dans un club où il peut jouer régulièrement, avec une charge et un rôle adaptés.";
            var path=world?.youth.FirstOrDefault(y=>y.player==id);return path?.mentor==null?"Le temps de jeu est régulier. Conservez ce cadre, évitez de surcharger le joueur et envisagez un mentor lorsqu’un parcours de formation existe.":"Le parcours comprend déjà un mentor et des matchs réguliers. Conservez la stabilité et ajustez le travail à sa fraîcheur plutôt que multiplier les changements.";
        }
        bool TalkContext(Database db,string id,string topic,bool phone)
        {
            if(!new[]{"role","development","recovery","settle","promiseReview"}.Contains(topic))return false;
            OffPitch();var player=db.Find(id);if(player==null||player.team!=club||!life.players.Any(l=>l.id==id))throw new InvalidOperationException("Ce joueur n’appartient plus à votre effectif.");var p=Person(id);
            if(life.day-p.lastTalk<7)throw new InvalidOperationException("Laissez sept jours à votre dernier échange pour produire ses effets.");
            if(topic=="promiseReview"&&p.promiseUntil<0)throw new InvalidOperationException("Aucune promesse de temps de jeu n’est en cours.");
            string response,question;bool ambitious=CareerPersonality(id)=="Ambitieux"||CareerPersonality(id)=="Exigeant";
            if(topic=="recovery")
            {
                bool needed=p.fitness<82||Injury(id)!=null;question="Parlons de ta charge et de ta récupération.";
                if(needed){p.restUntil=Math.Max(p.restUntil,life.day+3);p.trust=Math.Min(100,p.trust+1);response="Trois jours sans sélection me permettront de récupérer. J’attends que cet accord soit respecté.";}else{response=ambitious?"Je me sens prêt et je veux jouer. Ne me mettez pas à l’écart sans raison.":"Ma condition est bonne. Gardons simplement une charge adaptée.";if(ambitious)p.trust=Math.Max(0,p.trust-1);}
            }
            else if(topic=="development")
            {
                question="Quel parcours de développement te conviendrait ?";p.discussionFocus="development";
                bool young=player.age<24;response=young?"Je veux un objectif clair, des séances adaptées et surtout du football senior. Regardons un rôle réaliste ou un prêt de développement.":"À ce stade de ma carrière, je veux travailler un rôle précis et gérer ma charge. Un programme de jeune ne me correspond pas.";p.trust=Math.Min(100,p.trust+(young?1:0));
            }
            else if(topic=="settle")
            {
                question="Comment se passe ton intégration au groupe ?";var contract=world?.contracts.FirstOrDefault(c=>c.player==id);bool recent=contract?.joined>0&&life.day-contract.joined<60;
                response=recent?"Je dois encore apprendre les habitudes du groupe et vos consignes. Un cadre du vestiaire et un rôle stable m’aideraient.":"Je connais déjà le groupe. C’est surtout la clarté de mon rôle qui compte.";if(recent)p.trust=Math.Min(100,p.trust+1);p.discussionFocus="integration";
            }
            else if(topic=="promiseReview")
            {
                question="Faisons le point sur le temps de jeu convenu.";int made=Math.Max(0,p.appearances-p.promiseStarts);response=made>=2?"Les deux apparitions convenues ont été données. Nous pourrons juger la suite à l’échéance.":"Il reste "+(2-made)+" apparition(s) d’au moins 30 minutes à me donner avant le "+Epoch.AddDays(p.promiseUntil).ToString("dd/MM")+". Cet échange ne remplace pas les matchs promis.";
            }
            else
            {
                question="Je veux clarifier ton rôle et ce que j’attends de toi.";var contract=world?.contracts.FirstOrDefault(c=>c.player==id);bool lowMinutes=contract?.playingTime!=null?PlayingTimeConcern(id):p.appearances<Math.Max(1,life.matches/3);bool highRole=contract?.role=="key"||contract?.role=="starter";
                response=lowMinutes&&highRole?"Mon rôle contractuel suppose davantage de matchs. Une explication seule ne suffit pas ; il faut des sélections ou une vraie discussion de contrat.":lowMinutes?"Je peux accepter la rotation, mais j’ai besoin d’occasions concrètes et de savoir ce que je dois améliorer.":"Mon temps de jeu est cohérent. Gardons des consignes stables et un retour précis après les matchs.";
                if(contract?.playingTime!=null)response=PlayingTimeRoles.Label(contract.role)+". "+PlayingTimeProgress(id)+(lowMinutes?" J’attends des sélections cohérentes, pas seulement une explication.":" Faisons le point avec ce suivi et le plan convenu.");
                p.trust=Mathx.Clamp(p.trust+(lowMinutes&&ambitious?-1:contract?.playingTime!=null?0:1),0,100);p.discussionFocus="role";
            }
            p.lastTalk=life.day;Mail("Vous",phone?"Appel • compte rendu":"SMS envoyé",question,id,"talk");Mail(player.name,phone?"Après notre appel":"Réponse",response,id,"talk");ApplyLife(db);return true;
        }
        void PlayerConversationDay(Database db)
        {
            ReviewDepartureRequests(db);
            foreach(var p in life.players)if(p.restUntil==life.day){p.restUntil=-1;Mail("Préparateur physique","Fin du repos convenu",db.Find(p.id)?.name+" peut retrouver la sélection si son dossier médical et ses suspensions le permettent. Condition : "+p.fitness.ToString("0")+" %.",p.id,"talk");}
        }
        public string MessageCategory(ClubMessage m)
        {
            if(m.action=="medical")return "Médical";
            if(m.action=="scout"||m.action=="transfer")return "Recrutement";
            if(m.action=="facilities"||m.action=="finance"||m.sender?.IndexOf("financ",StringComparison.OrdinalIgnoreCase)>=0||m.sender=="Présidence"||m.action=="integrity")return "Direction";
            if(m.action=="talk"||m.player!=null&&m.sender=="Vous")return "Joueurs";
            return "Staff et club";
        }
        public bool MessageNeedsDecision(ClubMessage m)
        {
            if(m==null||life==null)return false;
            if(world!=null&&world.managerStatus!="employed"&&new[]{"medical","facilities","integrity"}.Contains(m.action))return false;
            if(m.action=="medical"){
                if(!PlayerMedicalResponsibility(m.player))return false;
                var injury=Injury(m.player);return injury?.treatment=="pending"&&
                    (!string.IsNullOrEmpty(m.reference)?m.reference=="medical/"+injury.id:m.day>=injury.opened&&(m.subject?.Contains("blessure")??false));
            }
            if(m.action=="talk"&&m.reference?.StartsWith("departure/",StringComparison.Ordinal)==true){var request=DepartureFor(m.player);return request?.status=="requested"&&world?.managerStatus=="employed"&&m.reference=="departure/"+club+"/"+m.player+"/"+request.opened;}
            if(m.action=="transfer")return PendingTransferAgreement(m)!=null||PendingOutgoingLoanAgreement(m)!=null;
            if(m.action=="staff")return PendingStaffAgreement(m)!=null;
            if(m.action=="finance")return PendingCommercialAgreement(m)!=null;
            if(m.action=="jobs")return PendingJobApproach(m)!=null;
            if(m.action=="facilities")return life.projects.Any(p=>p.status=="approved"&&
                (!string.IsNullOrEmpty(m.reference)?m.reference==FacilityMessageReference(p):m.subject=="Projet soutenu"&&m.day>=p.requested+3));
            if(m.action=="integrity")return life.investigations.Any(i=>i.status=="pending"&&i.alerted&&string.IsNullOrEmpty(i.response)&&
                (!string.IsNullOrEmpty(m.reference)?m.reference==IntegrityMessageReference(i):m.subject=="Une alerte interne vous concerne"&&m.day>=i.opened+3));
            return false;
        }
        static string FacilityMessageReference(FacilityProject p)=>"facility/"+p.kind+"/"+p.requested+"/"+p.level;
        static string IntegrityMessageReference(IntegrityCase i)=>"integrity/"+i.kind+"/"+i.opened+"/"+i.player;
        public int MessagePriority(ClubMessage m)
        {
            if(m.pinned)return 3;if(MessageNeedsDecision(m))return 3;
            if(m.sender=="Direction financière"&&life.cash<0)return 3;
            if(m.action=="jobs"&&world?.managerStatus!="employed")return 3;
            var person=life.players.FirstOrDefault(p=>p.id==m.player);
            if(m.action=="talk"&&person!=null&&(person.trust<45||person.morale<50||person.promiseUntil>=life.day&&person.promiseUntil-life.day<=7&&!PlayerPromiseFulfilled(person.id)))return 2;
            if(m.action=="transfer"&&person!=null&&world?.contracts?.Any(c=>c.player==person.id&&c.until>=life.day&&c.until-life.day<=30) ==true)return 2;
            return new[]{"medical","transfer","scout","jobs"}.Contains(m.action)?1:0;
        }
        public string MessageThreadKey(ClubMessage m)=>!string.IsNullOrEmpty(m.player)?"player/"+m.player:"staff/"+(m.action??"club")+"/"+m.sender;
    }
}
