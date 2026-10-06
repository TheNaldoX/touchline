using System;
using System.Linq;
using System.Reflection;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    // Synthetic agreement fixtures in the existing isolated LoanNegotiationSmoke.
    public static class RetiredMessageSmokeChecks
    {
        static string player;
        static int incomingMessage,outgoingMessage;
        static TransferOffer incoming;
        static OutgoingLoanOffer outgoing;
        static void Require(bool b,string text){if(!b)throw new InvalidOperationException(text);}
        static VisualElement Root(TouchlineApp app)=>app.GetComponent<UIDocument>().rootVisualElement;
        static void Call(TouchlineApp app,string method,params object[] args)=>typeof(TouchlineApp).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app,args);
        public static string DecisionSummary(TouchlineApp app)
        {
            var parent=new VisualElement();Call(app,"ManagerDecisionCard",parent,true);
            return string.Join("|",parent.Query<Label>().ToList().Select(l=>l.text));
        }
        public static void OpenOldTransferThread(TouchlineApp app)
        {
            var c=app.Career;var db=app.Database;player=c.life.retiredPlayers.Last(p=>db.Find(p.id)?.team=="retired").id;
            string decisions=DecisionSummary(app);
            incoming=new TransferOffer{player=player,seller=c.club,destination=c.club,renewal=true,status="accepted",due=c.life.day,wage=db.Find(player).wage,years=2};c.world.offers.Add(incoming);
            outgoing=new OutgoingLoanOffer{player=player,owner=c.club,borrower=db.clubs.First(t=>t.id!=c.club&&t.playable).id,status="accepted",due=c.life.day,terms=new MarketTerms{loanEndDay=c.life.day+90}};c.outgoingLoans.Add(outgoing);
            incomingMessage=c.Mail("Agent","Accord de principe","Ancien accord conserve pour le controle du joueur retraite.",player,"transfer").id;
            outgoingMessage=c.Mail("Agent","Accord de pr\u00eat","Ancien pret conserve pour le controle du joueur retraite.",player,"transfer").id;
            Require(c.life.messages.Where(m=>m.player==player).All(m=>!c.MessageNeedsDecision(m)),"Retired old agreements still appear in decision counters.");
            Require(DecisionSummary(app)==decisions,"Retired old agreements still create dashboard decisions.");
            int unread=c.life.messages.Count(m=>!m.read);Call(app,"OpenMessageThread",incomingMessage);
            Require(c.life.messages.Count(m=>!m.read)==unread-1&&!c.life.messages.Single(m=>m.id==outgoingMessage).read,"Reading one old agreement read another one or failed to update unread count.");
        }
        public static void AssertOldThread(TouchlineApp app)
        {
            var panel=Root(app).Q("inbox-conversation");Require(panel!=null,"Historical player conversation is missing.");
            var buttons=panel.Query<Button>().ToList();Require(buttons.Any(b=>b.text=="Fiche du joueur"&&b.enabledInHierarchy),"Retirement removed access to the historical player profile.");
            Require(!buttons.Any(b=>b.text=="N\u00e9gociation / contrat"||b.text=="Discuter avec le joueur")&&panel.Q("inbox-sign-transfer")==null&&panel.Q("inbox-sign-outgoing")==null,"Old retired mail still offers playing negotiations or signature.");
            Require(panel.Q("inbox-message-"+incomingMessage)!=null&&panel.Q("inbox-message-"+outgoingMessage)!=null,"Retirement erased the historic transfer conversation.");
        }
        public static void AssertDirectReviewsAndDialogAreReadOnly(TouchlineApp app)
        {
            var c=app.Career;long cash=c.life.cash;int saves=app.SaveRequestCount,contracts=c.world.contracts.Count,messages=c.life.messages.Count;
            Call(app,"TransferAgreementReview",incoming);var panel=Root(app).Q("transfer-agreement-review");
            Require(panel!=null&&!panel.Q<Button>("agreement-sign-transfer").enabledInHierarchy&&panel.Query<Label>().ToList().Any(l=>l.text.StartsWith("Retraite effective")),"Direct transfer review still permits signature or hides retirement.");
            Call(app,"OutgoingLoanAgreementReview",outgoing);panel=Root(app).Q("outgoing-loan-agreement-review");
            Require(panel!=null&&!panel.Q<Button>("agreement-sign-outgoing").enabledInHierarchy&&panel.Query<Label>().ToList().Any(l=>l.text.StartsWith("Retraite effective")),"Direct outgoing-loan review still permits signature or hides retirement.");
            Call(app,"TransferDialog",player);Require(Root(app).Q("transfer-negotiation-form")==null&&Root(app).Q("transfer-agreement-review")==null,"Retired direct negotiation was redirected into an actionable agreement.");
            Require(c.life.cash==cash&&c.world.contracts.Count==contracts&&c.life.messages.Count==messages&&app.SaveRequestCount==saves&&incoming.status=="accepted"&&outgoing.status=="accepted","Read-only retired navigation changed money, contract, agreement or history.");
            Call(app,"OpenMessageThread",incomingMessage);
        }
    }
}
