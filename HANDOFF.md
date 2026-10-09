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
- Outils .NET (sans Unity) : `tools/CoreTests` (`dotnet run -c Release`, 808 tests sur le candidat 0.60),
  `tools/Calibration` (`dotnet run -c Release -- 200 1` = 200 matchs, graine 1),
  `tools/SeasonSim` (plusieurs saisons, `--report`, `--dump`).

## 3. État au 9 octobre 2026

### Pression dans la surface — arbitrage (travail non livré)

- `fix/pressure-penalties`, base PR #51. Suppression de l'immunité `!ownBox` dans ResolveCarrierPressure ; le risque de faute est multiplié par .35 dans la surface, en plus de la prudence existante sur le duel. Une faute constatée déclenche le penalty au point des 11 mètres, un contact hors surface conserve le coup franc. Six tests couvrent deux équipes/deux périodes et hors surface ; quatre échouaient avant. 831/831 Core.
- Avant/après sur 200 matchs graines1 et17, rapports `pressure-penalties-*`. Graine1 : buts2,46→2,58, penalties.17→.19, tirs24,31→24,60, touches49,92→49,98, sorties13,74→13,79, favori54,55→54,55 %. Graine17 indépendante : buts2,56→2,60, penalties.16→.20, touches49,61→49,51, sorties13,40→13,37, favori61→57 %. Calibration encore incomplète : sorties basses, faible précision statistique des penalties, ligne haute à poursuivre. Aucun surajustement pour atteindre exactement .20 sur une graine.
- Causes UI externes identifiées dans les journaux de Claude : recrutement film/ui-recruitment-depth-1 ne compile pas, ScoutingWorkspaceUI.cs117/118 utilise Position.Absolute masqué par la méthode TouchlineApp.Position ; qualifier UnityEngine.UIElements.Position. Runtime-cam-bench2 :107/112, échecs limités à StadiumAtmosphereTests (banc compté comme public vide,53992 vertices pour limite53000, vêtement à hauteur8,008 pour borne8). Ne pas intégrer ces lots comme validés.
- La branche recrutement de Claude prépare huit ligues fictives, rapports détaillés et clubs rivaux ; lire ses fichiers au lieu de refaire cette extension. Toujours aucune fusion main/test matériel/nouvelle APK ; deux captures UI externes restent actives.

### Tempo — exécution des passes (travail non livré)

- `fix/tempo-pass-execution`, base moteur PR #49. Le terme `tempo*2` de dispersion devient une incertitude de base de 1 m avant pondération (valeur historique à tempo .5). La cadence, la vitesse du ballon, le ciblage du partenaire en mouvement et les difficultés de réception ne changent pas. Six tests symétriques passe/profondeur/renversement échouent avant, passent après ; 825/825 Core sur cette branche (sans les dix tests de direction de la PR #50).
- 200 matchs graine 1 avant/après, rapports `tempo-execution-*` : favori 60,61→54,55 %, buts 2,55→2,46, penalties .14→.17, touches49,54→49,92, sorties13,87→13,74, passes76,34→76,21 %. Buts un peu sous la cible ; pas de rééquilibrage terminé ni d'effet causal établi sur les victoires avec seulement99 favoris.
- Audit rythme 400 matchs avant +400 après : écart de tirs rapide−patient −1,43±.78→−.24±.74 (non concluant après), buts pour −.07±.22→+.03±.23 (non concluant dans les deux cas), réussite passes −3,26→−2,76 points. Un jeu rapide n'est pas une garantie d'efficacité et conserve ses risques physiques de contrôle.
- Prochaine cause identifiée : `ResolveCarrierPressure` interdit toute faute dans la surface (`!ownBox`), alors qu'une prudence accrue devrait réduire ce risque, pas créer une immunité. À traiter séparément, puis contrôler une graine indépendante.
- Management dans PR #50 (`feat/board-season-objectives`) : objectifs persistants et carte Carrière, dix tests, total829. Ne pas perdre cette branche lors de l'intégration. Rendus externes de Claude encore en cours ; pas de nouveau Unity lancé. Planche `cam-bench2-a2-night` relue : terrain et acteurs visibles, acteurs encore petits ; rendu logiciel 15s/450images, pas de mesure Fold.
### Direction — objectifs saisonniers (branche non livrée)

- `feat/board-season-objectives`, basée sur `tune/match-balance-diagnostics` / PR #49. Objectif sportif fixé une fois par club/division/saison : rang économique 60 %, rang de l'effectif 40 %, pondéré par les matchs restant à jouer pour une arrivée après cinq rencontres. Politique de simulation explicitement étiquetée, pas une déclaration réelle du propriétaire. Plafond salarial au moment de l'accord conservé comme référence, sans modifier le plafond financier actuel.
- `ReviewBoardObjective` remplace le classement attendu recalculé chaque mois. Premier jugement après cinq matchs ; variation sportive plafonnée à cinq points par mois, variation réellement appliquée indiquée, appels répétés bloqués pendant 30 jours. Licenciement et offres d'autres clubs existants conservés ; l'ancien entraîneur ne reçoit pas de nouveaux bilans. Objectifs des anciens clubs et vingt saisons conservés.
- 829/829 tests Core, dont dix nouveaux tests indépendants de la fixture ProfessionalTests exclue du runner. Migration null, sauvegarde, nouveau club/nouvelle saison, arrivée tardive, plafond de confiance et absence de gains par répétition vérifiés.
- Deux saisons passives avec Marseille, 92 matchs à scores simplifiés : sauvegarde/rechargement identique sur 23690 joueurs, format complet 90,75 Mo / compact 7,61 Mo. Club non recruté : 16 joueurs après expirations, un seul club sous 18 ; ne pas présenter cet essai comme une carrière bien gérée ou comme deux saisons jouées par le moteur complet. Ajustements ultérieurs des seuls bilans (variation réellement appliquée, arrivée tardive) couverts par les tests, pas encore par un nouveau passage long.
- Nouvelle carte `board-season-objective` dans Carrière ; scénario natif `film/ui-board-focus-*` préparé (objectif + bilan après six défaites synthétiques, formats plié/déplié). Compilation Unity, captures et revue visuelle en attente de la file externe de Claude. Aucune nouvelle APK ni fusion dans main. Quota consulté : 70 % hebdomadaire consommé, aucune réinitialisation utilisée.

### Suite de Claude — séparation ligne / pressing (travail non livré)

- Branche `tune/match-balance-diagnostics`, base `integ/0.60` / c3fda366. Main non fusionnée ; test Fold toujours absent. Le nouvel ordre utilisateur autorise les travaux indépendants du test matériel.
- `MatchMovement` : le seuil d'engagement dépend de l'intensité du pressing, plus de la hauteur de ligne ; celle-ci conserve son effet sur le placement. Quatre des huit nouveaux scénarios échouaient avant, les huit passent après, deux équipes couvertes. 819/819 tests Core (808 existants + 8 scénarios + 3 tests statistiques hors Unity).
- Rapports `tools/Calibration/Reports/pressing-separation-*` : mêmes 200 matchs, graine 1. Buts 2,71→2,55 ; touches 50,43→49,54 ; favori 63,64→60,61 % ; sorties de but 13,54→13,87 ; penalties 0,19→0,14. Ce correctif logique ne constitue pas une calibration terminée : penalties dégradés, ligne haute encore trop favorable, tempo non modifié. Audit ligne 400 matchs avant + 400 après : avantage buts pour 0,91→0,74, buts contre −0,91→−0,87.
- xG : estimation géométrique pré-tir distincte du calcul physique des buts ; écart moyen buts−xG 1,01 ±0,21 avant, 0,89 ±0,21 après (IC95 apparié approximatif). Ne pas multiplier arbitrairement les xG pour masquer cet écart. Favoris : 99 cas seulement, IC95 Wilson avant [53,82 ;72,44] %, après [50,76 ;69,66] %, sans prétendre que le changement de taux est statistiquement établi.
- Unity à valider après les jobs de Claude : 37900269212, 37898658725, 37898432737. Pas de nouvelle instance lancée. Ses commits figés ne sont pas modifiés par ce travail local. `film/cam-bench2-goal` contient déjà caméra/ergonomie/bancs/public/rambardes et arrondi mensuel aux montants ronds ; éviter de refaire ces fichiers. Nouvelle APK non construite.
- Suite : comparer rythme patient/rapide et traiter les risques contextuels de passe ; calibrer pénalties et ligne haute sans surajuster une graine ; approfondir objectifs de direction, demandes de départ, presse et actualités. La base réelle reste 21815 joueurs /715 clubs, pas 1434/45.
### Reprise de la finition de Claude (non livrée)

- Branche `fix/claude-visual-validation`, basée sur `film/cam-bench2-goal` f5431af (caméra portrait52°, ergonomie, salaires, bancs et tribunes). Ne contient pas encore les corrections moteur PR49/51/52 ni les objectifs PR50. Expérience PR53 explicitement exclue de toute livraison.
- Claude : rendu de nuit37900271544 réussi, planche relue ; runtime37900265486 échoue107/112 : les bancs étaient comptés comme supporters même à occupation nulle, triangles53992>53000, silhouettes debout au-delà de8m. Ce n'était pas cinq erreurs de compilation.
- Correction : API Crowd conserve le public seul ; CrowdWithBenches assemble public + bancs dans le même mesh/sept matériaux pour MatchArena. 13 sièges au lieu de14 dans chaque bloc latéral (108 silhouettes de fond de moins) pour financer les nouvelles poses et bancs sans relever le budget53000 triangles. Test renforcé occupation0 : bancs toujours présents, aucun public, budget commun maintenu. Borne verticale des spectateurs relevée de8 à8,5m pour les hanches debout rehaussées de0,49m.
- 818/818 Core sur cette branche (808 initiaux +10 conversions de salaire). Nouveau test stade natif à lancer. Arrondi de Claude conserve la semaine stockée et les mensuels multiples de10 ; les montants quelconques restent approximés à la précision hebdomadaire. Pas de nouvelle donnée de contrat prétendue exacte.
- Compilation/tests natifs et captures à valider avant de déclarer ce lot prêt. Le téléphone n'est pas connecté ; aucune performance Fold revendiquée. Recrutement Claude bc55851 à reprendre séparément : deux CS0119 Position.Absolute à qualifier dans ScoutingWorkspaceUI.

### Livraison 0.60 — état consolidé du 9 octobre

- Sources intégrées `0a398c2a18a77b78d2cd145a0b35cb3ef013f7b5`, build sur `43d852b50b62f67eb4ae7df3607a43df2d4f1376` (documentation seulement ensuite), `release/0.60`, version `0.60.0-preview.1` / code 53. PR #37–#46 intégrées, aucune fusion automatique dans main. Cette section prévaut sur les essais historiques ci-dessous. PR de livraison #47, à ne pas fusionner en remplacement des lots individuels.
- **808/808 Core ; 98/98 tests Unity ciblés** caméra/stade/tenues/filets (Actions 37879797631, source 2771e61). Changements ultérieurs : scénario Editor, workflow et documentation uniquement. Aucun warning/error CS nouveau ; toute la suite EditMode n'a pas été exécutée.
- Calibration finale : 200 matchs, graine 1, totaux deux équipes : 2,71 buts / 24,18 tirs / 7,52 cadrés / 9,46 corners / 20,43 fautes / 3,86 jaunes / 4,19 hors-jeu / 50,43 touches / 0,19 penalty / 76,26 % de passes réussies. Rapport symétrique SHA256 `9CD638A1B3FA80826838704CF25DE5C3F6E0C8195233F75D76C2919CEC5CED27`. Audit tactique 2800 matchs versionné : ligne haute trop avantageuse, tempo rapide défavorable, favori 63,64 % de victoires, xG sous-évalués. Pas de retuning de dernière minute.
- Carrière : `SeasonSim 10 176 --world --report --worldseed 77 --savecheck`, dernier passage sur ee663cc. 384 clubs jouables, aucun sous 18 joueurs, 97 % entre 22–30 hors départs annoncés ; quatre dépassent 40 avant ces départs. Niveau moyen 64,3→64,4 ; salaire médian 2450→3135 EUR/semaine. Rechargement identique carrière + 34619 joueurs ; 119,94 Mo complet / 20,65 Mo compact. Diagnostic mondial par scores simplifiés, pas dix saisons de matchs complets ; temps de sauvegarde mesurés sur PC seulement.
- Recrutement : 18 captures initiales (37875566265), 10 finales (37880450007), quatre ciblées de défilement (37882664497), relues dans les deux formats. Deux assertions ciblaient le haut de carte au lieu du bouton Fiche ; scénario corrigé dans Editor, puis réussite. Certains petits libellés et la navigation globale restent sous les cibles de confort.
- Prêts : six captures (37887026578), fiche/clauses/ancien accord bloqué, relues dans les deux formats. Fixture synthétique isolée (Kondogbia/Rayo), pas une donnée réelle. Les captures UI conservent une exception de démarrage UnityEditor.Search préexistante, sans trace Touchline.
- Visuel : film natif 24 s, 720 images, deux caméras, but puis ralenti et retour au direct (37883579670, source ee663cc), planches relues ; branche films, dossier `release060-goal-night`. Filet : test central 0,0488 m, zone distante immobile, retour au repos sur les deux buts. Film Mesa à pas fixe ; métriques mêlant direct et relecture, aucun diagnostic de FPS Android.
- Base : 21815 joueurs / 715 clubs / 36 ligues inchangés. 45 clubs et 1434 joueurs existants deviennent correctement ciblables par territoire ; sources UEFA/SFL, pas de nouveaux effectifs ni de salaires prétendus réels. 273 libres à la création ; portraits toujours absents.
- Les validations de recrutement et contrats sont terminées. Aucun nouveau lot ; Assets figés pendant Unity. Téléphone non connecté, performances et ergonomie matérielles non vérifiées. Les films avant/après de trajectoire imposée restent distincts des tests caméra unitaires et du film de match.
- Mise à jour 07:45 : négociation native réussie (Actions 37887965453, source 0a398c2), huit captures relues. Salaire nul et indemnité négative refusés sans perte des deux brouillons, rôle/prime conservés ; offre corrigée créée exactement une fois sans débit. Fixture financière synthétique. Conversion salaire mensuel→hebdomadaire entier→mensuel pouvant afficher 10001 EUR pour 10000 saisis : limite d'arrondi conservée. File de films arrêtée volontairement après ces contrôles ; priorité au build 0.60, diagnostics caméra comparatifs restants non exécutés.
- **APK 0.60 construite et vérifiée à 08:06** : Actions 37890028910, artefact 11598592932 ; `artifacts/Touchline-Unity-0.60-preview.apk`, 73122936 octets, package `fr.personal.touchline.unity`, ARM64 Unity/IL2CPP. SHA256 `D3F66AC9AAAAA2E19B63092712627D49C559EB3F3133FA6050556509AC082564`. Certificat SHA256 `130917e6d2b4ea2dcca487587dcff03b1356b14e7de39c3e01657097ad57ee13`, identique à 0.59. Ancienne APK intacte, SHA256 `9629488FFE63E2C47EB67B57038E91F8BCCB5B6668851D96A9FE9BD799C45DD6`. Aucun téléphone ADB connecté ; aucune installation matérielle prétendue.
- Après le build, diagnostic caméra portrait **37891803454 réussi** sur 0a398c2 (Assets identiques à l'APK) : 8 s / 240 images, ballon dans la zone utile à chaque image ; déplacement maximal 0,778 m/image à 30 i/s. Planches et image native relues. Ballon imposé, joueurs figés : ce n'est pas une simulation naturelle ni une mesure des FPS. Vue portrait encore très plongeante, joueurs petits ; compromis distance/zoom à travailler. Comparaisons natives avant/après et diagnostic paysage non exécutés, 37 tests caméra passés et film normal disponibles.
- Tous les lots sont arrêtés, APK livrée et preuves consignées. Pour la suite : isoler les causes du gain de ligne haute et du tempo (hauteur, seuil de pressing, cadence, vitesse et dispersion séparément), revoir petits textes/flèches secondaires et arrondis mensuels, vérifier sources des évaluations/salaires, profiler sur Fold. Ne pas retuner uniquement pour faire rentrer des moyennes dans les bornes.
- Le dossier principal est maintenant sur `release/0.60`, avec tous les changements intégrés ; `.worktrees/release060` est détaché et conservé. Les commits après le build ne changent que HANDOFF. Aucun APK, keystore ni fichier généré Unity n'est suivi par Git.
- Session close le 9 octobre à 08:30 Paris ; automatisation `touchline-am-liorations-jusqu-au-6-octobre` passée à PAUSED et état vérifié. Aucun job Unity restant. Dernier quota lu à 08:10 : 65 % hebdomadaire utilisé, sans achat ni utilisation du crédit de réinitialisation.

### Historique des lots (statuts intermédiaires)

- Lot `fix/negotiation-form-feedback` : erreur contractuelle affichée dans le formulaire conservé (salaire, prime, rôle, prêt), avec défilement/focus vers le message. Le succès conserve ApplyLife/Save/Build ; aucun changement Core. Harness `ui-negotiation-focus-*` ajouté à CiUiScreens : salaire nul, indemnité de prêt négative, absence de débit/offre sur refus, conservation des deux brouillons et renvoi corrigé ; variante `-before` pour reproduire la destruction initiale. Validation Unity/captures à lancer par le coordinateur ; aucun test Fold matériel revendiqué.

### Branches et PR
- **Session autonome du 9 octobre, jusqu'à 08:30 Paris** : recrutement sur `feat/recruitment-hub` (basée sur #37), caméra dans `.worktrees/match-visual` / `fix/match-visual-readability`. PR #38 `feat/visual-polish` découverte, déjà validée par son auteur ; éviter de refaire ciel/contre-jour/montants/tribunes. Ne pas fusionner automatiquement. Réserve APK à 07:15 au plus tard ou quota hebdomadaire 90 % utilisé. Quota lu à 02:55 : 13 % utilisé.
- Lot local `fix/match-visual-readability` : correction ciblée des sauts de caméra télé aux seuils du cadrage du but ; compilation/captures Unity à réaliser avant livraison. Complément à #38, sans refaire son éclairage ni son ciel.
- **`fix/offer-club-context`**, base `fix/loan-contract-ownership` : les discussions de transfert sont conservées pour leur destination d’origine lors du départ du manager (anciennes offres sans destination rattachées avant le changement). Réponses, notifications, retraits, clauses, doublons et tentatives sont isolés par club ; les précontrats signés restent exécutables pour l’ancien club. Exigences de rôle et attractivité utilisent `rating + development`, sans modifier les seuils économiques. Avant correction : 11 échecs sur 12 nouveaux cas ; après : 719/719 tests Core réussis. Unity à valider avant livraison. Aucun changement du moteur de match, du calendrier, de Runtime ou d’Editor.
- **`fix/loan-contract-ownership`**, base PR #37 : correction du prêt entrant acquis gratuitement par « prolongation ». Proposition et signature vérifient les droits du prêteur ; anciens accords sauvegardés protégés, parent vide compatible, option d’achat payante conservée. Fiche, négociation et dossier orientent vers le contrat de prêt. Avant : 5 échecs sur 8 nouveaux cas ; après : 707/707 tests Core réussis, dont restauration et paiement de l’option. Compilation Unity et revue visuelle à effectuer avant livraison ; aucune modification du moteur de match.
- Lot `fix/defensive-pass-anticipation` / PR43 : gardes symétriques défenseurs et destinataires avant contact, 60 régressions dédiées, 759 tests Core réussis sur le lot et 808 sur le candidat 0.60 intégré. Calibration finale de 200 matchs identique au rapport symétrique : 2,71 buts / 24,18 tirs. Audit final de 2 800 matchs versionné ; ligne haute encore trop avantageuse, tempo rapide défavorable dans cet échantillon et largeur étroite produisant beaucoup de tirs sans gain mesurable de buts. Validation Unity et livraison finales restent à confirmer par le coordinateur ; détails en fin de fichier.
- **PR #34, #35 et #36 fusionnées** : main `ae2df218` contient la pile intégrée, les tenues et l'ambiance.
- **PR #37 `feat/tactical-audit` → `main`** : consignes en deux touches et audit tactique. 699 tests Core réussis ; compilation/captures Unity ciblées validées, revue visuelle faite, test du rapport carrière réussi. APK 0.59/code52 construite et vérifiée, téléphone non vérifié.
- Git HTTPS fonctionne sur ce PC. Le connecteur GitHub renvoie encore 403 en écriture ; utiliser Git pour les branches et REST avec les identifiants Git en mémoire pour la PR, sans exposer de secret.
- **PR `feat/visual-polish` → `main`** : ciel en dégradé (`Runtime/StadiumSky.cs`, dôme qui suit la
  caméra, coupé en qualité basse), ambiance trois tons, ralenti du soir éclairé de face (projecteurs
  réorientés pendant le ralenti), montants de but ronds (`StadiumGeometry.GoalFrames`), filet gris
  clair, second anneau qui réagit aux buts du club. Films `vis-*`.
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

- Valider le lot caméra `fix/match-visual-readability` : dix nouveaux cas EditMode dans BroadcastFramingTests, films `camera-crossing-before/after` et `camera-crossing-portrait-before/after` (même harness sur les deux versions), puis courte séquence réelle jour/soir. Le mode diagnostic fige les joueurs et impose le ballon : ne pas le présenter comme une preuve des décisions ou animations du match.
- Candidat anticipation symétrique : valider Unity et examiner les réserves (touches 50,43, penalties 0,19, sorties de but 13,54, favori 63,64 %) avant intégration. Les chiffres dégradés de la défense seule sont historiques ; le complément destinataires ramène buts et tirs dans les bornes sans coefficient compensatoire.

1. **#37 : validation locale et Unity terminée** (captures finales Actions 37855587630). APK 0.59/code52 construite et signature vérifiée. Faire tester Victor (gels, fluidité,
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

### Territoires de recrutement — 9 octobre 2026
- Branche fix/scouting-territories, basée sur le centre de recrutement : helper Core ScoutingGeography partagé par le marché et les missions. Le pays du championnat reste prioritaire ; les clubs hors catalogue exigent un pays, une URL et une date de source valides.
- 45 clubs corrigés/documentés, dont Winterthur (pays déjà Suisse), 1 434 joueurs rendus accessibles aux recherches territoriales ; 44 pays auparavant remplacés par le nom du club. Sources UEFA et SFL consultées le 09/10, preuve structurée tools/DataQuality/club-country-evidence.json. Aucun nouveau championnat jouable.
- JSON modifié uniquement dans 45 blocs de clubs : 21 815 joueurs, 715 clubs et 36 ligues conservés ; section joueurs et suivants identique en octets, autres propriétés des clubs inchangées. Aucun salaire, note, effectif ou calendrier réécrit.
- 728/728 tests Core réussis après intégration des derniers correctifs du hub ; contrôle de la base désérialisée : 45 clubs / 1 434 joueurs accessibles, 53 territoires, 36 ligues conservées. Compilation/revue Unity attendues ; pas de test matériel Fold revendiqué. Prochaine vérification : marché Norvège et mission territoriale aux deux formats, puis build APK par le coordinateur.
- Reprise 03:58 : film tiers 37866788266 terminé en échec (aucune validation de nos sources) ; aucune autre CI active. Développement dégelé. Hub : alertes alignées sur la possibilité réelle de réobserver, contre-propositions récentes et club courant, avis salarial déduit des engagements. 719/719 tests Core ; capture des pistes après défilement ajoutée. Prêts et territoires sur worktrees séparés en cours, nouvelle validation Unity attendue.
### Lot caméra Codex du 9 octobre 2026 — seuils du cadre de but
- Fait : inclusion progressive du but dans le cadrage sur dix mètres avant les seuils existants ; transitions latérales portrait sur six mètres. Le cadrage du ballon et des couloirs de passe reste une borne minimale, le but est entièrement inclus là où il l'était auparavant. Aucun Core, joueur, animation, lumière ou matériau modifié ; budget triangles/draw calls/textures inchangé, aucun objet alloué par image par cette correction.
- Diagnostic géométrique indépendant, pas un rendu Unity : sur une trajectoire x23→33 m avec regard quatre mètres derrière, pas de 0,1 m, saut maximal paysage 1,32 de 25,44 à 0,58 m et portrait 0,43 de 15,67 à 0,43 m. Seuil de largeur du regard en portrait : 38,33 à 0,71 m. Ces mesures appliquent les formules du cadrage, sans le lissage d'arène.
- Vérifications préparées : dix cas EditMode (deux côtés, aller-retour, pas 0,1 et 0,5 m, seuils portrait), conservant la visibilité du ballon et des deux montants. CiMatchFilm reconnaît `camera-crossing` : diagnostic huit secondes à 30 i/s, ballon x20→36→20 imposé, simulation figée, caméra réelle de MatchArena ; affiche déplacement maximal et contrôle ballon visible. Suffixe `portrait` pour 540×1260, sinon 960×728. Mesures d'animation remplacées par un avertissement explicite dans ce mode.
- Non vérifié à ce stade : compilation Unity, exécution EditMode, vidéos avant/après, matériel Android. Aucun APK ni test de fluidité matérielle revendiqué. PR #38 traite déjà le contre-jour des ralentis, pas de doublon ajouté ici.

### Candidat 0.60 du 9 octobre, figé à 04:07 Paris
- PR #39 recrutement (028eba3, 720 Core), #40 caméra (45736c0), #41 droits des prêts (8504337, 707 Core), #42 territoires (3c8945c, 728 Core). #41 et #42 sont des brouillons attachés. Les références UI/caméra sont prêtes mais pas encore filmées. Aucune PR fusionnée dans main.
- Intégration locale .worktrees/release060 / release/0.60, commit sources 6961508983b14043b68db1328333769fd9c8eadd, puis ffa4190 (workflow seul). Contient aussi le rendu #38. 736/736 Core, preuve .validation/release060-core-tests.txt. Version préparée 0.60.0-preview.1/code53 ; branche release non poussée, aucun APK 0.60 construit.
- Tests Unity 37873052957 ont échoué AVANT démarrage : game-ci v4.4 transmet noCoverageEnabled au CLI v0.1.72 qui rejette cet argument. Aucun résultat NUnit. Lanceur épinglé à v4.3.2/fa6ced2 selon sa release officielle ; relance unique 37873266419 sur test/runtime-release060 / ffa4190, en cours. Assets gelés dans tous les worktrees jusqu'à fin de validation ; root seul lance Unity.
- Tableau de recrutement : observation stale alignée sur Knowledge < 90 (pas de fausse alerte jours 121–126 ou attributs révélés), dernières contre-propositions et droits de prêt respectés ; avis salarial déduit des salaires réservés. Prêts : 5 échecs sur 8 nouveaux cas avant correctif, tous passent après, option d'achat paie le propriétaire, ancien accord restauré ne peut l'effacer.
- Géographie : 45 clubs / 1 434 joueurs rendus ciblables sans ajout de joueur ou ligue ; 53 territoires, 715 clubs, 36 ligues, 21 815 joueurs bruts inchangés. Sources UEFA/SFL dans tools/DataQuality/club-country-evidence.json ; date de consultation distincte de publication. Capture candidate vérifie Norvège puis mission préremplie sans débit.
- File de validation : finir tests Unity caméra (37 cas attendus), puis références/candidats camera-crossing (portrait + paysage), référence recrutement et candidat intégré avec territoires, parcours ui-loan-focus. Regarder les images et assertions avant APK. Stop nouveaux lots à 07:15 ou quota 90 %, réserver build/livraison avant 08:30. Dernier quota lu 25 % utilisé vers 03:41. APK 0.59 reste disponible et préservée.

### Validation intégrée du 9 octobre — 04:46 Paris
- Tests caméra réellement exécutés : Actions 37873266419 sur ffa4190, 37/37 réussis dans Unity, zéro échec/ignoré ; aucun warning CS/error CS dans le journal. Preuves locales .validation/runtime060. Les autres tests Runtime ne sont pas couverts par ces 37 cas ; extension au stade/tenues préparée dans le workflow.
- Référence recrutement : Actions 37873951586 réussie, quatre captures natives plié/déplié relues. Filtres tronqués et petites cibles observés ; CSS corrigé dans d8670c1, intégré puis candidat 1f4a96d lancé sur film/ui-recruitment-focus-after (Actions 37875566265). Validation candidate en cours, pas encore annoncée réussie.
- Carrière IA : SeasonSim 10 176 --world --report --worldseed 77 --savecheck terminé en 213 s, année 2036 atteinte. 384 clubs jouables, aucun sous 18 joueurs ; quatre au-dessus de 40 avant départs, 97 % dans 22–30 après départs annoncés. Niveau moyen monde 64,3→64,4 ; salaire médian 2 450→3 135 euros/semaine. Sauvegarde complète 119,94 Mo, compacte 20,65 Mo ; rechargement identique de la carrière et des 34 619 joueurs. Test .NET, pas Unity/Android. Les matchs sont tirés par le modèle mondial, pas tous joués par le moteur. Le compteur historique des transferts plafonne à 3 000 entrées, ce n'est pas un arrêt du mercato. --savecheck retourne avant --dump : aucun dump produit par cette exécution. Preuve .validation/release060-world10.txt.
- Correctif d'anticipation défensive c315999 publié séparément en PR #43, EXCLU du candidat : deux gardes suppriment la connaissance de la destination avant contact (36 cas, 24 échouaient avant ; 735 Core passent). Mais les mêmes 200 matchs passent de 2,83 à 3,25 buts et de 26,84 à 29,78 tirs, favori 67,68→74,75 %. Ne pas le fusionner sans nouvelle correction défensive et calibration ; rapports versionnés dans sa branche.
- Audits lecture seule : erreur de négociation détruit le formulaire au lieu de conserver la saisie ; exigences de rôle ignorent development ; réponses aux offres antérieures utilisent le nouveau club du manager. Reproductions locales .validation/management-review060. Correctifs non encore appliqués à cette heure. Assets gelés pendant le tournage.
- ADB : aucun téléphone connecté. Aucun test matériel de fluidité, chauffe ou restauration après interruption revendiqué. Quota lu à 04:38 : 36 % utilisé ; réserve build inchangée (07:15 ou 90 %).
### Lot Codex du 9 octobre 2026 — anticipation défensive avant contact
- Fait sur `fix/defensive-pass-anticipation` : `DefensiveFocus` n'utilise la destination finale que si `ball.elapsed >= 0` ; même garde pour la branche `attack-cross` de `MatchMovement`. Avant contact, le foyer suit le ballon visible. Après contact, le comportement précédent reste disponible. Aucun autre tuning, aucun Runtime ou animation modifié.
- Preuve de défaut : 24 échecs sur 36 nouveaux cas avant correction (les 12 cas après contact passent déjà). Foyer déplacé de 13 m et cibles défensives déplacées de 12,626 à 20 m en modifiant seulement la destination future. Après correction : 36/36, suite Core complète 735/735. Tests sur passe, profondeur, centre, deux équipes, deux périodes ; bornes -0,18/-0,001 avant contact et 0/+0,12 après contact. Fichiers locaux ignorés `.validation/anticipation-before.txt`, `anticipation-after.txt`, `core-after.txt`.
- Calibration : même commande `tools/Calibration -- 200 1`, même fichier `release060/.../Resources/Data/database.json`, SHA256 8400910DA9EB2AED5155D8AE760B3983C0F3168072008593BDA152BBCD1BC1AA. Rapports immuables versionnés `tools/Calibration/Reports/defensive-anticipation-before-200-seed1.txt` et `defensive-anticipation-after-200-seed1.txt`. Référence fournie par root, SHA256 01E719A69FA3914D9E0689832FFAF9FA7B80FFCE00DDB651818D86E70F74624C. Deux séries de 200 matchs, zéro erreur.
- Résultats avant → après : buts 2,83 → 3,25 ; tirs 26,84 → 29,78 ; cadrés 8,42 → 9,46 ; corners 8,88 → 8,24 ; fautes 22,52 → 21,35 ; jaunes 3,96 → 4,10 ; penalties 0,17 → 0,23 ; hors-jeu 3,73 → 3,87 ; touches 36,88 → 36,80 ; passes réussies 81,82 → 81,74 % ; favori net 67,68 → 74,75 % de victoires.
- Limites : la correction supprime une information future avant le contact, mais n'améliore PAS l'équilibre agrégé de cet échantillon. Buts et tirs dépassent les bornes ; avantage du favori renforcé. La destination exacte reste utilisée après départ, pas de nouveau modèle de perception/réaction. Aucun retuning ajouté pour masquer ces effets. Compilation Unity, revue visuelle et APK non réalisées pour ce lot ; ne pas intégrer automatiquement à 0.60.
- Prochaine étape : revue du coordinateur et décision sur un lot de rééquilibrage distinct, avec comparaison appariée et contrôle de la ligne haute. Conserver ce candidat et ses preuves séparés en attendant.

### Complément Codex du 9 octobre 2026 — réception symétrique avant contact
- Fait : garde `elapsed >= 0` sur les deux appels destinataires de `ReceptionTarget` dans `MatchMovement` (joueur de champ et gardien). Avant départ, le joueur conserve son placement tactique déjà calculé et le gardien son placement face au ballon visible. Aucune modification de finition, seuil statistique, vitesse ou coefficient.
- Preuve : 24 nouveaux cas échouent avant correction (cible différente de 20 m), puis passent. États jumeaux avec mêmes positions/vitesses/angles et seule destination finale différente ; passe, profondeur, centre, deux côtés et périodes, gardien/joueur de champ, temps -0,18/-0,001/0/+0,12. Ensemble anticipation 60/60 ; suite Core 759/759. Preuves locales ignorées `.validation/receiver-expanded-before.txt`, `receiver-after.txt`, `core-symmetric-after.txt`.
- Calibration complémentaire, sans écraser les précédentes : `tools/Calibration/Reports/defensive-anticipation-symmetric-after-200-seed1.txt`, 200 matchs graine 1, zéro erreur. Même base release060 SHA256 8400910DA9EB2AED5155D8AE760B3983C0F3168072008593BDA152BBCD1BC1AA.
- Base → défense seule → correction symétrique : buts 2,83 → 3,25 → 2,71 ; tirs 26,84 → 29,78 → 24,18 ; cadrés 8,42 → 9,46 → 7,52 ; corners 8,88 → 8,24 → 9,46 ; fautes 22,52 → 21,35 → 20,43 ; jaunes 3,96 → 4,10 → 3,86 ; passes réussies 81,82 → 81,74 → 76,26 % ; favori 67,68 → 74,75 → 63,64 % de victoires. L'asymétrie du receveur était donc matériellement importante dans cette comparaison, sans prouver à elle seule une causalité générale pour tous les matchs.
- Réserves actuelles : touches 50,43 (cible 35–50), penalties 0,19 (0,20–0,35), sorties de but 13,54 (14–20), favori 63,64 % (45–55 %) ; xG 1,70 pour 2,71 buts, écart préexistant d'estimation non corrigé. Volume passes 816,72 et précision 76,26 % restent dans les bornes mais proches du bas. Aucun retuning supplémentaire. La destination exacte reste exploitable après contact ; aucune nouvelle perception probabiliste ajoutée.
- Statut : Assets gelés après ces mesures, pas de Runtime/Editor ni Unity/push. Ce complément remplace la conclusion de calibration défavorable de la version défense seule, mais ne vaut pas validation Unity ou autorisation automatique d'inclure PR43 dans l'APK. Coordinateur décide après revue.
- Finition recrutement après captures : cinq onglets visibles sans défilement horizontal, textes secondaires ≥12 unités, raison d’un plafond salarial nul affichée près du bouton d’envoi, flèches de listes éclaircies. 720/720 Core ; nouvelles assertions UI de visibilité et activation préparées, capture finale encore requise. Aucun changement des dépenses de mission.

### Audit final du candidat 0.60 — anticipation symétrique et consignes
- Les sorties finales du coordinateur sont archivées sans changement dans `tools/Calibration/Reports/symmetric-tactics-200-seed1.txt` et `.csv` : 7 consignes × 200 affiches appariées × 2 variantes = 2 800 lignes de matchs. SHA256 du rapport texte `5B92EBB041697332A99821E239BCF4B806C339E89C6298485C0F6509DE73DA23`, du CSV `F28AA51897AC269FBF30FC675A33B19B63FF6023DB3196B86213CFD7EF7210F2`. Ces mesures concernent le club dirigé à domicile, avec IA adverse active ; elles ne sont pas des totaux des deux équipes.
- La calibration finale du candidat intégré, 200 matchs graine 1, est identique octet par octet à `defensive-anticipation-symmetric-after-200-seed1.txt` : SHA256 commun `9CD638A1B3FA80826838704CF25DE5C3F6E0C8195233F75D76C2919CEC5CED27`. La suite Core finale du candidat intégré annonce 808/808 tests réussis ; les 759 tests précédents restent la mesure propre au lot PR43. Aucun nouveau match simulé ni coefficient modifié pour ce compte rendu.
- Ligne basse → haute : position moyenne −33,61 → −19,39 m, tirs 10,43 → 21,02, buts pour 1,31 → 2,22 (+0,91 ± 0,26) et contre 1,90 → 0,99 (−0,91 ± 0,21). L'avantage reste trop fort dans cet échantillon ; la correction d’information future ne constitue pas un rééquilibrage de la ligne haute.
- Tempo patient → rapide : passes 427,23 → 399,04 (−28,19 ± 7,47), précision 77,74 → 74,87 %, tirs 13,64 → 12,53 et buts pour 1,59 → 1,28 (−0,31 ± 0,21). Le volume de passes ne monte pas ; le compromis est défavorable ici. Il reste à distinguer pertes de balle, occupation et sélection des actions avant un éventuel réglage.
- Largeur étroite → large : étalement 34,75 → 54,01 m, tirs 22,72 → 13,19, xG 1,32 → 0,99, buts 1,47 → 1,56. L'écart de buts est non concluant (+0,09 ± 0,24) malgré les tirs supplémentaires en jeu étroit ; xG par tir d'environ 0,058 contre 0,075. Ne pas présenter l'option étroite comme une garantie de buts.
- Limites conservées : intervalles 95 % indicatifs sans correction des comparaisons multiples, protocole à domicile, adversaire adaptatif. Touches 50,43, penalties 0,19, sorties de but 13,54 et favori 63,64 % hors bornes ; xG 1,70 pour 2,71 buts. Aucun bilan ne prouve une parité FM ou la fluidité sur téléphone.
- Prochaine étape : coordinateur confirme les validations Unity et la livraison 0.60 ; conserver ces preuves pour un lot ultérieur de compromis tactiques, sans retuning opportuniste pendant la livraison. Brouillon de description PR43 préparé localement dans `.validation/pr43-final-body.md` (non versionné).

### Correctif Codex du 9 octobre 2026 — contact au centre du filet
- Défaut rapporté par la suite Unity intégrée : 94/95 tests réussis ; déplacement central du filet nul. Les fils avaient uniquement des sommets aux extrémités, hors de la zone d'impact centrale, malgré leur passage visuel sous le ballon.
- Correction ciblée : `StadiumGeometry.GoalNet` utilise des fils tubulaires carrés continus à anneaux partagés. Subdivision aux intersections des mailles existantes (25 colonnes, 8 rangs, 6 profondeurs ; intervalles ~0,30 m). Aucun bouchon entre segments ; GoalNetRipple, rayon 1,1 m, ressort et amortissement inchangés.
- Budget calculé à partir des boucles : par but 94 fils, 902 segments, 3 984 sommets / 7 216 triangles, contre 2 256 / 1 128 auparavant. Pour les deux buts : +3 456 sommets / +12 176 triangles, zéro nouveau draw call, matériau ou texture. Le traitement dynamique parcourt davantage de sommets pendant les mouvements du filet seulement ; coût nul au repos conservé. Aucun benchmark Android revendiqué.
- Tests : l'ancien test de déformation/zone distante fixe/retour au repos couvre désormais les deux buts. Deux cas supplémentaires vérifient les arêtes <0,35 m et le budget <4 500 sommets / <7 500 triangles par but. Mesures de déplacement et comptage écrites dans TestContext. La suite intégrée devrait passer de 95 à 98 cas. La zone centrale testée reste centrée en (±54,1,0), rayon0,5 m : elle n'inclut ni toit ni filet latéral.
- Validation locale : Core 689/689 réussis sur cette base PR38 ; aucun Core modifié. Diff sans erreur d'espacement. Tests Unity et revue visuelle restant au coordinateur, aucun Unity/push lancé ici. Approximation indépendante du ressort : déplacement central attendu ~0,049 m, pas présentée comme une mesure Unity.
- Prochaine étape : intégrer le correctif, lancer les 98 tests Unity puis vérifier le film de but de chaque côté avant publication de l'APK.

- Vérification recrutement 37880450007 : dix captures finales relues, onglets/mission/rapports accessibles. Deux assertions de défilement échouent ; le scénario ciblait le haut d’une carte et vérifiait son bouton bas, sans image d’échec. Scénario corrigé pour viser Fiche après restauration différée, capturer avant assertion et journaliser les limites. Aucun défaut de dimensionnement Runtime établi à ce stade ; validation ciblée scroll requise. Unity98/98 sur source intégrée2771e61 ; Core intégré808/808.

- CI film : seule la vue follow est facultative, broadcast/frame-0000.jpg est désormais obligatoire pour un film ; les parcours UI restent exemptés. Aucun Assets modifié. Contrôle réel attendu dans les films suivants.

### Reprise du recrutement de Claude — 9 octobre 2026
- Branche fix/claude-recruitment-validation, reprise de bc55851 (film/ui-recruitment-depth-1), base integ/0.60. Générateur déterministe figé : 116 clubs / 2784 joueurs / 8 ligues supplémentaires, explicitement fictifs, observables mais non jouables ; catalogue total attendu 24599 joueurs / 831 clubs / 44 ligues, toujours 384 clubs jouables. Aucun nouvel effectif réel ni salaire vérifié revendiqué.
- Rapports avec note A–E relative au club, fourchettes dépendant du recruteur, territoire, fraîcheur et répétition ; concurrence de recrutement et alertes de fin de contrat. Réparation des deux collisions Position.Absolute / TouchlineApp.Position qui empêchaient Unity de compiler les barres de rapport.
- Bug reproduit : une ancienne offre rivale pouvait vendre un joueur après arrivée du manager chez le vendeur. Le nouvel acheteur humain pouvait aussi hériter d’une opération automatique. Deux tests échouent avant ; offres désormais caduques si vendeur ou acheteur est le club dirigé. 823/823 Core après correction. Nouvelle validation Unity et captures recrutement requises ; ne pas présenter ce lot comme livré/testé sur Fold.
- Catalogue généré et tableaux fictifs sont des approximations de simulation, sans calendrier jouable ni données réelles supplémentaires. Rendu natif en attente du job visuel 37907040663 ; une seule instance Unity distante à la fois. Main reste conditionnée au test utilisateur de 0.60.

### Management contextuel et assemblage de validation — 9 octobre
- Branche feat/contextual-club-management issue de feat/career-review-integration. Assemblage de #49, #50, #51, #52, #54 et #55, SANS #53 : cette expérience de ligne haute est rejetée (favori 66 % graine17, touches/sorties dégradées). Aucune fusion dans main.
- 875/875 Core après assemblage et neuf nouveaux tests de management. Demandes de départ : 28 jours d’insatisfaction (temps de jeu observé insuffisant, moral <45, confiance <50), exclusion des absents et prêts, aucune vente automatique. Refus unique avec effet de confiance ; retrait si temps de jeu et relation rétablis ; état sérialisé et isolé par club. Les demandes deviennent des messages à décision ; dialogue avec deux réponses.
- Presse : contexte victoire/nul/défaite, seulement deux jours autour du match, une réponse par conférence, effets dépendant de la confiance et du résultat. Carrière : fil des messages direction/presse/offres, demandes de départ, plus large victoire parmi les archives conservées. Ce n’est ni un palmarès réel complet ni une simulation de médias autonomes.
- Finition visuelle #54 validée : Unity113/113, Actions37907040663, sourcef6b9236 ; artefact11605815087, preuve locale .validation/visual-f6b9236-tests. Aucun Fold connecté. Nouvelle suite Unity intégrée et vingt captures des nouveaux écrans à lancer séquentiellement ; scénario ui-career-review-* préparé, carrière synthétique isolée des sauvegardes personnelles.
- Réserve livraison : quota hebdomadaire75 % au dernier contrôle ; aucune réinitialisation consommée. APK0.60 et preuves antérieures préservées. Main toujours soumise au test utilisateur ; aucune APK nouvelle validée pour l’instant.

### Correction du catalogue fictif après validation native
- Unity intégré Actions37908628448 : compilation sans erreur CS, 186/187 tests réussis. Seul échec : empreinte du générateur procédural v1 différente entre .NET (9164199268431662725) et Mono Unity (9886741435214778634). Ne pas supprimer simplement cette vérification : les sauvegardes compactes ont besoin d’une base stable.
- fix/frozen-generated-catalogue : export de référence v1 en Resources/Data/generated-world-v1.json, contrôlé par l’empreinte attendue ; Runtime et SeasonSim chargent ce même catalogue et vérifient son empreinte. Le générateur reste un outil de création, jamais utilisé pour régénérer la base d’une sauvegarde en jeu. IDs et valeurs du catalogue .NET d’origine préservés ; première livraison de ces données, absentes de 0.60. Deux nouveaux tests couvrent corruption/refus atomique et indépendance des chargements.
- ScoutingWorkspace distingue le club fictif de l’identité du joueur : un vrai joueur transféré dans un club fictif ne devient pas une personne inventée, et l’inverse ne transforme pas son nouveau club réel en club fictif.
- Avant ce correctif de chargement : dix saisons monde, graine77, 384 clubs jouables : aucun sous18 ; cinq dépassent40 avant départs annoncés, effectifs hors partants22–31, 98 % entre22–30. Notes moyennes64,3→64,1 ; salaires médians2200→2708/semaine. Sept clubs dépassent70 % salaires/recettes et un a dette nette >100 % recettes : réserves à surveiller, pas d’économie parfaite. Sauvegardes identiques sur39521 joueurs, complet137,52Mo/compact23,84Mo. Scores simplifiés, aucun match 3D joué dans ce diagnostic monde.
- Calibration intégrée200/graine1 identique ligne par ligne à pressure-penalties-after-200-seed1.txt (encodage/fin de ligne différents seulement). Aucune modification moteur depuis #52. Nouvelle validation native et captures obligatoires après chargement figé.
- Complément compatibilité : 879/879 Core. Deux tests dans tools/CoreTests/CatalogueMigrationTests restaurent une ancienne base sans clubs fictifs après extension, en formats complet/compact : composition, calendrier et modifications joueur conservés. Deux saisons avec le catalogue figé : mêmes résultats de carrière, 26905 joueurs rechargés à l’identique, 103,04Mo complet / 8,66Mo compact. Runtime Assets inchangés pendant Actions37910092620.
- Validation native corrigée : Actions37910092620, sourceec844ba (Assets identiques à d911bf4), 189/189 tests Unity réussis ; artefact11606134852. Parcours visuel film/ui-career-review-061-v1 lancé sur d911bf4 après la fin du job, sources gelées. Compilation et unités validées ne remplacent pas la revue des captures ni le test Fold.
