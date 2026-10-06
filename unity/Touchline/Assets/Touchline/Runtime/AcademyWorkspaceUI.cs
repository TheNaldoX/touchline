using System;
using System.Linq;
using System.Collections.Generic;
using Touchline.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        string academyGroup="Tous",academySearch="";
        static readonly string[] AcademyFocusKeys={"balanced","physical","technical","tactical"};
        static readonly string[] AcademyLoadKeys={"rest","light","standard","intensive"};
        static readonly string[] AcademyLoadNames={"Repos","Allégée","Normale","Soutenue"};
        void AcademyWorkspace()
        {
            var paths=Career.AcademyPaths(Database).ToArray();var coach=Career.Staff("youth");
            var scroll=Scroll(content);scroll.name="academy-workspace";
            var hero=Card(scroll,"academy-hero");var top=Row(hero,"academy-heading");var identity=new VisualElement();identity.AddToClassList("academy-identity");top.Add(identity);Heading(identity,"Le centre de formation");
            Text(identity,paths.Length+" parcours suivis · installations niveau "+Career.Level("academy")+" · saison "+Career.world.year+"–"+(Career.world.year+1),"muted");
            var access=Row(top,"academy-actions");Button(access,"Staff",()=>Navigate("Staff et délégation"));Button(access,"Installations",()=>Navigate("Infrastructures"));
            var approach=new Foldout{text="Encadrement et délégation",value=false,name="academy-staff-options"};approach.AddToClassList("academy-explainer");hero.Add(approach);
            Text(approach,coach.wage>0?coach.name+" · formation "+coach.coaching+" / 20 · évaluation "+coach.judging+" / 20":"Responsable de formation : poste vacant","academy-staff");
            var delegation=new Toggle("Déléguer les axes et le mentorat au responsable"){value=Career.life.staff.youth,name="academy-delegation"};delegation.AddToClassList("academy-delegation");approach.Add(delegation);delegation.RegisterValueChangedCallback(e=>RunDecision(()=>Career.SetDelegation("youth",e.newValue)));
            Text(approach,StaffDelegationStatus("youth",Career.life.staff.youth),"muted").name="academy-delegation-status";
            Text(approach,"Quatre séances par semaine, avec récupération. Les infrastructures, l’encadrement, la charge, la condition et le mentorat influencent les progrès. Une promotion ne garantit pas de temps de jeu.","muted");
            var tools=Row(scroll,"academy-tools");var group=new DropdownField("Parcours",new List<string>{"Tous","U19","Réserve","Professionnels","En prêt"},academyGroup){name="academy-group"};tools.Add(group);
            var search=new TextField("Joueur"){value=academySearch,name="academy-search"};tools.Add(search);
            var count=Text(scroll,"","muted");count.name="academy-count";var list=new VisualElement{name="academy-players"};scroll.Add(list);
            void Populate(){
                list.Clear();var filtered=paths.Where(y=>(academyGroup=="Tous"||Core.Career.AcademyGroup(Database.Find(y.player),y)==academyGroup)&&DirectoryMatches(Database.Find(y.player).name,academySearch)).OrderByDescending(y=>Database.Find(y.player).age).ThenBy(y=>Database.Find(y.player).name).ToArray();
                count.text=filtered.Length+" joueur(s) · évaluations du staff relatives à votre équipe première";
                if(filtered.Length==0)Text(list,"Aucun joueur dans ce parcours avec cette recherche.","empty-state");
                foreach(var y in filtered){var p=Database.Find(y.player);var card=Card(list,"academy-player");card.name="academy-player-"+p.id;
                    var header=Row(card,"academy-player-heading");Text(header,p.name,"section-title");Text(header,Core.Career.AcademyGroup(p,y),"pill");Text(card,p.age+" ans · "+FrenchFootballPositions.Label(p.position)+" · "+(p.team.StartsWith("academy-")?Own.name:ClubName(p.team)),"muted");
                    float level=Career.AcademyEstimate(Database,p.id),potential=Career.AcademyEstimate(Database,p.id,true);
                    Text(card,level>0?Stars(level,ClubRatingBaseline())+"  Niveau estimé    "+Stars(potential,ClubRatingBaseline())+"  Potentiel incertain":"Avis indisponible : recrutez un responsable de formation.","academy-stars");
                    Text(card,Career.AcademyAdvice(Database,p.id),"academy-advice");
                    Text(card,y.trainingSessions+" séances suivies · "+y.seniorMinutes+" min jouées chez les pros · "+y.trackedLoanMinutes+" min estimées en prêt","academy-pathway");
                    var actions=Row(card,"academy-actions");Button(actions,"Parcours et plan",()=>AcademyPlayerPlan(p.id)).name="academy-plan-"+p.id;Button(actions,"Fiche",()=>PlayerProfile(p.id));
                }
            }
            group.RegisterValueChangedCallback(e=>{academyGroup=e.newValue;Populate();});search.RegisterValueChangedCallback(e=>{academySearch=e.newValue;Populate();});Populate();
            Text(scroll,"Les promotions générées sont fictives. U19 et réserve disposent ici d’un suivi d’entraînement ; elles n’ont pas encore de calendrier de compétition dédié. Les compteurs séparés démarrent avec cette mise à jour.","footnote");
        }
        void AcademyPlayerPlan(string id)
        {
            var y=Career.AcademyPaths(Database).FirstOrDefault(x=>x.player==id);if(y==null){Message("Ce parcours n’est plus sous votre responsabilité.");return;}
            var p=Database.Find(id);bool academy=p.team=="academy-"+Career.club;
            var panel=Modal("Parcours · "+p.name);panel.name="academy-plan";panel.AddToClassList("academy-plan");var s=Scroll(panel);
            Text(s,Core.Career.AcademyGroup(p,y)+" · "+p.age+" ans · "+FrenchFootballPositions.Label(p.position),"eyebrow");Text(s,Career.AcademyAdvice(Database,id),"academy-advice");
            Text(s,"Condition · "+p.fitness.ToString("0")+" %","academy-staff");var condition=new ProgressBar{value=p.fitness};s.Add(condition);
            Text(s,"Moral : "+p.morale.ToString("0")+" % · séances suivies : "+y.trainingSessions+"\nProfessionnels : "+y.seniorMinutes+" minutes jouées · prêts : "+y.trackedLoanMinutes+" minutes estimées","body-text");
            int legacy=Math.Max(0,y.minutes-y.trackedLoanMinutes);if(legacy>0)Text(s,legacy+" minutes d’ancien suivi non ventilées : elles ne sont pas présentées comme des matchs réellement joués.","footnote");
            if(academy){
                var focus=new DropdownField("Travail individuel",new List<string>{"Équilibré","Physique","Technique","Tactique"},Math.Max(0,Array.IndexOf(AcademyFocusKeys,y.focus))){name="academy-focus"};s.Add(focus);
                var load=new DropdownField("Charge",AcademyLoadNames.ToList(),Math.Max(0,Array.IndexOf(AcademyLoadKeys,y.trainingLoad??"standard"))){name="academy-load"};s.Add(load);
                Text(s,"La charge soutenue augmente le travail et la fatigue. Sous 65 % de condition, la séance devient de la récupération ; un joueur indisponible ne progresse pas à l’entraînement.","muted");
                var mentors=Database.Squad(Career.club).Where(m=>Career.CanMentorYouth(Database,id,m.id)).OrderBy(m=>m.name).ToArray();
                var names=new[]{"Sans mentor"}.Concat(mentors.Select(m=>m.name)).ToList();var mentor=new DropdownField("Mentor",names,Math.Max(0,Array.FindIndex(mentors,m=>m.id==y.mentor)+1)){name="academy-mentor"};s.Add(mentor);
                Text(s,"Un cadre de 26 ans ou plus pour trois jeunes au maximum. Les gardiens sont accompagnés par un gardien. Le bonus est suspendu si le mentor est indisponible ou démoralisé.","muted");
                bool requested=Career.life.staff.youth,delegated=requested&&Career.Staff("youth").wage>0;focus.SetEnabled(!delegated);mentor.SetEnabled(!delegated);if(requested)Text(s,delegated?"L’axe et le mentor sont délégués ; vous gardez la main sur la charge.":"Délégation en attente : le poste de responsable de formation est vacant. Vous pouvez choisir l’axe et le mentor ; votre préférence de délégation est conservée pour son remplacement.","notice").name="academy-plan-delegation-status";
                Button(s,"Enregistrer le plan",()=>RunDecision(()=>{if(!delegated)Career.SetYouthPlan(Database,id,AcademyFocusKeys[focus.index],mentor.index==0?null:mentors[mentor.index-1].id);Career.SetAcademyLoad(Database,id,AcademyLoadKeys[load.index]);})).name="academy-save-plan";
                Heading(s,"La prochaine étape");
                Text(s,"Intégration au groupe professionnel : salaire de base simulé "+Money(Core.Career.MonthlySalary(Math.Max(250,p.wage)))+" / mois. Le plafond salarial sera vérifié. Vous devrez ensuite gérer son contrat et ses apparitions.","muted");
                string issue=Career.YouthPromotionIssue(Database,id);if(issue!=null)Text(s,issue,"notice").name="academy-promotion-issue";
                var promote=Button(s,"Proposer l’intégration",()=>Confirm("Intégrer "+p.name+" ?","Il quitte le centre pour le groupe professionnel. Son salaire et son contrat de jeu entrent dans la gestion de l’effectif. Aucune titularisation n’est garantie.",()=>RunDecision(()=>Career.PromoteYouth(Database,id))));promote.name="academy-promote";promote.SetEnabled(issue==null);promote.tooltip=issue??"Intégration soumise à confirmation";
            }else if(p.team==Career.club){
                Text(s,"Sa charge dépend maintenant du groupe professionnel. Les apparitions sont comptées à partir des matchs réellement disputés.","muted");
                Button(s,"Discuter de son rôle",()=>Conversation(id));
                if(p.age>=18&&p.age<=23)Button(s,"Chercher un prêt adapté",()=>YouthLoanDialog(id)).name="academy-loan";
            }else Text(s,"Le club emprunteur gère sa charge et ses sélections. Le responsable des prêts envoie un suivi mensuel fondé sur ses rencontres terminées.","muted");
        }
    }
}
