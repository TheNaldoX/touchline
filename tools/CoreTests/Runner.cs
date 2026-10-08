// Exécute les tests [Test]/[TestCase] du Core hors Unity. Sortie : une ligne par test en échec + bilan.
// Usage : dotnet run -c Release -- [filtre] [--list fichier-résultats]
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;

static class Runner
{
    static int Main(string[] args)
    {
        if (string.IsNullOrEmpty(UnityEngine.TestPaths.Assets)) UnityEngine.TestPaths.Assets = FindAssets();
        UnityEngine.Application.dataPath = UnityEngine.TestPaths.Assets;
        int li = Array.IndexOf(args, "--list"); string listPath = li >= 0 && li + 1 < args.Length ? args[li + 1] : null;
        string filter = args.Where((a, i) => !a.StartsWith("--") && (li < 0 || i != li + 1)).FirstOrDefault();
        var cases = new List<(string name, Type type, MethodInfo m, object[] a)>();
        foreach (var t in typeof(Runner).Assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.Namespace != null && t.Namespace.StartsWith("Touchline")))
            foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (m.IsDefined(typeof(TestAttribute))) cases.Add((t.Name + "." + m.Name, t, m, null));
                foreach (var tc in m.GetCustomAttributes<TestCaseAttribute>()) cases.Add((t.Name + "." + m.Name + "(" + string.Join(",", tc.Arguments.Select(x => Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture))) + ")", t, m, tc.Arguments));
            }
        if (filter != null) cases = cases.Where(c => c.name.Contains(filter)).ToList();
        var results = new (string name, string error, double ms)[cases.Count];
        var sw = Stopwatch.StartNew();
        Parallel.For(0, cases.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            var c = cases[i]; var w = Stopwatch.StartNew(); string error = null;
            try
            {
                var inst = c.m.IsStatic ? null : Activator.CreateInstance(c.type, true);
                foreach (var s in c.type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(x => x.IsDefined(typeof(SetUpAttribute)) || x.IsDefined(typeof(OneTimeSetUpAttribute)))) s.Invoke(inst, null);
                var ps = c.m.GetParameters(); object[] a = null;
                if (c.a != null) a = ps.Select((p, k) => k < c.a.Length ? Coerce(c.a[k], p.ParameterType) : p.DefaultValue).ToArray();
                var r = c.m.Invoke(inst, a);
                if (r is System.Collections.IEnumerator e) while (e.MoveNext()) { }
                foreach (var s in c.type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(x => x.IsDefined(typeof(TearDownAttribute)))) s.Invoke(inst, null);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is SuccessException) { }
            catch (TargetInvocationException ex) { var inner = ex.InnerException; error = (inner is AssertionException ? "" : inner.GetType().Name + ": ") + inner.Message + (inner is AssertionException ? "" : " @ " + (inner.StackTrace ?? "").Split('\n').FirstOrDefault(l => l.Contains("Touchline"))?.Trim()); }
            catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; }
            results[i] = (c.name, error, w.Elapsed.TotalMilliseconds);
        });
        var failed = results.Where(r => r.error != null).OrderBy(r => r.name).ToList();
        foreach (var f in failed) Console.WriteLine("ÉCHEC " + f.name + " : " + f.error.Replace("\n", " ").Substring(0, Math.Min(400, f.error.Length)));
        if (listPath != null) File.WriteAllLines(listPath, results.OrderBy(r => r.name).Select(r => (r.error == null ? "OK   " : "FAIL ") + r.name));
        Console.WriteLine($"\n{results.Length - failed.Count}/{results.Length} tests réussis en {sw.Elapsed.TotalSeconds:0}s");
        return failed.Count == 0 ? 0 : 1;
    }
    static object Coerce(object v, Type t)
    {
        if (v == null) return null; if (t.IsInstanceOfType(v)) return v;
        if (t.IsEnum) return Enum.ToObject(t, v);
        return Convert.ChangeType(v, Nullable.GetUnderlyingType(t) ?? t, System.Globalization.CultureInfo.InvariantCulture);
    }
    static string FindAssets()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent) { var p = Path.Combine(d.FullName, "unity", "Touchline", "Assets"); if (Directory.Exists(p)) return p; }
        return "";
    }
}
