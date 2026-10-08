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
        // Ambiance en trois tons (ciel, horizon, sol), sans coût de rendu (harmoniques sphériques) :
        // le dessus des surfaces garde DayAmbient / NightAmbient (pelouse, ombre du toit inchangées),
        // les côtés des joueurs reçoivent la lumière renvoyée par les tribunes (le soir : les
        // projecteurs tout autour du stade), le dessous un rebond vert de la pelouse.
        public static readonly Color DayEquator=new Color(.45f,.49f,.53f),DayGround=new Color(.2f,.29f,.17f);
        public static readonly Color NightEquator=new Color(.3f,.31f,.34f),NightGround=new Color(.1f,.16f,.1f);

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

        // Surfaces à l'ombre simulée du toit (pelouse, abords, lignes) : matière sans
        // éclairage (Unlit) dont la couleur = couleur de base × ShadeTint(). Une matière
        // Lit assombrie gardait le reflet rasant du soleil et du ciel (pelouse grise,
        // ≈ 60/66/55 au lieu d'un vert sombre) ; Unlit est aussi moins coûteux.
        // Le matériau de Resources garantit que le shader Unlit est inclus dans l'APK.
        public const string ShadeMaterialPath="Rendering/ShadeUnlit";
        // Lumière reçue à l'ombre (couleur à multiplier, sRGB) : l'ambiance du ciel, plus la
        // lumière renvoyée par la pelouse au soleil et les tribunes. Calcul en linéaire.
        // Réglé sur les films : avec l'ambiance seule (× 0,6) l'ombre était presque noire
        // (vert 33 contre 115 au soleil) ; × 1,3 donne ≈ 50, lisible comme à la télévision.
        const float ShadeFill=1.3f; // lumière à l'ombre / ambiance de la scène (linéaire)
        public static Color ShadeTint()
        {
            float Channel(float ambient)=>Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(ambient)*ShadeFill);
            return new Color(Channel(DayAmbient.r),Channel(DayAmbient.g),Channel(DayAmbient.b),1);
        }
        // Matière d'une surface à l'ombre : Unlit (repli Lit si le matériau manque).
        public static Material ShadedMaterial(Color baseColor,Texture texture=null)=>UnlitMaterial(baseColor*ShadeTint(),texture);
        // Matière sans éclairage (couleur affichée telle quelle, brouillard compris).
        public static Material UnlitMaterial(Color color,Texture texture=null)
        {
            var template=Resources.Load<Material>(ShadeMaterialPath);
            var m=template!=null?new Material(template):PlayerView.Material(Color.white);
            color.a=1;m.color=color;if(texture!=null)m.mainTexture=texture;
            return m;
        }
        // Tribune d'en face (gradins, second anneau, toit, public du second anneau) l'après-midi :
        // entièrement sous l'ombre du toit. Multiplicateur de couleur (sRGB, matières Lit) :
        // les contremarches vues de la caméra sont déjà à contre-jour, seul le dessus des
        // gradins perd le soleil, d'où un assombrissement modéré.
        static readonly Color FarStandDayShade=new Color(.6f,.62f,.68f,1);
        public static Color FarStandShade(bool night)=>night?Color.white:FarStandDayShade;

        // Crée la lumière sous parent, règle ambiance et brouillard ; renvoie la couleur du ciel.
        public static Color Apply(Transform parent,bool night)
        {
            var sky=night?NightSky:DaySky;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=night?NightAmbient:DayAmbient;
            RenderSettings.ambientEquatorColor=night?NightEquator:DayEquator;RenderSettings.ambientGroundColor=night?NightGround:DayGround;
            RenderSettings.fog=true;RenderSettings.fogColor=sky;RenderSettings.fogMode=FogMode.Linear;
            if(night)Directional(parent,FloodlightName,FloodRotation(FloodYaw),FloodIntensity,FloodColor);
            else Directional(parent,"Afternoon sun",SunRotation,SunIntensity,SunColor);
            return sky;
        }
        public const string FloodlightName="Floodlights";
        public static Quaternion FloodRotation(float yaw)=>Quaternion.Euler(FloodElevation,yaw,0);
        // Lacet (°) de la lumière des projecteurs pour une caméra qui regarde vers lookX (signe en x) :
        // le stade est éclairé de partout, on garde la rampe placée derrière la caméra
        // (visages éclairés). Sert au ralenti de but le soir : sans cela, le plan bas côté
        // +x était à contre-jour (joueurs presque noirs).
        public static float FloodYawFacing(float lookX)=>lookX<0?-FloodYaw:FloodYaw;
        // Lampes des projecteurs : éteintes le jour (gris, éclairées par le soleil) ;
        // allumées le soir (blanc sans éclairage, pas d'émission ni de variante de shader).
        static readonly Color LampOff=new Color(.62f,.64f,.66f),LampOn=new Color(1,1,.94f)*LampGlow;
        const float LampGlow=3; // lampes allumées au-delà du blanc (HDR) : seul le bloom du soir les fait rayonner (BroadcastGrade)
        public static Material LampMaterial(bool night)=>night?UnlitMaterial(LampOn):PlayerView.Material(LampOff);
        // Panneaux à LED : lumière propre (Unlit), même éclat à l'ombre du toit, au soleil
        // ou de nuit ; un peu moins vifs en plein jour (contraste réduit par le soleil).
        public const float BoardLedDay=.9f,BoardLedNight=1.2f; // multiplicateur de l'atlas ; > 1 le soir : halo léger des LED par le bloom (HDR)
        public static Material BoardMaterial(bool night,Texture atlas){float led=night?BoardLedNight:BoardLedDay;return UnlitMaterial(new Color(led,led,led),atlas);}
        // Tribunes et public : les projecteurs visent la pelouse, les gradins restent
        // dans la pénombre (multiplicateur de couleur, alpha conservé).
        public static Color StandLight(bool night)=>night?new Color(.6f,.6f,.65f,1):Color.white; // le soir : gradins éclairés par le débord des projecteurs
        static void Directional(Transform parent,string name,Quaternion rotation,float intensity,Color color)
        {
            var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(parent,false);light.type=LightType.Directional;
            light.intensity=intensity;light.color=color;light.shadows=LightShadows.Soft;light.transform.rotation=rotation;
        }
    }
}
