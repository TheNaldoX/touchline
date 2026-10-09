# HANDOFF.md — Passation entre agents (Claude ↔ Codex)

Fichier de relais entre les agents IA qui travaillent sur Touchline (Claude Code, ChatGPT Codex…).
**Lire d'abord `AGENTS.md`** (règles obligatoires), puis ce fichier. Le mettre à jour à chaque fin de
session (section « État » et « À faire »), dans la même PR que le travail. Rien d'autre que ce fichier
ne sert de journal (pas de journal dans `docs/`).

## 1. Comment les deux agents partagent le contexte

- **Source de vérité unique : le dépôt GitHub `TheNaldoX/touchline`.** Aucun agent ne voit la
  conversation de l'autre. Tout ce qui doit être su doit être dans le dépôt :
  - `AGENTS.md` : règles (lu automatiquement par Codex ; Claude Code le lit aussi).
  - `HANDOFF.md` (ce fichier) : état, décisions, pièges, file de travail.
  - `CHANGELOG.md` : une ligne par PR.
  - Descriptions de PR : détail, mesures avant/après, incertitudes.
  - Branche `films` : rendus Unity des films/captures CI (voir §4), consultables par les deux.
- **Travailler par PR** sur des branches courtes partant de `main` (une fois la PR #34 fusionnée),
  jamais de force-push, jamais de copies de fichiers.
- **Victor** (propriétaire) n'a pas Unity sur son PC et ne lance pas de scripts : il teste les APK
  sur son Galaxy Z Fold 8 et valide les PR sur GitHub (téléphone/web). Répondre en français, simple.
- Pour reprendre un travail : `git fetch origin`, lire les PR ouvertes
  (`gh api repos/TheNaldoX/touchline/pulls`), lire ce fichier, puis la PR la plus récente.

## 2. Le projet en bref

Jeu de gestion de football type Football Manager, Unity **6000.3.24f1**, URP, Android (Fold 8 plié
1080×2520 et déplié 2184×1968), usage personnel hors ligne.
- `unity/Touchline/Assets/Touchline/Core/` : logique pure (moteur de match, IA, carrière). Testable
  hors Unity avec `tools/CoreTests`.
- `Runtime/` : affichage, UI Toolkit, animation (Mecanim), caméra, stade.
- `Editor/` : build, films CI, captures UI.
- `Tests/EditMode/` : tests Unity (ceux qui dépendent de Runtime sont listés dans
  `tools/CoreTests/excluded-tests.props`).
- Outils .NET (sans Unity) : `tools/CoreTests` (`dotnet run -c Release`, 689 tests),
  `tools/Calibration` (`dotnet run -c Release -- 200 1` = 200 matchs, graine 1),
  `tools/SeasonSim` (plusieurs saisons, `--report`, `--dump`).

## 3. État au 8 octobre 2026 (dernière session Claude)

### Branches et PR
- **PR #34 `integ/0.56` → `main`** : fusion de toute la pile (PR #21 à #33). **À fusionner en premier**,
  puis fermer #21–#33 et #20 (release/0.47, déjà couvert).
- **PR #35 `feat/player-look` → `integ/0.56`** : tenues de club, flocage nom/numéro, cheveux,
  chaussures, étalonnage télé (post-process), usure du gazon, filet réactif. À fusionner après #34
  (rebaser la base sur `main`).
- **PR #36 `feat/atmosphere` → `feat/player-look`** : tribunes qui réagissent (vague, sauts, bras et
  écharpes sur un but, demi-levée sur une frappe ; `Runtime/CrowdReaction.cs`), virage populaire
  (x < 0) aux couleurs du club avec écharpes et drapeaux, drapeaux de coin qui flottent
  (`Runtime/StadiumFlags.cs`), ombres de contact (`Runtime/ContactShadows.cs`), ralenti des buts en
  plan bas à côté du but (`Runtime/GoalReplayCamera.cs`), filet rejoué au ralenti. **À fusionner après
  #35.** Contient aussi `main` (HANDOFF.md) fusionné.
- **PR `feat/visual-polish` → `main`** : ciel en dégradé (`Runtime/StadiumSky.cs`, dôme qui suit la
  caméra, coupé en qualité basse), ambiance trois tons, ralenti du soir éclairé de face (projecteurs
  réorientés pendant le ralenti), montants de but ronds (`StadiumGeometry.GoalFrames`), filet gris
  clair, second anneau qui réagit aux buts du club. Films `vis-*`.
- Branches `release/0.xx` : uniquement pour construire un APK (voir §4). Ne pas fusionner.
- APK le plus récent : **0.57** (= `feat/player-look` + numéro de version), code 50.

### Ce qui a été fait (0.47 → 0.57)
- **Moteur / IA de match** : appels en profondeur, frappes de loin, retard de lecture, interceptions,
  fautes tactiques, forme du jour, avantage du terrain ; engagement après préparation d'une passe/frappe
  (#26) ; pression rapprochée qui coûte le ballon + IA qui l'évite, pressing dans son tiers (#28,
  `Core/MatchCarrierPressure.cs`).
- **Gestion** : IA des clubs (effectifs 22–30, joueurs libres, vendeurs, dette, vétérans, rotation en
  coupe ; `SeasonSim --report`) (#29) ; économie, retraites, jeunes.
- **Animation** : passage à **Mecanim** avec 67 clips Mixamo (`Resources/Animations/Mixamo/`, import
  `Editor/MixamoImportSettings.cs`), lecture par graphe Playables (`Runtime/PlayerMecanimMotion.cs`),
  calage des gestes sur l'instant de contact du moteur (table `ClipContact`), allures trot/course/sprint/
  arrière/pas chassés, verrouillage des pieds par IK + pas de replacement, amortis poitrine/cuisse, tacle
  debout, Sprint Turn, plongeons miroir, chute/relevé (#24, #27, #33). Squelette MakeHuman mappé en
  Humanoid (`Runtime/PlayerViewHumanoid.cs`, côtés inversés détectés). Réglage `match-mecanim`.
- **Stade / visuel** : gazon texturé procédural (`PitchTurf`), second anneau + toit + ombre du toit,
  panneaux LED aux couleurs des clubs, éclairage de soirée (`StadiumLighting`, réglage `match-lighting`),
  caméra télé 30° zoom 0,85 (#25, #32), puis joueurs (#35).
- **Fluidité** : sauvegarde en match seulement aux arrêts (+ toutes les 300 s), résolution dynamique
  sans oscillation (#31). Victor signalait de petits gels : **à confirmer sur le téléphone**.
- **Interface** : échelle réelle sur le Fold (1 unité ≈ 1 dp, cibles 48 dp, textes ≥ 11), mentalité en
  une touche en match, « Jusqu'au match », outil de captures UI en CI (#30).

### Calibration actuelle (200 matchs, graine 1, contenu de #34)
Buts 2,83 · tirs 26,8 · cadrés 8,4 · corners 8,9 · fautes 22,5 · jaunes 4,0 · hors-jeu 3,7 ·
touches 36,9 · passes 81,8 % · possession favori 56 %. **Hors fourchette** : penalties 0,17
(cible 0,20–0,35), sorties de but 13,1 (cible 14–20), victoires du favori 68 % (cible ~45–55 %).

### Non vérifié
- Rien de 0.55–0.57 n'a été testé sur le Fold (fluidité, gels, rendu). Tests EditMode jamais lancés
  dans Unity (seulement compilation via les films CI).

## 4. Infrastructure CI (GitHub Actions) — indispensable sans Unity local

- **APK** : `.github/workflows/build-android.yml` (game-ci). Se déclenche sur une branche `release/**`
  ou un tag `apk-*`. Pour une version : créer `release/0.NN` depuis la branche voulue, y appliquer le
  commit `ci: build de l'APK aussi sur les branches release/` (0569762) s'il n'y est pas déjà, et
  modifier dans `Editor/ProjectBuilder.cs` `bundleVersion` / `bundleVersionCode` / nom de l'APK
  (3 occurrences). L'APK est l'artefact « Touchline-APK » de la page Actions (90 jours). Secrets
  requis déjà configurés : UNITY_LICENSE/EMAIL/PASSWORD, ANDROID_KEYSTORE_BASE64/PASS.
- **Films / captures** : `.github/workflows/match-film.yml`, déclenché en poussant une branche
  `film/<nom>` (≈ 15–40 min). Unity tourne en batch avec Mesa (rendu logiciel). Selon le nom :
  - `proto*` → `Editor/MecanimPrototypeFilm.Run` : rend des clips + `clip-timing.csv` (vitesses
    naturelles, instants de contact, lacet…) ;
  - `ui*` → `Editor/CiUiScreens` : captures des écrans aux deux résolutions du Fold ;
  - autres → `Editor/CiMatchFilm.Run` : 15 s de match, `broadcast.mp4`, `follow.mp4`, planches
    `*-sheet.jpg`, `metrics.txt` (glissement des pieds par action, rotations), `editor-errors.txt`
    (chercher `error CS` = la compilation a échoué). Noms finissant par `-night` : éclairage du soir.
    Nom contenant `goal` : le film commence 6 s avant le premier but du club recevant (graine 731,
    puis suivantes) et dure 24 s (but, ralenti, tribunes) ; graine et instant du but dans `info.txt`.
  - Résultats publiés dans la branche `films`, dossier `films/<nom>/` :
    `git fetch origin films:refs/remotes/origin/films && git show origin/films:films/<nom>/metrics.txt`.
  - Statut : `gh api repos/TheNaldoX/touchline/commits/film/<nom>/check-runs`.
  - Un film réussi = le projet compile dans Unity. C'est la seule vérification de compilation possible.

## 5. Pièges connus

- **Fins de ligne** : certains fichiers `Runtime/` étaient en CRLF alors que `.gitattributes` impose LF.
  Éditer en préservant les fins de ligne ; vérifier `git diff --stat` avant chaque commit ; ne jamais
  committer une réécriture complète de fichier due aux fins de ligne.
- `HumanBone` existe dans Touchline et dans UnityEngine : écrire `UnityEngine.HumanBone`.
- Le post-traitement exige `postProcessData` dans `MatchRenderer.asset` ; les shaders créés à
  l'exécution doivent être référencés par une matière dans `Resources/` sinon ils sont retirés du build.
- MSAA : le pipeline indique 2× ; les films forcent 4×.
- Toute modification des réglages d'import Mixamo : incrémenter `GetVersion()` de
  `MixamoImportSettings` pour forcer la réimportation.
- Aléatoire dans `Core/` : uniquement le générateur à graine du moteur.
- Les API GraphQL de GitHub ne passaient pas depuis l'environnement Claude : PR via REST
  (`gh api repos/TheNaldoX/touchline/pulls -f title=… -f head=… -f base=… -F body=@fichier`).

## 6. À faire (par priorité)

1. **Fusionner #35 puis #36** (#34 est fusionnée), construire un APK depuis `main`, faire tester Victor (gels, fluidité,
   rendu). Si ça rame : réduire post-process, flocage, ombres (`RenderBudget`). Ambiance (#36) : vérifier
   sur le Fold qu'un but ne provoque pas d'à-coup (maillage des tribunes réécrit à 20 Hz pendant ~10 s).
   Ralenti de nuit : contre-jour corrigé (PR feat/visual-polish), à confirmer sur le Fold.
2. **Tactique** (demande de Victor) : vérifier que chaque consigne (mentalité, pressing, hauteur de
   ligne, largeur, tempo/jeu direct, rôles) a un effet mesurable dans le bon sens avec
   `tools/Calibration` (tableau consigne → effet), compléter les consignes manquantes avec des
   compromis réalistes, IA adverse qui adapte sa tactique (score, minute, rapport de force), écran
   tactique lisible sur téléphone (terrain, glisser-déposer, consignes expliquées, changements en
   match en ≤ 2 touches).
3. **Management / immersion** : attentes de la direction et confiance (renvoi, offres d'autres clubs),
   moral des joueurs (temps de jeu vs statut, promesses, demandes de transfert), conférences de presse
   à choix de ton, fil d'actualités, trophées et records de fin de saison, humeur des supporters. Équilibrer
   avec `tools/SeasonSim` sur plusieurs saisons.
4. **Interface** : identité visuelle cohérente (palette, typographie, cartes, couleurs du club), liste
   d'effectif triable lisible plié, fiche joueur avec barres/radar, HUD de match (score, horloge,
   élan/xG, fil d'événements), navigation en moins de touches, transitions légères. Vérifier avec les
   captures `film/ui-…`.
5. **Calibration** : remonter penalties et sorties de but dans leur fourchette, ramener la victoire du
   favori vers 45–55 %.

Idées d'ambiance restantes (priorité basse, animations gelées) : remplaçants et staff sur les bancs,
léger zoom sur les grosses occasions, variété du public (supporters debout, couleurs), escaliers et
rambardes des tribunes, nuances de tonte du gazon, vignettage léger pendant les ralentis.

## 7. Modèle de fin de session (à recopier ici)

```
### Session <agent> du <date>
- Fait : … (PR #…)
- Mesures : …
- Non vérifié : …
- Prochaine étape : …
```

### Session Claude du 8 octobre 2026 (rendu, PR feat/visual-polish)
- Fait : ciel en dégradé avec nuages/collines/ville (`StadiumSky`, dôme qui suit la caméra, dessiné
  en premier sans profondeur, sous le brouillard), ambiance trois tons (`StadiumLighting`),
  projecteurs réorientés pendant le ralenti du soir (fin du contre-jour), gradins plus clairs le
  soir (×0,6 au lieu de ×0,5), montants ronds vernis (`StadiumGeometry.GoalFrames`, remplace les
  LineRenderer), filet gris clair, second anneau qui se lève sur les buts du club.
- Mesures : appels de rendu +1 (ciel) −1 (deux LineRenderer → un maillage) = 0 ; matières +3
  (ciel, montants, filet) ; texture +1 (512×256 RGB24 sans mipmap, 384 Ko) ; ~3 k triangles (dôme)
  + 0,5 k (montants). Qualité basse (mode 0) : ciel et réaction du second anneau coupés. CPU : second
  anneau ≈ 6 k sommets à 20 Hz pendant ~10 s après un but du club seulement.
- Films : vis-homegoal(-night), vis-a2(-night), vis-goal-night (premier essai), vis2-homegoal-night,
  vis2-a2(-night) (horizon du soir corrigé). Avant : atm-*.
- Non vérifié : téléphone ; tests EditMode Unity (StadiumLookTests) seulement compilés ; silhouette
  de ville peu visible depuis la caméra télé (cachée par les tribunes, visible surtout en ralenti).
- Prochaine étape : §6 ; idées de rendu restantes dans la liste d'ambiance ci-dessus.

### Session Claude du 8 octobre 2026 (ambiance, PR #36)
- Fait : réactions du public, virage aux couleurs du club, drapeaux, ombres de contact, ralenti en plan
  bas, mode `film/…goal…` (PR #36, base `feat/player-look`).
- Mesures : +3 appels de rendu (drapeaux 3 sous-maillages réutilisant les matières des tribunes,
  ombres de contact 1, drapeaux de coin fixes supprimés −1), 0 matière nette (+1 ombres, −1 coins),
  texture 64² ; ~+700 triangles. CPU : rien au repos dans les tribunes ; pendant une réaction,
  sommets du camp concerné recalculés à 20 Hz (~36 k sommets envoyés par mise à jour) ; drapeaux
  ~230 sommets à 15 Hz ; ombres 92 sommets par image. Films : atm-goal(-night) (but visiteur),
  atm-homegoal(-night) (but du club), atm-shadow, atm-a2(-night) (comparables à kit-a1).
- Non vérifié : téléphone ; tests EditMode Unity (CrowdLifeTests) jamais exécutés, seulement compilés
  par les films ; ombres de contact peu visibles au premier essai (renforcées, voir atm-a2).
- Prochaine étape : §6.
