using System;
using System.Linq;
using Touchline.Core;

namespace Touchline
{
    // Read-only presentation of actual career state. Does not call Contract(),
    // alter a promise or apply any of Talk's effects.
    public sealed class DiscussionTopic
    {
        public string key,title,description,reason,warning,blocked;
        public int priority;
        public bool recommended=>priority>0;
    }
    public sealed class PlayerDiscussionPresentation
    {
        public bool own;
        public int waitDays,availableDay;
        public DiscussionTopic[] topics=Array.Empty<DiscussionTopic>();
        public string defaultTopic;
        public static PlayerDiscussionPresentation From(Career career,Database db,string id)
        {
            var result=new PlayerDiscussionPresentation();var life=career?.life;var player=db?.Find(id);var person=life?.players?.FirstOrDefault(p=>p.id==id);
            result.own=player!=null&&person!=null&&player.team==career.club&&!career.PlayerRetirementEffective(id);if(!result.own)return result;
            result.waitDays=Math.Max(0,7-(life.day-person.lastTalk));result.availableDay=life.day+result.waitDays;
            var injury=career.Injury(id);var contract=career.world?.contracts?.FirstOrDefault(c=>c.player==id);bool promise=person.promiseUntil>=0,lowMinutes=career.PlayingTimeConcern(id);
            bool highRole=PlayingTimeRoles.Normalize(contract?.role)=="key"||PlayingTimeRoles.Normalize(contract?.role)=="starter",recent=contract?.joined>0&&contract.joined<=life.day&&life.day-contract.joined<60,resting=person.restUntil>life.day;
            var topics=new[]{
                new DiscussionTopic{key="recovery",title="Charge et récupération",description="Si la fatigue le justifie, convenir de trois jours sans sélection.",reason=injury!=null?"Dossier médical actif : "+injury.remaining+" jours estimés. L’échange ne remplace pas le choix médical.":resting?"Repos déjà convenu jusqu’au "+Career.Epoch.AddDays(person.restUntil).ToString("dd/MM")+". Respectez cet accord.":person.fitness<82?"Condition actuelle : "+person.fitness.ToString("0")+" %. Le repos peut aider la récupération.":"Sa condition ne justifie pas particulièrement un repos supplémentaire.",priority=injury!=null?100:!resting&&person.fitness<82?95:0},
                new DiscussionTopic{key="role",title="Clarifier son rôle",description="Les attentes contractuelles et le temps de jeu comptent davantage qu’un discours.",reason=career.PlayingTimeProgress(id)+" Une discussion seule ne change pas les engagements contractuels.",priority=lowMinutes?(highRole?80:65):person.discussionFocus=="role"?40:0},
                new DiscussionTopic{key="support",title="Écouter et soutenir",description="Aider un joueur en difficulté ; effet limité quand tout va bien.",reason=person.morale<70?"Moral actuel : "+person.morale.ToString("0")+". Écoutez sa situation avant de demander davantage.":injury!=null?"La rééducation peut être accompagnée d’un échange de soutien.":"Aucune difficulté particulière de moral n’est visible.",priority=person.morale<70?85:injury!=null?60:0},
                new DiscussionTopic{key="promiseReview",title="Suivre notre engagement",description="Faire le point sans annuler ni renouveler une promesse.",reason=promise?career.PlayerPromiseStatus(id):"Aucune promesse de temps de jeu en cours.",priority=promise?(career.PlayerPromiseFulfilled(id)?70:person.promiseUntil-life.day<=7?98:90):0,blocked=promise?null:"Aucune promesse en cours à examiner."},
                new DiscussionTopic{key="development",title="Parcours et développement",description="Discuter du rôle, des entraînements et d’un éventuel prêt.",reason=player.age<=0?"Âge non renseigné. Examinez les informations disponibles avant de choisir un parcours.":player.age<24?"Jeune joueur de "+player.age+" ans. Un parcours doit se traduire en travail et en occasions de jouer.":"Pour ce joueur expérimenté, privilégiez rôle précis et gestion de la charge.",priority=person.discussionFocus=="development"?75:player.age>0&&player.age<24&&injury==null&&!resting&&person.fitness>=82?55:0},
                new DiscussionTopic{key="settle",title="Intégration au groupe",description="Accompagner une arrivée récente et expliquer le collectif.",reason=recent?"Arrivée enregistrée depuis "+(life.day-contract.joined)+" jours. Un rôle stable facilite l’intégration.":"Aucune arrivée récente confirmée dans les conditions de carrière.",priority=recent?75:0},
                new DiscussionTopic{key="explain",title="Expliquer les sélections",description="Donner un cadre clair à vos décisions.",reason=injury!=null?"Expliquez une reprise progressive en accord avec le dossier médical.":resting||person.fitness<80?"La récupération apporte un motif concret à vos décisions de sélection.":lowMinutes?"Reliez vos explications aux prochaines occasions de jouer.":"Présentez vos choix sans changer les engagements existants.",priority=injury!=null||resting||person.fitness<80?50:lowMinutes?45:0},
                new DiscussionTopic{key="demand",title="Demander davantage",description="Une exigence mal placée peut dégrader le moral.",reason="Évaluez sa fraîcheur et son moral avant d’exiger davantage.",warning=injury!=null||person.fitness<=80||person.morale<=60?"Cette pression risque d’être mal reçue dans sa situation actuelle.":"Une bonne condition ne garantit pas qu’un discours résoudra ses difficultés."},
                new DiscussionTopic{key="promise",title="Promettre du temps de jeu",description="Deux apparitions de 30 minutes en trois semaines : engagement réel.",reason="La parole donnée devra être suivie de sélections effectives.",warning="Ne promettez que ce que votre calendrier et vos décisions permettront de tenir.",blocked=promise?"Une promesse est déjà en cours. Consultez Suivre notre engagement.":injury!=null?"Une blessure active empêche cette promesse de temps de jeu.":null},
                new DiscussionTopic{key="apologize",title="Reconnaître une tension",description="Assumer la difficulté de votre relation.",reason=person.trust<55?"Confiance actuelle : "+person.trust.ToString("0")+". Les décisions suivantes devront reconstruire la relation.":"La confiance actuelle ne signale pas une tension particulière.",priority=person.trust<55?88:0},
                new DiscussionTopic{key="leadership",title="Mobiliser un cadre",description="Demander une contribution à la vie du vestiaire.",reason=player.age>=26&&person.trust>=60?"Son expérience et sa confiance permettent d’aborder un rôle dans le groupe.":"Il doit d’abord stabiliser sa propre situation.",warning=player.age<26||person.trust<60?"Le joueur peut ne pas se sentir prêt à porter le vestiaire.":null}
            };
            // Stable order makes focus and selection predictable for tied scores.
            result.topics=topics.Select((t,i)=>new{t,i}).OrderByDescending(x=>x.t.priority).ThenBy(x=>x.i).Select(x=>x.t).ToArray();
            result.defaultTopic=result.topics.FirstOrDefault(t=>t.recommended&&t.blocked==null)?.key??"role";return result;
        }
    }
}
