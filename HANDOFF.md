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

## 3. État au 9 octobre 2026

### Branches et PR
- **Session autonome du 9 octobre, jusqu'à 08:30 Paris** : recrutement sur `feat/recruitment-hub` (basée sur #37), caméra dans `.worktrees/match-visual` / `fix/match-visual-readability`. PR #38 `feat/visual-polish` découverte, déjà validée par son auteur ; éviter de refaire ciel/contre-jour/montants/tribunes. Ne pas fusionner automatiquement. Réserve APK à 07:15 au plus tard ou quota hebdomadaire 90 % utilisé. Quota lu à 02:55 : 13 % utilisé.
- **PR #34, #35 et #36 fusionnées** : main `ae2df218` contient la pile intégrée, les tenues et l'ambiance.
- **PR #37 `feat/tactical-audit` → `main`** : consignes en deux touches et audit tactique. 699 tests Core réussis ; compilation/captures Unity ciblées validées, revue visuelle faite, test du rapport carrière réussi. APK 0.59/code52 construite et vérifiée, téléphone non vérifié.
- Git HTTPS fonctionne sur ce PC. Le connecteur GitHub renvoie encore 403 en écriture ; utiliser Git pour les branches et REST avec les identifiants Git en mémoire pour la PR, sans exposer de secret.
- Branches `release/0.xx` : uniquement pour construire un APK (voir §4). Ne pas fusionner.
- APK le plus récent : **0.59.0-preview.1**, code 52 ; build GitHub Actions 37857283304 réussi sur release/0.59 (3043762397db9758c99e59795f6a7269ba6b32ca). Signature vérifiée, identique à 0.58 ; test matériel Fold restant.

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

1. **#37 : validation locale et Unity terminée** (captures finales Actions 37855587630). APK 0.59/code52 construite et signature vérifiée. Faire tester Victor (gels, fluidité,
   rendu). Si ça rame : réduire post-process, flocage, ombres (`RenderBudget`). Ambiance (#36) : vérifier
   sur le Fold qu'un but ne provoque pas d'à-coup (maillage des tribunes réécrit à 20 Hz pendant ~10 s).
   Ralenti de nuit : le plan bas est à contre-jour (joueurs sombres), à régler (lumière d'appoint ou
   autre côté du but) si Victor le remarque.
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
léger zoom sur les grosses occasions, tribune haute qui réagit aussi (aujourd'hui fixe).

## 7. Modèle de fin de session (à recopier ici)

```
### Session <agent> du <date>
- Fait : … (PR #…)
- Mesures : …
- Non vérifié : …
- Prochaine étape : …
```

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

### Session Codex du 8 octobre 2026 — audit tactique et raccourcis (branche locale feat/tactical-audit)
- Fait : clone propre de main ae2df218 (#34–#36 fusionnées), SDK .NET 8 local non versionné ; audit de sept consignes avec CSV par affiche et différences appariées ; accès « Consignes » puis choix du niveau en match, sans changer pause/vitesse, retour automatique au direct et lien vers la composition ; nouveaux tests Core et scénarios CiUiScreens. Les erreurs d'étape des captures ne sont plus annoncées comme une réussite.
- Mesures : 699/699 tests Core (689 référence + 10 nouveaux), 200 matchs graine 1 avant/après identiques (SHA256 commun 01E719A69FA3914D9E0689832FFAF9FA7B80FFCE00DDB651818D86E70F74624C). Aucune règle des décisions/mouvements n'est modifiée. Audit 2 800 matchs, sept consignes × 200 affiches × deux variantes. Rapports et données dans tools/Calibration/Reports/.
- Tableau consigne → effet : mentalité prudente/offensive, tirs 10,64→19,05 et précision 83,53→81,20 % ; pressing mesuré/intense, temps de pressing 787,40→924,35 joueur·s (fatigue finale non concluante) ; ligne basse/haute, ligne moyenne −33,89→−19,62 m ; largeur étroite/large, étalement 34,30→54,31 m ; rythme patient/rapide, précision 82,75→81,49 %, volume de passes non concluant ; passes courtes/directes, longueur 15,79→25,18 m et précision 86,31→75,44 % ; milieux défense/attaque, tirs 9,93→17,55 et précision 82,36→79,16 %. Mesures du club à domicile, 4-3-3, IA adverse active ; pas une preuve d'équilibre universel.
- Non vérifié : compilation/rendu Unity des changements, captures après, téléphone et APK. Le connecteur GitHub refuse la création de branche (403 Resource not accessible by integration), même après confirmation de l'utilisateur ; Git local n'a pas de connexion authentifiée. Aucun push, PR, film ou APK annoncé comme réalisé. Une tentative de connexion par code n'a pas fourni de code et a été arrêtée. Captures historiques ui-after2 consultées pour référence, pas présentées comme captures des changements.
- Réserves : ligne haute très avantageuse dans cet échantillon (buts pour 1,68→2,42 et contre 2,19→1,05), missions offensives des milieux très fortes ; tempo rapide n'augmente pas significativement le nombre de passes. Penalties 0,17, sorties de but 13,14, favori 67,68 % : non corrigés dans ce lot. IA adverse déjà présente (score/minute/formes observées, effectif et niveau initial), pas refaite ni prétendue nouvelle. Les lots management, identité UI complète et calibration restent à réaliser.
- Prochaine étape : débloquer l'écriture GitHub, publier cette branche et sa PR (modèle du dépôt), lancer film/ui-tactical-before sur ae2df218 puis film/ui-tactical-after sur le commit candidat, vérifier les captures et erreurs, préparer release/0.59/code52 avec déclencheur release/** (absent de main), livrer le lien Actions après succès. Ne pas fusionner main/release automatiquement. Puis corriger les compromis de la ligne haute et les consignes non concluantes avec calibration avant/après, poursuivre les lots B/C/D.
- Diagnostic carrière terminé : tools/SeasonSim 10 176 --world --report --worldseed 77, années 2026–2036, 533 s, zéro match au moteur complet (mode monde). Rapport tools/SeasonSim/Reports/world-10-seasons-seed77.txt. Effectifs finaux min/médiane/max 23/30/45 : zéro club sous 18, quatre au-dessus de 40. Niveau moyen 64,3→64,2 ; ratio médian salaires/recettes 35→37 % ; 51 % des promus maintenus. Réserves : certains effectifs restent surdimensionnés, dette chez 126/384 clubs ; pas de preuve que tous les systèmes humains sont équilibrés. Le cumul affiché des transferts plafonne à 3000 car l’historique est borné dans CareerAiEconomy, pas parce que le mercato s’arrête. Aucun changement carrière dans ce lot.

### Session Codex du 9 octobre 2026 — publication débloquée
- Git HTTPS authentifié : branche feat/tactical-audit publiée (d64d568), PR brouillon #37 créée : https://github.com/TheNaldoX/touchline/pull/37. Le connecteur reste en erreur 403 ; création effectuée via GitHub REST avec les identifiants Git, sans les afficher ni les conserver dans un fichier.
- Captures de référence lancées sur film/ui-tactical-before (ae2df218) : https://github.com/TheNaldoX/touchline/actions/runs/37850958030, en cours lors de cette mise à jour.
- Prochaine étape : attendre la référence, lancer film/ui-tactical-after sur le candidat, contrôler la compilation et les captures ; puis préparer la release après vérification des versions distantes. Aucun APK nouveau ni validation Unity réussie annoncés à ce stade. Les anciennes mentions de blocage Git ci-dessus sont désormais historiques.
- Parcours complet de référence 37850958030 annulé avant résultat : une exécution historique comparable consacrait 51 minutes au parcours après configuration. Nouveau mode CiUiScreens `ui-tactical-focus-*` limité aux écrans modifiés, toujours en résolution native, trois images de stabilisation au lieu de douze. Le parcours complet reste disponible. Le candidat vérifie explicitement les consignes avec pause puis en direct x2 ; le parcours `-before` capture seulement les contrôles existants. Ne pas présenter ce contrôle ciblé comme une revalidation de tous les menus.
- APK précédente 0.58 téléchargée pour comparaison : package fr.personal.touchline.unity, code 51, ARM64, certificat SHA256 130917e6d2b4ea2dcca487587dcff03b1356b14e7de39c3e01657097ad57ee13 (apksigner verify réussi).
- Revue tactique en lecture seule : DefensiveFocus utilise la destination finale dès la préparation de passe (avant contact) ; piste à tester séparément. La consigne de ligne modifie aussi le seuil de pressing : couplage avéré, effet sur le déséquilibre restant à isoler. Aucun changement moteur ajouté au candidat.
- Validation ciblée de référence : https://github.com/TheNaldoX/touchline/actions/runs/37852640470 (film/ui-tactical-focus-before, db23d2f). Candidat : 4814b12. Contrôles Android reproductibles préparés dans .validation/verify-apk.ps1 (signature cryptographique, identité/version, bibliothèques Unity/IL2CPP ARM64, empreinte).
- Preuve déterministe de la piste d'anticipation : avec ballon (0,0), receveur (20,0) et elapsed=-0,18 s, modifier seulement ball.end de (20,-10) à (20,+10) change DefensiveFocus(1) de (20,-6,5) à (20,+6,5), soit 13 m avant contact. Exécuté par réflexion contre le CoreTests.dll existant ; diagnostic local .validation/tactical-repro/. Prouve une fuite de destination future, pas encore un effet mesuré sur le score. À corriger dans une PR moteur séparée avec comparaison appariée.
- Premier candidat 4814b12 validé dans Unity (Actions 37854005384) : six captures, quatre assertions pause/direct réussies, aucun débordement latéral relevé. Revue visuelle faite. Exception UnityEditor.Search identique à la référence, aucune erreur CS relevée. Limites de rendu : titre du panneau trop haut sur écran plié, boutons du nouveau panneau à environ 46 dp déplié.
- Retouche après revue : titre « Consignes », texte raccourci, cibles du panneau à 50 unités (~48,4 dp déplié), texte des niveaux à 13 unités. Nouvelle validation nécessaire ; ajout de captures après défilement et test du dernier choix « Passes directes » pour vérifier l'accès aux consignes basses.
- Précision après audit carrière : le rapport décennal montre un maximum de 31 joueurs hors partants et 97 % des clubs dans la cible 22–30. Le scénario existant de départs massifs a été rejoué contre le binaire : 46 joueurs dont 24 partants deviennent 22 joueurs + 24 libres à l'échéance. Ne pas traiter les quatre clubs >40 comme une dérive permanente démontrée.
- Correction de l'outil SeasonSim uniquement : le résumé cherchait team nul/vide, alors que les libres portent team="free". Il réutilise maintenant le compteur du tableau détaillé. Test d'intégration Test-FreeAgentReport.ps1 : échec reproduit 0→0 contre 273 attendus, puis réussite 273→273 avec zéro saison simulée. Le rapport décennal historique reste intact : son résumé 0→0 est erroné ; son tableau détaillé donne 273→1278. Aucun nouveau diagnostic de dix saisons ni changement de carrière revendiqué.
- Validation finale UI ba2b380 : Actions 37855587630 réussi, huit captures natives et six assertions consignes réussies (pause, direct x2 et dernier choix après défilement sur chaque écran). Revue des panneaux, du bas de liste et du retour au match faite. Aucun texte coupé latéralement ; aucun nouveau bouton du panneau sous 48 dp ni texte de niveau sous 12 sp. Les avertissements restants concernent le HUD préexistant (notamment vitesse et cibles de ~46 dp déplié), pas une validation globale d'accessibilité.
- Aucune erreur ou alerte CS relevée. Exception UnityEditor.Search préexistante reproduite sur référence et candidat ; ne pas la présenter comme une exception du jeu corrigée. Le commit 79ddb82 n'ajoute ensuite que le correctif de rapport/test et les documents : diff Assets par rapport au candidat validé vide. Pas de test matériel Fold, de chauffe/batterie, ni de promesse de fluidité réelle sur la base de ces captures.

### Livraison Codex du 9 octobre 2026 — APK 0.59
- Build Android réussi : https://github.com/TheNaldoX/touchline/actions/runs/37857283304 ; release/0.59 figée sur 3043762397db9758c99e59795f6a7269ba6b32ca. Aucun changement Assets après validation hormis les métadonnées de version du constructeur. PR #37 reste ouverte, non fusionnée.
- APK téléchargée : artifacts/Touchline-Unity-0.59-preview.apk, 73 076 252 octets, package fr.personal.touchline.unity, version 0.59.0-preview.1 / code 52. Artefact GitHub 11585375805. SHA256 : 9629488FFE63E2C47EB67B57038E91F8BCCB5B6668851D96A9FE9BD799C45DD6.
- Signature cryptographique apksigner vérifiée ; certificat SHA256 130917e6d2b4ea2dcca487587dcff03b1356b14e7de39c3e01657097ad57ee13, identique à 0.58. Manifest et bibliothèques libunity/libil2cpp ARM64 contrôlés. Preuve locale ignorée .validation/apk059/verified.json ; aucun APK committé.
- Validation du lot : 699 tests Core, comparaison 200 matchs identique, audit 2 800 matchs, huit captures Unity finales et six assertions UI, test de non-régression du rapport joueurs libres. Installation, sauvegardes après mise à jour, chauffe, batterie et fluidité sur Fold non testées matériellement. Portraits joueurs absents du dépôt : affichage des initiales inchangé.
- Prochaine étape : test Fold de cette APK, puis PR moteur distincte pour DefensiveFocus avant contact (preuve de fuite de destination documentée ci-dessus), avec test déterministe et calibration appariée. Rééquilibrage ligne haute, management et interface globale restent à poursuivre. Ne pas fusionner release/0.59 ; ne pas la repousser pour éviter un deuxième build.

### Session Codex du 9 octobre — recrutement et cadrage, en cours
- `feat/recruitment-hub` : nouvelle synthèse recrutement, besoins de couverture selon les postes de la tactique (affectation unique des polyvalents), absences distinguées des déficits structurels, échéances prêts/contrats, marge salariale déduisant les précontrats, recommandations fondées sur les observations connues. Aucun changement de décisions du moteur, RNG ou format de sauvegarde. 711/711 tests Core réussis dont 12 nouveaux ; `.validation/recruitment-core-tests-v3.txt`.
- UI : parcours synthèse → poste → marché / mission ciblée, rapports, quatre pistes connues, accès libres/précontrats/staff. Salaire mission par défaut calculé après engagements, zéro si aucune marge ; observation séparée d'une offre. Captures ciblées `ui-recruitment-focus-*` préparées ; validation Unity et revue visuelle encore à faire.
- Référence UI : worktree `.worktrees/recruitment-reference`, branche `film/ui-recruitment-focus-before`, commit d7d570e (runtime de #37 + même harness). Aucun film lancé au moment de cette note. Tous les agents ont gelé Assets avant lancement Unity.
- Audit base brute : 21 815 joueurs, 715 clubs, 36 ligues ; 21 815 lignes de provenance, 9 174 dates d'effectif renseignées, 21 210 salaires marqués simulation. Les libres sont importés séparément à l'initialisation ; zéro libre dans database.json ne signifie pas zéro en carrière. Aucun joueur/club inventé ou ajouté dans ce lot. La qualité des sources reste à améliorer.
- Caméra : agent a isolé un saut de cadrage au franchissement de seuils d'approche du but ; correction et diagnostic Unity préparés dans le worktree dédié, non validés visuellement et non intégrés à ce stade. PR #38 reste distincte.
- Publication : recrutement PR #39 https://github.com/TheNaldoX/touchline/pull/39 (9cf0a75), caméra PR #40 https://github.com/TheNaldoX/touchline/pull/40 (04ed2ff). Deux brouillons empilés sur feat/tactical-audit, pas de fusion. Références caméra préparées : film/camera-crossing-before et film/camera-crossing-portrait-before sur aef6f11 ; pas poussées. Workflow runtime-tests ajouté dans la branche caméra, déclencheur test/runtime-* ; tests caméra Unity pas encore exécutés.
- **File Unity au 9 octobre 03:24 Paris** : tournage tiers `film/ui-mgr-before`, Actions 37866788266 actif depuis environ 02:50, ne pas annuler ni doubler. Notre watcher local session 99690 attend sa fin. Aucun de nos nouveaux films n'est lancé. Tous Assets figés, agents en lecture seule. Vérifier tous les jobs actifs avant toute relance ; ne pas confondre ce film avec une validation de nos changements.
- Deux corrections du hub à faire après dégel AVANT sa capture candidate : aligner rapports à actualiser avec Knowledge < 90 (121–126 jours restent à 90, y compris option attributs visibles), inclure les contre-propositions réellement actionnables dans le compteur. Diagnostic indépendant `.validation/recruitment-review/result.txt` ; aucun correctif de ces points encore intégré.
- Bug de management prouvé à traiter séparément : un prêt entrant peut être « prolongé » comme un joueur détenu ; SignTransfer efface parent et ne paie pas de transfert. Protéger proposition et signature d'anciens accords acceptés, conserver option d'achat légitime. Programme de reproduction isolé `.validation/recruitment-review/`, pas de sauvegarde personnelle touchée.
- Autre lot prioritaire préparé : 45 clubs / 1 434 joueurs hors catalogue des ligues, 44 champs pays contenant le nom du club. Sources UEFA/SFL consultées pour les 45, table `.validation/geography-country-evidence.json`. Ajouter un territoire de recrutement fiable distinct des ligues jouables ; aucune nouvelle ligue ou note à déduire. ClubData ne lit pas encore le champ country du JSON. Source petite et corrections ciblées à implémenter sur branche dédiée, rien intégré.
- Diagnostic de coût des nouvelles vues sur la base réelle initialisée (22 104 joueurs, 273 libres), .NET PC : environ 4,15 ms sans rapport, 4,84 ms avec 500 rapports artificiels, 9,38 ms attributs révélés ; 25 consultations/scénario, hash de carrière inchangé. `.validation/recruitment-benchmark/result.txt`. Ce n'est pas une mesure Android ; allocations par consultation 0,77 / 1,13 / 3,67 Mo, hors boucle de rendu.
- Quota hebdomadaire contrôlé à 03:24 : 22 % utilisé, 78 % disponible. Ne pas consommer de crédit de réinitialisation ni acheter des crédits. Arrêt des nouveaux lots à 90 % utilisé ou 07:15, validation et APK ensuite avant 08:30.

- Reprise 03:58 : film tiers 37866788266 terminé en échec (aucune validation de nos sources) ; aucune autre CI active. Développement dégelé. Hub : alertes alignées sur la possibilité réelle de réobserver, contre-propositions récentes et club courant, avis salarial déduit des engagements. 719/719 tests Core ; capture des pistes après défilement ajoutée. Prêts et territoires sur worktrees séparés en cours, nouvelle validation Unity attendue.

### Candidat 0.60 du 9 octobre, figé à 04:07 Paris
- PR #39 recrutement (028eba3, 720 Core), #40 caméra (45736c0), #41 droits des prêts (8504337, 707 Core), #42 territoires (3c8945c, 728 Core). #41 et #42 sont des brouillons attachés. Les références UI/caméra sont prêtes mais pas encore filmées. Aucune PR fusionnée dans main.
- Intégration locale .worktrees/release060 / release/0.60, commit sources 6961508983b14043b68db1328333769fd9c8eadd, puis ffa4190 (workflow seul). Contient aussi le rendu #38. 736/736 Core, preuve .validation/release060-core-tests.txt. Version préparée 0.60.0-preview.1/code53 ; branche release non poussée, aucun APK 0.60 construit.
- Tests Unity 37873052957 ont échoué AVANT démarrage : game-ci v4.4 transmet noCoverageEnabled au CLI v0.1.72 qui rejette cet argument. Aucun résultat NUnit. Lanceur épinglé à v4.3.2/fa6ced2 selon sa release officielle ; relance unique 37873266419 sur test/runtime-release060 / ffa4190, en cours. Assets gelés dans tous les worktrees jusqu'à fin de validation ; root seul lance Unity.
- Tableau de recrutement : observation stale alignée sur Knowledge < 90 (pas de fausse alerte jours 121–126 ou attributs révélés), dernières contre-propositions et droits de prêt respectés ; avis salarial déduit des salaires réservés. Prêts : 5 échecs sur 8 nouveaux cas avant correctif, tous passent après, option d'achat paie le propriétaire, ancien accord restauré ne peut l'effacer.
- Géographie : 45 clubs / 1 434 joueurs rendus ciblables sans ajout de joueur ou ligue ; 53 territoires, 715 clubs, 36 ligues, 21 815 joueurs bruts inchangés. Sources UEFA/SFL dans tools/DataQuality/club-country-evidence.json ; date de consultation distincte de publication. Capture candidate vérifie Norvège puis mission préremplie sans débit.
- File de validation : finir tests Unity caméra (37 cas attendus), puis références/candidats camera-crossing (portrait + paysage), référence recrutement et candidat intégré avec territoires, parcours ui-loan-focus. Regarder les images et assertions avant APK. Stop nouveaux lots à 07:15 ou quota 90 %, réserver build/livraison avant 08:30. Dernier quota lu 25 % utilisé vers 03:41. APK 0.59 reste disponible et préservée.

- Finition recrutement après captures : cinq onglets visibles sans défilement horizontal, textes secondaires ≥12 unités, raison d’un plafond salarial nul affichée près du bouton d’envoi, flèches de listes éclaircies. 720/720 Core ; nouvelles assertions UI de visibilité et activation préparées, capture finale encore requise. Aucun changement des dépenses de mission.

- Vérification recrutement 37880450007 : dix captures finales relues, onglets/mission/rapports accessibles. Deux assertions de défilement échouent ; le scénario ciblait le haut d’une carte et vérifiait son bouton bas, sans image d’échec. Scénario corrigé pour viser Fiche après restauration différée, capturer avant assertion et journaliser les limites. Aucun défaut de dimensionnement Runtime établi à ce stade ; validation ciblée scroll requise. Unity98/98 sur source intégrée2771e61 ; Core intégré808/808.
