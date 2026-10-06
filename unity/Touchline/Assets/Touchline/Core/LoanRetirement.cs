using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    public partial class Career
    {
        bool PlayingCareerEnded(PlayerData p)=>p==null||p.team=="retired"||world?.contracts?.Any(c=>c.player==p.id&&c.retirement>=0&&c.retirement<=life.day)==true;
        void ArchiveRetiredPlayer(PlayerData p)
        {
            // Preserve only the state actually recorded by a managed club.
            // An NPC's unknown appearances are never generated here.
            ArchiveRetiredPlayer(life,p.id);
            if(previousClubs!=null)foreach(var managed in previousClubs)ArchiveRetiredPlayer(managed.life,p.id);
        }
        static void ArchiveRetiredPlayer(ClubLife state,string id)
        {
            if(state==null)return;
            var known=state.players?.FirstOrDefault(x=>x.id==id)??state.loanedPlayers?.FirstOrDefault(x=>x.player?.id==id)?.player;if(known==null)return;
            state.retiredPlayers??=new List<PlayerLife>();
            if(!state.retiredPlayers.Any(x=>x.id==id))state.retiredPlayers.Add(known);
            state.players?.RemoveAll(x=>x.id==id);state.loanedPlayers?.RemoveAll(x=>x.player?.id==id);
        }
        static void CloseRetiredLoan(Employment c)
        {
            c.parent=null;c.loanUntil=0;c.parentUntil=0;c.originalWage=0;c.parentConditions=null;c.purchaseConditions=null;c.parentConditionsUnavailable=false;c.nextWage=0;c.wageChangeDay=0;c.aiRelease=false;
            c.terms=PermanentContractTerms(c.terms);c.conditionsSource="Retraite effective : engagement terminé, clauses de prêt et d’achat annulées. Les indemnités déjà payées restent enregistrées ; aucun remboursement n’est simulé.";
        }
        void RetireEmployment(Database db,PlayerData p,Employment c,bool notify=false)
        {
            bool alreadyRetired=p.team=="retired";
            string owner=c.parent,employer=c.club;
            ArchiveRetiredPlayer(p);
            // Old saves may already have lost the borrower identity. Do not invent
            // an earlier ledger or refund; close only their remaining loan state.
            if(!alreadyRetired)BeforeFinancialTermsChange(db,owner,employer);
            p.team="retired";c.club="retired";CloseRetiredLoan(c);
            if(!alreadyRetired)AfterFinancialTermsChange(db,owner,employer);
            SavePlayer(p);
            if(notify&&!alreadyRetired){life.players.RemoveAll(x=>x.id==p.id);Mail("Secrétariat","Retraite effective",p.name+" met un terme à sa carrière. Les clauses de prêt ne provoquent pas d’achat ; les frais déjà payés ne sont pas remboursés.",p.id);}
        }
    }
}
