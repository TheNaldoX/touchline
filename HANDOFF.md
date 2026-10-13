# HANDOFF — Touchline
Lire AGENTS.md. Une tâche/branche/PR, une Unity locale OU CI, aucun Assets modifié pendant compilation/test/capture/build. Aucun nouvel agent. Préserver sauvegardes/APK/pack privé. Ne jamais imprimer database.json (132Mo).

## Demande et état
Six chantiers : caméra, IA/tactique, équilibre multi-graines, contacts/clips adaptés, U19 réels,196échecs puis consolidation main. Aucun joueur fictif initial. Automation PAUSED. Quota63% utilisé ; réserver livraison à95%. Main non fusionnée, PR146–174 empilées draft. APK0.71 inchangée.
PR174/7c26be2 appuis gardien publiée. Branche fix/standing-duel-foot-trajectory, lot validé à publier.

## Lot tacle debout validé
BasePR174/7c26be2. Pied repart de son appui visible initial, pas du swing déjà joué par clip. Retrait revient à cet appui, ancrages course libérés et raccord final via mélange des supports existant(.12s). Premier essai sans raccord créait saut en idle60Hz, rejeté ; preuves préservées.
Ciblés40/40, sixcadences/côtés30/60/120, pause<3mm, acteur inchangé ; contacts simulés4graines toujours valides. Retrait30Hz pasmax.275→.157m, préparation.363→.287m, seuils.18/.3inchangés. Native standing-duel-foot-trajectory-native422images, left33/37,right40 revues ; geste construit annulé, pas match/Fold. Globale2610/2649,39anciens/0nouveau (.validation/standing-duel-foot-trajectory-global.xml),157/196 initiaux résolus. Aucun diagnosticC#. Unity arrêtée/imports nettoyés. Core1046inchangé.

## Lot précédent gardien
MecanimKeeperDiveContact : appui atteint plus tôt que les mains (poids×4), corrige 9–10cm de décollage en préparation centrale haute. Tests sur semelles réelles/torse anatomique, récupération observée après idle ; anciennes coordonnées de chevilles procédurales retirées. Exigences sol>=−2.5cm, un appui<8cm, contact<15cm conservées. Ajout cas1.5m60Hz ; sixcas hauts30/60/120Hz contrôlent continuité et pause<3mm.
Ciblés31/31 puis22/22 continuité. Native central-catch-preparation-support-native : high008/high020/low120 revues, pas Fold. Globale2607/2647,40anciens échecs/0nouveau (.validation/central-catch-preparation-support-global.xml),156des196initiaux résolus, dont corrections de fixtures. Aucun diagnosticC#. Unity arrêtée/imports nettoyés. Core1046/1046 inchangé depuis PR154.
PR173 tests-only : fixture simulait les pressure-pokes sans rendre l'acteur ; filtre corrigé, root<1mm et58contacts sur4graines, écartboot<4micromètres, seuil18cm conservé. PR172/19e9f09 tête progression nulle garde dt0, pieds29→30=0m natif. PR171/a30f1ce prise haute centrale choisit catch debout, appuis/contact corrigés. PR170/03c28b6 regard gardien borné360deg/s ; PR169/ff4056e récupération comparée à phase idle identique. PR168/0dc35d9 clip14 célébration debout avec entrée .55s :8/21clips actifs, clip12 agenouillé sans relevé reste inactif. Preuves détaillées dans PR/Git et .validation.

## Équilibrage retenu / rejets
PR154/b5ddb3d800matchs4graines : buts2.752→2.680,tirs25.550→24.948,touches49.478→50.115,penalties.185→.197,sorties13.825→13.720,favoris59.56→59.81%. Ligne seed2/200paires surplus tirs8.83→1.75, avantage défensif persiste. .validation/defensive-line-four-seed-summary.csv et defensive-line-shape-after-200-seed1..4.txt baseline. Aucun Core changé depuis.
Rejets stashes à préserver : OpponentCoach fenêtre récente800matchs/200paires dégrade touches/penalties/avantage ligne ; open-space-carrying400matchs dérive buts/favoris ; claims capturés retarget direct corrige7contacts mais crée saut main .784m ; through-pass-run-lead80/target-reading. Ne pas réappliquer.
Pistes non implémentées : anticipation corps entier pour claims (préparation limitée35cm vs impact1.05m), valeur future de la réception pour les passes en profondeur ; tout changement moteur exige mêmes graines/calibration et rejet si dérive.

## Prochain travail
39échecs : dribble2, claims7, distant keeper2, readiness2, headerlanding3,  pivots/redémarrages/supports, entre autres. Liste exacte dans dernière XML. Ne pas desserrer les contacts physiques pour verdir.
Prochaine piste : HeaderLanding3 fixtures slot0implicite et bassinRig au lieu de hanches réelles, ordre des pieds imposé par préférence plutôt que capture. Vérifier contact/sol/absorption réels sans desserrer exigences physiques. Claims7 nécessitent anticipation corporelle ; anciens retarget directs rejetés.
PR146 caméra portrait37tests/6vues, confort rapide/Fold toujours non validés. Main ne pas fusionner avec dette restante.

## Données : attention dates
Supplément22jeunes réels (12OM/10Nantes), catalogue21837/715clubs. PR163/dddf2db ajoute Forget,Aiki,Foucan,Dago,Merrifield,Khenissi ; sources FCN2025–26 + Montaigu2024p4. Notes45/potentiel65/valeur50k provisoires explicites, salaire0inconnu. U19 incomplets.
Recherche non intégrée : article TribuneNantaise22mai2026 liste départs fin saison Forget/Khenissi/Oissa (et Conti/Dore/Gassama/Yon). Transfermarkt Forget libre depuis01/07/2026. Initial15juin peut rester cohérent, mais droits après juin et date effective à vérifier avec sources primaires avant expansion. Ne pas traiter roster2025–26 comme2026–27. LouisDoré FFF naissance19/11/2007 mais départ annoncé ; Zeze et Camara existent déjà dans base, ne pas dupliquer. URLs candidates dans .validation/nantes-academy-source-candidates.json ; source naissance Dore https://www.fff.fr/equipe-nationale/joueur/10824-dore-louis/fiche.html .

## Outils et livraison
.validation/run-local-unity.ps1 -Mode Test -Name unique -Filter Touchline.Tests ; aprèsarrêt .validation/cleanup-unity-imports.ps1 puis restaurer scène/ProjectSettings générés. Dotnet .tools/dotnet/dotnet.exe,DOTNET_CLI_HOME=.tools/cli. API .validation/github-api.ps1 corpsJSONfichier. AttachePRlimite100, ne pas supprimeranciens.
APK https://github.com/TheNaldoX/touchline/releases/download/v0.71.0-preview.1/Touchline-Unity-0.71-preview.apk ; source0cf0a80a61609df4be7d4e16e44766a6fc547ed1 CI38334925329 ; .validation/apk071/verified.json/source.json/published-asset.json. Packagefr.personal.touchline.unity ARM64, signature habituelle. Prochaine>=0.72/code65. Aucun tous-tests-verts/Fold/paritéFM revendiqué.
