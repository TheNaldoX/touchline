using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Touchline.Core
{
    // Fictional recruitment leagues, generated at load time from a fixed seed.
    // Nothing here describes a real club or person: names are assembled from
    // syllables, levels/values follow distributions measured on the shipped
    // database (rating offsets per position group, value/wage by rating).
    // The output of version 1 is FROZEN (see GeneratedWorldTests checksum):
    // saves only store changed players, so regenerated players must match.
    // Change the rules only behind a new version and a new id prefix.
    public static class GeneratedWorld
    {
        public const int Version = 1;
        public const string IdPrefix = "fic-", LeaguePrefix = "fic.";
        public const string ProvenanceNote = "Club et joueurs fictifs générés par Touchline (graine fixe). Aucune donnée réelle : noms, niveaux, valeurs et salaires sont inventés.";
        const int SquadSize = 24;
        const float RatingSpread = 4.2f;          // points (1–99): spread of players around the club level
        const float ClubSpread = 4f;              // points: best club +4, weakest −4 around the league mean
        const float MinRating = 42, MaxRating = 82, MaxPotential = 90;
        const float ValueAt60 = 440000;           // EUR: median value of a 60-rated player (measured on mid-European leagues)
        const float WeeklyWageAt60 = 2300;        // EUR per week at rating 60 (same sample)
        const float ValueGrowthPerPoint = .11f;   // log-scale growth of value and wage per rating point
        const float SecondTierValueFactor = .7f, SecondTierWageFactor = .55f;
        const float AttributeSpreadFactor = .85f; // generated attribute spread vs measured spread (keeps profiles coherent)

        public static bool IsGenerated(string id) => id != null && (id.StartsWith(IdPrefix, StringComparison.Ordinal) || id.StartsWith(LeaguePrefix, StringComparison.Ordinal));
        public static bool IsGeneratedLeague(LeagueData league) => league != null && IsGenerated(league.id);

        sealed class Spec
        {
            public string id, code, country, label; public int tier, clubs; public float mean; public long revenue;
            public (string nationality, float share)[] origins; public string style;
        }
        // tier 2 leagues sit under real first divisions; tier 1 leagues are new territories.
        static readonly Spec[] Specs = {
            new Spec{id="fic.por.2",code="por2",country="Portugal",label="Portugal · D2 générée",tier=2,clubs=16,mean=58,revenue=4500000,style="pt",origins=new[]{("Portugal",.72f),("Brazil",.2f),("France",.08f)}},
            new Spec{id="fic.bel.2",code="bel2",country="Belgique",label="Belgique · D2 générée",tier=2,clubs=12,mean=57,revenue=4000000,style="be",origins=new[]{("Belgium",.68f),("France",.17f),("Netherlands",.15f)}},
            new Spec{id="fic.tur.2",code="tur2",country="Turquie",label="Turquie · D2 générée",tier=2,clubs=18,mean=59,revenue=5000000,style="tr",origins=new[]{("Türkiye",.8f),("Brazil",.1f),("Germany",.1f)}},
            new Spec{id="fic.aut.2",code="aut2",country="Autriche",label="Autriche · D2 générée",tier=2,clubs=14,mean=55,revenue=2500000,style="at",origins=new[]{("Austria",.8f),("Germany",.2f)}},
            new Spec{id="fic.swe.1",code="swe1",country="Suède",label="Suède · D1 générée",tier=1,clubs=16,mean=61,revenue=9000000,style="se",origins=new[]{("Sweden",.85f),("Finland",.15f)}},
            new Spec{id="fic.pol.1",code="pol1",country="Pologne",label="Pologne · D1 générée",tier=1,clubs=18,mean=62,revenue=12000000,style="pl",origins=new[]{("Poland",.85f),("Brazil",.07f),("Germany",.08f)}},
            new Spec{id="fic.irl.1",code="irl1",country="Irlande",label="Irlande · D1 générée",tier=1,clubs=10,mean=55,revenue=2500000,style="ie",origins=new[]{("Republic of Ireland",.75f),("England",.25f)}},
            new Spec{id="fic.fin.1",code="fin1",country="Finlande",label="Finlande · D1 générée",tier=1,clubs=12,mean=56,revenue=2500000,style="fi",origins=new[]{("Finland",.85f),("Sweden",.15f)}},
        };

        // Common given names and surnames by nationality (no real person intended).
        static readonly Dictionary<string, (string[] given, string[] family)> Names = new Dictionary<string, (string[], string[])>(StringComparer.Ordinal)
        {
            ["Portugal"] = (S("Tiago Rui Diogo Hugo Bruno Nuno Rafael Gonçalo Duarte Martim Afonso Vasco Tomás Henrique Fábio Luís Pedro Simão Rodrigo Miguel"), S("Almeida Teixeira Carvalho Ribeiro Correia Mendes Pinto Moreira Lopes Marques Fonseca Gaspar Antunes Barros Sequeira Tavares Leal Brandão Faria Neves Coelho Matos")),
            ["Brazil"] = (S("Thiago Caio Matheus Renan Igor Douglas Wesley Luan Vinícius Gustavo Everton Danilo Jefferson Kauã Davi Murilo Raul Allan"), S("Souza Oliveira Lima Pereira Barbosa Rocha Cardoso Nascimento Araújo Moura Freitas Batista Campos Teles Prado Siqueira Vieira")),
            ["France"] = (S("Théo Hugo Maxime Bastien Florian Quentin Romain Jordan Kylian Yanis Nathan Axel Enzo Loïc Samuel Clément"), S("Lefèvre Garnier Fontaine Chevalier Rousseau Faure Gauthier Perrot Marchand Barbier Brunet Renard Colin Lemoine Masson Picard")),
            ["Belgium"] = (S("Wout Jens Arne Sander Lander Bram Thibaut Senne Robbe Kobe Maxime Cédric Jarne Brecht Mathis Ruben Lowie Seppe"), S("Peeters Janssens Maes Jacobs Mertens Willems Claes Goossens Wouters Vermeulen Hermans Dubois Lambert Michiels Desmet Verhoeven Pauwels Smet")),
            ["Netherlands"] = (S("Daan Sem Thijs Lars Bas Joep Niels Stijn Ruud Teun Koen Wessel Mees Jelle"), S("de Vries Bakker Visser Smit Meijer de Boer Mulder Bos Vos Dekker Kok Brouwer Hoekstra Postma")),
            ["Türkiye"] = (S("Emre Burak Mert Kerem Yusuf Onur Barış Serkan Oğuz Furkan Caner Eren Berk Umut Tolga Alper Hakan Volkan Ozan Sinan"), S("Yılmaz Kaya Demir Şahin Çelik Yıldız Aydın Öztürk Arslan Doğan Kılıç Aslan Çetin Kurt Koç Özdemir Polat Erdem Korkmaz Güneş")),
            ["Germany"] = (S("Lukas Jonas Felix Niklas Tim Moritz Jannik Marvin Lennart Dominik Philipp Tobias Malte Jannis"), S("Müller Schmidt Schneider Fischer Weber Becker Wagner Hoffmann Koch Richter Klein Wolf Schröder Neumann Braun")),
            ["Austria"] = (S("Florian Stefan Lukas Matthias Christoph Fabian Julian Raphael Simon Andreas Patrick Valentin Dominik Manuel Thomas Elias Jakob Leon"), S("Gruber Huber Bauer Wagner Pichler Steiner Moser Mayer Hofer Leitner Berger Fuchs Eder Fischer Schmid Winkler Weber Lechner Haas Reiter")),
            ["Sweden"] = (S("Erik Lucas Oskar Viktor Axel Filip Emil Gustav Isak Albin Hampus Linus Anton Jonatan Melker Ludvig Arvid Elias"), S("Andersson Johansson Karlsson Nilsson Eriksson Larsson Olsson Persson Svensson Gustafsson Pettersson Jonsson Lindberg Lindqvist Berg Holm Sandberg Ekström")),
            ["Finland"] = (S("Eero Aleksi Joona Oskari Veeti Niko Juho Lauri Ville Teemu Arttu Santeri"), S("Korhonen Virtanen Mäkinen Nieminen Mäkelä Hämäläinen Laine Heikkinen Koskinen Järvinen Lehtonen Saarinen Salminen")),
            ["Poland"] = (S("Jakub Kacper Szymon Mateusz Bartosz Michał Filip Kamil Dawid Patryk Łukasz Wiktor Oskar Paweł Adrian Damian Hubert Igor"), S("Nowak Kowalski Wiśniewski Wójcik Kowalczyk Kamiński Lewandowski Zieliński Szymański Woźniak Dąbrowski Kozłowski Jankowski Mazur Kwiatkowski Krawczyk Piotrowski Grabowski")),
            ["Republic of Ireland"] = (S("Conor Seán Cian Darragh Oisín Ciarán Eoin Liam Jack Ronan Shane Niall Aaron Fionn Dylan Callum"), S("Murphy Kelly O'Sullivan Walsh O'Brien Byrne Ryan O'Connor O'Neill Doyle McCarthy Gallagher Doherty Kennedy Lynch Quinn Brennan Duffy")),
            ["England"] = (S("Harry Jack Oliver George Charlie Alfie Jacob Ben Sam Joe Reece Lewis Ryan Tom"), S("Smith Jones Taylor Brown Wilson Evans Thomas Johnson Roberts Walker Wright Robinson Thompson Hughes")),
        };
        static string[] S(string text) => text.Split(' ');

        // Town-like stems and club patterns per style; {T} is the invented town.
        static readonly Dictionary<string, (string[] start, string[] end, string[] pattern)> Clubs = new Dictionary<string, (string[], string[], string[])>(StringComparer.Ordinal)
        {
            ["pt"] = (S("Alva Ribe Mon Cas Pena Sal Tor Ama Lou Fon Ver Cor"), S("rela deira tejo vinha lim cal vado mira quel"), "{T} FC|SC {T}|Atlético {T}|CD {T}|União {T}".Split('|')),
            ["be"] = (S("Hoog Ber Wes Lan Kor Mol Ste Rum Ver Zan Ol Hel"), S("dael bergen hout inge meer velde rode zele"), "K. {T} SK|RFC {T}|{T} VV|Royal {T}".Split('|')),
            ["tr"] = (S("Kara Ak Yeşil Dağ Göl Kum Taş Sarı Bey Ova Çam Gün"), S("köy pınar dere tepe yazı kent ova lı"), "{T}spor|{T} Belediyespor|{T} Gençlik SK|{T} FK".Split('|')),
            ["at"] = (S("Alt Brunn Eich Hohen Kirch Lind Ober Stein Wald Hof Gries Moos"), S("au bach dorf egg feld heim stätten wang"), "SV {T}|FC {T}|ASK {T}|Union {T}".Split('|')),
            ["se"] = (S("Björk Ek Fors Gran Hag Lind Sjö Strand Ulv Vik Ros Al"), S("by hamn holm lunda ås vik berga torp"), "{T} IF|{T} BK|IK {T}|{T} FF".Split('|')),
            ["pl"] = (S("Biel Dąb Grab Krzy Las Mił Ostr Piask Ryb Wierz Sos Brz"), S("owo ice ów nik owiec ewo in any"), "KS {T}|MKS {T}|{T} FC|GKS {T}".Split('|')),
            ["ie"] = (S("Kil Glen Dun Car Ard Clon Knock Bal Rath Lis"), S("more beg annon ross garry tully mona brack"), "{T} Rovers|{T} Athletic|{T} Town|{T} United".Split('|')),
            ["fi"] = (S("Järvi Mänty Pihla Rauta Kuusi Vaara Haapa Lumi Kivi Koivu"), S("la nen vesi koski mäki niemi salo"), "FC {T}|{T}n Palloseura|JK {T}|{T} IF".Split('|')),
        };

        // Measured offset (mean, spread) of each attribute from the overall rating, by position group.
        static readonly Dictionary<string, (string key, float mean, float spread)[]> Profiles = new Dictionary<string, (string, float, float)[]>(StringComparer.Ordinal)
        {
            ["GB"] = P("acceleration:-31:10 agility:-28:10 jumping:-9:7 stamina:-38:8 strength:-6:10 aggression:-42:9 balance:-27:12 ballControl:-47:8 composure:-26:10 crossing:-53:8 curve:-52:8 def:-31:10 defensiveAwareness:-54:8 dri:1:2 dribbling:-53:8 finishing:-56:8 freeKickAccuracy:-53:8 gkDiving:0:4 gkHandling:-2:4 gkKicking:-3:5 gkPositioning:-1:4 gkReflexes:1:4 headingAccuracy:-53:8 interceptions:-52:8 longPassing:-37:10 longShots:-56:8 pac:0:2 pas:-3:4 penalties:-49:9 phy:-1:2 positioning:-57:8 reactions:-5:5 sho:-2:3 shortPassing:-36:9 shotPower:-18:4 slidingTackle:-53:8 sprintSpeed:-30:10 standingTackle:-53:8 vision:-25:10 volleys:-56:8"),
            ["DEF"] = P("acceleration:-2:12 agility:-7:13 jumping:4:7 stamina:0:9 strength:3:10 aggression:-2:6 balance:-7:13 ballControl:-6:6 composure:-8:6 crossing:-15:14 curve:-21:13 def:-2:3 defensiveAwareness:-2:4 dri:-8:7 dribbling:-11:10 finishing:-30:12 freeKickAccuracy:-30:12 gkDiving:-58:7 gkHandling:-58:7 gkKicking:-58:7 gkPositioning:-58:7 gkReflexes:-58:7 headingAccuracy:-5:7 interceptions:-2:4 longPassing:-10:7 longShots:-28:13 pac:-1:11 pas:-12:7 penalties:-25:9 phy:1:6 positioning:-20:14 reactions:-4:3 sho:-26:10 shortPassing:-5:5 shotPower:-16:11 slidingTackle:-2:3 sprintSpeed:-1:12 standingTackle:0:3 vision:-18:11 volleys:-32:11"),
            ["MIL"] = P("acceleration:0:11 agility:2:9 jumping:-4:9 stamina:1:9 strength:-6:12 aggression:-7:11 balance:3:11 ballControl:1:3 composure:-4:5 crossing:-9:7 curve:-9:9 def:-12:12 defensiveAwareness:-13:13 dri:0:3 dribbling:-1:4 finishing:-10:8 freeKickAccuracy:-15:10 gkDiving:-59:7 gkHandling:-59:7 gkKicking:-58:7 gkPositioning:-58:7 gkReflexes:-59:7 headingAccuracy:-15:10 interceptions:-12:14 longPassing:-3:5 longShots:-8:8 pac:0:11 pas:-3:3 penalties:-14:9 phy:-4:8 positioning:-6:6 reactions:-3:4 sho:-9:7 shortPassing:1:3 shotPower:-4:7 slidingTackle:-13:13 sprintSpeed:-1:11 standingTackle:-10:13 vision:-2:4 volleys:-16:9"),
            ["ATT"] = P("acceleration:5:10 agility:2:10 jumping:3:10 stamina:-3:9 strength:-3:13 aggression:-15:12 balance:0:12 ballControl:0:3 composure:-5:5 crossing:-13:11 curve:-11:9 def:-35:9 defensiveAwareness:-38:11 dri:0:4 dribbling:0:4 finishing:-1:4 freeKickAccuracy:-18:9 gkDiving:-59:7 gkHandling:-59:7 gkKicking:-59:7 gkPositioning:-59:7 gkReflexes:-59:7 headingAccuracy:-9:12 interceptions:-39:12 longPassing:-14:8 longShots:-6:5 pac:5:10 pas:-9:5 penalties:-6:7 phy:-5:9 positioning:-1:4 reactions:-3:4 sho:-2:3 shortPassing:-4:4 shotPower:0:5 slidingTackle:-40:12 sprintSpeed:5:10 standingTackle:-36:12 vision:-7:6 volleys:-8:6"),
        };
        static (string, float, float)[] P(string text) => text.Split(' ').Select(t => t.Split(':')).Select(x => (x[0], float.Parse(x[1], CultureInfo.InvariantCulture), float.Parse(x[2], CultureInfo.InvariantCulture))).ToArray();

        // Squad template: canonical positions of the 24 players.
        static readonly string[] Template = S("GK GK GK CB CB CB CB LB LB RB RB CDM CDM CM CM CM CM CAM LW LW RW RW ST ST");
        static readonly int[] AgeWeights = { 0, 0, 2, 4, 6, 7, 8, 8, 9, 8, 8, 7, 7, 6, 5, 4, 3, 3, 2, 1, 1 }; // ages 16..36

        // Deterministic splitmix64 stream: never UnityEngine/System.Random.
        struct Rng
        {
            ulong state;
            public Rng(string key) { ulong h = 14695981039346656037UL; foreach (char c in key) h = unchecked((h ^ c) * 1099511628211UL); state = h; }
            public ulong Next() { unchecked { state += 0x9E3779B97F4A7C15UL; ulong z = state; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); } }
            public float Unit() => (Next() >> 40) / 16777216f;
            public int Range(int count) => (int)(Unit() * count) % Math.Max(1, count);
            public float Normal() { float u = Math.Max(1e-6f, Unit()), v = Unit(); return (float)(Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v)); }
            public T Pick<T>(T[] values) => values[Range(values.Length)];
        }

        /// <summary>Adds the generated leagues once (idempotent). Returns the number of players added.</summary>
        public static int Expand(Database db)
        {
            if (db?.players == null || db.clubs == null || db.leagues == null) return 0;
            if (db.leagues.Any(IsGeneratedLeague)) return 0;
            var leagues = new List<LeagueData>(); var clubs = new List<ClubData>(); var players = new List<PlayerData>();
            var usedNames = new HashSet<string>(db.clubs.Select(c => c.name ?? ""), StringComparer.OrdinalIgnoreCase);
            foreach (var spec in Specs)
            {
                leagues.Add(new LeagueData { id = spec.id, name = spec.label, country = spec.country, tier = spec.tier, scoutingOnly = true, format = "scouting-only",
                    calendarSource = "Aucun calendrier · résultats simulés à la demande", rulesNote = ProvenanceNote });
                for (int i = 0; i < spec.clubs; i++) clubs.Add(GenerateClub(spec, i, usedNames, players));
            }
            db.leagues = db.leagues.Concat(leagues).ToArray();
            db.clubs = db.clubs.Concat(clubs).ToArray();
            db.players = db.players.Concat(players).ToArray();
            return players.Count;
        }

        static ClubData GenerateClub(Spec spec, int index, HashSet<string> usedNames, List<PlayerData> players)
        {
            var rng = new Rng("touchline-generated-v" + Version + "/" + spec.id + "/club/" + index);
            var style = Clubs[spec.style]; string name = null;
            for (int attempt = 0; attempt < 40 && name == null; attempt++)
            {
                string town = rng.Pick(style.start) + rng.Pick(style.end);
                string candidate = rng.Pick(style.pattern).Replace("{T}", town);
                if (usedNames.Add(candidate)) name = candidate;
            }
            name ??= spec.label + " " + (index + 1);
            string id = IdPrefix + spec.code + "-" + (index + 1).ToString("00", CultureInfo.InvariantCulture);
            // Best club first: an even ladder from +ClubSpread to −ClubSpread, lightly perturbed.
            float offset = spec.clubs <= 1 ? 0 : ClubSpread - 2 * ClubSpread * index / (spec.clubs - 1) + rng.Normal() * .8f;
            long revenue = (long)(spec.revenue * Math.Exp(.1 * offset) * (.9 + .2 * rng.Unit()) / 1000) * 1000;
            string color = "#" + ((rng.Next() & 0xFFFFFF) | 0x202020).ToString("x6", CultureInfo.InvariantCulture);
            var club = new ClubData { id = id, name = name, league = spec.id, country = spec.country, color = color, playable = false, reserve = false, annualRevenue = revenue,
                financeSource = "Club fictif généré · revenus modélisés, aucune donnée réelle", sourceSeason = "Touchline généré v" + Version };
            for (int n = 0; n < SquadSize; n++) players.Add(GeneratePlayer(spec, club, offset, n));
            return club;
        }

        static PlayerData GeneratePlayer(Spec spec, ClubData club, float clubOffset, int index)
        {
            var rng = new Rng("touchline-generated-v" + Version + "/" + club.id + "/player/" + index);
            string role = Template[index % Template.Length];
            string group = role == "GK" ? "GB" : role == "CB" || role == "LB" || role == "RB" ? "DEF" : role == "CDM" || role == "CM" || role == "CAM" ? "MIL" : "ATT";
            float pick = rng.Unit(), cumulative = 0; string nationality = spec.origins[0].nationality;
            foreach (var origin in spec.origins) { cumulative += origin.share; if (pick < cumulative) { nationality = origin.nationality; break; } }
            var names = Names[nationality];
            string given = rng.Pick(names.given), family = rng.Pick(names.family);
            int total = AgeWeights.Sum(), roll = rng.Range(total), age = 16;
            for (int a = 0; a < AgeWeights.Length; a++) { roll -= AgeWeights[a]; if (roll < 0) { age = 16 + a; break; } }
            // Keepers mature later; teenagers are rarely already established.
            if (role == "GK" && age < 20) age += 3;
            float youthGap = Math.Max(0, 23 - age) * 1.2f;
            float rating = Mathx.Clamp((float)Math.Round(spec.mean + clubOffset + rng.Normal() * RatingSpread - youthGap * .5f), MinRating, MaxRating);
            float potential = Mathx.Clamp((float)Math.Round(rating + youthGap * (.5f + rng.Unit()) + Math.Abs(rng.Normal()) * 1.5f), rating, MaxPotential);
            float ageValue = age <= 20 ? 1.5f : age <= 23 ? 1.2f : age <= 28 ? 1f : age <= 30 ? .8f : age <= 32 ? .6f : .4f;
            double growth = Math.Exp(ValueGrowthPerPoint * (rating - 60));
            long value = Math.Max(10000, (long)(ValueAt60 * growth * ageValue * (spec.tier == 1 ? 1 : SecondTierValueFactor) * (.8 + .4 * rng.Unit()) / 5000) * 5000);
            long wage = Math.Max(150, (long)(WeeklyWageAt60 * growth * (spec.tier == 1 ? 1 : SecondTierWageFactor) * (age < 21 ? .6 : 1) * (.85 + .3 * rng.Unit()) / 50) * 50);
            int height = role == "GK" ? 186 + rng.Range(11) : role == "CB" ? 182 + rng.Range(12) : role == "ST" ? 176 + rng.Range(16) : 168 + rng.Range(17);
            bool left = role == "LB" || role == "LW" ? rng.Unit() < .7f : rng.Unit() < .18f;
            var positions = new List<string> { role };
            if (rng.Unit() < .3f)
            {
                string second = role switch { "CB" => "CDM", "LB" => "LW", "RB" => "RW", "CDM" => "CM", "CM" => rng.Unit() < .5f ? "CDM" : "CAM", "CAM" => "CM", "LW" => "ST", "RW" => "ST", "ST" => rng.Unit() < .5f ? "LW" : "RW", _ => null };
                if (second != null) positions.Add(second);
            }
            var birth = Career.Epoch.AddYears(-age).AddDays(-(1 + rng.Range(364)));
            var attributes = Profiles[group].Select(a => new AttributeValue { key = a.key, value = Mathx.Clamp((float)Math.Round(rating + a.mean + rng.Normal() * a.spread * AttributeSpreadFactor), 10, 95) }).ToArray();
            return new PlayerData
            {
                id = club.id + "-" + (index + 1).ToString("00", CultureInfo.InvariantCulture), name = given + " " + family, givenNames = given, surname = family,
                team = club.id, age = age, birthDate = birth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), position = group, positions = positions.ToArray(),
                number = index < 3 ? new[] { 1, 12, 30 }[index] : index + 1, nationality = nationality, preferredFoot = left ? "Gauche" : "Droit",
                rating = rating, potential = potential, fitness = 100, morale = 75, value = value, wage = wage, heightCm = height, weightKg = height - 105 + rng.Range(11) - 5,
                attributes = attributes, source = "touchline:generated:v" + Version, physiqueSource = "Généré · fictif",
                assessment = "Joueur fictif généré par Touchline : aucune personne réelle, niveau et attributs inventés.",
                salarySource = "Estimation Touchline · joueur fictif", valueSource = "Estimation Touchline · joueur fictif",
            };
        }

        /// <summary>Order-independent fingerprint of the generated content (frozen per version).</summary>
        public static ulong Fingerprint(Database db)
        {
            ulong h = 14695981039346656037UL;
            void Mix(string s) { foreach (char c in s ?? "") h = unchecked((h ^ c) * 1099511628211UL); h = unchecked((h ^ 31) * 1099511628211UL); }
            foreach (var l in db.leagues.Where(IsGeneratedLeague)) { Mix(l.id); Mix(l.name); Mix(l.country); }
            foreach (var c in db.clubs.Where(c => IsGenerated(c.id))) { Mix(c.id); Mix(c.name); Mix(c.annualRevenue.ToString(CultureInfo.InvariantCulture)); }
            foreach (var p in db.players.Where(p => IsGenerated(p.id)))
            {
                Mix(p.id); Mix(p.name); Mix(p.nationality); Mix(p.birthDate); Mix(string.Join("/", p.positions));
                Mix(((int)p.rating).ToString(CultureInfo.InvariantCulture)); Mix(((int)p.potential).ToString(CultureInfo.InvariantCulture));
                Mix(p.value.ToString(CultureInfo.InvariantCulture)); Mix(p.wage.ToString(CultureInfo.InvariantCulture));
                foreach (var a in p.attributes) Mix(((int)a.value).ToString(CultureInfo.InvariantCulture));
            }
            return h;
        }

        public sealed class SimulatedStanding { public string club; public int played, won, drawn, lost, goalsFor, goalsAgainst; public int Points => won * 3 + drawn; }
        const float HomeGoals = 1.45f, AwayGoals = 1.15f; // average goals per side in the simulated league
        const float GoalsPerStrengthPoint = .06f;         // log-scale change of expected goals per rating point of difference
        static readonly Dictionary<string, List<SimulatedStanding>> tableCache = new Dictionary<string, List<SimulatedStanding>>(StringComparer.Ordinal);
        static PlayerData[] tableSource;

        /// <summary>Double round robin simulated from current squad strength; deterministic per league and season.</summary>
        public static List<SimulatedStanding> SimulatedTable(Database db, string leagueId, int season)
        {
            if (db?.clubs == null || db.players == null) return new List<SimulatedStanding>();
            if (!ReferenceEquals(tableSource, db.players)) { tableCache.Clear(); tableSource = db.players; }
            string key = leagueId + "/" + season;
            if (tableCache.TryGetValue(key, out var cached)) return cached;
            var ids = db.clubs.Where(c => c.league == leagueId).Select(c => c.id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var squads = db.players.Where(p => Array.IndexOf(ids, p.team) >= 0).GroupBy(p => p.team).ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.rating + p.development).Take(14).Select(p => p.rating + p.development).DefaultIfEmpty(50).Average());
            var rows = ids.ToDictionary(id => id, id => new SimulatedStanding { club = id });
            foreach (var home in ids) foreach (var away in ids)
            {
                if (home == away) continue;
                var rng = new Rng("touchline-table/" + leagueId + "/" + season + "/" + home + "/" + away);
                float diff = (squads.TryGetValue(home, out var h) ? h : 50) - (squads.TryGetValue(away, out var a) ? a : 50);
                int hg = Poisson(ref rng, HomeGoals * (float)Math.Exp(diff * GoalsPerStrengthPoint)), ag = Poisson(ref rng, AwayGoals * (float)Math.Exp(-diff * GoalsPerStrengthPoint));
                var r1 = rows[home]; var r2 = rows[away]; r1.played++; r2.played++; r1.goalsFor += hg; r1.goalsAgainst += ag; r2.goalsFor += ag; r2.goalsAgainst += hg;
                if (hg > ag) { r1.won++; r2.lost++; } else if (hg < ag) { r2.won++; r1.lost++; } else { r1.drawn++; r2.drawn++; }
            }
            var table = rows.Values.OrderByDescending(r => r.Points).ThenByDescending(r => r.goalsFor - r.goalsAgainst).ThenByDescending(r => r.goalsFor).ThenBy(r => r.club, StringComparer.Ordinal).ToList();
            tableCache[key] = table; return table;
        }
        static int Poisson(ref Rng rng, float lambda)
        {
            double limit = Math.Exp(-lambda), product = rng.Unit(); int k = 0;
            while (product > limit && k < 12) { product *= rng.Unit(); k++; }
            return k;
        }
    }
}
