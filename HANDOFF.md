# HANDOFF — Touchline
Lire AGENTS.md. Une branche/PR par tâche. Une seule Unity locale/CI ; aucun Assets modifié pendant tests/capture/build. Préserver sauvegardes, APK et pack privé. Aucun nouvel agent.

## Travail demandé le 13 octobre (nouvelle autorisation après 0.71)
Six chantiers : caméra portrait/confort ; IA et tactique ; calibration multi-graines ; contacts/animations et clips adaptés ; U19 réels sourcés ; résorption des 196 échecs puis consolidation main. Quota réinitialisé (0% au début). Ancienne automation PAUSED, ne pas réactiver une échéance périmée.

## Lot caméra validé localement
- Branche fix/portrait-action-framing, base a7815ed (feat/android-071-match-fixes). Portrait 45° au lieu de52°, couloir ±5,5m ; regard recentré entre ballon et but, ouverture progressive dès8m. Marges12% identiques entre calcul du cadre et confinement : supprime la dérive en pause observée pendant les essais.
- Core1029/1029 (.validation/portrait-frame-core.txt) ; caméra37/37 (.validation/portrait-close-targeted-final.xml) ; globale2318/2514 (.validation/portrait-close-global.xml), mêmes196 anciens échecs, aucun nouveau ; aucun diagnostic C#.
- Six captures natives revues artifacts/unity/portrait-close-native-verified/report.json : ballon dans le cadre, dérive0 après240images en pause. Profondeur ballon portrait85,51m contre105–108 précédemment. Pas de test physique Fold ni preuve subjective vidéo du confort. Essais précédents rejetés conservés.

## Diagnostics à poursuivre
- Baseline IA inchangée : .validation/continuation-baseline-200-seed2.txt,200matchs : buts2,71,tirs24,29,penalties0,27,sorties13,54,favoris64,08% (103matchs,IC95[54,46;72,68]). Graine1 aprèsPR144 : buts2,61,tirs24,79,sorties13,82,favoris54,55%. Ne pas annoncer équilibre universel.
- Audit ligne graine2,200affiches appariées/400matchs : .validation/continuation-line-seed2.{txt,csv}. Ligne haute : tirs+8,27,buts pour+0,94,contre−0,48. Réceptions adverses en profondeur+4,95 mais presque toutes au-delà25m ; tirs après réception sans hausse significative. Ne pas reprendre l’essai through-pass-target-reading rejeté (HANDOFF antérieur/Git).
- 196échecs surtout Runtime animations. PlayerView.Render retourne après MecanimRender avant les anciennes corrections BodyReceptionPose/GroundContactPose/FinalDuelContact ; étudier adaptation au chemin capturé, pas désactivation des assertions. Source L/R inversée par HumanSidesSwapped ; mesurer anatomie et géométrie réelle (BakeMesh(mesh,true)).
- Pack21clips :7actifs (08passe,09/10/11frappes,07touche,13célébration,17prise basse). Autres non adaptés, pas de déclaration d’intégration complète.
- Sources jeunesse lues13oct : https://www.om.fr/fr/equipe-u19 (26noms/postes2026–27), https://www.om.fr/fr/equipe-u19-2025-2026 ; aucune importation. Saïd Remadnia : FFF https://www.fff.fr/equipe-nationale/joueur/11550-remadnia-said/fiche.html, naissance2009-03-27, attaquant, OMdepuis07/2019. Kaïs Ali : https://comorosfootball.com/player/kais-ali/ naissance2009-03-26, à recouper. Ne pas inventer âges/salaires/notes réels, distinguer observations2026 et faits antérieurs au départ juin2026.

## Livraison conservée
APK0.71.0-preview.1/code64 : https://github.com/TheNaldoX/touchline/releases/download/v0.71.0-preview.1/Touchline-Unity-0.71-preview.apk
PR145/source0cf0a80a61609df4be7d4e16e44766a6fc547ed1, CI38334925329. Vérifications .validation/apk071/{verified.json,source.json,published-asset.json}. SHA256CD91D99BF80E41AD42E4CA4790A5116379C9CB455701BD7043670467803CF4AB. Cert habituel130917e6d2b4ea2dcca487587dcff03b1356b14e7de39c3e01657097ad57ee13 ; packagefr.personal.touchline.unity ARM64. Prochaine>=0.72/code65. Main non fusionnée.
PR142caméra/Moments clés ; PR143a1f5167suivi des jeunes pros réels ; PR144311c71dréévaluation tir proche pendant préparation passe en retrait. Historique/preuves dans Git et PR. Ne pas revendiquer tous tests verts, Fold validé ou paritéFM.