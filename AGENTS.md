# AGENTS.md — Règles pour les agents IA (Codex, Claude, Gemini…)

Ce fichier est lu automatiquement par Codex au début de chaque tâche.
Il s'applique à tout agent qui modifie ce dépôt.

## Le projet

**Touchline** : jeu de gestion de football type Football Manager, Unity, Android
(cible : Galaxy Z Fold 8, écran plié et déplié). Usage personnel, hors ligne.

- `unity/Touchline/Assets/Touchline/Core/` : logique pure (moteur de match,
  décisions, carrière, finances, compétitions). Assembly `Touchline.Core`.
  **Ne dépend jamais de `Runtime/`.**
- `unity/Touchline/Assets/Touchline/Runtime/` : affichage, UI, animations, caméra.
- `unity/Touchline/Assets/Touchline/Editor/` : outils, build, tests de fumée.
- `unity/Touchline/Assets/Touchline/Tests/` : tests EditMode (`Touchline.Tests`).
- L'ancien prototype web 0.10 (Three.js / WebView) n'est pas dans ce dépôt.
- Absent du dépôt pour l'instant : `Assets/StreamingAssets/Portraits`
  (portraits joueurs, le jeu affiche les initiales sans eux).

## Priorités actuelles (dans cet ordre)

1. **Moteur de match et IA de décision** : choix de passe/frappe/dribble crédibles,
   statistiques de match réalistes (voir « Calibration »).
2. **Management** : IA des autres clubs (mercato, compositions, rotation),
   économie équilibrée sur plusieurs saisons, progression des joueurs.
3. **UI** : lisibilité sur téléphone plié et déplié, nombre de touches pour les
   actions fréquentes.
4. **Animations** : gelées. Seulement des corrections de bugs visibles
   (glissements de pieds, rotations instantanées, poses cassées). Pas de nouvelles
   animations sans demande explicite.

## Règles de travail — obligatoires

### Git, pas de copies
- **Une tâche = une branche = une pull request.** Nom : `feat/…`, `fix/…`,
  `tune/…` (calibration), `refactor/…`.
- **Interdit** : créer des copies de fichiers ou de dossiers pour « sauvegarder »
  (`*.bak.cs`, `*.original.cs`, `candidate-*`, `staging/`, `sessionN-*`,
  `v2-…`). Git garde l'historique. Unity compile **tous** les `.cs` sous
  `Assets/` : une copie crée des classes en double et casse la compilation.
- Ne jamais committer : APK/AAB, `Library/`, `Temp/`, `Logs/`, keystores.
- Pas de journal de session dans `docs/`. Le compte rendu va dans la
  description de la PR (voir modèle).

### Modifier le code
- Changements **ciblés** : ne pas réécrire une fonction entière quand quelques
  lignes suffisent. Ne pas reformater du code non concerné.
- Toute nouvelle règle de jeu va dans `Core/`, testable sans Unity Player.
- Pas de nombre magique nouveau sans un nom ou un commentaire qui dit ce qu'il
  représente (unité : mètres, secondes, 0–100…).
- Rotations et déplacements visibles : toujours **interpolés** dans le temps,
  jamais affectés instantanément (`p.angle = …` direct = saut visible).
- Aléatoire : passer par le générateur à graine du moteur, jamais
  `UnityEngine.Random` ni `System.Random` neuf dans `Core/`, pour que les matchs
  restent rejouables.

### Définition de « terminé »
Une PR n'est prête que si :
1. Le projet compile dans Unity sans erreur ni nouvel avertissement.
2. Tous les tests EditMode passent.
3. Si la PR touche le moteur ou l'IA : le **rapport de calibration** (ci-dessous)
   est joint **avant / après**, sur la même graine et le même nombre de matchs.
4. Un nouveau test couvre le comportement ajouté ou le bug corrigé.
5. `CHANGELOG.md` a une ligne (une seule) décrivant le changement.

## Calibration du moteur

Simuler au moins **200 matchs** entre équipes de niveau proche et comparer les
moyennes **par match (deux équipes cumulées)** à ces fourchettes indicatives
de grands championnats européens. Elles servent de garde-fous ; un écart doit
être expliqué dans la PR.

| Statistique            | Fourchette visée |
|------------------------|------------------|
| Buts                   | 2,5 – 3,0        |
| Tirs                   | 22 – 28          |
| Tirs cadrés            | 7 – 10           |
| Corners                | 8 – 11           |
| Fautes                 | 20 – 26          |
| Cartons jaunes         | 3 – 5            |
| Penalties              | 0,20 – 0,35      |
| Hors-jeu               | 3 – 5            |
| Touches                | 35 – 50          |
| Possession du favori   | 52 – 62 %        |
| Réussite des passes    | 75 – 87 %        |

Vérifier aussi que **l'équipe la plus forte gagne plus souvent** (environ 45–55 %
de victoires pour un écart net de niveau) et que les consignes tactiques ont un
effet mesurable dans le sens attendu.

## Format de la description de PR

Utiliser `.github/pull_request_template.md`. Court et factuel :
quoi, pourquoi, comment c'est vérifié, ce qui reste incertain.
