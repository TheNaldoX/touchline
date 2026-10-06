# Backlog Touchline

Chaque tâche est formulée pour être collée telle quelle dans Codex.
Une tâche = une branche = une PR. Les critères d'acceptation font foi.

---

## 0. Socle (à faire en premier)

### T0.1 — Outil de calibration en une commande
Créer dans `Editor/` un menu `Touchline > Calibration` (et une méthode appelable
en ligne de commande `-batchmode -executeMethod`) qui simule N matchs avec une
graine fixe et écrit un tableau des statistiques par match (voir AGENTS.md).
- [ ] Même graine → mêmes résultats, deux fois de suite
- [ ] Sortie Markdown prête à coller dans une PR
- [ ] Exécution de 200 matchs en moins de 2 minutes sur PC

### T0.2 — Nettoyer les restes de sessions
Supprimer de `Assets/` tout fichier `*.bak.cs`, `*.original.cs` et tout dossier
de copie. Vérifier que chaque classe n'est définie qu'une fois.
- [ ] Compilation propre, tests verts
- [ ] Aucun fichier de sauvegarde restant (`git ls-files | grep -E "bak|original"` vide)

---

## 1. Moteur de match et IA

### T1.1 — Trop de touches et de penalties
Le rapport 0.33 indique trop de touches et de penalties. Trouver la cause
(trajectoires de passe qui sortent ? fautes dans la surface trop fréquentes ?)
avant de changer des coefficients.
- [ ] Explication de la cause dans la PR, chiffres à l'appui
- [ ] Touches et penalties dans la fourchette sans faire sortir les autres stats

### T1.2 — Décisions de l'IA évaluées par options
Remplacer les choix « premier qui passe le test » par un score par option
(passe à chaque coéquipier, conduite, frappe, centre) combinant gain de position,
risque de perte et consignes tactiques. Logguer en mode debug les 3 meilleures
options avec leur score.
- [ ] Test : un joueur seul face au but frappe plus souvent qu'il ne passe en retrait
- [ ] Test : sous pressing fort, la passe courte sûre l'emporte sur la passe longue risquée
- [ ] Calibration avant/après jointe

### T1.3 — Tester une à une les idées proposées par Gemini
Le « PATCH 0.42 » (centres moins prisés, passes au sol favorisées, dégagements
dans le terrain) est déjà dans `MatchDecisions.cs` : le mesurer avant/après.
Puis une PR par idée restante, mesurée par la calibration : tempo de décision sous pression,
orientation du corps au contrôle (interpolée, pas instantanée), avance de la
passe en profondeur, erreur de passe. Garder seulement ce qui améliore les stats
ou la sensation sans en dégrader d'autres.

---

## 2. Management

### T2.1 — IA des clubs adverses
Compositions selon fatigue et forme, rotation en semaine chargée, mercato selon
les besoins de poste et le budget.
- [ ] Sur 5 saisons simulées, aucun club ne finit avec moins de 2 joueurs par poste
- [ ] Les clubs riches n'accumulent pas plus de N joueurs de même niveau sans jouer

### T2.2 — Équilibre économique sur 10 saisons
- [ ] Rapport sur 10 saisons : trésorerie médiane des clubs, nombre de faillites,
      inflation des salaires et des valeurs

---

## 3. UI

### T3.1 — Audit des écrans fréquents
Pour : composition, tactique, match en direct, mercato, boîte de réception —
compter les touches nécessaires pour les actions courantes et proposer de réduire
les parcours de plus de 3 touches.
- [ ] Captures plié / déplié de chaque écran dans la PR
