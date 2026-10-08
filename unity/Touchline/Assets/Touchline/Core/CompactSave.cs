using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Touchline.Core
{
    // Fingerprint of the shipped players, captured once from the untouched
    // database when a career is created or loaded. It lets a save keep only
    // what the career changed, without holding a second copy of the 59 MB
    // database in memory (strings are kept as 64-bit fingerprints).
    public sealed class SaveBaseline
    {
        internal readonly Dictionary<string,long[]> leaves=new Dictionary<string,long[]>(StringComparer.Ordinal);
        internal readonly Dictionary<string,float[]> attributes=new Dictionary<string,float[]>(StringComparer.Ordinal);
        internal readonly Dictionary<string,string[]> attributeKeys=new Dictionary<string,string[]>(StringComparer.Ordinal);
        // The shipped text values themselves: the live player usually still
        // holds the very same string object, which compares without hashing.
        internal readonly Dictionary<string,object[]> texts=new Dictionary<string,object[]>(StringComparer.Ordinal);
        public int Count=>leaves.Count;
        public static SaveBaseline From(Database pristine)
        {
            var b=new SaveBaseline();var shared=new Dictionary<string,string[]>(StringComparer.Ordinal);var shape=CompactSave.PlayerShape;
            foreach(var p in pristine?.players??Array.Empty<PlayerData>()){
                if(p?.id==null||b.leaves.ContainsKey(p.id))continue;
                b.leaves[p.id]=CompactSave.LeafBits(p,shape);b.texts[p.id]=CompactSave.LeafTexts(p,shape);
                if(p.attributes!=null&&p.attributes.All(a=>a!=null)){
                    var keys=p.attributes.Select(a=>a.key??"").ToArray();string signature=string.Join("\n",keys);
                    if(!shared.TryGetValue(signature,out var interned))shared[signature]=interned=keys;
                    b.attributeKeys[p.id]=interned;b.attributes[p.id]=p.attributes.Select(a=>a.value).ToArray();
                }
            }
            return b;
        }
    }

    // Compact career save. Large lists are written as one short line per
    // object holding only what differs from a reference: the shipped player
    // for rosterChanges, a new Employment / Fixture / PlayerData otherwise.
    // Reloading rebuilds exactly what JsonUtility returned for the former,
    // complete format (null text -> "", missing objects -> default objects),
    // so the game behaves identically. tools/SeasonSim --savecheck proves it.
    public static class CompactSave
    {
        public const int Format=1;
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;

        // ---- Unity-compatible field model ---------------------------------
        static readonly Dictionary<Type,FieldInfo[]> fieldCache=new Dictionary<Type,FieldInfo[]>();
        static FieldInfo[] Fields(Type t)
        {
            lock(fieldCache){
                if(fieldCache.TryGetValue(t,out var f))return f;
                var list=new List<FieldInfo>();
                for(var c=t;c!=null&&c!=typeof(object);c=c.BaseType)
                    list.InsertRange(0,c.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
                        .Where(x=>!x.IsInitOnly&&!x.IsNotSerialized&&(x.IsPublic||x.GetCustomAttributes(false).Any(a=>a.GetType().Name=="SerializeField"))));
                return fieldCache[t]=list.ToArray();
            }
        }
        static bool Leaf(Type t)=>t.IsPrimitive||t.IsEnum||t==typeof(string)||t==typeof(string[])||t==typeof(List<string>);
        static bool Nested(Type t)=>t.IsClass&&t!=typeof(string)&&!typeof(IEnumerable).IsAssignableFrom(t)&&t.IsSerializable;

        // A leaf is a chain of fields from the root type to a primitive, a
        // text or a list of texts. Other members (lists of objects) are
        // "complex": the object is kept whole when one of them is not empty.
        internal sealed class Path { public string name; public FieldInfo[] chain; public Type type; public int kind; }
        // Tree of the same fields, walked once per object: a missing nested
        // object is skipped as a whole when the reference is its default.
        internal sealed class Node { public FieldInfo field; public int leaf=-1,first,last; public Node[] children; }
        internal sealed class Shape { public Type root; public Path[] leaves; public FieldInfo[][] complex; public Dictionary<string,int> index; public Node[] tree; }
        const int KString=0,KStrings=1,KFloat=2,KDouble=3,KBool=4,KInt=5,KLong=6,KOther=7;
        static int KindOf(Type t)=>t==typeof(string)?KString:t==typeof(string[])||t==typeof(List<string>)?KStrings:t==typeof(float)?KFloat:t==typeof(double)?KDouble:t==typeof(bool)?KBool:t==typeof(int)?KInt:t==typeof(long)?KLong:KOther;
        static readonly Dictionary<Type,Shape> shapes=new Dictionary<Type,Shape>();
        static Shape ShapeOf(Type root,params string[] skip)
        {
            lock(shapes){
                if(shapes.TryGetValue(root,out var s))return s;
                var leaves=new List<Path>();var complex=new List<FieldInfo[]>();
                Node[] Walk(Type t,List<FieldInfo> prefix,int depth){
                    var nodes=new List<Node>();
                    foreach(var f in Fields(t)){
                        if(depth==0&&skip.Contains(f.Name))continue;
                        var chain=new List<FieldInfo>(prefix){f};
                        if(Leaf(f.FieldType)){nodes.Add(new Node{field=f,leaf=leaves.Count});leaves.Add(new Path{name=string.Join(".",chain.Select(x=>x.Name)),chain=chain.ToArray(),type=f.FieldType,kind=KindOf(f.FieldType)});}
                        else if(Nested(f.FieldType)&&depth<6){int first=leaves.Count;var children=Walk(f.FieldType,chain,depth+1);nodes.Add(new Node{field=f,children=children,first=first,last=leaves.Count});}
                        else complex.Add(chain.ToArray());
                    }
                    return nodes.ToArray();
                }
                var tree=Walk(root,new List<FieldInfo>(),0);
                s=new Shape{root=root,leaves=leaves.ToArray(),complex=complex.ToArray(),index=new Dictionary<string,int>(),tree=tree};
                for(int i=0;i<s.leaves.Length;i++)s.index[s.leaves[i].name]=i;
                return shapes[root]=s;
            }
        }
        internal static Shape PlayerShape=>ShapeOf(typeof(PlayerData),"attributes");
        static Shape ContractShape=>ShapeOf(typeof(Employment));
        static Shape FixtureShape=>ShapeOf(typeof(Fixture));

        static readonly Dictionary<Type,object> defaults=new Dictionary<Type,object>();
        static object DefaultOf(Type t){lock(defaults){if(!defaults.TryGetValue(t,out var d))defaults[t]=d=New(t);return d;}}
        static object New(Type t){try{return Activator.CreateInstance(t,true);}catch(MissingMethodException){return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);}}
        // Value along a path as JsonUtility would return it after a round
        // trip: a missing nested object reads as its default, null text as "".
        static object Get(object root,Path p)
        {
            object o=root;
            foreach(var f in p.chain){if(o==null)o=DefaultOf(f.DeclaringType);o=f.GetValue(o);}
            if(p.type==typeof(string))return o??"";
            if(p.type==typeof(string[]))return ((string[])o)??Array.Empty<string>();
            if(p.type==typeof(List<string>))return ((List<string>)o)?.ToArray()??Array.Empty<string>();
            return o;
        }
        static void Set(object root,Path p,object value)
        {
            object o=root;
            for(int i=0;i<p.chain.Length-1;i++){var f=p.chain[i];var next=f.GetValue(o);if(next==null){next=Normalize(New(f.FieldType));f.SetValue(o,next);}o=next;}
            var last=p.chain[p.chain.Length-1];
            last.SetValue(o,last.FieldType==typeof(List<string>)&&value is string[] arr?arr.ToList():value);
        }
        static string Text(object v,Type t)
        {
            if(t==typeof(string))return (string)v;
            if(t==typeof(string[])||t==typeof(List<string>))return string.Join(",",((string[])v).Select(x=>Escape(x??"",',')));
            if(t==typeof(float))return ((float)v).ToString("R",Inv);
            if(t==typeof(double))return ((double)v).ToString("R",Inv);
            if(t==typeof(bool))return (bool)v?"1":"0";
            if(t.IsEnum)return Convert.ToInt64(v).ToString(Inv);
            return Convert.ToString(v,Inv);
        }
        static object Parse(string s,Type t)
        {
            if(t==typeof(string))return s;
            if(t==typeof(string[])||t==typeof(List<string>))return s.Length==0?Array.Empty<string>():SplitEscaped(s,',').ToArray();
            if(t==typeof(float))return float.Parse(s,NumberStyles.Float,Inv);
            if(t==typeof(double))return double.Parse(s,NumberStyles.Float,Inv);
            if(t==typeof(bool))return s=="1";
            if(t.IsEnum)return Enum.ToObject(t,long.Parse(s,Inv));
            return Convert.ChangeType(s,t,Inv);
        }
        static int FloatBits(float v)=>BitConverter.ToInt32(BitConverter.GetBytes(v),0);
        static long Bits(object v,int kind,Type t)
        {
            switch(kind){
                case KString:return Hash((string)v??"");
                case KFloat:return FloatBits((float)v);
                case KDouble:return BitConverter.DoubleToInt64Bits((double)v);
                case KBool:return (bool)v?1:0;
                case KInt:return (int)v;
                case KLong:return (long)v;
                default:return Bits(v,t);
            }
        }
        static long Bits(object v,Type t)
        {
            if(t==typeof(string))return Hash((string)v);
            if(t==typeof(string[])||t==typeof(List<string>))return Hash("\u0001"+Text(v,t));
            if(t==typeof(float))return FloatBits((float)v);
            if(t==typeof(double))return BitConverter.DoubleToInt64Bits((double)v);
            if(t==typeof(bool))return (bool)v?1:0;
            return Convert.ToInt64(v,Inv);
        }
        // Two FNV-1a variants: stable across runtimes, unlike string.GetHashCode.
        static long Hash(string s){uint a=2166136261,b=0x811C9DC5^0x5bd1e995;foreach(char c in s){a^=c;a*=16777619;b^=(uint)(c*31+7);b*=0x01000193;}return (long)a<<32|b;}
        internal static object[] LeafTexts(object o,Shape s){var t=new object[s.leaves.Length];for(int i=0;i<t.Length;i++)if(s.leaves[i].kind==KString||s.leaves[i].kind==KStrings)t[i]=Get(o,s.leaves[i]);return t;}
        static bool SameTexts(object v,object reference)
        {
            if(ReferenceEquals(v,reference))return true;
            if(v is string[] a&&reference is string[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!string.Equals(a[i]??"",b[i]??"",StringComparison.Ordinal))return false;return true;}
            return false;
        }
        internal static long[] LeafBits(object o,Shape s){var bits=new long[s.leaves.Length];for(int i=0;i<bits.Length;i++)bits[i]=Bits(Get(o,s.leaves[i]),s.leaves[i].type);return bits;}
        static bool ComplexEmpty(object root,FieldInfo[] chain){object o=root;foreach(var f in chain){if(o==null)return true;o=f.GetValue(o);}return o==null||o is ICollection c&&c.Count==0;}

        // What JsonUtility would hand back after a round trip of this object.
        public static T Normalize<T>(T o)where T:class{Normalize((object)o,0);return o;}
        static object Normalize(object o,int depth)
        {
            if(o==null||depth>8)return o;
            foreach(var f in Fields(o.GetType())){
                var t=f.FieldType;var v=f.GetValue(o);
                if(t==typeof(string)){if(v==null)f.SetValue(o,"");}
                else if(t.IsArray){if(v==null)f.SetValue(o,Array.CreateInstance(t.GetElementType(),0));else if(!t.GetElementType().IsPrimitive&&t.GetElementType()!=typeof(string))foreach(var x in (Array)v)Normalize(x,depth+1);}
                else if(t.IsGenericType&&t.GetGenericTypeDefinition()==typeof(List<>)){if(v==null)f.SetValue(o,New(t));else foreach(var x in (IEnumerable)v)if(x!=null&&!x.GetType().IsPrimitive&&!(x is string))Normalize(x,depth+1);}
                else if(Nested(t)){if(v==null){v=New(t);f.SetValue(o,v);}Normalize(v,depth+1);}
            }
            return o;
        }

        static string Escape(string s,char sep)
        {
            if(s.IndexOf('\\')<0&&s.IndexOf(sep)<0&&s.IndexOf('\n')<0&&s.IndexOf('\r')<0)return s;
            return s.Replace("\\","\\\\").Replace(sep.ToString(),sep=='\t'?"\\t":"\\"+sep).Replace("\n","\\n").Replace("\r","\\r");
        }
        static List<string> SplitEscaped(string s,char sep)
        {
            var result=new List<string>();var sb=new StringBuilder();
            for(int i=0;i<s.Length;i++){
                char c=s[i];
                if(c=='\\'&&i+1<s.Length){char n=s[++i];sb.Append(n=='t'?'\t':n=='n'?'\n':n=='r'?'\r':n);continue;}
                if(c==sep){result.Add(sb.ToString());sb.Clear();continue;}
                sb.Append(c);
            }
            result.Add(sb.ToString());return result;
        }

        // ---- Encoding context: string table + attribute key table ----------
        sealed class Writer
        {
            public readonly List<string> strings=new List<string>();readonly Dictionary<string,int> stringIndex=new Dictionary<string,int>(StringComparer.Ordinal);
            public readonly List<string> keys=new List<string>();readonly Dictionary<string,int> keyIndex=new Dictionary<string,int>(StringComparer.Ordinal);
            // Text of 6+ characters (or starting with '@') goes to a shared table.
            public string Value(object v,Type t)
            {
                string text=Text(v,t);
                if(t!=typeof(string)||text.Length<6&&!text.StartsWith("@",StringComparison.Ordinal))return text;
                if(!stringIndex.TryGetValue(text,out int i)){i=strings.Count;strings.Add(text);stringIndex[text]=i;}
                return "@"+i.ToString(Inv);
            }
            readonly Dictionary<int,string> deltas=new Dictionary<int,string>(),floats=new Dictionary<int,string>();
            public string Delta(float d){int k=FloatBits(d);if(!deltas.TryGetValue(k,out var t))deltas[k]=t="~"+d.ToString("R",Inv);return t;}
            public string Float(float v){int k=FloatBits(v);if(!floats.TryGetValue(k,out var t))floats[k]=t=v.ToString("R",Inv);return t;}
            public int Key(string k){if(!keyIndex.TryGetValue(k,out int i)){i=keys.Count;keys.Add(k);keyIndex[k]=i;}return i;}
        }
        static IEnumerable<KeyValuePair<int,string>> Decode(string data)
        {
            if(string.IsNullOrEmpty(data))yield break;
            foreach(var item in SplitEscaped(data,'\t')){int eq=item.IndexOf('=');yield return new KeyValuePair<int,string>(int.Parse(item.Substring(0,eq),Inv),item.Substring(eq+1));}
        }
        static string Diff(object o,Shape s,long[] reference,Writer w,bool referenceIsDefault,object[] texts=null)
        {
            var sb=new StringBuilder();Diff(o,s.tree,s,reference,w,referenceIsDefault,sb,texts);return sb.ToString();
        }
        static void Diff(object o,Node[] nodes,Shape s,long[] reference,Writer w,bool referenceIsDefault,StringBuilder sb,object[] texts)
        {
            foreach(var n in nodes){
                if(n.children!=null){
                    object sub=o==null?null:n.field.GetValue(o);
                    if(sub==null&&referenceIsDefault)continue;
                    if(sub==null){var d=DefaultBits(s);bool same=true;for(int i=n.first;i<n.last&&same;i++)same=d[i]==reference[i];if(same)continue;}
                    Diff(sub,n.children,s,reference,w,referenceIsDefault,sb,texts);continue;
                }
                var p=s.leaves[n.leaf];object v=o==null?null:n.field.GetValue(o);
                if(v==null)v=p.kind==KString?"":p.kind==KStrings?(object)Array.Empty<string>():o==null?Get(DefaultOf(s.root),p):v;
                else if(p.type==typeof(List<string>))v=((List<string>)v).ToArray();
                if(texts!=null&&(p.kind==KString||p.kind==KStrings)&&SameTexts(v,texts[n.leaf]))continue;
                if(Bits(v,p.kind,p.type)==reference[n.leaf])continue;
                if(sb.Length>0)sb.Append('\t');sb.Append(n.leaf.ToString(Inv)).Append('=').Append(Escape(w.Value(v,p.type),'\t'));
            }
        }
        static void Apply(object o,Shape s,string data,int[] map,List<string> strings)
        {
            foreach(var pair in Decode(data)){
                if(pair.Key>=map.Length||map[pair.Key]<0)continue;
                var path=s.leaves[map[pair.Key]];string text=pair.Value;
                if(path.type==typeof(string)&&text.StartsWith("@",StringComparison.Ordinal))text=strings[int.Parse(text.Substring(1),Inv)];
                Set(o,path,Parse(text,path.type));
            }
        }

        // ---- Attributes ----------------------------------------------------
        // Against the shipped values: "" unchanged, "~d" = shipped value + d
        // (only when that sum gives back the exact float), else the value.
        // Identical neighbours are run-length encoded: token*count.
        static string Attributes(AttributeValue[] current,float[] baseValues,Writer w)
        {
            var parts=new List<string>(current.Length);bool changed=false;
            for(int i=0;i<current.Length;i++){
                float v=current[i].value,b=baseValues[i];
                if(FloatBits(v)==FloatBits(b)){parts.Add("");continue;}
                // "R" text parses back to the same float, so b+d is exact iff
                // it is exact with d itself.
                changed=true;float d=v-b;
                parts.Add(FloatBits(b+d)==FloatBits(v)?w.Delta(d):w.Float(v));
            }
            return changed?Rle(parts):"";
        }
        static string Rle(List<string> parts)
        {
            var sb=new StringBuilder();
            for(int i=0;i<parts.Count;){
                int j=i;while(j<parts.Count&&parts[j]==parts[i])j++;
                if(i>0)sb.Append(',');sb.Append(parts[i]);if(j-i>1)sb.Append('*').Append((j-i).ToString(Inv));
                i=j;
            }
            return sb.ToString();
        }
        static List<string> UnRle(string s)
        {
            var parts=new List<string>();
            foreach(var token in s.Split(',')){int star=token.LastIndexOf('*');if(star>=0){string v=token.Substring(0,star);int n=int.Parse(token.Substring(star+1),Inv);for(int k=0;k<n;k++)parts.Add(v);}else parts.Add(token);}
            return parts;
        }
        static void ApplyAttributes(PlayerData p,string encoded,string id)
        {
            if(string.IsNullOrEmpty(encoded))return;
            var parts=UnRle(encoded);
            if(p.attributes==null||parts.Count!=p.attributes.Length)throw new ArgumentException("Attributs incompatibles : "+id);
            for(int i=0;i<parts.Count;i++){
                string t=parts[i];if(t.Length==0)continue;
                p.attributes[i].value=t[0]=='~'?p.attributes[i].value+float.Parse(t.Substring(1),NumberStyles.Float,Inv):float.Parse(t,NumberStyles.Float,Inv);
            }
        }
        // Full list for a player without shipped attributes: keyIndex:value.
        static string AllAttributes(AttributeValue[] current,Writer w)=>string.Join(",",current.Select(a=>w.Key(a.key??"").ToString(Inv)+":"+a.value.ToString("R",Inv)));
        static AttributeValue[] ReadAllAttributes(string s,List<string> keys)=>s.Length==0?Array.Empty<AttributeValue>():s.Split(',').Select(t=>{int c=t.IndexOf(':');return new AttributeValue{key=keys[int.Parse(t.Substring(0,c),Inv)],value=float.Parse(t.Substring(c+1),NumberStyles.Float,Inv)};}).ToArray();

        // ---- Records -------------------------------------------------------
        // Player:  "=id|diff|attributes"   shipped player, changes only
        //          "+diff|attributes"      generated player, against new PlayerData
        //          "#n"                    kept whole in compactFullPlayers[n]
        // Others:  "diff" (against a new object) or "#n".
        static string PackPlayer(PlayerData p,SaveBaseline baseline,Writer w)
        {
            var s=PlayerShape;
            if(p==null)return null;foreach(var c in s.complex)if(!ComplexEmpty(p,c))return null;if(p.attributes!=null)foreach(var a in p.attributes)if(a==null)return null;
            var current=p.attributes??Array.Empty<AttributeValue>();
            if(p.id!=null&&baseline.leaves.TryGetValue(p.id,out var bits)){
                baseline.attributeKeys.TryGetValue(p.id,out var keys);baseline.attributes.TryGetValue(p.id,out var values);
                string attrs;
                if(keys==null){if(current.Length>0)return null;attrs="";}
                else{
                    if(current.Length!=keys.Length)return null;
                    for(int i=0;i<keys.Length;i++)if((current[i].key??"")!=keys[i])return null;
                    attrs=Attributes(current,values,w);
                }
                baseline.texts.TryGetValue(p.id,out var texts);
                return "="+Escape(p.id,'|')+"|"+Escape(Diff(p,s,bits,w,false,texts),'|')+"|"+attrs;
            }
            return "+"+Escape(Diff(p,s,DefaultBits(s),w,true),'|')+"|"+AllAttributes(current,w);
        }
        static readonly Dictionary<Type,long[]> defaultBits=new Dictionary<Type,long[]>();
        static long[] DefaultBits(Shape s){lock(defaultBits){if(!defaultBits.TryGetValue(s.root,out var b))defaultBits[s.root]=b=LeafBits(Normalize(New(s.root)),s);return b;}}
        // Never starts with '#': a diff begins with a digit or is empty.
        static string PackPlain(object o,Shape s,Writer w)=>o==null||s.complex.Any(c=>!ComplexEmpty(o,c))?null:Diff(o,s,DefaultBits(s),w,true);

        sealed class Reader { public List<string> strings,keys;public int[] playerMap,contractMap,fixtureMap;public Dictionary<string,PlayerData> originals; }
        static PlayerData UnpackPlayer(string record,Reader r)
        {
            var parts=SplitEscaped(record.Substring(1),'|');var s=PlayerShape;
            if(record[0]=='='){
                string id=parts[0];
                if(!r.originals.TryGetValue(id,out var basePlayer))throw new ArgumentException("Joueur de base introuvable : "+id);
                var p=Normalize(basePlayer.Copy());Apply(p,s,parts[1],r.playerMap,r.strings);ApplyAttributes(p,parts[2],id);return p;
            }
            var made=Normalize(new PlayerData());Apply(made,s,parts[0],r.playerMap,r.strings);made.attributes=ReadAllAttributes(parts[1],r.keys);return made;
        }
        static T UnpackPlain<T>(string record,Shape s,int[] map,List<string> strings)where T:class,new()
        {
            var o=Normalize(new T());Apply(o,s,record,map,strings);return o;
        }

        // ---- Whole save ----------------------------------------------------
        internal static void Pack(Career c,SaveBaseline baseline)
        {
            var w=c.world;var writer=new Writer();
            w.compactFormat=Format;
            w.compactPlayerSchema=PlayerShape.leaves.Select(l=>l.name).ToList();
            w.compactContractSchema=ContractShape.leaves.Select(l=>l.name).ToList();
            w.compactFixtureSchema=FixtureShape.leaves.Select(l=>l.name).ToList();
            w.compactFullPlayers=new List<PlayerData>();w.compactFullContracts=new List<Employment>();w.compactFullFixtures=new List<Fixture>();
            w.compactRoster=new List<string>(w.rosterChanges.Count);
            foreach(var p in w.rosterChanges){var r=PackPlayer(p,baseline,writer);if(r==null){r="#"+w.compactFullPlayers.Count.ToString(Inv);w.compactFullPlayers.Add(p);}w.compactRoster.Add(r);}
            w.compactContracts=new List<string>(w.contracts.Count);
            foreach(var e in w.contracts){var r=PackPlain(e,ContractShape,writer);if(r==null){r="#"+w.compactFullContracts.Count.ToString(Inv);w.compactFullContracts.Add(e);}w.compactContracts.Add(r);}
            w.compactFixtures=new List<string>(w.fixtures.Count);
            foreach(var f in w.fixtures){var r=PackPlain(f,FixtureShape,writer);if(r==null){r="#"+w.compactFullFixtures.Count.ToString(Inv);w.compactFullFixtures.Add(f);}w.compactFixtures.Add(r);}
            w.compactStrings=writer.strings;w.compactAttributeKeys=writer.keys;
        }
        internal static void Unpack(CareerWorld w,Database original)
        {
            if(w.compactFormat!=Format)throw new ArgumentException("Format de sauvegarde compacte inconnu : "+w.compactFormat);
            // Indices were written against the field order of the build that
            // saved them: remap by name so a later field order still loads.
            int[] Map(List<string> saved,Shape now)=>(saved??new List<string>()).Select(n=>now.index.TryGetValue(n,out int i)?i:-1).ToArray();
            var r=new Reader{strings=w.compactStrings??new List<string>(),keys=w.compactAttributeKeys??new List<string>(),
                playerMap=Map(w.compactPlayerSchema,PlayerShape),contractMap=Map(w.compactContractSchema,ContractShape),fixtureMap=Map(w.compactFixtureSchema,FixtureShape),
                originals=new Dictionary<string,PlayerData>(StringComparer.Ordinal)};
            foreach(var p in original.players)if(p?.id!=null&&!r.originals.ContainsKey(p.id))r.originals[p.id]=p;
            bool IsFull(string rec)=>rec.StartsWith("#",StringComparison.Ordinal);
            int Full(string rec)=>int.Parse(rec.Substring(1),Inv);
            w.rosterChanges=(w.compactRoster??new List<string>()).Select(rec=>IsFull(rec)?w.compactFullPlayers[Full(rec)]:UnpackPlayer(rec,r)).ToList();
            w.contracts=(w.compactContracts??new List<string>()).Select(rec=>IsFull(rec)?w.compactFullContracts[Full(rec)]:UnpackPlain<Employment>(rec,ContractShape,r.contractMap,r.strings)).ToList();
            w.fixtures=(w.compactFixtures??new List<string>()).Select(rec=>IsFull(rec)?w.compactFullFixtures[Full(rec)]:UnpackPlain<Fixture>(rec,FixtureShape,r.fixtureMap,r.strings)).ToList();
            Clear(w);
        }
        internal static void Clear(CareerWorld w)
        {
            w.compactFormat=0;w.compactPlayerSchema=new List<string>();w.compactContractSchema=new List<string>();w.compactFixtureSchema=new List<string>();
            w.compactRoster=new List<string>();w.compactContracts=new List<string>();w.compactFixtures=new List<string>();
            w.compactFullPlayers=new List<PlayerData>();w.compactFullContracts=new List<Employment>();w.compactFullFixtures=new List<Fixture>();
            w.compactStrings=new List<string>();w.compactAttributeKeys=new List<string>();
        }
    }

    public partial class Career
    {
        // Captured from the untouched database when the career is created or
        // loaded. Without it a save keeps the former, complete format.
        [NonSerialized] public SaveBaseline saveBaseline;
        [NonSerialized] List<PlayerData> packedRoster;[NonSerialized] List<Employment> packedContracts;[NonSerialized] List<Fixture> packedFixtures;

        // Before JsonUtility.ToJson(career): moves rosterChanges, contracts and
        // fixtures into their compact form. Always call RestoreAfterSave
        // afterwards (in a finally), which puts the very same lists back.
        public bool PrepareCompactSave()
        {
            if(world==null||saveBaseline==null||saveBaseline.Count==0||packedRoster!=null)return false;
            CompactSave.Pack(this,saveBaseline);
            packedRoster=world.rosterChanges;packedContracts=world.contracts;packedFixtures=world.fixtures;
            world.rosterChanges=new List<PlayerData>();world.contracts=new List<Employment>();world.fixtures=new List<Fixture>();
            return true;
        }
        public void RestoreAfterSave()
        {
            if(world==null||packedRoster==null)return;
            world.rosterChanges=packedRoster;world.contracts=packedContracts;world.fixtures=packedFixtures;
            packedRoster=null;packedContracts=null;packedFixtures=null;
            CompactSave.Clear(world);
        }
        // After JsonUtility.FromJson, before RestoreWorld: rebuilds the lists a
        // compact save stored. Saves in the former format are left untouched.
        public void ExpandCompactSave(Database original)
        {
            if(world==null||world.compactFormat==0)return;
            CompactSave.Unpack(world,original);
        }
    }
}
