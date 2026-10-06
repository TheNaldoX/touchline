using System;
using System.Globalization;

namespace Touchline.Core
{
    public static class PlayerChronology
    {
        public static bool TryAge(PlayerData player,DateTime at,out int age)
        {
            age=0;if(player==null)return false;
            if(!TryBirthDate(player,at,out var birth))return false;
            age=at.Year-birth.Year;
            // Game convention: a 29 February birthday occurs on 28 February
            // in a non-leap year, consistently with DateTime.AddYears.
            if(at.Date<birth.AddYears(age))age--;
            return age>=0;
        }
        public static bool TryBirthDate(PlayerData player,DateTime at,out DateTime birth)
        {
            birth=default;return player!=null&&(TryBirth(player.birthDate,at,out birth)||TryBirth(player.evidence?.birthDate,at,out birth));
        }
        static bool TryBirth(string raw,DateTime at,out DateTime birth)
            =>DateTime.TryParseExact(raw,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out birth)
                &&birth.Year>=1850&&birth.Date<=at.Date;
    }
    public partial class Career
    {
        public void SynchronizePlayerAges(Database db)
        {
            foreach(var player in db.players??Array.Empty<PlayerData>())
                if(PlayerChronology.TryAge(player,Date,out int age))player.age=age;
        }
    }
}
