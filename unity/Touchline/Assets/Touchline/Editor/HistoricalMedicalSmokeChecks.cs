using System;
using System.Linq;
using System.Reflection;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    public static class HistoricalMedicalSmokeChecks
    {
        static void Require(bool b,string text){if(!b)throw new InvalidOperationException(text);}
        static VisualElement Root(TouchlineApp app)=>app.GetComponent<UIDocument>().rootVisualElement;
        static object Call(TouchlineApp app,string method,params object[] args)=>typeof(TouchlineApp).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app,args);
        public static void PrepareHistoricalCaseAndOpen(TouchlineApp app)
        {
            var c=app.Career;var p=c.life.retiredPlayers.Last(x=>app.Database.Find(x.id)?.team=="retired");
            string decisions=RetiredMessageSmokeChecks.DecisionSummary(app);
            var episode=new MedicalCase{id=++c.life.medicalCount,player=p.id,opened=c.life.day,remaining=8,total=8,diagnosis="Synthetic historical episode",consent=true,injection=true};c.life.medical.Add(episode);
            var message=c.Mail("Medical staff","Known prior injury","Synthetic historical record remains available.",p.id,"medical","medical/"+episode.id);
            Require(!c.MessageNeedsDecision(message),"Retired medical history remains an urgent decision.");
            Require(RetiredMessageSmokeChecks.DecisionSummary(app)==decisions,"Historical care still creates a dashboard decision.");
            var before=(string)Call(app,"InitializationStamp");int saves=app.SaveRequestCount;c.ApplyLife(app.Database);var after=(string)Call(app,"InitializationStamp");
            Require(before!=after&&episode.responsibilityEndedReason=="retirement"&&episode.closed==-1&&episode.remaining==8,"Startup probe did not detect closure, or closure invented recovery.");
            c.ApplyLife(app.Database);Require(after==(string)Call(app,"InitializationStamp")&&app.SaveRequestCount==saves,"Closure repeats on subsequent application or saves directly during normalization.");
            Call(app,"OpenMessageThread",message.id);var history=Root(app).Q<Button>("inbox-medical-history");Require(history!=null&&history.enabledInHierarchy,"Historical medical mail lost its read-only dossier.");
            using(var e=NavigationSubmitEvent.GetPooled()){e.target=history;history.SendEvent(e);}
        }
        public static void AssertReadOnlyHistory(TouchlineApp app)
        {
            var panel=Root(app).Q("medical-player-detail");Require(panel!=null,"Historical medical dossier is missing.");
            var labels=panel.Query<Label>().ToList();Require(labels.Any(l=>l.text=="Dossier historique hors effectif")&&labels.Any(l=>l.text.StartsWith("Fin de responsabilit"))&&labels.Any(l=>l.text.StartsWith("Derni")),"Historical case lacks responsibility closure or preserves no diagnosis estimate.");
            Require(!panel.Query<Button>().ToList().Any(b=>b.text.StartsWith("Soins conservateurs")||b.text.StartsWith("Retenir l")||b.text.StartsWith("Demander la prise")),"Historical medical case still offers clinical treatment.");
            Require(!labels.Any(l=>l.text.StartsWith("Reprise le ")||l.text.StartsWith("Le joueur est disponible")),"Closure of club responsibility was presented as medical recovery.");
            int saves=app.SaveRequestCount;var player=app.Career.life.medical.Last(m=>m.responsibilityEndedReason=="retirement").player;
            Call(app,"MedicalDetail",player);Require(app.SaveRequestCount==saves,"Reading a historical medical dossier generated a save.");
        }
    }
}
