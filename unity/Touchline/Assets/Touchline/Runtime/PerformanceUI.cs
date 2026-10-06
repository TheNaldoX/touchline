using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        string MatchPerformanceText()
        {
            var budget=GetComponent<RenderBudget>();var sample=budget.MatchFrames;
            if(sample.count==0)return "Aucune mesure de match pour le moment. Lancez la rencontre pour observer sa fluidité sur cet appareil.";
            return sample.FramesPerSecond.ToString("0.0",French)+" images/s en moyenne · "+sample.count+" images sur "+sample.seconds.ToString("0.0",French)+" s\n"+
                "95 % des images affichées en moins de "+sample.p95Ms.ToString("0.0",French)+" ms · Pic : "+sample.worstMs.ToString("0.0",French)+" ms\n"+
                "Dernière résolution 3D estimée : "+budget.MatchWidth+" × "+budget.MatchHeight+" · Vitesse du match : ×"+budget.MatchSpeed+
                (sample.count<120?"\nÉchantillon court : laissez jouer quelques secondes pour une mesure plus représentative.":"");
        }
        string MatchDiagnostic=>"Touchline "+Application.version+"\n"+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+"\n"+SystemInfo.deviceModel+" · "+SystemInfo.operatingSystem+"\n"+SystemInfo.graphicsDeviceName+"\n"+MatchPerformanceText()+"\nMesure des dernières images en match, hors pauses et menus. Aucune mesure de température ou de batterie.";
        void MatchPerformance()
        {
            arena.Paused=true;var panel=Modal("Fluidité du match");Text(panel,MatchPerformanceText());Text(panel,"Ces mesures concernent les dernières images jouées sur cet appareil. Elles restent disponibles dans Réglages après avoir quitté le terrain.","muted");
            var controls=Row(panel);Button(controls,"Copier le diagnostic",()=>{GUIUtility.systemCopyBuffer=MatchDiagnostic;Message("Diagnostic copié. Vous pouvez le coller pour partager les mesures de cet appareil.");});Button(controls,"Reprendre",()=>{CloseModal();arena.Paused=Career.match.finished||Career.match.halfTime;}).AddToClassList("primary");
        }
    }
}
