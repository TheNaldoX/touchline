using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public sealed class DepartureRequest
    {
        public string club,player,status="watching";
        public int since,opened=-1,answered=-1,lastChecked=-1;
    }
    public partial class Career
    {
        public List<DepartureRequest> departureRequests=new List<DepartureRequest>();
        const int DepartureConcernDays=28; // sustained discontent, not one bad result
        const float DepartureMorale=45,DepartureTrust=50; // 0–100
        public DepartureRequest DepartureFor(string id)=>departureRequests?.LastOrDefault(r=>r.club==club&&r.player==id&&r.status!="closed"&&r.status!="withdrawn");
        public void ReviewDepartureRequests(Database db)
        {
            if(world?.managerStatus!="employed"||life==null)return;
            departureRequests??=new List<DepartureRequest>();
            foreach(var r in departureRequests.Where(r=>r.status!="closed"&&r.status!="withdrawn").ToArray())
                if(db.Find(r.player)?.team!=r.club)r.status="closed";
            foreach(var p in life.players)
            {
                if(db.Find(p.id)?.team!=club)continue;
                var contract=world.contracts.FirstOrDefault(c=>c.player==p.id&&c.club==club);
                if(!string.IsNullOrEmpty(contract?.parent))continue;
                var r=DepartureFor(p.id);
                bool concern=PlayingTimeConcern(p.id)&&p.morale<DepartureMorale&&p.trust<DepartureTrust&&Available(p.id);
                if(r==null){if(!concern)continue;r=new DepartureRequest{club=club,player=p.id,since=life.day};departureRequests.Add(r);}
                if(r.lastChecked==life.day)continue;r.lastChecked=life.day;
                if(r.status=="refused"&&!concern)r.since=life.day;
                if(r.status=="watching"){
                    if(!concern){r.since=life.day;continue;}
                    if(life.day-r.since<DepartureConcernDays)continue;
                    r.status="requested";r.opened=life.day;
                    Mail(db.Find(p.id).name,"Je souhaite partir","Mon rôle et mon temps de jeu ne correspondent plus à notre accord. Cette situation dure : je souhaite que nous étudiions un départ. Aucune vente ne peut être signée sans votre décision.",p.id,"talk","departure/"+club+"/"+p.id+"/"+r.opened);
                }
                else if(!PlayingTimeConcern(p.id)&&p.morale>=55&&p.trust>=55){
                    r.status="withdrawn";
                    Mail(db.Find(p.id).name,"Je retire ma demande","Mon utilisation et notre relation ont évolué. Je suis prêt à poursuivre au club. Une éventuelle offre de vente déjà reçue reste à votre appréciation.",p.id,"talk");
                }
                else if(r.status=="refused"&&r.answered>=0&&life.day-Math.Max(r.answered,r.since)>=DepartureConcernDays&&concern){
                    r.status="requested";r.opened=life.day;
                    Mail(db.Find(p.id).name,"Ma situation n’a pas changé","Depuis notre dernier échange, mon temps de jeu reste inférieur à notre accord. Je souhaite reparler de mon avenir. Étudions un départ ou trouvons une solution concrète ; mon contrat reste en vigueur tant qu’aucun transfert n’est signé.",p.id,"talk","departure/"+club+"/"+p.id+"/"+r.opened);
                }
            }
            foreach(var r in departureRequests.Where(r=>r.status=="closed"||r.status=="withdrawn").OrderBy(r=>r.lastChecked).Take(Math.Max(0,departureRequests.Count-300)).ToArray())departureRequests.Remove(r);
        }
        public void AnswerDeparture(Database db,string id,bool allow)
        {
            OffPitch();var r=DepartureFor(id);
            if(r?.status!="requested"||db.Find(id)?.team!=club)throw new InvalidOperationException("Aucune demande de départ à traiter pour ce joueur.");
            // Listing can fail (squad size, ownership, no buyer). Keep the request
            // untouched until that check succeeds; listing is not a signed sale.
            if(allow&&!world.offers.Any(o=>o.player==id&&o.status=="sale"&&o.due>=life.day))ListForSale(db,id);
            r.status=allow?"listed":"refused";r.answered=life.day;r.since=life.day;
            var p=Person(id);p.trust=Mathx.Clamp(p.trust+(allow?3:-5),0,100);
            Mail(db.Find(id).name,"Suite à ma demande",allow?"Merci d’étudier les offres. Mon contrat continue tant qu’aucun transfert n’est signé.":"Je prends acte du refus. Il faudra des changements concrets de temps de jeu pour me convaincre de rester.",id,"talk");ApplyLife(db);
        }
        public string DepartureStatus(string id)
        {
            var r=DepartureFor(id);if(r==null||r.status=="watching")return null;
            return r.status=="requested"?"Demande de départ en attente de votre réponse.":r.status=="listed"?"Départ envisagé : une offre reste à accepter, aucun transfert automatique.":"Demande refusée : le joueur attend des changements concrets.";
        }
    }
}
