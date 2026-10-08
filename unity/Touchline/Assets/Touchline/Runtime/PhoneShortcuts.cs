using UnityEngine;

namespace Touchline
{
    // Raccourcis d'usage au téléphone : actions fréquentes en une touche.
    public static class PhoneShortcuts
    {
        // Mentalité rapide pendant le match (valeur Tactic.mentality, 0 = très prudente,
        // 1 = très offensive ; 0,5 = valeur par défaut du moteur).
        public static readonly string[] MentalityLabels={"Prudente","Équilibrée","Offensive"};
        public static readonly float[] MentalityLevels={.25f,.5f,.75f};
        public static int NearestMentality(float value)
        {
            int best=0;for(int i=1;i<MentalityLevels.Length;i++)if(Mathf.Abs(MentalityLevels[i]-value)<Mathf.Abs(MentalityLevels[best]-value))best=i;
            return best;
        }

        // « Avancer jusqu'au match » : nombre maximal de jours avancés d'une seule touche
        // (garde-fou ; une intersaison dure environ 70 jours).
        public const int MaxAdvanceDays=90;
        public static bool KeepAdvancing(int day,int nextFixture,int advanced)=>day<nextFixture&&advanced<MaxAdvanceDays;
    }
}
