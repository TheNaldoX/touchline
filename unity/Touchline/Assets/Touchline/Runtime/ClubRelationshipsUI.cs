using System;
using System.Linq;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        void ShowDepartureDecision(VisualElement parent,string id)
        {
            var status=Career.DepartureStatus(id);if(status==null)return;
            var card=Card(parent);card.name="departure-request";Text(card,"SON AVENIR AU CLUB","eyebrow");Text(card,status,"notice");
            if(Career.DepartureFor(id)?.status!="requested")return;
            PlayerManagementButton(card,"Étudier les offres",()=>RunDecision(()=>Career.AnswerDeparture(Database,id,true))).name="departure-allow";
            PlayerManagementButton(card,"Refuser le départ",()=>RunDecision(()=>Career.AnswerDeparture(Database,id,false))).name="departure-refuse";
            Text(card,"Une mise sur le marché ne vaut pas signature. Le club doit conserver au moins dix-huit joueurs.","footnote");
        }
        void ShowClubNews(VisualElement parent)
        {
            var card=Card(parent);card.name="club-news";Text(card,"LA VIE DU CLUB","eyebrow");
            foreach(var request in (Career.departureRequests??new System.Collections.Generic.List<DepartureRequest>()).Where(r=>r.club==Career.club&&r.status=="requested"))
                Button(card,(Database.Find(request.player)?.name??request.player)+" souhaite partir",()=>Conversation(request.player));
            foreach(var m in Career.life.messages.Where(m=>m.action=="press"||m.action=="jobs"||m.sender=="Présidence").OrderByDescending(m=>m.day).ThenByDescending(m=>m.id).Take(5))
                Button(card,Core.Career.Epoch.AddDays(m.day).ToString("dd MMM",French)+" · "+m.subject,()=>OpenMessage(m.id));
            var matches=Career.world.history.Concat(Career.world.fixtures).Where(f=>f.played&&(f.home==Career.club||f.away==Career.club)).GroupBy(f=>f.id).Select(g=>g.First()).ToArray();
            int Margin(Fixture f)=>f.home==Career.club?f.hg-f.ag:f.ag-f.hg;
            var best=matches.Where(f=>Margin(f)>0).OrderByDescending(Margin).ThenByDescending(f=>f.day).FirstOrDefault();
            if(best!=null)Text(card,"Plus large victoire archivée : "+ClubName(best.home)+" "+best.hg+"–"+best.ag+" "+ClubName(best.away)+" · "+FixtureDay(best));
            Text(card,matches.Length+" rencontres dans les archives conservées. Records de cette carrière uniquement, pas de l’histoire réelle du club.","footnote");
        }
        void ContextualPressPage()
        {
            var s=Scroll(content);Heading(s,"La salle de presse");
            Text(s,"Votre réponse agit sur le groupe et les supporters selon le résultat et la confiance des joueurs.","muted");
            foreach(var phase in new[]{"before","after"}){
                var context=Career.Conference(phase);var f=context.fixture;var card=Card(s,"press-stage");card.name="press-"+phase;
                Text(card,phase=="before"?"AVANT-MATCH":"APRÈS-MATCH","eyebrow");
                if(f!=null){Text(card,ClubName(f.home)+" · "+ClubName(f.away),"section-title");Text(card,(f.venue??"Stade de "+ClubName(f.home))+" · "+FixtureDay(f),"muted");if(phase=="after")Text(card,f.hg+" – "+f.ag,"display-title");}
                var wall=Row(card,"sponsor-wall");foreach(var d in Career.world.sponsors.Where(d=>d.status=="signed"))Text(wall,d.name,"pill");
                Text(card,context.question??"Aucune rencontre concernée.","section-title");
                if(!context.Available){Text(card,context.unavailable,"notice");continue;}
                foreach(var answer in new[]{"calm","ambition","protect"}){
                    var choice=answer;Button(card,answer=="calm"?"Rester mesuré":answer=="ambition"?"Afficher notre ambition":"Assumer et protéger le groupe",()=>RunDecision(()=>Career.Press(phase,choice))).name="press-"+phase+"-"+answer;
                }
            }
        }
    }
}
