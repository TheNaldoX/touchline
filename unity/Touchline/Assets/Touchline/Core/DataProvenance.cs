using System;
using System.Globalization;

namespace Touchline.Core
{
    [Serializable] public sealed class DataProvenance
    {
        public int schema;
        public string inputSha256,importedAt,checkedAt;
        public PlayerProvenance[] players=Array.Empty<PlayerProvenance>();
        public ClubProvenance[] clubs=Array.Empty<ClubProvenance>();
        public FinancialReference[] financialReferences=Array.Empty<FinancialReference>();
    }
    [Serializable] public sealed class PlayerProvenance
    {
        public string id,baselineClub,identityUrl,rosterUrl,rosterAsOf,ratingProvider,ratingEdition,ratingUrl,ratingDate,valueUrl,valueDate,salaryStatus,contractStatus,salaryUrl,salaryDate,salaryCurrency,salaryProvider;
        public long referenceValue;
        public double annualSalaryBase,annualGuaranteedCompensation;
        public PlayerPerformanceEvidence[] performanceEvidence=Array.Empty<PlayerPerformanceEvidence>();
    }
    [Serializable] public sealed class PlayerPerformanceEvidence
    {
        public string metric,label,unit,season,publisher,url,observedAt,scope;
        public double value;
    }
    [Serializable] public sealed class ClubProvenance { public string id,rosterUrl,rosterAsOf,financeSource; }
    [Serializable] public sealed class FinancialReference
    {
        public string club,metric,label,currency,period,asOf,publishedAt,publisher,url,scope,checkedAt;
        public long amount;
    }
    // Read-only evidence labels; external metadata never changes a career or reveals ability.
    public static class ProvenanceLabels
    {
        public static string Date(string value)=>DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date.ToString("dd/MM/yyyy",CultureInfo.InvariantCulture):"date non renseignée";
        public static string Roster(PlayerProvenance record,string currentClub)=>record==null?"Joueur généré ou provenance non disponible dans cette version.":record.baselineClub!=currentClub?"Affiliation modifiée dans votre carrière. La source décrit l’effectif de départ.":"Affiliation issue de la base importée ; elle n’est pas vérifiée en temps réel.";
        public static string Salary(bool? estimated)=>estimated==false?"Salaire négocié dans votre carrière. Ce n’est pas une rémunération réelle vérifiée.":"Estimation Touchline : formule de jeu, puis répartition selon les revenus modélisés du club. Aucune rémunération privée vérifiée.";
        public static string Salary(bool? estimated,PlayerProvenance source)
        {
            if(estimated==false)return Salary(false);
            if(source?.salaryStatus=="reported-base-currency-converted")return "Référence initiale : base brute publiée par la MLSPA, dans sa monnaie d’origine. Le montant en euros est normalisé pour la carrière et peut évoluer ; la compensation garantie reste une référence séparée.";
            if(source?.salaryStatus=="public-estimate-composite-contract-cost")return "Coût contractuel équivalent estimé pour la carrière, comprenant des composantes de salaire et de droits d’image. Ce total n’est pas un salaire de base réel vérifié.";
            if(source?.salaryStatus=="public-estimate-fixed-cost")return "Coût contractuel estimé à partir d’une source publique, sans confirmation officielle des comptes du club. Le montant de la carrière reste simulé.";
            return Salary(estimated);
        }
        public static string Contract(bool? estimated)=>estimated==false?"Conditions négociées dans votre carrière.":"Échéance et clauses définies par la simulation ; contrat réel non vérifié.";
        public static string Assessment(PlayerProvenance record,int knowledge)
        {
            string origin=string.IsNullOrEmpty(record?.ratingProvider)?"Évaluation Touchline":record.ratingProvider+" "+record.ratingEdition;
            return origin.Trim()+" · base d’une évaluation de jeu, pas une mesure objective des performances réelles. "+(knowledge>=90?"Attributs accessibles dans votre partie.":knowledge>=40?"Observation partielle : fourchettes uniquement.":"Attributs masqués : observation nécessaire.");
        }
    }
}
