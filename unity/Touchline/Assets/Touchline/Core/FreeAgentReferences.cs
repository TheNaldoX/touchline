using System;

namespace Touchline.Core
{
    [Serializable] public class FreeAgentReference
    {
        public string id,playerId,displayName,surname,givenNames,birthDate,nationality;
        public string[] positions;
        public string formerClubId,formerClubName,databaseClubId,databaseClubName;
        public string announcedAt,contractEndsOn,effectiveDate,effectiveDateBasis,status,identityStatus;
        public string sourceId,sourceUrl,sourcePage,asOfDate,observedAt,consultedDate;
        public bool autoReleaseEligible,freeAtCareerStart;
        public long marketValueEuro;
        public string valuationDate,valuationSourceUrl,valuationObservedAt;
    }
}
