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
        DateTime calendarMonth;int calendarSelectedDay;string calendarClub,calendarTab="Calendrier",calendarStatus="Tous";
        string CalendarDate(int day,string format="dd MMM yyyy")=>Core.Career.Epoch.AddDays(day).ToString(format,French);
        bool CalendarOwn(Fixture f)=>f.home==Career.club||f.away==Career.club;
        string CampName(string kind)=>kind=="local"?"Centre du club":kind=="altitude"?"Altitude":"Tournée";
        bool CalendarCanPlan=>Career.world?.managerStatus=="employed"&&Career.Preseason&&Career.life.managerBanUntil<=Career.life.day&&!(Career.match!=null&&!Career.match.finished);
        List<Fixture> CalendarFixtures()=>Career.world.fixtures.Concat(Career.world.history).GroupBy(f=>f.id).Select(g=>g.First()).Where(f=>calendarCompetition=="Mon club"?CalendarOwn(f):f.league==calendarCompetition).Where(f=>calendarStatus=="Tous"||calendarStatus=="À venir"&&!f.played||calendarStatus=="Joués"&&f.played).OrderBy(f=>f.day).ThenBy(f=>f.id).ToList();
        void CalendarWorkspace()
        {
            if(Career.world==null)return;
            if(calendarClub!=Career.club||calendarMonth==default){calendarClub=Career.club;calendarMonth=new DateTime(Career.Date.Year,Career.Date.Month,1);calendarSelectedDay=Career.life.day;}
            var scroll=Scroll(content);scroll.name="calendar-workspace";
            var heading=Row(scroll,"calendar-heading");var identity=new VisualElement();identity.AddToClassList("calendar-heading-copy");heading.Add(identity);
            Text(identity,"SAISON "+Career.world.year+" / "+(Career.world.year+1),"eyebrow");Heading(identity,"Le calendrier du football");
            Text(identity,"Aujourd’hui · "+Career.Date.ToString("dddd dd MMMM",French),"muted");
            var actions=Row(heading,"calendar-actions");bool hasCamp=Career.camps.Any(c=>(c.club==null||c.club==Career.club)&&Core.Career.CampYear(c)==Career.PreparationYear&&c.status!="cancelled");
            var campAction=Button(actions,hasCamp?"Suivre mon stage":"Organiser un stage",()=>{if(hasCamp){calendarTab="Préparation";Build();}else CalendarCampDialog();});campAction.name="calendar-camp";campAction.SetEnabled(hasCamp||CalendarCanPlan);
            var friendlyAction=Button(actions,"Proposer un amical",CalendarFriendlyDialog);friendlyAction.name="calendar-friendly";friendlyAction.SetEnabled(CalendarCanPlan);
            if(!CalendarCanPlan)Text(scroll,Career.Preseason?"Les réservations reprennent lorsque vos décisions de manager sont disponibles.":"Les stages et invitations seront disponibles à la prochaine préparation estivale. Les rendez-vous déjà confirmés restent visibles.","footnote");
            var tabs=Row(scroll,"calendar-tabs");foreach(var tab in new[]{"Calendrier","Préparation"}){var button=Button(tabs,tab,()=>{calendarTab=tab;Build();});button.EnableInClassList("active",calendarTab==tab);button.name=tab=="Calendrier"?"calendar-tab-month":"calendar-tab-preparation";}
            if(calendarTab=="Préparation"){CalendarPreparation(scroll);return;}
            var filters=Row(scroll,"calendar-filters");var choices=new List<string>{"Mon club"};choices.AddRange(Career.world.divisions.Select(d=>d.id));choices.AddRange(Career.world.cups.Select(c=>c.id));choices=choices.Distinct().ToList();if(!choices.Contains(calendarCompetition))calendarCompetition="Mon club";
            var competition=new DropdownField("Afficher",choices,choices.IndexOf(calendarCompetition)){name="calendar-competition"};competition.formatSelectedValueCallback=id=>id=="Mon club"?ClubName(Career.club):Career.CompetitionName(Database,id);competition.formatListItemCallback=id=>id=="Mon club"?"Mon club":Career.CompetitionName(Database,id);filters.Add(competition);
            var status=new DropdownField("Rencontres",new List<string>{"Tous","À venir","Joués"},new List<string>{"Tous","À venir","Joués"}.IndexOf(calendarStatus)){name="calendar-status"};filters.Add(status);
            competition.RegisterValueChangedCallback(e=>{calendarCompetition=e.newValue;Build();});status.RegisterValueChangedCallback(e=>{calendarStatus=e.newValue;Build();});
            var workspace=Row(scroll,"calendar-columns");var month=Card(workspace,"calendar-month");var agenda=Card(workspace,"calendar-agenda");
            void Refresh(){month.Clear();agenda.Clear();CalendarMonthGrid(month,Refresh,()=>{if(Screen.height>Screen.width)scroll.schedule.Execute(()=>scroll.ScrollTo(agenda));});CalendarDayAgenda(agenda);}
            Refresh();
            var next=Career.world.fixtures.Where(f=>CalendarOwn(f)&&!f.played&&f.day>=Career.life.day).OrderBy(f=>f.day).Take(3).ToArray();
            Text(scroll,"Les prochains rendez-vous de votre club","section-title");var upcoming=Row(scroll,"calendar-upcoming");
            if(next.Length==0)Text(upcoming,"Aucune rencontre confirmée à venir pour le moment.","muted");
            foreach(var f in next){var card=Card(upcoming,"calendar-upcoming-card");Text(card,CalendarDate(f.day,"ddd dd MMM")+" · "+(f.home==Career.club?"Domicile":"Extérieur"),"eyebrow");Text(card,ClubName(f.home==Career.club?f.away:f.home),"section-title");Text(card,Career.CompetitionName(Database,f.league),"muted");Button(card,"Voir le rendez-vous",()=>CalendarFixtureDialog(f));}
            if(calendarCompetition!="Mon club")CalendarStandings(scroll);
            Text(scroll,"Naviguer dans les dates ne fait pas avancer la carrière. Seul Continuer fait passer une journée. Les dates simulées et les tours conditionnels sont signalés sur chaque rencontre.","footnote");
        }
        void CalendarMonthGrid(VisualElement parent,Action refresh,Action daySelected=null)
        {
            var nav=Row(parent,"calendar-month-nav");var previous=Button(nav,"‹",()=>{calendarMonth=calendarMonth.AddMonths(-1);calendarSelectedDay=(calendarMonth-Core.Career.Epoch).Days;refresh();});previous.name="calendar-previous";previous.tooltip="Mois précédent";
            var title=Text(nav,calendarMonth.ToString("MMMM yyyy",French),"calendar-month-title");title.name="calendar-month-title";
            var next=Button(nav,"›",()=>{calendarMonth=calendarMonth.AddMonths(1);calendarSelectedDay=(calendarMonth-Core.Career.Epoch).Days;refresh();});next.name="calendar-next";next.tooltip="Mois suivant";
            Button(nav,"Aujourd’hui",()=>{calendarMonth=new DateTime(Career.Date.Year,Career.Date.Month,1);calendarSelectedDay=Career.life.day;refresh();}).name="calendar-today";
            var days=Row(parent,"calendar-weekdays");foreach(var day in new[]{"Lun","Mar","Mer","Jeu","Ven","Sam","Dim"})Text(days,day);
            var grid=Row(parent,"calendar-grid");grid.name="calendar-grid";var fixtures=CalendarFixtures().GroupBy(f=>f.day).ToDictionary(g=>g.Key,g=>g.ToArray());
            int offset=((int)calendarMonth.DayOfWeek+6)%7;int count=DateTime.DaysInMonth(calendarMonth.Year,calendarMonth.Month);
            for(int cell=0;cell<((offset+count+6)/7)*7;cell++){
                int number=cell-offset+1;if(number<1||number>count){var empty=new VisualElement();empty.AddToClassList("calendar-day-empty");grid.Add(empty);continue;}
                int day=(calendarMonth.AddDays(number-1)-Core.Career.Epoch).Days;fixtures.TryGetValue(day,out var matches);
                bool own=calendarCompetition=="Mon club";bool camp=own&&Career.camps.Any(c=>(c.club==null||c.club==Career.club)&&c.status!="cancelled"&&day>=c.start&&day<c.end);
                bool pending=own&&Career.friendlies.Any(i=>(i.club==null||i.club==Career.club)&&i.status=="pending"&&i.day==day);
                string detail=matches?.Length>0?(matches.Length>1?matches.Length+" matchs":matches[0].league=="friendly"?"Amical":"Match"):camp?"Stage":pending?"Invitation":"";
                var button=Button(grid,number+"\n"+detail,()=>{calendarSelectedDay=day;refresh();daySelected?.Invoke();});button.AddToClassList("calendar-day");button.name="calendar-day-"+day;
                button.EnableInClassList("calendar-day-selected",day==calendarSelectedDay);button.EnableInClassList("calendar-day-today",day==Career.life.day);button.EnableInClassList("calendar-day-match",matches?.Length>0);button.EnableInClassList("calendar-day-camp",camp);button.tooltip=CalendarDate(day,"dddd dd MMMM")+(camp?" · stage":"")+(pending?" · invitation à confirmer":"");
            }
            Text(parent,"Vert : rencontre · Or : stage · Invitation : réponse attendue","calendar-legend");
        }
        void CalendarDayAgenda(VisualElement parent)
        {
            parent.name="calendar-day-agenda";Text(parent,CalendarDate(calendarSelectedDay,"dddd dd MMMM yyyy"),"section-title");
            if(calendarSelectedDay==Career.life.day)Text(parent,"Aujourd’hui dans votre carrière","eyebrow");
            var fixtures=CalendarFixtures().Where(f=>f.day==calendarSelectedDay).ToArray();
            foreach(var f in fixtures){var row=Card(parent,"calendar-event");Text(row,Career.CompetitionName(Database,f.league),"eyebrow");Text(row,ClubName(f.home)+"  "+(f.played?f.hg+" – "+f.ag:"—")+"  "+ClubName(f.away),"calendar-event-title");Text(row,f.played?"Rencontre jouée":f.published?"Date publiée":"Date simulée / tour conditionnel","muted");Button(row,"Détails",()=>CalendarFixtureDialog(f));}
            bool own=calendarCompetition=="Mon club";
            var camps=Career.camps.Where(c=>own&&(c.club==null||c.club==Career.club)&&c.status!="cancelled"&&calendarSelectedDay>=c.start&&calendarSelectedDay<c.end).ToArray();
            foreach(var camp in camps){Text(parent,"Stage · "+CampName(camp.kind),"calendar-event-title");Text(parent,CalendarDate(camp.start)+" → "+CalendarDate(camp.end)+" · "+Money(camp.cost)+" déjà réservé","muted");}
            var invites=Career.friendlies.Where(i=>own&&(i.club==null||i.club==Career.club)&&i.day==calendarSelectedDay&&i.status=="pending").ToArray();
            foreach(var i in invites){Text(parent,"Invitation · "+ClubName(i.opponent),"calendar-event-title");Text(parent,"Réponse attendue le "+CalendarDate(i.answerDay)+" · garantie conditionnelle "+Money(i.guarantee),"muted");}
            if(fixtures.Length+camps.Length+invites.Length==0)Text(parent,"Aucun rendez-vous enregistré avec ces filtres.","muted");
            if(own){var previous=Career.world.fixtures.Where(CalendarOwn).Where(f=>f.day<calendarSelectedDay).OrderByDescending(f=>f.day).FirstOrDefault();var next=Career.world.fixtures.Where(CalendarOwn).Where(f=>f.day>calendarSelectedDay).OrderBy(f=>f.day).FirstOrDefault();if(previous!=null)Text(parent,(calendarSelectedDay-previous.day)+" jour(s) après la rencontre précédente","footnote");if(next!=null)Text(parent,(next.day-calendarSelectedDay)+" jour(s) avant la rencontre suivante","footnote");}
        }
        void CalendarFixtureDialog(Fixture f)
        {
            var panel=Modal("Le rendez-vous");panel.name="calendar-fixture";var body=Scroll(panel);
            Text(body,CalendarDate(f.day,"dddd dd MMMM yyyy"),"eyebrow");Heading(body,ClubName(f.home)+" — "+ClubName(f.away));Text(body,Career.CompetitionName(Database,f.league),"section-title");
            if(f.played)Text(body,"Résultat : "+f.hg+" – "+f.ag,"display-title");if(f.penHome>0||f.penAway>0)Text(body,"Tirs au but : "+f.penHome+" – "+f.penAway);
            Text(body,"Lieu : "+(string.IsNullOrEmpty(f.venue)?"Non renseigné":f.venue),"muted");Text(body,f.published?"Date publiée":"Date simulée / tour conditionnel","muted");if(!string.IsNullOrEmpty(f.source))Text(body,f.source,"footnote");
            var actions=Row(panel,"calendar-actions");Button(actions,"Club à domicile",()=>ClubProfile(f.home));Button(actions,"Club visiteur",()=>ClubProfile(f.away));
            if(CalendarOwn(f)&&!f.played&&Career.NextFixture()?.id==f.id)Button(actions,"Préparer le match",()=>{CloseModal();Navigate("Match");}).AddToClassList("primary");
            if(CalendarOwn(f)&&f.league=="friendly"&&!f.played&&f.day-Career.life.day>=3)Button(actions,"Annuler cet amical",()=>Confirm("Annuler l’amical ?","La garantie déjà versée reste acquise à l’adversaire.",()=>RunDecision(()=>Career.CancelFriendly(f.id))));
        }
        void CalendarStandings(VisualElement parent)
        {
            if(!Career.world.divisions.Any(d=>d.id==calendarCompetition)&&!Career.world.cups.Any(c=>c.id==calendarCompetition&&c.european))return;
            var table=Career.Table(calendarCompetition);if(table.Count==0)return;
            var foldout=new Foldout{text="Classement · "+Career.CompetitionName(Database,calendarCompetition),value=false};foldout.AddToClassList("calendar-standings");parent.Add(foldout);
            Text(foldout,"MJ / DIFF / POINTS","eyebrow");for(int i=0;i<table.Count;i++){var t=table[i];var row=Row(foldout,"calendar-table-row");Text(row,(i+1)+". "+ClubName(t.club),"calendar-team");Text(row,t.played+" / "+(t.gf-t.ga)+" / "+t.points);Button(row,"Club",()=>ClubProfile(t.club));}
        }
    }
}
