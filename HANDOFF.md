# HANDOFF — Touchline
Lire AGENTS.md. Une tâche/branche/PR, une Unity locale OU CI, aucun Assets modifié pendant compilation/test/capture/build. Aucun nouvel agent. Préserver sauvegardes/APK/pack privé. Ne jamais imprimer database.json (132Mo).

## Demande / état
Six chantiers sans échéance : caméra, IA/tactique, équilibre multi-graines, contacts/clips adaptés, U19 réels,196échecs puis consolidation main. Aucun joueur fictif initial. Automation ancienne PAUSED. Quota55%utilisé ; réserver livraison à95%.
Main non fusionnée (d74e9a9/PR38), drafts empilées PR146–167. PR168/0dc35d9 célébrérations validées ; branche actuelle fix/animation-recovery-observations.

## Preuves retenues
Core1046/1046 inchangé depuis PR154. Dernière globale complète2555/2618 :63anciens échecs,0nouveau,133des196initiaux résolus (certaines fixtures corrigées, ne pas confondre avec bugs du jeu). .validation/captured-upright-contact-global.xml.
PR165/05fc17c contrôles au pied : contact<6cm, appui opposé, pause<3mm,6cas30/60/120Hz, native422images revue. Globale2537/2606,69anciens.
PR166/ad893a1 mains/doigts des chutes : surfaces au-dessus du sol,6cas30/60/120Hz tailles160/205, ciblés69/69, native revue. Globale2545/2612,67anciens. Hypothèse retirer HandPose rejetée, pas retenue.
PR167/368293f torse/bras réagissent à pression latérale sans déplacer appuis/root. Ciblés23/23,6nouveaux, native422images brace-left60/right60 revue. Globale63anciens. Pas collision simulée/Fold.
PR163/dddf2db six vrais jeunes Nantes (Forget,Aiki,Foucan,Dago,Merrifield,Khenissi), supplément22identités, catalogue21837/715clubs. Sources FCN2025–26 + PDF Montaigu2024page4. Notes45/potentiel65/valeur50k provisoires,salaire0inconnu. Ciblés243/243, UI19parcours réels plié/déplié revue. U19 incomplets.
PR146 caméra portrait37tests/6vues améliorée ; confort rapide/Fold non validés.

## Lot actuel célébrations
Charge clip14 en plus13 (8/21actifs), sélection déterministe actionSequence,14 entier ajusté aux3secondes de réaction. Entrée réelle mémorisée : évite démarrage bras levés après frappe tardive ; fondu14=.55s,13=.25s, sortie2.6–3s. Aucun Core changé.
Neuf nouveaux tests30/60/120Hz, rotation<=600deg/s+1parimage,pause<3mm,acteur inchangé. Ciblés43/43 .validation/captured-goal-late-entry-reference.xml. Premier test tardif comparait phases idle différentes, remplacé par témoin même cadence ; seuil3mm strict.
Native captured-goal-variant-native422images terminée0 : goal-standing60, goal-turning60/114 revues ; rotation puis retour debout. Clip12 reste inactif : finit agenouillé, relevé absent. Globale captured-goal-variant-global2564/2627,63anciens échecs/0nouveau. Aucun diagnosticC#. Unity arrêtée/imports/scène/settings nettoyés ; changelog fait, PR à créer sur fix/captured-upright-contact.

## Équilibrage / rejets
PR154/b5ddb3d800matchs4graines : buts2.752→2.680,tirs25.550→24.948,touches49.478→50.115,penalties.185→.197,sorties13.825→13.720,favoris59.56→59.81%. Ligne seed2/200paires surplus tirs8.83→1.75, avantage défensif persiste. .validation/defensive-line-four-seed-summary.csv et defensive-line-shape-after-200-seed1..4.txt baseline.
Fenêtre récente OpponentCoach REJETÉE/stash :800matchs+200paires,touches50.115→50.373,penalties.1975→.19,avantage butscontre ligne−.81→−.88. Preuves opponent-line-window-after-200-seed1..4.txt et opponent-line-window-line-seed2.txt. Aucun changementCore retenu.
Autres stash rejetés : open-space-carrying400matchs dérive buts/favoris ; claims capturés7contacts corrigés mais3continuités cassées (.784m). Traiter anticipation corps entier, pas bras téléportés ; préparation portée35cm vs cible1,05m. Through-pass-run-lead80/target-reading rejetés. Stash1f2bae2 déjà réappliqué anciennement.

## Prochaines pistes
63anciens :8SlidingExtension comparent bassin à phase idle différente (contact/sol/longueur/continuité passent) ; garder exigences mais témoin mêmecadence. HeaderFacing3 attendentSlerp non borné malgré plafond600°/s. HeaderLanding3slot0implicite, RiglocalYvsosréels. CompactCatch5 chevilles fixes±.18etRigidentity incompatiblescaptures,2plongeons jugentRigplutôtspine. Ne pas desserrer vrais contacts/supports.
Autres vrais défauts : dribble2, gardiens claims/gaze/readiness, pivots, reprise course, headers/duels. Sources/notes/U19 toujourspartiels. Main ne pas fusionner avec dette restante.

## Outils / livraison
.validation/run-local-unity.ps1 -Mode Test -Name unique -Filter Touchline.Tests ; aprèsarrêt .validation/cleanup-unity-imports.ps1 puis restaurer scène/ProjectSettings générés. Dotnet .tools/dotnet/dotnet.exe,DOTNET_CLI_HOME=.tools/cli. API .validation/github-api.ps1 corpsJSONfichier. AttachePRlimite100, ne pas supprimeranciens.
APK0.71.0-preview.1/code64 inchangée : https://github.com/TheNaldoX/touchline/releases/download/v0.71.0-preview.1/Touchline-Unity-0.71-preview.apk
Source0cf0a80a61609df4be7d4e16e44766a6fc547ed1,CI38334925329 ; .validation/apk071/verified.json/source.json/published-asset.json. SHA256CD91D99BF80E41AD42E4CA4790A5116379C9CB455701BD7043670467803CF4AB ; cert130917e6d2b4ea2dcca487587dcff03b1356b14e7de39c3e01657097ad57ee13 ; fr.personal.touchline.unity ARM64. Prochaine>=0.72/code65. Aucun tous-tests-verts/Fold/paritéFM revendiqué.

## Lot tests-only récupération
BasePR168/0dc35d9. SlidingExtension compare bassin au témoin idle même phase/cadence, conserve contact<3/10cm,extension>.76,longueur jambes,sol>=−2.5cm,pas bassin<24cm ; ajoute pause<3mm et cas182cm60Hz. HeaderFacing garde contact<12cm mais remplace ancien Slerp non borné par limite600deg/s du runtime existant. Ciblés16/16 (.validation/recovery-observation-targeted.xml),11anciens fixtures résolus. Aucune modification runtime/Core, globale reste dernière preuve63anciens ;52attendus à confirmer dans prochaine globale. Pas gain visuel revendiqué.
