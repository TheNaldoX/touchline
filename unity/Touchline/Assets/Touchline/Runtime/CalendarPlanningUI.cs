using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        string CalendarInvitationStatus(FriendlyInvitation invite)
        {
            if(invite.status=="pending")return "Réponse attendue · "+CalendarDate(invite.answerDay);
            if(invite.status=="declined")return "Invitation refusée";
            bool scheduled=Career.world.fixtures.Concat(Career.world.history).Any(f=>f.league=="friendly"&&f.day==invite.day&&f.home==(invite.home?Career.club:invite.opponent)&&f.away==(invite.home?invite.opponent:Career.club));
            return scheduled?"Confirmé au calendrier":"Accepté, rencontre retirée du calendrier";
        }
        void CalendarPreparation(VisualElement parent)
        {
            Text(parent,"PRÉPARATION ESTIVALE · "+Career.PreparationYear,"eyebrow");Heading(parent,"Préparer le groupe");
            var summary=Row(parent,"calendar-prep-summary");var condition=Card(summary,"calendar-summary-card");Text(condition,"FRAÎCHEUR ACTUELLE","eyebrow");
            float fitness=Career.life.players.Count==0?100:(float)Career.life.players.Average(p=>p.fitness);Text(condition,fitness.ToString("0",French)+" %","calendar-summary-value");Text(condition,Career.life.players.Count(p=>p.fitness<75)+" joueur(s) sous 75 %","muted");
            var load=Card(summary,"calendar-summary-card");Text(load,"CHARGE ACTUELLE","eyebrow");Text(load,Career.life.training=="rest"?"Récupération":Career.life.training=="heavy"?"Intensif":"Équilibré","section-title");
            bool delegated=Career.life.staff?.training==true&&Career.life.staff.members?.Any(s=>s.role=="fitness"&&s.wage>0)==true;
            Text(load,delegated?"Le préparateur ajuste le programme chaque jour.":"Vous définissez le programme depuis le bureau.","muted");Button(load,"Régler la charge",()=>Navigate("Club"));Button(load,"Délégation",()=>Navigate("Staff et délégation"));
            Text(parent,"Les trois jours entre rencontres sont un minimum. Gardez davantage de marge si la fraîcheur du groupe baisse ; les matchs et les soins peuvent modifier le programme.","notice");
            var camps=Career.camps.Where(c=>(c.club==null||c.club==Career.club)&&Core.Career.CampYear(c)==Career.PreparationYear).OrderBy(c=>c.start).ToArray();
            var campCard=Card(parent,"calendar-preparation-card");Text(campCard,"Le stage collectif","section-title");
            if(camps.Length==0){Text(campCard,"Aucun stage réservé cette saison. Choisissez un départ, un type de travail et vérifiez son coût avant de réserver.","muted");var button=Button(campCard,"Organiser un stage",CalendarCampDialog);button.SetEnabled(CalendarCanPlan);}
            foreach(var camp in camps){Text(campCard,CampName(camp.kind)+" · "+(camp.status=="active"?"En cours":camp.status=="complete"?"Terminé":camp.status=="cancelled"?"Annulé":"Réservé"),"calendar-event-title");Text(campCard,CalendarDate(camp.start)+" → "+CalendarDate(camp.end)+" · "+Money(camp.cost)+" payé","muted");}
            var invitations=Card(parent,"calendar-preparation-card");var heading=Row(invitations,"calendar-heading");Text(heading,"Matchs amicaux et invitations","section-title");var propose=Button(heading,"Proposer un amical",CalendarFriendlyDialog);propose.SetEnabled(CalendarCanPlan);
            var invites=Career.friendlies.Where(i=>(i.club==null||i.club==Career.club)&&Core.Career.Epoch.AddDays(i.day).Year==Career.PreparationYear).OrderBy(i=>i.day).ToArray();
            if(invites.Length==0)Text(invitations,"Aucune invitation cette saison. Le secrétariat répond après deux journées de carrière.","muted");
            foreach(var invite in invites){var card=Card(invitations,"calendar-invitation");Text(card,CalendarDate(invite.day)+" · "+(invite.home?"Domicile":"Extérieur"),"eyebrow");Text(card,ClubName(invite.opponent),"calendar-event-title");Text(card,CalendarInvitationStatus(invite),"calendar-invitation-status");Text(card,(invite.status=="pending"?"Garantie à payer si accepté : ":invite.status=="accepted"?"Garantie versée : ":"Garantie proposée, non versée : ")+Money(invite.guarantee),"muted");}
            Text(parent,"Un seul stage par club et par saison. Les réservations coûtent immédiatement leur montant ; la garantie d’un amical n’est débitée qu’à l’acceptation. Une annulation ne rembourse pas cette garantie.","footnote");
        }
        List<int> CalendarPlanningDates(bool camp)=>Enumerable.Range(camp?2:3,camp?34:43).Select(i=>Career.life.day+i).Where(d=>Core.Career.Epoch.AddDays(d).Year==Career.Date.Year&&Core.Career.Epoch.AddDays(d+(camp?7:0)).Month<=8).ToList();
        void CalendarCampDialog()
        {
            var panel=Modal("Organiser un stage");panel.name="calendar-camp-dialog";panel.AddToClassList("calendar-planning-dialog");var body=Scroll(panel);
            if(!CalendarCanPlan){Text(body,"Les réservations ne sont pas disponibles actuellement.","notice");return;}
            if(Career.camps.Any(c=>(c.club==null||c.club==Career.club)&&Core.Career.CampYear(c)==Career.PreparationYear&&c.status!="cancelled")){Text(body,"Votre stage de cette saison est déjà réservé. Son suivi est disponible dans Préparation.","notice");return;}
            var dates=CalendarPlanningDates(true);if(dates.Count==0){Text(body,"Aucune période de sept jours ne reste disponible cet été.","notice");return;}
            Text(body,"SEPT JOURS ENSEMBLE","eyebrow");Text(body,"Un stage de préparation permet de travailler le groupe. Le calendrier officiel reste prioritaire.","muted");
            var kind=new DropdownField("Programme",new List<string>{"Centre du club","Altitude","Tournée"},0){name="calendar-camp-kind"};body.Add(kind);
            var date=new DropdownField("Date de départ",dates.Select(d=>CalendarDate(d,"ddd dd MMM yyyy")).ToList(),Math.Max(0,dates.IndexOf(dates.Contains(calendarSelectedDay)?calendarSelectedDay:Career.life.day+7))){name="calendar-camp-date"};body.Add(date);
            var summary=Card(body,"calendar-booking-summary");var price=Text(summary,"","calendar-summary-value");price.name="calendar-camp-price";var details=Text(summary,"","muted");var warning=Text(body,"","notice");warning.name="calendar-camp-warning";
            var book=Button(panel,"Réserver le stage",()=>RunDecision(()=>Career.ScheduleCamp(new[]{"local","altitude","tour"}[kind.index],dates[date.index])));book.name="calendar-camp-book";book.AddToClassList("primary");
            void Refresh(){int day=dates[date.index];long cost=Math.Max(3000,Career.life.revenue/(kind.index==0?20000:kind.index==1?4000:1800));price.text=Money(cost);details.text="Départ "+CalendarDate(day)+" · retour "+CalendarDate(day+7)+"\nDébité à la réservation · trésorerie "+Money(Career.life.cash);
                bool conflict=Career.world.fixtures.Any(f=>!f.played&&f.league!="friendly"&&CalendarOwn(f)&&f.day>=day&&f.day<=day+7);warning.text=conflict?"Une rencontre officielle empêche ce départ.":Career.life.cash<cost?"Trésorerie insuffisante pour réserver ce stage.":kind.index==1?"L’altitude apporte un complément de préparation physique aux joueurs disponibles.":"Le stage accompagne la préparation et la cohésion des joueurs disponibles. Les amicaux restent à organiser séparément.";book.SetEnabled(!conflict&&Career.life.cash>=cost&&CalendarCanPlan);}
            kind.RegisterValueChangedCallback(_=>Refresh());date.RegisterValueChangedCallback(_=>Refresh());Refresh();
        }
        void CalendarFriendlyDialog()
        {
            var panel=Modal("Proposer un match amical");panel.name="calendar-friendly-dialog";panel.AddToClassList("calendar-planning-dialog");var body=Scroll(panel);
            if(!CalendarCanPlan){Text(body,"Les invitations ne sont pas disponibles actuellement.","notice");return;}
            var dates=CalendarPlanningDates(false);if(dates.Count==0){Text(body,"Aucune date d’amical ne reste disponible cet été.","notice");return;}
            var date=new DropdownField("Date souhaitée",dates.Select(d=>CalendarDate(d,"ddd dd MMM yyyy")).ToList(),Math.Max(0,dates.IndexOf(dates.Contains(calendarSelectedDay)?calendarSelectedDay:Career.life.day+14))){name="calendar-friendly-date"};body.Add(date);
            var home=new Toggle("Recevoir à domicile"){value=true,name="calendar-friendly-home"};body.Add(home);var warning=Text(body,"","notice");warning.name="calendar-friendly-warning";
            Text(body,"Adversaires recommandés","section-title");Text(body,"Sélection de votre cellule pour une préparation progressive. Chaque invitation est une demande ; l’adversaire peut refuser si son calendrier est incompatible.","muted");
            Text(body,"Réponse après deux journées. À domicile, la garantie est due seulement si la rencontre est acceptée. À l’extérieur, aucune garantie n’est prévue par ce système.","footnote");
            var opponents=Career.FriendlyRecommendations(Database);var rows=new VisualElement();rows.AddToClassList("calendar-opponents");body.Add(rows);
            void Refresh(){rows.Clear();int day=dates[date.index];bool conflict=Career.world.fixtures.Any(f=>!f.played&&CalendarOwn(f)&&Math.Abs(f.day-day)<3);bool pendingDate=Career.friendlies.Any(i=>(i.club==null||i.club==Career.club)&&i.status=="pending"&&Math.Abs(i.day-day)<3);
                warning.text=conflict?"Cette date ne laisse pas trois jours entre vos rencontres. Choisissez un autre jour.":pendingDate?"Une invitation proche de cette date attend déjà une réponse.":"Date proposée : "+CalendarDate(day)+" · réponse attendue le "+CalendarDate(Career.life.day+2);
                if(opponents.Count==0)Text(rows,"Aucun adversaire recommandé disponible.","muted");
                foreach(var opponent in opponents){var row=Card(rows,"calendar-opponent");var identity=Row(row,"calendar-opponent-heading");Text(identity,opponent.name,"section-title");Text(row,Career.CompetitionName(Database,opponent.league),"muted");long guarantee=home.value?Math.Max(1000,opponent.annualRevenue/10000):0;Text(row,"Garantie conditionnelle · "+Money(guarantee),"calendar-guarantee");
                    bool pending=Career.friendlies.Any(i=>(i.club==null||i.club==Career.club)&&i.opponent==opponent.id&&i.status=="pending");if(pending)Text(row,"Votre précédente invitation attend une réponse.","footnote");if(guarantee>Career.life.cash)Text(row,"Trésorerie actuelle insuffisante : le club peut refuser si elle ne couvre pas la garantie lors de la réponse.","footnote");
                    var invite=Button(row,"Envoyer l’invitation",()=>RunDecision(()=>{Career.InviteFriendly(Database,opponent.id,dates[date.index],home.value);calendarTab="Préparation";}));invite.name="calendar-invite-"+opponent.id;invite.SetEnabled(!conflict&&!pendingDate&&!pending&&CalendarCanPlan);}
            }
            date.RegisterValueChangedCallback(_=>Refresh());home.RegisterValueChangedCallback(_=>Refresh());Refresh();
        }
    }
}
