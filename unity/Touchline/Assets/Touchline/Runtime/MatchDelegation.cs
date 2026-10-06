using System;
using System.Collections;
using Touchline.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        bool delegatingMatch,applicationSuspended;
        void DelegateMatch(MatchSimulation simulation)
        {
            if(delegatingMatch)return;
            if(arena!=null)arena.Paused=true;
            var panel=Modal("L’adjoint dirige la rencontre");
            panel.name="match-delegation";
            foreach(var button in panel.Query<Button>().ToList())button.style.display=DisplayStyle.None;
            var progress=new ProgressBar{title="Préparation",lowValue=0,highValue=90};panel.Add(progress);
            Text(panel,"La rencontre reste intégralement simulée. Vous pouvez interrompre le calcul pour revenir au match.","muted");
            bool cancel=false;Button(panel,"Revenir au match",()=>cancel=true);
            delegatingMatch=true;StartCoroutine(DelegatedMatchFrames(simulation,progress,()=>cancel));
        }
        IEnumerator DelegatedMatchFrames(MatchSimulation simulation,ProgressBar progress,Func<bool> cancelled)
        {
            Exception failure=null;var budget=new System.Diagnostics.Stopwatch();
            // All state changes remain on Unity's thread. Limit work per frame;
            // never skip fixed steps or consume RNG from a background callback.
            while(!simulation.State.finished&&!cancelled()){
                if(applicationSuspended){yield return null;continue;}
                budget.Restart();
                try{
                    do {simulation.AdvanceDelegatedStep();}
                    while(!simulation.State.finished&&budget.Elapsed.TotalMilliseconds<5);
                    progress.value=simulation.State.Minute;
                    progress.title=simulation.State.Minute+"′ · "+simulation.State.score[0]+" – "+simulation.State.score[1];
                }catch(Exception e){failure=e;}
                if(failure!=null)break;
                yield return null;
            }
            delegatingMatch=false;CloseModal();Save();
            if(failure!=null){Debug.LogException(failure);Message("La simulation a été interrompue : "+failure.Message);yield break;}
            if(simulation.State.finished){Career.ProcessMedicalEvents(Database);Career.RecordMatch(Database);Navigate("Club");}
            else {page="Match";Build();if(arena!=null)arena.Paused=true;}
        }
    }
}
