// Sous-ensemble de NUnit utilisé par les tests EditMode du Core, pour les exécuter hors Unity.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute : Attribute { public string Description; }
    [AttributeUsage(AttributeTargets.Method)] public sealed class SetUpAttribute : Attribute {}
    [AttributeUsage(AttributeTargets.Method)] public sealed class TearDownAttribute : Attribute {}
    [AttributeUsage(AttributeTargets.Method)] public sealed class OneTimeSetUpAttribute : Attribute {}
    [AttributeUsage(AttributeTargets.Class)] public sealed class TestFixtureAttribute : Attribute {}
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)] public sealed class CategoryAttribute : Attribute { public CategoryAttribute(string n) {} }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)] public sealed class TestCaseAttribute : Attribute
    { public object[] Arguments; public string TestName; public object ExpectedResult; public TestCaseAttribute(params object[] args) { Arguments = args ?? new object[] { null }; } }

    public class AssertionException : Exception { public AssertionException(string m) : base(m) {} }
    public class SuccessException : Exception { public SuccessException() : base("pass") {} }

    public abstract class Constraint
    {
        public abstract bool Matches(object actual); public abstract string Describe();
        public static Constraint operator !(Constraint c) => new Not(c);
        public Constraint Within(double tol) { if (this is EqualC e) e.Tol = tol; return this; }
        public Constraint Percent => this;
        public ConstraintOps And => new ConstraintOps(this, true);
        public ConstraintOps Or => new ConstraintOps(this, false);
    }
    public class ConstraintOps
    {
        readonly Constraint left; readonly bool and; public ConstraintOps(Constraint l, bool a) { left = l; and = a; }
        Constraint Join(Constraint r) => new Combo(left, r, and);
        public Constraint GreaterThan(object v) => Join(Is.GreaterThan(v)); public Constraint LessThan(object v) => Join(Is.LessThan(v));
        public Constraint GreaterThanOrEqualTo(object v) => Join(Is.GreaterThanOrEqualTo(v)); public Constraint LessThanOrEqualTo(object v) => Join(Is.LessThanOrEqualTo(v));
        public Constraint EqualTo(object v) => Join(Is.EqualTo(v));
    }
    class Combo : Constraint { readonly Constraint a, b; readonly bool and; public Combo(Constraint a, Constraint b, bool and) { this.a = a; this.b = b; this.and = and; } public override bool Matches(object x) => and ? a.Matches(x) && b.Matches(x) : a.Matches(x) || b.Matches(x); public override string Describe() => a.Describe() + (and ? " et " : " ou ") + b.Describe(); }
    class Not : Constraint { readonly Constraint c; public Not(Constraint c) { this.c = c; } public override bool Matches(object a) => !c.Matches(a); public override string Describe() => "pas " + c.Describe(); }
    class Pred : Constraint { readonly Func<object, bool> f; readonly string d; public Pred(Func<object, bool> f, string d) { this.f = f; this.d = d; } public override bool Matches(object a) => f(a); public override string Describe() => d; }
    class EqualC : Constraint
    {
        readonly object e; public double? Tol; public EqualC(object e) { this.e = e; }
        public override bool Matches(object a) => Tol.HasValue && Cmp.IsNum(a) && Cmp.IsNum(e) ? Math.Abs(Cmp.D(a) - Cmp.D(e)) <= Tol.Value : Cmp.Eq(e, a);
        public override string Describe() => "égal à " + Cmp.S(e) + (Tol.HasValue ? " ±" + Tol : "");
    }
    public class NotOps
    {
        public Constraint Null => new Not(Is.Null); public Constraint Empty => new Not(Is.Empty);
        public Constraint EqualTo(object v) => new Not(Is.EqualTo(v)); public Constraint SameAs(object v) => new Not(Is.SameAs(v));
        public Constraint Zero => new Not(Is.EqualTo(0)); public Constraint NaN => new Not(Is.NaN);
        public Constraint GreaterThan(object v) => new Not(Is.GreaterThan(v)); public Constraint LessThan(object v) => new Not(Is.LessThan(v));
        public Constraint InRange(object a, object b) => new Not(Is.InRange(a, b));
    }
    public static class Is
    {
        public static Constraint EqualTo(object v) => new EqualC(v);
        public static Constraint SameAs(object v) => new Pred(a => ReferenceEquals(a, v), "même instance");
        public static Constraint Null => new Pred(a => a == null, "null");
        public static Constraint True => new Pred(a => a is bool b && b, "vrai");
        public static Constraint False => new Pred(a => a is bool b && !b, "faux");
        public static Constraint Zero => EqualTo(0);
        public static Constraint NaN => new Pred(a => Cmp.IsNum(a) && double.IsNaN(Cmp.D(a)), "NaN");
        public static Constraint Empty => new Pred(a => a is string s ? s.Length == 0 : a is IEnumerable e && !e.Cast<object>().Any(), "vide");
        public static Constraint Positive => GreaterThan(0); public static Constraint Negative => LessThan(0);
        public static Constraint GreaterThan(object v) => new Pred(a => Cmp.C(a, v) > 0, "> " + Cmp.S(v));
        public static Constraint LessThan(object v) => new Pred(a => Cmp.C(a, v) < 0, "< " + Cmp.S(v));
        public static Constraint GreaterThanOrEqualTo(object v) => new Pred(a => Cmp.C(a, v) >= 0, ">= " + Cmp.S(v));
        public static Constraint LessThanOrEqualTo(object v) => new Pred(a => Cmp.C(a, v) <= 0, "<= " + Cmp.S(v));
        public static Constraint AtLeast(object v) => GreaterThanOrEqualTo(v); public static Constraint AtMost(object v) => LessThanOrEqualTo(v);
        public static Constraint InRange(object lo, object hi) => new Pred(a => Cmp.C(a, lo) >= 0 && Cmp.C(a, hi) <= 0, "dans [" + Cmp.S(lo) + ", " + Cmp.S(hi) + "]");
        public static Constraint EquivalentTo(IEnumerable v) => new Pred(a => Cmp.Equiv(v, a as IEnumerable), "équivalent");
        public static NotOps Not => new NotOps();
        public static Constraint Ordered => new Pred(a => { var l = ((IEnumerable)a).Cast<object>().ToList(); for (int i = 1; i < l.Count; i++) if (Cmp.C(l[i - 1], l[i]) > 0) return false; return true; }, "trié");
    }
    public static class Does
    {
        public static Constraint Contain(object v) => new Pred(a => a is string s ? s.Contains((string)v) : ((IEnumerable)a).Cast<object>().Any(x => Cmp.Eq(v, x)), "contient " + Cmp.S(v));
        public static Constraint StartWith(string v) => new Pred(a => ((string)a).StartsWith(v, StringComparison.Ordinal), "commence par " + v);
        public static Constraint EndWith(string v) => new Pred(a => ((string)a).EndsWith(v, StringComparison.Ordinal), "finit par " + v);
        public static Constraint Match(string p) => new Pred(a => Regex.IsMatch((string)a, p), "correspond à " + p);
        public static NotOps Not => new NotOps();
    }
    public static class Has
    {
        public static Constraint Count(int n) => new Pred(a => ((IEnumerable)a).Cast<object>().Count() == n, "compte " + n);
        public static Constraint Member(object v) => Does.Contain(v);
    }
    static class Cmp
    {
        public static bool IsNum(object o) => o is sbyte || o is byte || o is short || o is ushort || o is int || o is uint || o is long || o is ulong || o is float || o is double || o is decimal;
        public static double D(object o) => Convert.ToDouble(o, CultureInfo.InvariantCulture);
        public static bool Eq(object e, object a)
        {
            if (e == null || a == null) return e == null && a == null;
            if (IsNum(e) && IsNum(a)) return D(e) == D(a) || (double.IsNaN(D(e)) && double.IsNaN(D(a)));
            if (e is string || a is string) return Equals(e, a);
            if (e is IEnumerable ee && a is IEnumerable ae) { var x = ee.Cast<object>().ToList(); var y = ae.Cast<object>().ToList(); return x.Count == y.Count && x.Zip(y, Eq).All(b => b); }
            return Equals(e, a);
        }
        public static bool Equiv(IEnumerable e, IEnumerable a)
        {
            if (e == null || a == null) return e == a; var rest = a.Cast<object>().ToList();
            foreach (var x in e) { int i = rest.FindIndex(y => Eq(x, y)); if (i < 0) return false; rest.RemoveAt(i); }
            return rest.Count == 0;
        }
        public static int C(object a, object b) { if (IsNum(a) && IsNum(b)) return D(a).CompareTo(D(b)); return Comparer.Default.Compare(a, b); }
        public static string S(object o) => o == null ? "null" : o is string s ? "\"" + s + "\"" : o is IEnumerable e ? "[" + string.Join(", ", e.Cast<object>().Take(12).Select(S)) + "]" : Convert.ToString(o, CultureInfo.InvariantCulture);
    }
    public static class Assert
    {
        static void Fail0(string why, string msg, object[] args) => throw new AssertionException((msg == null ? "" : (args != null && args.Length > 0 ? string.Format(msg, args) : msg) + " — ") + why);
        public static void Fail(string msg = null, params object[] args) => Fail0("échec", msg, args);
        public static void Pass(string msg = null) => throw new SuccessException();
        public static void Ignore(string msg = null) => throw new SuccessException();
        public static void Inconclusive(string msg = null) => throw new SuccessException();
        public static void That(object actual, Constraint c, string msg = null, params object[] args) { if (!c.Matches(actual)) Fail0("attendu " + c.Describe() + ", obtenu " + Cmp.S(actual), msg, args); }
        public static void That<T>(Func<T> f, Constraint c, string msg = null, params object[] args) => That((object)f(), c, msg, args);
        public static void That(bool cond, string msg = null, params object[] args) { if (!cond) Fail0("condition fausse", msg, args); }
        public static void AreEqual(object e, object a, string msg = null, params object[] args) { if (!Cmp.Eq(e, a)) Fail0("attendu " + Cmp.S(e) + ", obtenu " + Cmp.S(a), msg, args); }
        public static void AreEqual(double e, double a, double tol, string msg = null, params object[] args) { if (!(Math.Abs(e - a) <= tol) && !(double.IsNaN(e) && double.IsNaN(a))) Fail0("attendu " + e + " ±" + tol + ", obtenu " + a, msg, args); }
        public static void AreNotEqual(object e, object a, string msg = null, params object[] args) { if (Cmp.Eq(e, a)) Fail0("valeurs égales " + Cmp.S(a), msg, args); }
        public static void AreSame(object e, object a, string msg = null, params object[] args) { if (!ReferenceEquals(e, a)) Fail0("instances différentes", msg, args); }
        public static void AreNotSame(object e, object a, string msg = null, params object[] args) { if (ReferenceEquals(e, a)) Fail0("même instance", msg, args); }
        public static void IsTrue(bool c, string msg = null, params object[] args) { if (!c) Fail0("attendu vrai", msg, args); }
        public static void True(bool c, string msg = null, params object[] args) => IsTrue(c, msg, args);
        public static void IsFalse(bool c, string msg = null, params object[] args) { if (c) Fail0("attendu faux", msg, args); }
        public static void False(bool c, string msg = null, params object[] args) => IsFalse(c, msg, args);
        public static void IsNull(object o, string msg = null, params object[] args) { if (o != null) Fail0("attendu null, obtenu " + Cmp.S(o), msg, args); }
        public static void Null(object o, string msg = null, params object[] args) => IsNull(o, msg, args);
        public static void IsNotNull(object o, string msg = null, params object[] args) { if (o == null) Fail0("attendu non null", msg, args); }
        public static void NotNull(object o, string msg = null, params object[] args) => IsNotNull(o, msg, args);
        public static void IsEmpty(object o, string msg = null, params object[] args) { if (!Is.Empty.Matches(o)) Fail0("attendu vide", msg, args); }
        public static void IsNotEmpty(object o, string msg = null, params object[] args) { if (Is.Empty.Matches(o)) Fail0("attendu non vide", msg, args); }
        public static void IsNaN(double v, string msg = null) { if (!double.IsNaN(v)) Fail0("attendu NaN", msg, null); }
        public static void Greater(object a, object b, string msg = null, params object[] args) { if (!(Cmp.C(a, b) > 0)) Fail0(Cmp.S(a) + " n'est pas > " + Cmp.S(b), msg, args); }
        public static void GreaterOrEqual(object a, object b, string msg = null, params object[] args) { if (!(Cmp.C(a, b) >= 0)) Fail0(Cmp.S(a) + " n'est pas >= " + Cmp.S(b), msg, args); }
        public static void Less(object a, object b, string msg = null, params object[] args) { if (!(Cmp.C(a, b) < 0)) Fail0(Cmp.S(a) + " n'est pas < " + Cmp.S(b), msg, args); }
        public static void LessOrEqual(object a, object b, string msg = null, params object[] args) { if (!(Cmp.C(a, b) <= 0)) Fail0(Cmp.S(a) + " n'est pas <= " + Cmp.S(b), msg, args); }
        public static T Throws<T>(TestDelegate d, string msg = null, params object[] args) where T : Exception
        {
            try { d(); } catch (T e) when (e.GetType() == typeof(T)) { return e; } catch (Exception e) { Fail0("attendu " + typeof(T).Name + ", obtenu " + e.GetType().Name + ": " + e.Message, msg, args); }
            Fail0("attendu " + typeof(T).Name + ", aucune exception", msg, args); return null;
        }
        public static T Catch<T>(TestDelegate d, string msg = null) where T : Exception { try { d(); } catch (T e) { return e; } Fail0("aucune exception", msg, null); return null; }
        public static void DoesNotThrow(TestDelegate d, string msg = null, params object[] args) { try { d(); } catch (Exception e) { Fail0("exception " + e.GetType().Name + ": " + e.Message, msg, args); } }
    }
    public delegate void TestDelegate();
    public static class CollectionAssert
    {
        public static void AreEqual(IEnumerable e, IEnumerable a, string msg = null, params object[] args) => Assert.AreEqual(e, a, msg, args);
        public static void AreNotEqual(IEnumerable e, IEnumerable a, string msg = null, params object[] args) => Assert.AreNotEqual(e, a, msg, args);
        public static void AreEquivalent(IEnumerable e, IEnumerable a, string msg = null, params object[] args) { if (!Cmp.Equiv(e, a)) throw new AssertionException((msg ?? "") + " — collections non équivalentes : " + Cmp.S(e) + " / " + Cmp.S(a)); }
        public static void Contains(IEnumerable c, object v, string msg = null, params object[] args) => Assert.That(c, Does.Contain(v), msg, args);
        public static void DoesNotContain(IEnumerable c, object v, string msg = null, params object[] args) => Assert.That(c, new NotOps().EqualTo(null) is Constraint ? (Constraint)new Pred(x => !Does.Contain(v).Matches(x), "ne contient pas " + v) : null, msg, args);
        public static void IsEmpty(IEnumerable c, string msg = null) => Assert.IsEmpty(c, msg);
        public static void IsNotEmpty(IEnumerable c, string msg = null) => Assert.IsNotEmpty(c, msg);
        public static void AllItemsAreUnique(IEnumerable c, string msg = null) { var l = c.Cast<object>().ToList(); if (l.Distinct().Count() != l.Count) throw new AssertionException((msg ?? "") + " — doublons"); }
        public static void IsSubsetOf(IEnumerable sub, IEnumerable sup, string msg = null) { var s = sup.Cast<object>().ToList(); foreach (var x in sub) if (!s.Any(y => Cmp.Eq(x, y))) throw new AssertionException((msg ?? "") + " — pas un sous-ensemble"); }
    }
    public static class StringAssert
    {
        public static void Contains(string e, string a, string msg = null, params object[] args) { if (a == null || !a.Contains(e)) throw new AssertionException((msg ?? "") + " — \"" + a + "\" ne contient pas \"" + e + "\""); }
        public static void DoesNotContain(string e, string a, string msg = null, params object[] args) { if (a != null && a.Contains(e)) throw new AssertionException((msg ?? "") + " — \"" + a + "\" contient \"" + e + "\""); }
        public static void StartsWith(string e, string a, string msg = null, params object[] args) { if (a == null || !a.StartsWith(e, StringComparison.Ordinal)) throw new AssertionException((msg ?? "") + " — \"" + a + "\" ne commence pas par \"" + e + "\""); }
        public static void EndsWith(string e, string a, string msg = null, params object[] args) { if (a == null || !a.EndsWith(e, StringComparison.Ordinal)) throw new AssertionException((msg ?? "") + " — ne finit pas par \"" + e + "\""); }
        public static void IsMatch(string p, string a, string msg = null) { if (!Regex.IsMatch(a ?? "", p)) throw new AssertionException((msg ?? "") + " — ne correspond pas"); }
    }
}
