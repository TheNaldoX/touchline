using System;
using System.Linq;
namespace Touchline.Core
{
    public partial class Career
    {
        public bool StaffTrainingInProgress=>life!=null&&!string.IsNullOrEmpty(life.staffTrainingRole);
        public long StaffTrainingCost=>Math.Max(5000,life.revenue/5000);
        public StaffMember StaffTrainingBeneficiary()
        {
            if(!StaffTrainingInProgress||string.IsNullOrEmpty(life.staffTrainingStaffId)||life.staff?.members==null)return null;
            var matches=life.staff.members.Where(s=>s.id==life.staffTrainingStaffId&&s.role==life.staffTrainingRole&&s.club==club&&s.wage>0).Take(2).ToArray();
            return matches.Length==1?matches[0]:null;
        }
        string StaffTrainingReference()=>"staff-training/"+Uri.EscapeDataString(life.staffTrainingStaffId??"")+"/"+life.staffTrainingUntil;
        string StaffTrainingName()
        {
            var known=staffMarket?.Where(s=>s.id==life.staffTrainingStaffId&&!string.IsNullOrEmpty(life.staffTrainingStaffId)).Take(2).ToArray();
            return known?.Length==1?known[0].name:"Dossier de formation";
        }
        void ClearStaffTraining(){life.staffTrainingRole=null;life.staffTrainingStaffId=null;life.staffTrainingUntil=0;}
        void StopStaffTraining(string reason)
        {
            if(!StaffTrainingInProgress)return;
            string reference=StaffTrainingReference(),name=StaffTrainingName();ClearStaffTraining();
            Mail("Secrétariat","Formation interrompue",name+" : "+reason+" Les frais engagés restent à la charge du club. Vous pouvez désormais proposer un nouveau programme à un responsable en poste.",null,"staff",reference);
        }
        void CancelStaffTrainingFor(StaffMember member,string reason)
        {
            if(member==null||!StaffTrainingInProgress)return;
            if(!string.IsNullOrEmpty(life.staffTrainingStaffId)?life.staffTrainingStaffId==member.id:life.staffTrainingRole==member.role)StopStaffTraining(reason);
        }
        void ReviewStaffTraining()
        {
            if(!StaffTrainingInProgress){if(life.staffTrainingUntil!=0||!string.IsNullOrEmpty(life.staffTrainingStaffId))ClearStaffTraining();return;}
            // Old saves contain only a role. They cannot prove who paid-for
            // training belonged to after a replacement, so grant nobody.
            if(string.IsNullOrEmpty(life.staffTrainingStaffId)){StopStaffTraining("Le dossier de cette ancienne formation est incomplet : son bénéficiaire ne peut pas être identifié.");return;}
            var member=StaffTrainingBeneficiary();
            if(member==null){StopStaffTraining("Le responsable ne peut plus poursuivre ce programme : son poste ou son dossier a changé.");return;}
            if(life.day<life.staffTrainingUntil)return;
            member.tactics=Math.Min(20,member.tactics+1);member.coaching=Math.Min(20,member.coaching+1);member.judging=Math.Min(20,member.judging+1);member.people=Math.Min(20,member.people+1);
            string reference=StaffTrainingReference();ClearStaffTraining();
            Mail("Staff","Formation terminée",member.name+" a achevé son programme de 45 jours. Le travail réalisé a renforcé ses compétences. Consultez le bilan dans Staff et délégation.",null,"staff",reference);
        }
        public string StaffTrainingStatus()
        {
            if(!StaffTrainingInProgress)return "Aucune formation du staff en cours.";
            var member=StaffTrainingBeneficiary();
            if(member==null)return "Dossier de formation incomplet : bénéficiaire non identifié en poste. Le secrétariat fera le point au prochain jour.";
            return member.name+" · formation en cours · "+Math.Max(0,life.staffTrainingUntil-life.day)+" jour(s) restant(s) · fin prévue le "+Epoch.AddDays(life.staffTrainingUntil).ToString("dd/MM/yyyy");
        }
    }
}
