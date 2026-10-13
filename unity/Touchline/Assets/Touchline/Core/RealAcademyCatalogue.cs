using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Touchline.Core
{
    // Supplement with individually sourced identities. Never generates people
    // or replaces an existing player, including one transferred in a saved game.
    public static class RealAcademyCatalogue
    {
        public static int Append(Database db,Database supplement)
        {
            if(db?.players==null||db.clubs==null||supplement?.players==null)return 0;
            var ids=new HashSet<string>(db.players.Select(p=>p.id));
            var names=new HashSet<string>(db.players.Select(p=>NameKey(p.name)));
            var clubs=new HashSet<string>(db.clubs.Select(c=>c.id));var added=new List<PlayerData>();
            var start=new DateTime(Career.ImportedRosterYear,6,15);
            foreach(var p in supplement.players){
                if(p==null||string.IsNullOrWhiteSpace(p.id)||string.IsNullOrWhiteSpace(p.name)||p.team==null||!p.team.StartsWith("academy-",StringComparison.Ordinal))continue;
                if(!clubs.Contains(p.team.Substring(8))||ids.Contains(p.id)||names.Contains(NameKey(p.name)))continue;
                if(!PublicSource(p.source)||!PublicSource(p.rosterSource)||string.IsNullOrWhiteSpace(p.assessment)||string.IsNullOrEmpty(p.position))continue;
                if(!PlayerChronology.TryAge(p,start,out int age)||age<14||age>20)continue;
                var copy=p.Copy();copy.age=age;added.Add(copy);ids.Add(copy.id);names.Add(NameKey(copy.name));
            }
            if(added.Count>0)db.players=db.players.Concat(added).ToArray();
            return added.Count;
        }
        static bool PublicSource(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&(uri.Scheme=="https"||uri.Scheme=="http");
        static string NameKey(string name)
        {
            var result=new StringBuilder();foreach(char c in (name??"").Normalize(NormalizationForm.FormD))
                if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark&&char.IsLetterOrDigit(c))result.Append(char.ToUpperInvariant(c));
            return result.ToString();
        }
    }
}
