using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class StaffOffer { public string staff,club,employer,status="pending";public long wage,compensation;public int years,due; }
    public class AgentEstimate { public long feeLow,feeHigh,monthlyLow,monthlyHigh;public string message; }
    public partial class Career
    {
        public List<StaffMember> staffMarket=new List<StaffMember>();
        public List<StaffOffer> staffOffers=new List<StaffOffer>();
        static uint StableIdentity(string id){uint hash=2166136261;foreach(char c in id)hash=unchecked((hash^c)*16777619);return hash;}
        // Les salaires sont stockés par semaine (entier) ; un montant mensuel saisi est converti par
        // WeeklySalary. Plusieurs mensuels (≈ 4,3 €) donnent la même semaine : l'affichage choisit le plus
        // « rond » d'entre eux (10 000 plutôt que 10 001), donc tout mensuel multiple de 10 € revient à
        // l'identique et WeeklySalary(MonthlySalary(w)) == w reste vrai pour toute semaine.
        static readonly long[] SalaryDisplaySteps={100000,10000,1000,100,10}; // € : arrondis préférés, du plus rond au plus fin
        public static long MonthlySalary(long weekly)
        {
            decimal exact=weekly*52m/12m;
            foreach(long step in SalaryDisplaySteps){long round=(long)Math.Round(exact/step)*step;if(WeeklySalary(round)==weekly)return round;}
            return (long)Math.Round(exact);
        }
        public static long WeeklySalary(long monthly)=>(long)Math.Round(monthly*12m/52m);
        public void EnsureStaffMarket(Database db)
        {
            EnsureStaff();staffMarket??=new List<StaffMember>();staffOffers??=new List<StaffOffer>();
            // Unity JsonUtility restores serialized null strings as empty strings.
            // Null is the employment sentinel used by the market and negotiations.
            // Normalize only absent employers, preserving identities, terms and statuses.
            foreach(var member in staffMarket)if(member.club=="")member.club=null;
            foreach(var offer in staffOffers)if(offer.employer=="")offer.employer=null;
            if(staffMarket.Count==0){
                string psg="https://www.psg.fr/football-masculin/staff",om="https://www.om.fr/fr/equipe-pro-2";
                AddRealStaff(db,"rafel-pol","Rafel Pol Cabanellas","Paris Saint-Germain","assistant",18,psg,"Entraîneur adjoint de l’équipe masculine.");
                AddRealStaff(db,"guillem-hernandez","Guillem Hernandez Folguera","Paris Saint-Germain","assistant",16,psg,"Entraîneur adjoint de l’équipe masculine.");
                AddRealStaff(db,"pedro-gomez","Pedro Gomez Piqueras","Paris Saint-Germain","fitness",18,psg,"Préparateur physique de l’équipe masculine.");
                AddRealStaff(db,"alberto-piernas","Alberto Piernas Carcelen","Paris Saint-Germain","fitness",16,psg,"Préparateur physique adjoint.");
                AddRealStaff(db,"romain-ferrier","Romain Ferrier","Marseille","youth",15,om,"Entraîneur de la Pro 2. Rôle de formation regroupé dans la simulation.");
                AddRealStaff(db,"ahmed-nouri","Ahmed Nouri","Marseille","youth",12,om,"Entraîneur adjoint de la Pro 2.");
                AddRealStaff(db,"marius-lancet","Marius Lancet","Marseille","fitness",12,om,"Préparateur physique de la Pro 2 ; promotion interne possible.");
                AddRealStaff(db,"didier-farrugia","Didier Farrugia","Marseille","fitness",12,om,"Préparateur physique de la Pro 2 ; promotion interne possible.");
                int index=0;foreach(var team in db.clubs.Where(c=>c.playable))foreach(var role in new[]{"assistant","fitness","scout","youth"}){
                    int level=(int)Mathx.Clamp(6+(float)Math.Log10(Math.Max(1000000,team.annualRevenue)/1000000.0)*4,6,18);index++;
                    staffMarket.Add(new StaffMember{id="staff-gen-"+team.id+"-"+role,name=new[]{"Nicolas","Marco","Daniel","Lucas","André","Luca","Hugo","Alex"}[index%8]+" "+new[]{"Morel","Costa","Meyer","Martin","Rossi","Silva","Bernard"}[index%7]+" · "+index,club=team.id,role=role,tactics=level,coaching=level,judging=level,people=level,wage=Math.Max(150,team.annualRevenue/100000),until=life.day+730,biography="Profil fictif de complément."});
                }
                for(int i=0;i<16;i++)staffMarket.Add(new StaffMember{id="staff-free-"+i,name="Consultant "+new[]{"Dumas","Verdi","Alves","Weber"}[i%4]+" "+(i+1),role=new[]{"assistant","fitness","scout","youth"}[i%4],tactics=7+i%10,coaching=8+i%10,judging=7+i%10,people=9+i%8,wage=200+i*70,biography="Profil fictif libre de contrat."});
            }
            EnsureVerifiedStaffCatalog(db);
            if(!life.staff.marketBound){life.staff.members=staffMarket.Where(s=>s.club==club).GroupBy(s=>s.role).Select(g=>g.OrderBy(s=>s.biography!=null&&s.biography.Contains("Pro 2")&&s.role!="youth"?2:s.fictional?1:0).First()).ToList();life.staff.marketBound=true;}
            else{life.staff.members=life.staff.members.Select(s=>staffMarket.FirstOrDefault(m=>m.id==s.id)??s).Where(s=>s.club==club).ToList();}
        }
        void AddRealStaff(Database db,string id,string name,string teamName,string role,int rating,string source,string bio)
        {
            var team=db.clubs.FirstOrDefault(c=>c.name.IndexOf(teamName,StringComparison.OrdinalIgnoreCase)>=0);if(team==null)return;
            staffMarket.Add(new StaffMember{id="real-"+id,name=name,club=team.id,role=role,tactics=rating,coaching=rating,judging=Math.Max(1,rating-2),people=rating,wage=Math.Max(1200,team.annualRevenue/70000),until=life.day+730,fictional=false,source=source,biography=bio+" Page vérifiée le 14/09/2026. Notes, salaire et échéance estimés pour la carrière."});
        }
        public long StaffReleaseCost(StaffMember s)=>s.club==null?0:s.wage*Math.Max(0,(s.until-life.day+6)/7);
        public AgentEstimate PlayerAgent(Database db,string id,bool loan=false)
        {
            var p=db.Find(id)??throw new ArgumentException("Joueur inconnu.");bool own=p.team==club;float uncertainty=Knowledge(id)>=90?.12f:.3f;long fee=own||p.team=="free"?0:(long)(p.value*(loan?.12:1.05));long monthly=MonthlySalary(own||loan?(long)(p.wage*(own?1.08:1)):Math.Max((long)(p.wage*1.12),PlayerTransferInterest(db,id).requiredWeeklyWage));
            return new AgentEstimate{feeLow=(long)(fee*(1-uncertainty)),feeHigh=(long)(fee*(1+uncertainty)),monthlyLow=loan?MonthlySalary(p.wage):(long)(monthly*.95),monthlyHigh=loan?MonthlySalary(p.wage):(long)(monthly*1.15),message=loan?"Son salaire intégral reste celui du contrat parent. Négocions votre pourcentage de prise en charge, l’indemnité et le temps de jeu.":own?"Discutons de son avenir et du temps de jeu promis.":p.rating>Strength(db,club)+8?"Le projet sportif est un obstacle : le salaire seul ne suffira pas.":"Mon joueur écoutera votre projet. Ces fourchettes sont indicatives ; l’accord du club reste distinct du contrat personnel."};
        }
        public AgentEstimate StaffAgent(string id)
        {
            var s=staffMarket.First(x=>x.id==id);long fee=StaffReleaseCost(s),monthly=MonthlySalary(s.wage);
            return new AgentEstimate{feeLow=s.club==club?0:fee,feeHigh=s.club==club?0:fee,monthlyLow=monthly,monthlyHigh=(long)(monthly*1.25),message=s.club==null?"Mon client est libre. Parlons de son rôle et de la durée de son engagement.":s.club==club?"Nous pouvons prolonger son engagement sans indemnité de changement de club.":"Le club demandera la compensation contractuelle simulée. Mon client souhaite une progression salariale et un projet adapté à son niveau."};
        }
        public void ProposeStaffContract(Database db,string id,long monthly,int years)
        {
            OffPitch();EnsureStaffMarket(db);var s=staffMarket.First(x=>x.id==id);if(years<1||years>5||monthly<=0||monthly>10000000)throw new ArgumentException("Salaire ou durée invalide.");if(staffOffers.Any(o=>o.staff==id&&o.club==club&&(o.status=="pending"||o.status=="accepted")))throw new InvalidOperationException("Une discussion est déjà en cours.");
            var offer=new StaffOffer{staff=id,club=club,employer=s.club,wage=WeeklySalary(monthly),years=years,compensation=s.club==club?0:StaffReleaseCost(s),due=life.day+2};staffOffers.Add(offer);Mail("Agent · "+s.name,"Proposition reçue","Nous répondrons sous deux jours. Le poste doit être disponible avant une nouvelle embauche.",null,"staff",StaffMessageReference(offer));
        }
        public void FireStaff(Database db,string id)
        {
            OffPitch();EnsureStaffMarket(db);var s=staffMarket.First(x=>x.id==id&&x.club==club);Charge(StaffReleaseCost(s),"Rupture du contrat • "+s.name);s.club=null;s.until=0;life.staff.members.RemoveAll(m=>m.id==id);life.boardTrust=Math.Max(0,life.boardTrust-1);CancelStaffTrainingFor(s,"Son départ du club interrompt le programme.");Mail("Secrétariat","Départ du staff",s.name+" quitte son poste. Les délégations associées sont suspendues tant qu’il reste vacant.",null,"staff");
        }
        public void SignStaffContract(Database db,string id)
        {
            OffPitch();EnsureStaffMarket(db);var o=staffOffers.LastOrDefault(x=>x.staff==id&&x.club==club&&x.status=="accepted"&&x.due+7>=life.day);if(o==null)throw new InvalidOperationException("Aucun accord valable.");var s=staffMarket.First(x=>x.id==id);if(s.club!=o.employer)throw new InvalidOperationException("L’employeur a changé : reprenez les discussions.");
            bool renewal=life.staff.members.Any(m=>m.id==id);if(!renewal&&life.staff.members.Count(m=>m.role==s.role)>=(s.role=="scout"?ScoutSlots:1))throw new InvalidOperationException(s.role=="scout"?"La cellule de recrutement est complète ("+ScoutSlots+" recruteurs pour votre budget). Libérez un poste avant d’embaucher.":"Libérez le poste avant d’embaucher son remplaçant.");
            long payroll=life.staff.members.Where(m=>m.id!=id).Sum(m=>m.wage)+o.wage;if(payroll>Math.Max(1500,life.revenue/52/40))throw new InvalidOperationException("Le budget salarial du staff est insuffisant.");
            Charge(o.compensation+o.wage*2,"Contrat staff • "+s.name);s.club=club;s.wage=o.wage;s.until=life.day+365*o.years;o.status="signed";if(!renewal)life.staff.members.Add(s);Mail("Secrétariat","Contrat du staff signé",s.name+" : "+MonthlySalary(s.wage).ToString("N0")+" € par mois.",null,"staff");
        }
        void StaffMarketDay(Database db)
        {
            RenewAiStaffContracts(db);
            foreach(var s in staffMarket.Where(s=>s.club!=null&&s.until<=life.day).ToArray()){bool own=s.club==club;s.club=null;if(own){CancelStaffTrainingFor(s,"Son contrat a expiré avant la validation du programme.");life.staff.members.RemoveAll(m=>m.id==s.id);Mail("Staff","Fin de contrat",s.name+" devient libre. Le poste doit être pourvu.",null,"staff");}}
            foreach(var o in staffOffers.Where(o=>o.club==club&&o.status=="pending"&&o.due<=life.day)){var s=staffMarket.First(x=>x.id==o.staff);bool own=s.club==club;long expected=(long)(s.wage*(s.club==null?1:own?1.05:1.15));bool attractive=s.tactics<=Staff("assistant").tactics+4||life.reputation>=80;bool accepted=o.wage>=expected&&(own||attractive)&&s.club==o.employer;o.status=accepted?"accepted":"counter";if(!accepted)o.wage=expected;Mail("Agent · "+s.name,accepted?"Accord de principe":"Contre-proposition",accepted?"Accord valable sept jours. La signature finale vous appartient.":"Salaire demandé : "+MonthlySalary(expected).ToString("N0")+" € / mois. "+(!attractive?"Le projet sportif reste insuffisant.":"Vous pouvez soumettre une nouvelle proposition."),null,"staff",StaffMessageReference(o));}
            foreach(var o in staffOffers.Where(o=>o.status=="accepted"&&o.due+7<life.day))o.status="expired";
            FillAiStaffVacancies(db);
        }
    }
}
