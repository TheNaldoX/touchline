# HANDOFF — Touchline
Lire AGENTS.md. Une tâche/branche/PR, une Unity locale OU CI ; aucun Assets modifié pendant compilation/test/capture/build. Aucun nouvel agent. Préserver sauvegardes/APK/pack privé. Ne jamais imprimer database.json (132Mo).

## Demande / état
Six chantiers actifs sans échéance : caméra, IA/tactique, équilibre multi-graines, contacts/clips adaptés, U19 réels,196échecs puis consolidation main. Aucun joueur fictif initial. Ancienne automation PAUSED. Quota48%utilisé, réserver livraison à95%.
Main non fusionnée (d74e9a9/PR38). Drafts empilées PR146–164 : caméra portrait, contacts/anticipation/appuis, gardiens/tacles, contrôle couru, ligne défensive, U19, gestes sociaux et fixtures corrigées. Historique/preuves dans PR. PR165/05fc17c contrôles au pied validés ; base pour lot suivant.

## Validations acquises
- Core1046/1046 ; dernière globale achevée2537/2606,69anciens/0nouveau (.validation/captured-foot-reception-planted-global.xml).127des196initiaux résolus, dont corrections de fixtures explicitement documentées.
- PR162/b579148 : appels/applaudissements capturés,9nouveaux tests30/60/120Hz, paumes face-à-face, native422images revue.
- PR163/dddf2db : six jeunes Nantes (Forget,Aiki,Foucan,Dago,Merrifield,Khenissi), complément22identités, catalogue21837. Sources effectif FCN2025–26 + PDF organisateur Montaigu2024 page4. Notes45/potentiel65/valeur50k provisoires,salaire inconnu0. Ciblés243/243 ; ui-existing-youth-montaigu-final-165 :19parcours réels, vues plié/déplié revues. U19 incomplets ; petit badge11sp déplié.
- PR164/ec106da : tests-only slots/actionKind explicites, paume au lieu poignet, intérieur et côté anatomique des passes. Sept anciens échecs de fixtures résolus ciblés45/49, quatre vrais défauts dribble/réception maintenus. Deux nouveaux tests rôle touche/gardien. Globale71anciens attendus avant prochain patch, PAS encore prouvée.

## Lot actuel : contrôles au pied capturés
MecanimFootReceptionContact : pied du côté du ballon, enveloppe existante80ms/190–300ms, correction bornée85cm, autre pied appui. Ancres locomotion libérées pendant/après contrôle, supprime dérive en pause. Aucun Core changé. Six nouveaux tests côtés30/60/120Hz : contact<6cm, pause<3mm, pas<30cm, appui, acteur inchangé. Ciblés30/30 (.validation/captured-foot-reception-planted.xml).
Premier film rejeté : autre pied levé ; corrigé et film captured-foot-reception-planted-native422images revu contacts153 deux côtés. Trajectoires construites avec événement Core.Control, pas match/Fold. PREMIÈRE globale interrompue pour corriger ce défaut visuel ; ne pas la compter.
Globale finale2537/2606 :69anciens échecs, neuf résolus (septfixturesPR164 + deuxcontrôles), aucun nouveau. Aucun diagnosticC#. Unity arrêtée/imports/scène/settings nettoyés. Prêt pour PR draft sur fix/explicit-animation-fixtures ; main reste non fusionnée.

## Restant / diagnostics
- Moteur PR154/b5ddb3d :800matchs4graines, buts2.752→2.680,tirs25.550→24.948,touches49.478→50.115,penalties.185→.197,sorties13.825→13.720,favoris59.56→59.81%. Ligne seed2/200paires : surplus tirs8.83→1.75, avantage défensif persiste. .validation/defensive-line-four-seed-summary.csv, defensive-line-shape-after-200-seed1..4.txt =baselineCoreactuelle.
- Essai fenêtre récente OpponentCoach REJETÉ/stash : trois tests/Core1049 expérimentaux,800matchs graines1–4 +200paires ligne seed2. Touches50.115→50.373, penalties.1975→.19 ; avantage buts contre de lignehaute−.81→−.88. Réaction plus récente mais pas amélioration équilibrage. Preuves opponent-line-window-after-200-seed1..4.txt et opponent-line-window-line-seed2.txt. Aucun changement Core retenu,1046 référence.
- Rejetés/isolés stash : open-space-carrying400matchs dérive buts/favoris ; claims capturés corrige7contacts mais casse3continuités (préparation portée35cm vs cible1,05m), traiter anticipation globale. through-pass-run-lead80 et target-reading rejetés. Ne pas réappliquer aveuglément. Stash1f2bae2 déjà réappliqué anciennement.
- Autres animations : dribble, claims/gaze, pivots, genoux en reprise, doigts/chutes, headers ; quelques anciennes fixtures SlidingExtension comparent bassin à phase idle différente.7/21clips privés actifs. Pas nouvellemocap ni fluiditéFold prouvée.
- Caméra portrait améliorée37tests/6vues, confort rapide et Fold encore non validés. Main ne pas fusionner avec dette restante.

## Outils / livraison
.validation/run-local-unity.ps1 -Mode Test -Name unique -Filter Touchline.Tests. Après arrêt .validation/cleanup-unity-imports.ps1 et restaurer scène/ProjectSettings générés. Dotnet .tools/dotnet/dotnet.exe, DOTNET_CLI_HOME=.tools/cli. API .validation/github-api.ps1 ; corpsJSONfichier ; attachePRéchoue limite100, ne pas supprimer anciens.
APK0.71.0-preview.1/code64 inchangée : https://github.com/TheNaldoX/touchline/releases/download/v0.71.0-preview.1/Touchline-Unity-0.71-preview.apk
Source0cf0a80a61609df4be7d4e16e44766a6fc547ed1,CI38334925329 ; .validation/apk071/{verified.json,source.json,published-asset.json}. SHA256CD91D99BF80E41AD42E4CA4790A5116379C9CB455701BD7043670467803CF4AB ; cert130917e6d2b4ea2dcca487587dcff03b1356b14e7de39c3e01657097ad57ee13 ; fr.personal.touchline.unity ARM64. Prochaine>=0.72/code65. Aucun tous-tests-verts/Fold/paritéFM revendiqué.



## Lot validé : fix/captured-fall-hands
BasePR165/f9cfac3. Hypothèse conserver doigts capturés rejetée (pénétration quasi identique), code non retenu. Correction actuelle : extrémités des doigts mises en cache, clearance main par IK au-dessus du sol, aucune modificationCore. Deux anciens défauts doigts−9/−11cm résolus ciblés ; six nouveaux cas30/60/120Hz tailles160/205 : surfaces, pause<3mm, acteur inchangé. AnimationGroundSurfaceTests69/69 (.validation/captured-fall-hands-pause.xml).
Globale2545/2612,67anciens échecs/0nouveau (.validation/captured-fall-hands-global.xml), deux anciens doigts résolus.129des196initiaux résolus, incluant fixtures documentées. Native422images terminée0, fall-left69/fall-right81 revues ; chute/relevé restent raides, pasFold. Aucun diagnosticC#. Unity arrêtée/imports/scène/settings nettoyés. Prochain : contacts debout ContextBody ignorés dans chemin capturé (4BodyContactMotionTests), extraire overlay torse/bras progressif sans toucher pieds/root. Nouvel essai IA fenêtre ligne reste stash REJETÉ, Core1046 référence ; main etAPKinchangées.

## Lot validé : fix/captured-upright-contact
BasePR166/ad893a1. MecanimUprightContact : pression monde filtrée8unités/s, rotation torse/bras existante portée sur capturé ; pieds/root inchangés, pause et relâchement. Quatre anciens BodyContactMotionTests résolus ciblés. Six nouveaux cas30/60/120Hz deux côtés, témoin rendu à cadence identique y compris pauses ; première comparaison corrigée car témoin ne recevait pas pauses. Ciblés23/23 (.validation/captured-upright-contact-continuity.xml), autres ciblés dribble2anciens échouent toujours. Core1046 inchangé.
Globale2555/2618,63anciens échecs/0nouveau (.validation/captured-upright-contact-global.xml), quatrecontacts debout résolus ;133des196initiaux résolus avec fixtures documentées. Native422images revues brace-left60/right60, réaction subtile ; pas collision simulée/nouvelle mocap/Fold. Aucun diagnosticC#. Unity arrêtée/imports/scène/settings nettoyés. Commit/PR sur fix/captured-fall-hands ; main etAPKinchangées.
Prochaines pistes : fixtures headers attendent Slerp non borné malgré plafond600°/s, anciennes compactcatch imposent chevilles à±.18m et Rigidentity alors que poses capturées bougent. Garder vraies exigences contact/support/continuité. Septclips actifs : envisager variantes célébration12/14 (déjàreviewéesanciennement, à revoir), durée3,48/3,18s doit s’accorder aux3sréaction avec entrée/retour propres ; ne pas brancher aveuglément ni annoncer fait.

