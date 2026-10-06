using System;
using System.Linq;
using System.Reflection;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    // Invoke only from a visual-validation smoke that cannot write real saves.
    // The caller supplies viewport sizing, screenshots and session lifecycle.
    public static class RetiredProfileSmokeChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static VisualElement Root(TouchlineApp app)=>app.GetComponent<UIDocument>().rootVisualElement;
        static void OpenContract(TouchlineApp app){var b=Root(app).Q("player-profile").Query<Button>().ToList().Single(x=>x.text=="Contrat");using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        public static string RetireKnownFixtureAndOpen(TouchlineApp app)
        {
            var c=app.Career;var db=app.Database;c.EnsureWorld(db);var p=db.Squad(c.club).First(x=>x.age>=18);c.Person(p.id).appearances=41;long wage=p.wage;var contract=c.Contract(db,p.id);contract.retirement=c.life.day+90;app.PlayerProfile(p.id);OpenContract(app);
            var labels=Root(app).Q("profile-body").Query<Label>().ToList();var announced=labels.SingleOrDefault(x=>x.text=="Retraite annoncée");string date=Career.Epoch.AddDays(contract.retirement).ToString("dd MMMM yyyy",System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));Require(announced!=null&&announced.parent.Query<Label>().ToList().Any(x=>x.text==date)&&labels.Any(x=>x.text.Contains("précontrat prévue après sa retraite sera annulée")),"Announced retirement date or precontract consequence is missing.");contract.retirement=c.life.day;
            typeof(Career).GetMethod("RetireEmployment",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{db,p,contract,true});
            Require(p.team=="retired"&&p.wage==wage&&c.life.retiredPlayers.Single(x=>x.id==p.id).appearances==41,"Retired smoke fixture erased recorded salary or appearances.");app.PlayerProfile(p.id);return p.id;
        }
        public static void AssertOverviewAndOpenContract(TouchlineApp app)
        {
            var panel=Root(app).Q("player-profile");var labels=panel.Query<Label>().ToList();Require(labels.Any(x=>x.text=="DERNIER SALAIRE CONTRACTUEL MENSUEL")&&!labels.Any(x=>x.text=="SALAIRE EN JEU / MOIS"),"Retired overview presents historical wage as currently due.");
            Require(labels.Any(x=>x.text=="Retraité")&&labels.Any(x=>x.text=="Carrière de joueur terminée")&&!labels.Any(x=>x.text=="Niveau à observer"||x.text=="Potentiel encore incertain"||x.text=="retired"),"Retired hero invites active scouting or development.");
            Require(!panel.Query<Button>().ToList().Any(x=>x.text=="Négocier"||x.text=="Observer"||x.text=="Contrat / prolonger"),"Retired profile still offers an active playing contract or scouting task.");OpenContract(app);
        }
        public static void AssertContract(TouchlineApp app,bool known=true)
        {
            var body=Root(app).Q("profile-body");var labels=body.Query<Label>().ToList();Require(labels.Any(x=>x.text=="Dernier salaire contractuel mensuel")&&labels.Any(x=>x.text=="Retraité · aucun salaire actuellement dû")&&labels.Any(x=>x.text.StartsWith("Source : retraite effective")),"Retired contract lacks explicit historical salary/status/source.");
            Require(!labels.Any(x=>x.text=="Fin du prêt"||x.text=="Option d’achat"||x.text=="Obligation d’achat"||x.text=="Fin du contrat"||x.text=="Salaire mensuel"),"Retired contract still displays executable playing clauses.");
            var fact=labels.FirstOrDefault(x=>x.text=="Apparitions conservées dans un club dirigé");if(known)Require(fact!=null&&fact.parent.Query<Label>().ToList().Any(x=>x.text=="41"),"Known archived appearances are missing from retired contract.");else Require(fact==null&&labels.Any(x=>x.text.Contains("n’ont pas été enregistrées")),"Unknown retired appearances were invented.");
        }
    }
}
