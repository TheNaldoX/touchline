using System;
using System.Linq;

namespace Touchline.Core
{
    public partial class Career
    {
        public long RecurringAnnualIncome(Database db)
        {
            // The database revenue already includes commercial and matchday income. Carve these
            // streams out before paying actual gates/deals, instead of charging wages against 60%.
            if(life.financeBaselineRevenue<=0){
                int homes=world?.fixtures.Count(f=>f.home==club&&!f.knockout&&f.league!="friendly")??19;
                homes=Math.Max(15,Math.Min(23,homes));
                int capacity=world?.capacity??Math.Max(4000,db.clubs.First(c=>c.id==club).stadiumCapacity);
                float ticket=Mathx.Clamp((float)Math.Sqrt(life.revenue)/400,8,90);
                life.financeBaselineRevenue=Math.Max(1,life.revenue);
                life.financeBaselineGate=Math.Min(life.revenue*30/100,(long)(capacity*.93f*ticket*.85f*homes));
            }
            long gate=(long)(life.financeBaselineGate*(life.revenue/(double)life.financeBaselineRevenue));
            long commercial=0;
            foreach(var slot in new[]{"maillot","équipementier","naming"}){
                // An initially implicit partner is replaced by the negotiated deal. Its expired
                // slot stays vacant until renewed; the old implicit income must not return.
                if(world?.sponsors.Any(s=>s.slot==slot&&(s.status=="signed"||s.status=="expired"))==true)
                    commercial+=(long)(life.revenue*(slot=="maillot"?.05:slot=="naming"?.012:.025));
            }
            return Math.Max(0,life.revenue-gate-commercial);
        }
    }
}
