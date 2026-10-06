using System;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;

namespace Touchline.Core
{
    public partial class Career
    {
        public bool freeAgentsImported;
        // Session cache: the legacy keeper-attribute repair below is a no-op once
        // applied, and only the import can add such keepers again.
        [NonSerialized] bool freeAgentKeepersRepaired;
        public int freeAgentsImportVersion,freeAgentsLastCheckedDay=-1;
        public List<string> announcedFreeAgentReferences=new List<string>(),processedFreeAgentReferences=new List<string>();
        [NonSerialized] List<Employment> employmentIndexSource;
        [NonSerialized] Dictionary<string,int> employmentIndex;
        [NonSerialized] int employmentIndexCount=-1;
        [NonSerialized] PlayerData[] freeReferencePlayers;
        [NonSerialized] FreeAgentReference[] freeReferenceSource;
        [NonSerialized] Dictionary<string,FreeAgentReference[]> freeReferenceIndex;
        [NonSerialized] PlayerData[] freeImportNameSource;
        [NonSerialized] Dictionary<string,PlayerData[]> freeImportNames;
        Employment LookupEmployment(string id)
        {
            if(world?.contracts==null)return null;
            if(employmentIndex==null||!ReferenceEquals(employmentIndexSource,world.contracts)||employmentIndexCount!=world.contracts.Count){employmentIndexSource=world.contracts;employmentIndexCount=world.contracts.Count;employmentIndex=new Dictionary<string,int>();for(int i=0;i<world.contracts.Count;i++)if(!string.IsNullOrEmpty(world.contracts[i].player))employmentIndex[world.contracts[i].player]=i;}
            if(employmentIndex.TryGetValue(id,out int index)&&world.contracts[index].player==id)return world.contracts[index];
            for(int i=0;i<world.contracts.Count;i++)if(world.contracts[i].player==id){employmentIndex[id]=i;return world.contracts[i];}return null;
        }
        FreeAgentReference[] PlayerFreeReferences(Database db,string id)
        {
            if(db.freeAgents==null)return Array.Empty<FreeAgentReference>();
            if(freeReferenceIndex==null||!ReferenceEquals(freeReferenceSource,db.freeAgents)||!ReferenceEquals(freeReferencePlayers,db.players))
            {
                freeReferenceSource=db.freeAgents;freeReferencePlayers=db.players;var identities=db.players.GroupBy(p=>Identity(p.name)).ToDictionary(g=>g.Key,g=>g.ToArray());var mapped=new List<(string player,FreeAgentReference reference)>();
                foreach(var r in db.freeAgents){if(!string.IsNullOrEmpty(r.playerId)){mapped.Add((r.playerId,r));continue;}if(identities.TryGetValue(Identity(r.displayName),out var matches)){var candidates=matches.Where(p=>FreeIdentityMatches(p,r)).ToArray();if(candidates.Length==1)mapped.Add((candidates[0].id,r));}}
                freeReferenceIndex=mapped.GroupBy(x=>x.player).ToDictionary(g=>g.Key,g=>g.Select(x=>x.reference).ToArray());
            }
            return freeReferenceIndex.TryGetValue(id,out var references)?references:Array.Empty<FreeAgentReference>();
        }
        static string Identity(string name)=>string.IsNullOrEmpty(name)?"":new string(name.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark&&char.IsLetter(c)).ToArray()).ToLowerInvariant();
        void ImportFreeAgents(Database db)
        {
            ImportReferencedFreeAgents(db);
            if(!freeAgentKeepersRepaired)foreach(var keeper in db.players.Where(p=>p.id.StartsWith("unfp-free-",StringComparison.Ordinal)&&p.Goalkeeper))
            {
                var missing=FreeAgentAttributes(keeper,"GK").Where(a=>a.key.StartsWith("gk",StringComparison.Ordinal)&&!(keeper.attributes?.Any(v=>v.key==a.key)??false)).ToArray();
                if(missing.Length>0){var oldReflex=keeper.attributes?.FirstOrDefault(a=>a.key=="goalkeeperReflexes");foreach(var a in missing)if(a.key=="gkReflexes"&&oldReflex!=null)a.value=oldReflex.value;keeper.attributes=(keeper.attributes??Array.Empty<AttributeValue>()).Concat(missing).ToArray();SavePlayer(keeper);}
            }
            freeAgentKeepersRepaired=true;
            if(freeAgentsImported||Date<new DateTime(2026,9,14))return;freeAgentsImported=true;freeAgentKeepersRepaired=false;
            string[] names={"Mathieu Acapandie","Moise Adilehou","Dava David Agossa","Johanne Akassou","Rassambek Akhmatov","Sofiane Alakouch","Rachid Alioui","Stéfan Bajic"};
            string[] births={"2004-12-14","1995-11-01","2003-05-13","1996-03-27","1996-05-31","1998-07-29","1992-06-18","2001-12-23"};string[] roles={"CB","CB","GK","ST","CM","RB","ST","GK"};
            int[] estimates={54,61,48,55,56,64,62,63};
            for(int i=0;i<names.Length;i++){
                // Do not move an already imported player away from a club based on a conflicting snapshot.
                if(db.players.Any(p=>Identity(p.name)==Identity(names[i])))continue;
                var birth=DateTime.Parse(births[i],CultureInfo.InvariantCulture);int age=Date.Year-birth.Year;if(birth.AddYears(age)>Date)age--;
                var p=new PlayerData{id="unfp-free-"+i,name=names[i],team="free",age=age,positions=new[]{roles[i]},position=BroadRole(roles[i]),rating=estimates[i],potential=estimates[i]+(age<25?6:0),fitness=80,morale=60,wage=500+(estimates[i]-45)*100,value=50000+(estimates[i]-45)*15000,preferredFoot=null,source="https://www.unfp.org/joueurs-libres/",rosterSource="https://www.unfp.org/joueurs-libres/",rosterAsOf="2026-09-14",assessment="Identité, naissance et statut libre : liste UNFP consultée le 14/09/2026, disponible à partir de cette observation. Niveau, potentiel et prétentions salariales estimés ; pied non renseigné."};
                p.attributes=FreeAgentAttributes(p,roles[i]);db.players=db.players.Concat(new[]{p}).ToArray();SavePlayer(p);Contract(db,p.id).until=life.day;
            }
        }
        static string BroadRole(string role)=>role=="GK"||role=="GB"?"GB":new[]{"CB","LB","RB","DEF"}.Contains(role)?"DEF":new[]{"ST","LW","RW","ATT"}.Contains(role)?"ATT":"MIL";
        static AttributeValue[] FreeAgentAttributes(PlayerData p,string role)
        {
            var keys=new[]{"sprintSpeed","acceleration","agility","balance","stamina","strength","shortPassing","longPassing","finishing","shotPower","vision","composure","reactions","positioning","defensiveAwareness","interceptions","dribbling","ballControl","crossing","headingAccuracy","standingTackle","slidingTackle","jumping","penalties","longShots","freeKickAccuracy","curve","volleys","aggression"};
            if(p.Goalkeeper)keys=keys.Concat(new[]{"gkReflexes","gkDiving","gkHandling","gkKicking","gkPositioning"}).ToArray();
            return keys.Select(key=>{
                float offset=(int)(StableIdentity(p.id+"/"+key)%9)-4;
                bool physical=key=="sprintSpeed"||key=="acceleration"||key=="agility"||key=="stamina";
                if(physical)offset-=Math.Max(0,p.age-30)*.65f;
                if(key=="composure"||key=="vision"||key=="positioning")offset+=Math.Min(4,Math.Max(0,p.age-23)*.35f);
                if(role=="ST"&&(key=="finishing"||key=="positioning"))offset+=5;
                if((role=="CB"||role=="DEF")&&(key=="standingTackle"||key=="defensiveAwareness"||key=="headingAccuracy"))offset+=5;
                if((role=="LW"||role=="RW")&&(key=="dribbling"||key=="crossing"||key=="acceleration"))offset+=4;
                if((role=="CM"||role=="AM"||role=="DM"||role=="MIL")&&(key=="shortPassing"||key=="vision"))offset+=4;
                if(p.Goalkeeper)offset+=key.StartsWith("gk",StringComparison.Ordinal)?3:(key=="finishing"||key=="standingTackle"||key=="slidingTackle"||key=="dribbling"?-25:0);
                return new AttributeValue{key=key,value=Mathx.Clamp(p.rating+offset,15,90)};
            }).ToArray();
        }
        // A market valuation is an economic signal, not a measured skill or a
        // transfer fee for an unattached player. Its dated, age-adjusted proxy
        // seeds an explicitly uncertain simulation profile until scouting.
        float FreeReferenceLevel(Database db,FreeAgentReference r,int age,out bool datedValue)
        {
            datedValue=r.marketValueEuro>0&&ReferenceDate(r.valuationDate,out var valuation)&&valuation.Date<=Date.Date;
            if(datedValue){float ageAdjustment=Mathx.Clamp((age-27)*.45f,-4,6);return Mathx.Clamp(50+(float)Math.Log(Math.Max(25000,r.marketValueEuro)/50000d)*7+ageAdjustment,38,82);}
            var former=db.clubs.FirstOrDefault(c=>c.id==r.formerClubId);
            return former!=null?Mathx.Clamp(Strength(db,former.id)-18,42,68):50;
        }
        static bool ReferenceDate(string value,out DateTime date)=>DateTime.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out date);
        bool FreeIdentityMatches(PlayerData p,FreeAgentReference r)
        {
            if(!ReferenceDate(r.birthDate,out var birth))return true;
            if(ReferenceDate(p.birthDate??p.evidence?.birthDate,out var knownBirth))return birth.Date==knownBirth.Date;
            int expectedAge=Date.Year-birth.Year;if(birth.AddYears(expectedAge)>Date)expectedAge--;
            // Older roster snapshots may contain only age, with a birthday
            // between snapshot and career opening; reject obvious homonyms.
            return Math.Abs(expectedAge-p.age)<=1;
        }
        bool ReferenceKnown(FreeAgentReference r)=>ReferenceDate(r.asOfDate,out var observed)&&observed.Date<=Date.Date&&(string.IsNullOrEmpty(r.announcedAt)||ReferenceDate(r.announcedAt,out var announced)&&announced.Date<=Date.Date);
        static string ReferenceKey(FreeAgentReference r)=>(r.sourceId??r.sourceUrl??r.id??"catalogue")+"/"+Identity(string.IsNullOrEmpty(r.surname)?r.displayName:r.surname+" "+r.givenNames)+"/"+r.birthDate+"/"+(r.formerClubId??Identity(r.formerClubName))+"/"+r.effectiveDate;
        bool ReferenceClubMatches(Database db,PlayerData p,FreeAgentReference r)
        {
            if(!string.IsNullOrEmpty(r.formerClubId))return p.team==r.formerClubId;
            var team=db.clubs.FirstOrDefault(c=>c.id==p.team);return team!=null&&!string.IsNullOrEmpty(r.formerClubName)&&Identity(team.name)==Identity(r.formerClubName);
        }
        bool CareerEmploymentProtected(PlayerData p)
        {
            var contract=LookupEmployment(p.id);
            return contract!=null&&(!contract.estimated||contract.parent!=null||contract.joined>0||contract.nextWage>0)||(world.offers?.Any(o=>o.player==p.id&&new[]{"signed","scheduled"}.Contains(o.status))??false)||(world.aiTransfers?.Any(t=>t.player==p.id)??false);
        }
        void ImportReferencedFreeAgents(Database db)
        {
            if(world==null||db.freeAgents==null||db.freeAgents.Length==0)return;
            if(freeAgentsLastCheckedDay==life.day&&freeAgentsImportVersion>=Math.Max(1,db.freeAgentCatalogVersion))return;
            freeAgentsLastCheckedDay=life.day;freeAgentsImportVersion=Math.Max(1,db.freeAgentCatalogVersion);announcedFreeAgentReferences??=new List<string>();processedFreeAgentReferences??=new List<string>();
            var processed=new HashSet<string>(processedFreeAgentReferences);var announced=new HashSet<string>(announcedFreeAgentReferences);
            if(freeImportNames==null||!ReferenceEquals(freeImportNameSource,db.players)){freeImportNameSource=db.players;freeImportNames=db.players.GroupBy(p=>Identity(p.name)).ToDictionary(g=>g.Key,g=>g.ToArray());}
            var names=freeImportNames;
            foreach(var r in db.freeAgents)
            {
                string key=ReferenceKey(r);if(processed.Contains(key)||!ReferenceKnown(r)||!ReferenceDate(r.effectiveDate,out var release))continue;
                bool scheduled=r.status=="scheduled-release"&&r.autoReleaseEligible,continuous=r.status=="free-continuous"&&r.autoReleaseEligible,observed=r.status=="free-observed-later"||continuous;
                if(!scheduled&&!observed)continue;
                var p=!string.IsNullOrEmpty(r.playerId)?db.Find(r.playerId):null;
                if(p==null&&names.TryGetValue(Identity(r.displayName),out var matching)){var candidates=matching.Where(person=>FreeIdentityMatches(person,r)).ToArray();if(candidates.Length>1)continue;if(candidates.Length==1)p=candidates[0];}
                if(p!=null)
                {
                    if(p.team=="retired"||CareerEmploymentProtected(p)){processed.Add(key);processedFreeAgentReferences.Add(key);continue;}
                    if(p.team!="free"&&(!(scheduled||continuous)||!ReferenceClubMatches(db,p,r)))continue;
                    if(scheduled&&p.team!="free"&&ReferenceDate(r.contractEndsOn,out var expiry))
                    {
                        var contract=Contract(db,p.id);
                        if(!announced.Contains(key)){contract.until=DayOf(expiry);contract.aiRelease=true;announced.Add(key);announcedFreeAgentReferences.Add(key);}
                        if(contract.until!=DayOf(expiry)||!contract.aiRelease){processed.Add(key);processedFreeAgentReferences.Add(key);continue;}
                    }
                    if(release.Date>Date.Date)continue;
                    SettlePermanentDepartureGrowth(p);p.team="free";var e=Contract(db,p.id);e.club="free";e.aiRelease=false;e.until=Math.Min(e.until,life.day);life.players.RemoveAll(x=>x.id==p.id);SavePlayer(p);
                }
                else
                {
                    if(release.Date>Date.Date||!ReferenceDate(r.birthDate,out var birth)||birth>Date||r.positions==null||r.positions.Length==0||string.IsNullOrWhiteSpace(r.displayName))continue;
                    string role=r.positions[0];if(!new[]{"GK","GB","CB","LB","RB","DEF","DM","CM","AM","MIL","LW","RW","ST","ATT"}.Contains(role))continue;
                    int age=Date.Year-birth.Year;if(birth.AddYears(age)>Date)age--;if(age<16||age>45)continue;
                    float referenceLevel=FreeReferenceLevel(db,r,age,out bool datedValue);
                    float growth=age<23?6+StableIdentity(key+"/development")%7:age<27?2:0;
                    p=new PlayerData{id="verified-free-"+StableIdentity(key),name=r.displayName,team="free",positions=r.positions,position=BroadRole(role),nationality=r.nationality,age=age,rating=referenceLevel,potential=Math.Min(88,referenceLevel+growth),fitness=80,morale=65,wage=Math.Max(250,(long)(500*Math.Pow(1.12,referenceLevel-50))),value=datedValue?r.marketValueEuro:Math.Max(25000,(long)(50000*Math.Pow(1.08,referenceLevel-50))),source=r.sourceUrl,rosterSource=r.sourceUrl,rosterAsOf=r.asOfDate,salarySource="Prétentions hebdomadaires estimées par la simulation, négociables ; salaire réel inconnu.",valueSource=datedValue?r.valuationSourceUrl+" · cotation du "+r.valuationDate+" · consultée le "+r.valuationObservedAt:"Valeur économique estimée par la simulation, aucune cotation vérifiée.",assessment="Identité, naissance, poste et statut : catalogue sourcé. Niveau, potentiel et attributs estimés avec une forte incertitude ; "+(datedValue?"proxy économique corrigé de l’âge à partir d’une cotation datée, pas une mesure de performance. ":"aucune mesure individuelle de performance disponible. ")+"Prétentions salariales estimées ; pied non renseigné."};
                    p.surname=r.surname;p.givenNames=r.givenNames;p.birthDate=r.birthDate;
                    p.attributes=FreeAgentAttributes(p,role);db.players=db.players.Concat(new[]{p}).ToArray();freeImportNameSource=db.players;SavePlayer(p);var employment=Contract(db,p.id);employment.until=life.day;
                    string identity=Identity(p.name);names[identity]=names.TryGetValue(identity,out var homonyms)?homonyms.Concat(new[]{p}).ToArray():new[]{p};
                }
                p.rosterSource=r.sourceUrl;p.rosterAsOf=r.asOfDate;processed.Add(key);processedFreeAgentReferences.Add(key);
            }
        }
        public FreeAgentReference AnnouncedFreeAgentRelease(Database db,string id)
        {
            var references=PlayerFreeReferences(db,id);if(references.Length==0)return null;var p=db.Find(id);if(p==null||p.team=="free"||p.team=="retired"||CareerEmploymentProtected(p))return null;var contract=LookupEmployment(id);if(contract==null)return null;
            return references.FirstOrDefault(r=>r.status=="scheduled-release"&&r.autoReleaseEligible&&ReferenceKnown(r)&&ReferenceClubMatches(db,p,r)&&(string.IsNullOrEmpty(r.contractEndsOn)||ReferenceDate(r.contractEndsOn,out var expiry)&&contract.until==DayOf(expiry))&&ReferenceDate(r.effectiveDate,out var release)&&release.Date>Date.Date);
        }
        public bool CanPrecontract(Database db,string id)
        {
            var p=db.Find(id);if(p==null||p.team==club||p.team=="retired"||p.team=="free"||p.age<18||p.team.StartsWith("academy-"))return false;var c=Contract(db,id);
            var departure=AnnouncedFreeAgentRelease(db,id);if(departure!=null&&string.IsNullOrEmpty(departure.contractEndsOn))return false;
            return c.parent==null&&c.until>life.day&&Epoch.AddDays(c.until)<=Date.AddMonths(6)&&!world.offers.Any(o=>o.player==id&&o.status=="scheduled");
        }
        public long ReservedWages=>world?.offers.Where(o=>o.destination==club&&o.status=="scheduled").Sum(o=>o.wage)??0;
        void ActivatePrecontracts(Database db)
        {
            foreach(var o in world.offers.Where(o=>o.status=="scheduled"&&o.joinDay<=life.day)){
                var p=db.Find(o.player);if(PlayingCareerEnded(p)){o.status="cancelled";continue;}var c=Contract(db,p.id);
                string previous=p.team;SettlePermanentDepartureGrowth(p);p.team=o.destination;p.wage=o.wage;c.club=p.team;c.parent=null;c.joined=life.day;c.until=DayOf(new DateTime(Date.Year+o.years,6,30));c.wage=o.wage;c.role=o.role;c.playingTime=new PlayingTimeUsage{club=c.club};c.appearancesAtSigning=0;c.appearanceBonus=o.bonus;c.releaseClause=o.clause;c.terms=o.terms;c.estimated=false;o.status="signed";SavePlayer(p);
                if(previous==club)life.players.RemoveAll(x=>x.id==p.id);
                if(p.team==club){if(!life.players.Any(x=>x.id==p.id))life.players.Add(new PlayerLife{id=p.id,fitness=p.fitness,morale=p.morale});Mail("Secrétariat","Arrivée du joueur précontracté",p.name+" rejoint le groupe à l’expiration de son ancien contrat. Son salaire commence aujourd’hui.",p.id,"transfer");}
            }
        }
    }
}
