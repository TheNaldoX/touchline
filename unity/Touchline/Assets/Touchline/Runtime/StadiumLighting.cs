using System;
using System.Globalization;
using UnityEngine;
using Touchline.Core;

namespace Touchline
{
    // Éclairage du stade : après-midi (soleil derrière la tribune d'en face, dont le
    // toit ombre la partie éloignée du terrain, comme à la télévision) ou soirée
    // (projecteurs). Une seule lumière directionnelle avec ombres dans les deux cas.
    // Choix selon l'horaire du match, ou la préférence "match-lighting".
    public static class StadiumLighting
    {
        public const string PreferenceKey="match-lighting"; // 0 auto (selon le match), 1 jour, 2 soirée
        public const int Auto=0,Day=1,Night=2;

        // Soleil d'après-midi : venant de -Z (derrière le toit), dirigé vers la caméra.
        public const float SunElevation=36f;   // degrés au-dessus de l'horizon
        public const float SunYaw=-35f;        // degrés (rotation Y de la lumière)
        public const float SunIntensity=1.9f;  // même éclairement au sol qu'avant (1,5 à 48°)
        public static readonly Color SunColor=new Color(1,.94f,.84f);
        public static Quaternion SunRotation=>Quaternion.Euler(SunElevation,SunYaw,0);

        // Projecteurs : lumière principale haute, venant du côté caméra (visages éclairés).
        public const float FloodElevation=60f,FloodYaw=165f,FloodIntensity=1.3f; // degrés, degrés, intensité
        static readonly Color FloodColor=new Color(.93f,.96f,1);

        public static readonly Color DaySky=new Color(.56f,.67f,.79f),NightSky=new Color(.035f,.045f,.075f);
        public static readonly Color DayAmbient=new Color(.48f,.57f,.67f),NightAmbient=new Color(.17f,.19f,.25f);

        // Projecteurs allumés si le milieu du match tombe après le coucher du soleil.
        const float FloodlightLead=1f;         // h après le coup d'envoi (≈ mi-temps)
        const float SunsetMeanUtc=17.95f,SunsetSwingUtc=2f; // h UTC : coucher moyen et amplitude saisonnière (≈ 48° N)
        const int LongestDay=172;              // jour de l'année du solstice d'été

        public static float SunsetUtcHours(int dayOfYear)=>SunsetMeanUtc+SunsetSwingUtc*Mathf.Cos(2*Mathf.PI*(dayOfYear-LongestDay)/365f);

        // Préférence (0/1/2) puis horaire du match : heure de coup d'envoi connue
        // (calendrier importé, "2027-02-20T20:00Z") ou, à défaut, règle fixe : soirée
        // en semaine et pour les matchs à élimination directe, après-midi le week-end.
        // Sans match de calendrier : après-midi.
        public static bool Floodlit(int preference,Fixture fixture)
        {
            if(preference==Day)return false;if(preference==Night)return true;
            if(fixture==null)return false;
            if(!string.IsNullOrEmpty(fixture.date)&&fixture.date.Contains("T")&&DateTime.TryParse(fixture.date,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out var kickoff))
                return (float)kickoff.TimeOfDay.TotalHours+FloodlightLead>=SunsetUtcHours(kickoff.DayOfYear);
            if(fixture.knockout)return true;
            var date=Career.Epoch.AddDays(fixture.day);
            return date.DayOfWeek!=DayOfWeek.Saturday&&date.DayOfWeek!=DayOfWeek.Sunday;
        }

        // Point du sol (y = 0) à l'ombre d'un point p, au soleil d'après-midi.
        public static Vector3 SunShadowOnGround(Vector3 p){var d=SunRotation*Vector3.forward;return p+d*(p.y/-d.y);}
        static float RoofTop=>StadiumGeometry.RoofHeight+StadiumGeometry.RoofThickness*.5f; // m
        // Bord de l'ombre du toit d'en face sur la pelouse (z, m), parallèle à la ligne de touche.
        public static float RoofShadowEdge()=>SunShadowOnGround(new Vector3(0,RoofTop,-StadiumGeometry.RoofFront)).z;
        // Fin de l'ombre côté +x (m) : le soleil venant de biais, le coin du toit ne couvre pas tout le terrain.
        public static float RoofShadowEndX()=>SunShadowOnGround(new Vector3(StadiumGeometry.RoofWidth*.5f,RoofTop,-StadiumGeometry.RoofFront)).x;

        // Facteur (couleur à multiplier, espace sRGB) qui donne à une surface horizontale
        // la luminosité qu'elle aurait sans le soleil direct (lumière ambiante seule) :
        // l'ombre simulée de la pelouse a donc la même teinte que l'ombre réelle du toit
        // sur les joueurs. Calcul en linéaire (projet en espace linéaire).
        public static Color ShadeTint()
        {
            float sun=SunIntensity*Mathf.Sin(SunElevation*Mathf.Deg2Rad);
            float Channel(float ambient,float light){float a=Mathf.GammaToLinearSpace(ambient);return Mathf.LinearToGammaSpace(a/(a+Mathf.GammaToLinearSpace(light)*sun));}
            return new Color(Channel(DayAmbient.r,SunColor.r),Channel(DayAmbient.g,SunColor.g),Channel(DayAmbient.b,SunColor.b),1);
        }

        // Crée la lumière sous parent, règle ambiance et brouillard ; renvoie la couleur du ciel.
        public static Color Apply(Transform parent,bool night)
        {
            var sky=night?NightSky:DaySky;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=night?NightAmbient:DayAmbient;
            RenderSettings.fog=true;RenderSettings.fogColor=sky;RenderSettings.fogMode=FogMode.Linear;
            if(night)Directional(parent,"Floodlights",Quaternion.Euler(FloodElevation,FloodYaw,0),FloodIntensity,FloodColor);
            else Directional(parent,"Afternoon sun",SunRotation,SunIntensity,SunColor);
            return sky;
        }
        // Lampes des projecteurs : grises le jour ; la nuit, couleur bien au-delà de 1
        // pour qu'elles saturent en blanc avec la seule lumière ambiante (pas d'émission,
        // donc aucune variante de shader supplémentaire).
        public static Color LampColor(bool night)=>night?new Color(7f,7f,6.4f):new Color(.62f,.64f,.66f);
        // Panneaux à LED : couleur au-delà de 1 = lumineux même à l'ombre du toit ou de nuit.
        public static Color BoardGlow(bool night)=>night?new Color(1.25f,1.25f,1.25f):new Color(1.6f,1.6f,1.6f);
        static void Directional(Transform parent,string name,Quaternion rotation,float intensity,Color color)
        {
            var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(parent,false);light.type=LightType.Directional;
            light.intensity=intensity;light.color=color;light.shadows=LightShadows.Soft;light.transform.rotation=rotation;
        }
    }
}
