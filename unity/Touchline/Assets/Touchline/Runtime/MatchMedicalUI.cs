using System;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        void MatchMedicalAlert(MedicalCase[] cases)
        {
            var panel=Modal("Message du staff médical");panel.name="match-medical-alert";var list=Scroll(panel);
            Text(list,"La rencontre est en pause. Le staff conseille de préparer le remplacement pour éviter de solliciter davantage le joueur.","muted");
            foreach(var item in cases){var card=Card(list);Text(card,Database.Find(item.player).name,"section-title");Text(card,item.diagnosis+" · "+item.remaining+" jours estimés, sous réserve de réévaluation.");
                Button(card,"Préparer un remplacement",()=>{selectedSlot=Array.FindIndex(Career.match.actors,p=>p.side==0&&p.id==item.player);if(selectedSlot<0)selectedSlot=9;tacticalTab="Composition";Navigate("Tactique");}).AddToClassList("primary");}
            Text(list,"Le compte rendu est aussi dans Messages. Les décisions de soins se prennent au centre médical après la rencontre.","footnote");
            Button(panel,"Rester sur le terrain",CloseModal);
        }
    }
}
