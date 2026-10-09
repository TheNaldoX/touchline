# HANDOFF.md — Touchline

Lire AGENTS.md. État courant uniquement ; historique dans Git et les PR. Claude et Codex alternent, jamais simultanément.

## Lot courant : contact des passes capturées — PR #71

- Branche `fix/pass-ball-alignment`, base PR70, commit219225d. Ajustement IK de la jambe de passe vers actionTarget/actionHeight autour de l’impact (.18s), fondu .14s, correction bornée35cm ; points invalides sous le bassin ignorés. Aucun Core, ballon ou déplacement racine modifié.
-44/44 tests ciblés réussis : `.validation/pass-contact-targeted-valid-point.xml`. Les6 nouveaux cas de contact (tailles/pieds/fréquences) échouent tous sur151089f, preuve `.validation/pass-contact-before.xml`, passent après avec écart <5,5cm. Quatre anciens tests de contact de passe auparavant rouges passent aussi.
- Capture prototype `pass-ball-contact-review` revue, passes droite frames496–543, gauche544–591 ; ballon témoin statique, ne pas prétendre que ce diagnostic valide la trajectoire de match. Planche `.validation/pass-contact-review.jpg`.
- Suite complète terminée : **1963/2265 réussis,302 échecs**, quatre anciens InsidePass corrigés, aucun nouvel échec ; `.validation/pass-contact-comparison.json` et XML `pass-contact-all.xml`. Pas de validation globale verte.
- Films appariés `pass-contact-before-match-night` (runtime151089f) / `pass-contact-match-night` (219225d), graine731,95–110s,30Hz : statistiques hors kick identiques. Métrique pied bas globale .09→.10m/s, p95 .41→.46 ; kick .02→.63/p95 .01→4.82, car la jambe de frappe descend vers le ballon. Six pics kick≥4m/s concernent tous le pied fort (Højbjerg droit, Aguerd/Kondogbia gauche), pas le pied de soutien. Ne pas prétendre réduire globalement les glissements. Neuf images successives et comparaison frame257 revues.
- Avant/après vidéo `artifacts/Touchline-pass-contact-before-after-2026-10-10.mp4`, planche `.validation/pass-contact-before-after.jpg`. Runtime final restauré depuis Git après baseline ; aucune Unity active. Nettoyage imports seulement, sauvegardes/APK préservées. PR71 en brouillon, aucune APK nouvelle, automatisation PAUSED.

## Lot précédent : pied des passes capturées — PR #70

- Branche `fix/mecanim-pass-contact`, base PR69, commit `fc41266`. Les gauchers utilisaient Soccer Pass (pied droit) : sélection du clip miroir existant pour passes/passages en profondeur/cutbacks. Aucun Core, durée, instant de contact ou fondu changé.
- Tests ciblés34/34 ; nouveau test6 combinaisons pied/style vérifie clip, jambe de suivi devant appui et position simulée inchangée. Baseline f7495c5 :3 gauchers échouent/3 droitiers passent ; après :6 passent. XML `.validation/preferred-pass-foot-{targeted,baseline}.xml`.
- Clips bruts droit/gauche revus dans `unity/Touchline/build/film/preferred-foot-clips-review` (frames10–57/58–105), planche `.validation/preferred-foot-review.jpg`. Capture prototype sans ballon, pas une preuve de contact ni de FPS Android. Vidéo comparative `artifacts/Touchline-passing-feet-review-2026-10-09.mp4`.
- Suite complète terminée : **1953/2259 réussis, les mêmes306 échecs préexistants, aucun nouveau**. `.validation/preferred-pass-foot-all.xml`, comparaison `.validation/preferred-pass-foot-comparison.json`. Aucun processus Unity actif après validation. PR70 reste en brouillon à cause de la dette globale ; aucune APK nouvelle, automatisation toujours PAUSED. Prochaine priorité : écarts géométriques pied–ballon sous Mecanim, distincts du choix de pied corrigé ici.

## Lot précédent : visibilité nocturne et appuis — PR #69

- Branche `fix/night-fill-foot-release`, base PR68/0.63 ; PR69 en brouillon. Session du 9 octobre 22h12–23h42 Paris terminée, aucun agent supplémentaire, aucun Core modifié. Automatisation désactivée au bilan.
- Éclairage nocturne latéral renforcé, sans lumière supplémentaire ni changement du ciel/projecteur. Images locales appariées à4s : maillot clair +28,3%, sombre +10,0%, pelouse +1,6% (luminance écran sRGB, pas mesure photométrique). Vue proche finale revue.
- Correctif runtime final `2e4316c` : un pas de replacement part du pied réellement affiché uniquement si l’ancien ancrage est inaccessible ET l’écart dépasse6cm. Les appuis normaux sont préservés. Test sur160/182/200cm : ancien code saute de60–65cm (3 échecs attendus), nouveau code passe (déplacement initial <1,5cm).
- Core923/923 inchangé : `.validation/night-support-core.txt`. Unity locale317/317 : `.validation/step-visible-local-full.xml`. Ancien défaut : `.validation/step-origin-baseline-repro.xml`. Compilation finale du diagnostic `583392d` réussie dans `local-step-visible-trace-night`, métriques strictement identiques au film précédent (SHA C5CE4247BE2F51E7931BBE8F4CB8750E5F8E2F3EB3D33F349F9B70BA862253A7).
- Films locaux appariés `unity/Touchline/build/film/local-baseline-063-night` et `local-step-visible-night` : graine731,95–110s,30fps. Glissement moyen .09→.09m/s, p95 .40→.40, max12.04→12.07. Correction d’un cas limite reproduit, PAS amélioration globale du glissement. Essai de pied descendant retiré car p95 .55 ; essai de récupération inconditionnelle retiré car p95 .43.
- Film24s `local-step-visible-goal-night` revu par planches : but, ralenti et retour vue large. La caméra de diagnostic «follow» reste près du filet ; ce n’est pas la caméra «broadcast» réellement utilisée en jeu. Aucun test Fold ni mesure FPS Android. Aperçu local : `artifacts/Touchline-night-support-preview-2026-10-09.mp4`.
- Diagnostic `foot-events.txt` ajoute frame/joueur/pied/action/position écran aux pics ≥4m/s. Pic résiduel frame102, joueur289114, pied0, actionrun,12.068m/s, hauteur .117→.096m ; neuf images adjacentes revues. Prochaine enquête : différencier pied réellement en appui et pied bas en phase de vol (seuil actuel12cm), puis corriger sans masquer les défauts.
- Validation jour terminée : `local-step-visible-day`, capture à4s revue et métriques identiques à la nuit. Suite complète `.validation/night-support-all-editmode.xml` : **1947/2253 passent,306 échouent**. Les317 tests précédents sont seulement le sous-ensemble CI. PR à garder en brouillon ; ne pas déclarer toute la validation réussie.
- **Comparaison complète terminée** : baseline334fe84 avec les mêmes tests :1943 réussis/310 échecs ; final1947 réussis/306 échecs. Aucun nouvel échec, quatre tests résolus (3 tailles du pied + éclairage). Les306 échecs sont présents avant/après. Preuve `.validation/night-support-test-comparison.json`, XML `night-support-baseline-all-editmode.xml` et `night-support-all-editmode.xml`. Runtime final automatiquement restauré et diff HEAD vérifié ; toutes les Unity arrêtées. Ne pas présenter les317 tests ciblés comme la suite complète réussie.
- **Unity locale disponible et sous licence** : `C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe`, RTX5070Ti/D3D11. Vérifier processus via Get-CimInstance Win32_Process **avec escalade**, sinon il peut être invisible. Helper `.validation/run-local-unity.ps1`. Une seule Unity, Assets figés pendant exécution.
- CI37989994475 a échoué deux fois AVANT Unity : limite anonyme Docker Hub, exit125. Tests/rendus finaux remplacés par validation locale réelle. Aucun secret Docker Hub configuré ; ne pas relancer en boucle. Clé de signature Android seulement disponible en CI, aucune nouvelle APK produite ;0.63 reste la dernière livrée.
- Nettoyage après arrêt Unity :73 fichiers connus générés (metas/profils URP) supprimés, packages-lock/GraphicsSettings restaurés. Sources éclairage intactes, Library/sauvegardes/APK/preuves préservées.

## Dernière APK livrée

- `artifacts/Touchline-Unity-0.63-preview.apk`, version0.63.0-preview.1/code56,73911418octets, ARM64. Build37982451316, source9d0246f02304700ff8894dbd7b0511049e2cb27e, PR68.
- SHA256 `8BBCA02E30C4F106E5984A3FDA40635F5880DC4AC6B8B596F154EB1A27ED46F6` ; preuve `.validation/apk063-proof.json` ; artefact11642358317.
- Certificat stable `130917e6d2b4ea2dcca487587dcff03b1356b14e7de39c3e01657097ad57ee13`, package `fr.personal.touchline.unity`. Mise à jour sans désinstallation. Aucun essai Fold ; ne pas affirmer fluidité/chauffe Android vérifiées. Main non fusionnée, attente test utilisateur.
- 0.62/code55 conservée : `artifacts/Touchline-Unity-0.62-preview.apk`,73909010octets, SHA256 `76C88DEC6F177C9AEC3AD46FA0CB68702C893C4685AFDBBFA16D2B4BA1588EB7`, preuve `.validation/apk062-proof.json`. Préserver0.61 et antérieures, sauvegardes personnelles et preuves. Prochaine APK nouveau nom0.64/code57 minimum.

## État fonctionnel et limites

- PR62 assemblage Claude : cellule de recrutement, missions/connaissance régionale/intérêt, immersion tactique, causeries, consignes, adjoint. Sauvegarde entraîneur sans emploi corrigée. PR63 rattache les signatures au bon club.
- PR64 : recommandations selon budget/besoins/tactique, comparaison titulaires, explications adjoint ; xG affichés recalibrés seulement. PR65 : trésorerie, réserves, engagements et marges salariales explicités. Huit captures revues, rendu37953810119 ; Unity275/275. Trois libellés11sp préexistants restent à améliorer.
- PR67 : tests neutres corrigés, Core923/923 et Unity277/277 (37962815505). Deux essais de passes retirés pour régressions ; rapports `.validation/through-target-*`, `through-onside-*`. Aucun changement de moteur retenu.
- Calibration baseline200 graine1 : buts2,61, tirs24,72, cadrés7,74, touches49,93, favori52,53% sur99 affiches ; penalties0,19 et sorties13,79 sous cibles. Ligne haute trop avantageuse non résolue. PR64 deux graines : xG1,69/1,72→2,50/2,52 pour2,61/2,69 buts ; statistiques hors xG identiques, pas validation sur données réelles.
- Carrière cinq saisons à scores simplifiés :384 clubs, aucun sous18 joueurs, un au-dessus de40 ; sauvegardes identiques sur27368 joueurs. Salaires médians2450→2977€/semaine, libres273→1122, cinq clubs dépassent70% salaires/recettes. Preuve `.validation/claude-relay-five-seasons.txt`. Pas simulation3D de toutes les rencontres ni validation sur plusieurs décennies.
- Base21815 entrées importées/715 clubs/36 ligues, sources ESPN ; notes/salaires non tous certifiés. **Pas de joueurs fictifs au catalogue initial**. Ancienne extension conservée uniquement pour compatibilité sauvegardes. Newgens à partir2027, aucun U19 fictif initial. Portraits absents du dépôt : initiales.
- Ne pas intégrer PR53 (ligne défensive e1509ee, rejetée). Pile de travail non fusionnée #62→#63→#64→#65→#66→#67→#68.

## Outils et prochaine étape

- Unity6000.3.24f1 locale disponible (voir lot courant), ou via Actions : une seule instance. `.tools/dotnet/dotnet.exe`, `DOTNET_CLI_HOME=.tools/cli`, CoreTests/Calibration/SeasonSim. Ne pas relancer les tests inchangés sans raison.
- `runtime-tests.yml` : workflow_dispatch, filtre ciblé. `match-film.yml` : push `film/<nom>`, suffixe-night ; vidéos dans branche `films`. Films Mesa à pas fixe ≠ FPS Android. `build-android.yml` : manuel ou release/**, versions/noms dans Editor/ProjectBuilder.cs.
- Helpers ignorés `.validation/` : github-api.ps1, watch-run.ps1 (attente45s), download-artifact.ps1, fetch-film.ps1, verify-apk.ps1. Git réseau/mutations peuvent nécessiter escalade. Dépôt partiel : fetch explicite des branches utiles.
- Exception UnityEditor.Search/QuickSearch préexistante dans les rendus ; distinguer des erreurs Touchline. Shaders runtime dans Resources, postProcessData requis. Qualifier HumanBone/Position en cas de collision. Ne pas utiliser apostrophes typographiques dans les chaînes PowerShell entre apostrophes simples.
- Demander retour Fold0.63 : option Rendu3D/Qualité, nuit, fluidité, après-but, pliage. Priorité animation : trier les306 échecs préexistants (contacts, gardiens, tacles, poses au sol), distinguer anciens tests procéduraux des défauts réellement visibles avec Mecanim. PR69 ne résout pas cette dette. Puis ligne haute/sorties/penalties avec200 matchs appariés ; management long terme et UI. Automatisation PAUSED, aucun travail autonome planifié après cette session.

Historique détaillé avant condensation : `git show bb8f233:HANDOFF.md`, PR et preuves locales.
