// Sous-ensemble minimal de UnityEngine pour exécuter les tests du Core hors Unity.
// JsonUtility imite la sérialisation Unity : champs publics (ou [SerializeField]) non readonly,
// pas de propriétés, null string -> "", null liste/tableau -> [], null classe [Serializable] -> instance.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute {}
    [AttributeUsage(AttributeTargets.Field)] public sealed class HideInInspector : Attribute {}
    public class Object {}
    public class TextAsset : Object { public string text; public byte[] bytes => Encoding.UTF8.GetBytes(text); public TextAsset(string t) { text = t; } }
    public static class Application { public static string dataPath = TestPaths.Assets; public static string persistentDataPath = Path.GetTempPath(); }
    public static class TestPaths { public static string Assets = Environment.GetEnvironmentVariable("TOUCHLINE_ASSETS") ?? ""; }
    public static class Debug { public static void Log(object o) { } public static void LogWarning(object o) { } public static void LogError(object o) => Console.Error.WriteLine(o); }
    public static class Resources
    {
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        public static T Load<T>(string path) where T : class
        {
            if (typeof(T) != typeof(TextAsset)) return null;
            lock (cache)
            {
                if (!cache.TryGetValue(path, out var text))
                {
                    var dir = Path.Combine(TestPaths.Assets, "Touchline", "Resources");
                    var file = new[] { ".json", ".txt", ".bytes", ".csv", "" }.Select(e => Path.Combine(dir, path + e)).FirstOrDefault(File.Exists);
                    text = file == null ? null : File.ReadAllText(file);
                    cache[path] = text;
                }
                return text == null ? null : new TextAsset(text) as T;
            }
        }
    }
    public struct Vector2
    {
        public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static Vector2 zero => new Vector2();
    }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; } }
    public static class Mathf
    {
        public const float PI = (float)Math.PI, Deg2Rad = PI / 180f, Rad2Deg = 180f / PI, Epsilon = float.Epsilon;
        public static float Abs(float v) => Math.Abs(v); public static float Sin(float v) => (float)Math.Sin(v); public static float Cos(float v) => (float)Math.Cos(v);
        public static float Sqrt(float v) => (float)Math.Sqrt(v); public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Min(float a, float b) => Math.Min(a, b); public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v; public static float Clamp01(float v) => Clamp(v, 0, 1);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Repeat(float t, float l) => Clamp(t - (float)Math.Floor(t / l) * l, 0, l);
        public static float DeltaAngle(float c, float t) { float d = Repeat(t - c, 360f); if (d > 180f) d -= 360f; return d; }
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < Math.Max(1e-6f * Math.Max(Math.Abs(a), Math.Abs(b)), Epsilon * 8);
        public static int RoundToInt(float v) => (int)Math.Round(v); public static int FloorToInt(float v) => (int)Math.Floor(v); public static int CeilToInt(float v) => (int)Math.Ceiling(v);
    }

    public static class JsonUtility
    {
        const int MaxDepth = 10;
        public static string ToJson(object obj) => ToJson(obj, false);
        public static string ToJson(object obj, bool pretty)
        {
            if (obj == null) return "";
            var sb = new StringBuilder(); WriteObject(sb, obj, obj.GetType(), 0); return sb.ToString();
        }
        public static T FromJson<T>(string json) => (T)FromJson(json, typeof(T));
        public static object FromJson(string json, Type type)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var target = New(type);
            using (var doc = JsonDocument.Parse(json)) target = Populate(target, type, doc.RootElement, 0);
            return target;
        }
        public static void FromJsonOverwrite(string json, object target)
        {
            using (var doc = JsonDocument.Parse(json)) Populate(target, target.GetType(), doc.RootElement, 0);
        }

        static readonly Dictionary<Type, FieldInfo[]> fieldCache = new Dictionary<Type, FieldInfo[]>();
        static FieldInfo[] Fields(Type t)
        {
            lock (fieldCache)
            {
                if (fieldCache.TryGetValue(t, out var f)) return f;
                var list = new List<FieldInfo>();
                for (var c = t; c != null && c != typeof(object); c = c.BaseType)
                    list.InsertRange(0, c.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                        .Where(x => !x.IsInitOnly && !x.IsNotSerialized && (x.IsPublic || x.IsDefined(typeof(SerializeField))) && Supported(x.FieldType)));
                return fieldCache[t] = list.ToArray();
            }
        }
        static object New(Type t) { try { return Activator.CreateInstance(t, true); } catch (MissingMethodException) { return System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t); } }
        static bool IsList(Type t) => t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>));
        static Type Elem(Type t) => t.IsArray ? t.GetElementType() : t.GetGenericArguments()[0];
        static bool Serializable(Type t) => (t.IsClass || (t.IsValueType && !t.IsPrimitive && !t.IsEnum)) && t != typeof(string) && !IsList(t) && (t.IsSerializable || t.Namespace == "UnityEngine") && !typeof(Delegate).IsAssignableFrom(t);
        static bool Supported(Type t)
        {
            if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal)) return true;
            if (IsList(t)) { var e = Elem(t); return !IsList(e) && Supported(e); }
            if (t.IsGenericType) return false;
            return Serializable(t);
        }
        static void WriteValue(StringBuilder sb, object v, Type t, int depth)
        {
            if (t == typeof(string)) { WriteString(sb, (string)v ?? ""); return; }
            if (t == typeof(bool)) { sb.Append((bool)v ? "true" : "false"); return; }
            if (t.IsEnum) { sb.Append(Convert.ToInt64(v).ToString(CultureInfo.InvariantCulture)); return; }
            if (t == typeof(float)) { sb.Append(((float)v).ToString("R", CultureInfo.InvariantCulture)); return; }
            if (t == typeof(double)) { sb.Append(((double)v).ToString("R", CultureInfo.InvariantCulture)); return; }
            if (t.IsPrimitive || t == typeof(decimal)) { sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); return; }
            if (IsList(t))
            {
                sb.Append('['); var e = Elem(t); bool first = true;
                if (v != null) foreach (var item in (IEnumerable)v) { if (!first) sb.Append(','); first = false; WriteValue(sb, item, e, depth + 1); }
                sb.Append(']'); return;
            }
            if (v == null) { if (depth >= MaxDepth || t.IsAbstract) { sb.Append("{}"); return; } v = New(t); }
            WriteObject(sb, v, t, depth + 1);
        }
        static void WriteObject(StringBuilder sb, object obj, Type t, int depth)
        {
            sb.Append('{'); bool first = true;
            if (depth <= MaxDepth)
                foreach (var f in Fields(t))
                {
                    if (!first) sb.Append(','); first = false;
                    WriteString(sb, f.Name); sb.Append(':'); WriteValue(sb, f.GetValue(obj), f.FieldType, depth);
                }
            sb.Append('}');
        }
        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
                switch (c)
                {
                    case '"': sb.Append("\\\""); break; case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break; case '\r': sb.Append("\\r"); break; case '\t': sb.Append("\\t"); break;
                    default: if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c); break;
                }
            sb.Append('"');
        }
        static object Read(JsonElement e, Type t, object existing, int depth)
        {
            if (t == typeof(string)) return e.ValueKind == JsonValueKind.String ? e.GetString() : e.ValueKind == JsonValueKind.Null ? null : e.GetRawText();
            if (t == typeof(bool)) return e.ValueKind == JsonValueKind.True || (e.ValueKind == JsonValueKind.Number && e.GetDouble() != 0);
            if (t.IsEnum) return Enum.ToObject(t, e.ValueKind == JsonValueKind.Number ? e.GetInt64() : 0);
            if (t.IsPrimitive || t == typeof(decimal))
            {
                if (e.ValueKind == JsonValueKind.String) { double.TryParse(e.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d); return Convert.ChangeType(d, t, CultureInfo.InvariantCulture); }
                if (e.ValueKind != JsonValueKind.Number) return Activator.CreateInstance(t);
                if (t == typeof(float)) return (float)e.GetDouble();
                if (t == typeof(double)) return e.GetDouble();
                if (t == typeof(long)) return e.TryGetInt64(out var l) ? l : (long)e.GetDouble();
                if (t == typeof(ulong)) return e.TryGetUInt64(out var ul) ? ul : (ulong)e.GetDouble();
                if (t == typeof(uint)) return e.TryGetUInt32(out var ui) ? ui : (uint)e.GetDouble();
                return Convert.ChangeType(e.TryGetInt64(out var i) ? (object)i : e.GetDouble(), t, CultureInfo.InvariantCulture);
            }
            if (IsList(t))
            {
                var et = Elem(t); var items = new List<object>();
                if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) items.Add(Read(x, et, null, depth + 1));
                if (t.IsArray) { var arr = Array.CreateInstance(et, items.Count); for (int i = 0; i < items.Count; i++) arr.SetValue(items[i], i); return arr; }
                var list = (IList)Activator.CreateInstance(t); foreach (var x in items) list.Add(x); return list;
            }
            var target = existing ?? New(t);
            if (e.ValueKind == JsonValueKind.Object) target = Populate(target, t, e, depth + 1);
            return target;
        }
        static object Populate(object target, Type t, JsonElement e, int depth)
        {
            if (e.ValueKind != JsonValueKind.Object) return target;
            foreach (var f in Fields(t))
                if (e.TryGetProperty(f.Name, out var v)) f.SetValue(target, Read(v, f.FieldType, Serializable(f.FieldType) && !f.FieldType.IsValueType ? f.GetValue(target) : null, depth));
            return target;
        }
    }
}
