using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    // Career projections anchored once to the loaded roster; these are not verified future salaries.
    [Serializable] public class ClubDevelopmentReference
    {
        public string club; public float rating; public long wage,value,revenue; public int squadSize;
    }
    public partial class Career
    {
        public static int RetirementThreshold(PlayerData player)=>(player.Goalkeeper?40:37)+(player.rating>=80?3:player.rating>=72?1:0);
        void AnnualPlayerDevelopment(Database db)
        {
            ProcessAiEmployment(db);
            // Daily training is an accrued improvement, not a temporary match modifier.
            // Settle it once before aging/renewals so ability, salary and attributes agree.
            foreach(var person in life.players){
                var player=db.Find(person.id);if(player==null||player.team!=club)continue;
                float gain=Math.Min(Math.Max(0,person.growth),Math.Max(0,player.potential-player.rating));
                player.rating+=gain;foreach(var attribute in player.attributes??Array.Empty<AttributeValue>())attribute.value=Math.Min(99,attribute.value+gain);
                person.growth=0;player.development=0;
            }
            world.developmentReferences ??= new List<ClubDevelopmentReference>();
            var references=world.developmentReferences.ToDictionary(x=>x.club);
            var squads=db.players.Where(p=>p.team!="retired").GroupBy(p=>p.team).ToDictionary(g=>g.Key,g=>g.ToList());
            foreach(var team in db.clubs){
                if(references.ContainsKey(team.id)||!squads.TryGetValue(team.id,out var squad)||squad.Count==0)continue;
                var reference=new ClubDevelopmentReference{club=team.id,rating=squad.Average(p=>p.rating),wage=(long)squad.Average(p=>p.wage),value=(long)squad.Average(p=>p.value),revenue=Math.Max(1,team.annualRevenue),squadSize=Math.Max(22,Math.Min(32,squad.Count))};
                references.Add(team.id,reference);world.developmentReferences.Add(reference);
            }
            var contracts=world.contracts.ToDictionary(c=>c.player);
            var facilities=(world.aiAccounts??new List<AiClubAccount>()).ToDictionary(a=>a.club);
            foreach(var p in db.players.Where(p=>p.team!="retired")){
                // Known birthdays advance with the actual date, never a
                // second time at season rollover. Unknown ages keep the
                // existing annual simulation rather than inventing a birthday.
                p.age=PlayerChronology.TryAge(p,Date,out int datedAge)?datedAge:p.age+1;
                // Managed players develop through daily training; do not apply the AI increment twice.
                if((p.team!=club||world.managerStatus!="employed")&&p.team!="free"&&!p.team.StartsWith("academy-")&&p.age<=26){
                    float training=facilities.TryGetValue(p.team,out var infrastructure)?1+(infrastructure.training-1)*.06f:1;
                    float gain=Math.Min(Math.Max(0,p.potential-p.rating),(p.age<22?2.8f:p.age<25?1.8f:.7f)*(.8f+StableIdentity(p.id)%41/100f)*training);
                    p.rating+=gain;foreach(var a in p.attributes??Array.Empty<AttributeValue>())a.value=Math.Min(99,a.value+gain);
                }
                if(p.age>31){p.rating=Math.Max(25,p.rating-(p.Goalkeeper?.5f:1.3f));p.potential=Math.Max(p.rating,p.potential-1);foreach(var a in p.attributes??Array.Empty<AttributeValue>()){bool physical=a.key=="sprintSpeed"||a.key=="acceleration"||a.key=="stamina";a.value=Mathx.Clamp(a.value-(physical?1.8f:p.Goalkeeper?.35f:.6f),10,99);}}
                if(p.age>=RetirementThreshold(p)||contracts.TryGetValue(p.id,out var retirementContract)&&retirementContract.retirement>=0&&retirementContract.retirement<=life.day){
                    bool managed=p.team==club;ArchiveRetiredPlayer(p);p.team="retired";if(contracts.TryGetValue(p.id,out var employment)){employment.club="retired";CloseRetiredLoan(employment);}
                    life.players.RemoveAll(x=>x.id==p.id);
                    if(managed)Mail("Secrétariat","Retraite effective",p.name+" met un terme à sa carrière.",p.id);
                }else if(p.team.StartsWith("academy-")&&p.age>=22){
                    bool managed=p.team=="academy-"+club;p.team="free";p.wage=Math.Max(250,p.wage);
                    var path=world.youth.FirstOrDefault(y=>y.player==p.id);if(path!=null){path.group="released";path.mentor=null;}
                    if(managed)Mail("Formation","Fin du parcours au centre",p.name+" quitte le centre à 22 ans et recherche un contrat senior. Il reste accessible sur le marché libre.",p.id,"academy");
                }
            }
            var additions=new List<PlayerData>();
            // Replace retiring roles, then fill vacancies left by sales. Never take control of the user's squad.
            foreach(var team in db.clubs.Where(t=>t.id!=club||world.managerStatus!="employed")){
                if(!references.TryGetValue(team.id,out var reference)||!squads.TryGetValue(team.id,out var before))continue;
                var live=before.Where(p=>p.team==team.id).ToList();long wageCeiling=AiGrossWageCeiling(team);
                foreach(var retired in before.Where(p=>p.team=="retired")){
                    if(live.Count>=reference.squadSize)break;
                    var newcomer=CreateReplacement(db,team,reference,retired,additions.Count);additions.Add(newcomer);live.Add(newcomer);
                }
                while(live.Count<reference.squadSize){
                    var template=before.OrderBy(p=>live.Count(x=>x.Goalkeeper==p.Goalkeeper&&x.position==p.position)).First();
                    var candidate=db.players.Where(p=>p.team=="free"&&p.age>=18&&p.age<32&&p.Goalkeeper==template.Goalkeeper&&p.position==template.position&&p.rating>=reference.rating-9&&p.rating<=reference.rating+5&&(!contracts.TryGetValue(p.id,out var activeLoan)||activeLoan.parent==null)&&!world.offers.Any(o=>o.player==p.id&&o.status=="scheduled")).OrderBy(p=>Math.Abs(p.rating-reference.rating)).FirstOrDefault();
                    if(candidate==null){candidate=CreateReplacement(db,team,reference,template,additions.Count);additions.Add(candidate);}else candidate.team=team.id;
                    live.Add(candidate);
                }
                var surplus=new HashSet<string>();int remaining=live.Count,keepers=live.Count(p=>p.Goalkeeper);
                foreach(var fringe in live.Where(p=>contracts.TryGetValue(p.id,out var e)&&e.parent==null&&e.until<=life.day+21).OrderBy(p=>p.rating)){
                    if(remaining<=reference.squadSize)break;if(fringe.Goalkeeper&&keepers<=2)continue;
                    // Do not empty a role merely to reduce payroll; wait for a different expiry.
                    if(live.Count(p=>p.position==fringe.position&&!surplus.Contains(p.id))<=1)continue;
                    surplus.Add(fringe.id);remaining--;if(fringe.Goalkeeper)keepers--;
                }
                double weights=live.Where(p=>!surplus.Contains(p.id)).Sum(p=>Math.Max(p.wage,reference.wage*Math.Pow(1.075,p.rating-reference.rating)));
                var departing=new List<PlayerData>();
                foreach(var p in live){
                    if(contracts.TryGetValue(p.id,out var contract)&&contract.parent!=null)continue;
                    if(surplus.Contains(p.id)){contract.aiRelease=true;continue;}
                    bool newEmployment=contract==null||contract.club!=team.id;
                    if(newEmployment||contract.until<=life.day+21){
                        float level=(float)Math.Pow(1.075,p.rating-reference.rating),resources=Mathx.Clamp(team.annualRevenue/(float)reference.revenue,.45f,2);
                        double expected=reference.wage*level*resources;if(!newEmployment)expected=Math.Max(expected,p.wage*(p.age<30?1.02:.95));
                        double share=Math.Max(p.wage,reference.wage*level)/Math.Max(1,weights);long offer=Math.Max(250,(long)Math.Min(expected,wageCeiling*share));
                        if(!newEmployment&&p.wage>0&&offer<p.wage*.75){contract.aiRelease=true;departing.Add(p);continue;}
                        p.value=Math.Max(25000,(long)(reference.value*level*(p.age>30?.55f:1)));
                        p.salarySource="Projection de carrière : contrat estimé, niveau et plafond salarial du club";p.valueSource="Estimation de simulation, hors cotation réelle";
                        if(contract==null){contract=new Employment{player=p.id,club=team.id};world.contracts.Add(contract);contracts.Add(p.id,contract);}
                        if(!newEmployment&&contract.until>life.day){contract.nextWage=offer;contract.wageChangeDay=contract.until+1;}else {p.wage=offer;contract.wage=offer;contract.nextWage=0;contract.wageChangeDay=0;}
                        contract.club=team.id;contract.until=life.day+365*(p.age>30?2:3);contract.estimated=true;contract.aiRelease=false;
                    }
                }
                // Announced non-renewals open an academy pathway now; the departing player keeps
                // his signed wage until expiry. No forced cut and no unplayable squad in August.
                foreach(var leaving in departing){
                    var p=CreateReplacement(db,team,reference,leaving,additions.Count);p.wage=Math.Max(250,(long)(wageCeiling*.85/Math.Max(22,reference.squadSize)));p.value=Math.Max(25000,reference.value/2);
                    p.salarySource="Premier contrat simulé, enveloppe du club";additions.Add(p);
                    world.contracts.Add(new Employment{player=p.id,club=team.id,wage=p.wage,until=life.day+365*3,estimated=true});
                }
            }
            db.players=db.players.Concat(additions).ToArray();
            var retiredIds=new HashSet<string>(db.players.Where(p=>p.team=="retired").Select(p=>p.id));world.contracts.RemoveAll(c=>retiredIds.Contains(c.player));
            var saved=world.rosterChanges.ToDictionary(p=>p.id);foreach(var p in db.players)saved[p.id]=p;world.rosterChanges=saved.Values.ToList();
            world.youth.RemoveAll(y=>db.Find(y.player)?.team=="retired");
        }
        void ProcessAiEmployment(Database db)
        {
            foreach(var contract in world.contracts){
                if(contract.nextWage<=0&&!contract.aiRelease)continue;
                var p=db.Find(contract.player);if(p==null||p.team!=contract.club||contract.parent!=null)continue;
                if(contract.nextWage>0&&contract.wageChangeDay<=life.day){p.wage=contract.nextWage;contract.wage=p.wage;contract.nextWage=0;contract.wageChangeDay=0;SavePlayer(p);}
                if(contract.aiRelease&&contract.until<=life.day&&!(contract.retirement>=0&&contract.retirement<=life.day)&&!world.offers.Any(o=>o.player==p.id&&o.status=="scheduled")){
                    bool managed=p.team==club;SettlePermanentDepartureGrowth(p);p.team="free";contract.club="free";contract.aiRelease=false;life.players.RemoveAll(x=>x.id==p.id);SavePlayer(p);
                    if(managed)Mail("Secrétariat","Départ en fin de contrat",p.name+" n’a pas accepté la proposition de renouvellement et quitte le club à l’échéance.",p.id,"transfer");
                }
            }
        }
        PlayerData CreateReplacement(Database db,ClubData team,ClubDevelopmentReference reference,PlayerData template,int serial)
        {
            string[] first={"Alex","Noah","Elias","Adam","Gabriel","Sacha","Amine","Leo","Mathis","Ilyes","Nolan","Hugo"};
            string[] last={"Martin","Morel","Bernard","Petit","Roux","Diallo","Laurent","Perrin","Henry","Simon","Benoit","Garcia"};
            var nationalNames=db.players.Where(p=>p.nationality==template.nationality&&!p.id.StartsWith("gen-")&&!p.id.StartsWith("regen-")&&!string.IsNullOrWhiteSpace(p.name)).Select(p=>p.name).Distinct().ToArray();
            if(nationalNames.Length>0){first=nationalNames.Select(n=>n.Split(' ')[0]).Distinct().ToArray();last=nationalNames.Select(n=>n.Split(' ').Last()).Distinct().ToArray();}
            float rating=Mathx.Clamp(reference.rating-10+Roll()*10,30,83);
            var p=new PlayerData{id="regen-"+team.id+"-"+world.year+"-"+serial,name=first[(int)(Roll()*first.Length)]+" "+last[(int)(Roll()*last.Length)],team=team.id,age=18+(int)(Roll()*4),position=template.position,positions=template.positions?.ToArray(),nationality=template.nationality,number=template.number,preferredFoot=Roll()<.24f?"Left":"Right",rating=rating,potential=Mathx.Clamp(reference.rating+Roll()*9, rating,94),fitness=100,morale=75,heightCm=(template.Goalkeeper?184:170)+(int)(Roll()*17),weightKg=67+(int)(Roll()*20),source="Joueur fictif généré par la carrière",assessment="Niveau et potentiel simulés à partir de la référence initiale du club",physiqueSource="Morphologie fictive générée"};
            var academy=world.aiAccounts?.FirstOrDefault(a=>a.club==team.id);if(academy!=null)p.potential=Math.Min(94,p.potential+(academy.academy-1)*.35f);
            p.attributes=(template.attributes??Array.Empty<AttributeValue>()).Select(a=>new AttributeValue{key=a.key,value=Mathx.Clamp(rating+(a.value-template.rating)*.65f+(Roll()-.5f)*8,10,95)}).ToArray();return p;
        }
    }
}
