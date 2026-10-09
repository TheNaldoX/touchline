# HANDOFF.md — Touchline

Lire AGENTS.md. État courant uniquement ; historique dans Git et les PR. Claude et Codex alternent, jamais simultanément.

## Lot courant : graphismes et fluidité — PR #68

- Branche `feat/match-surface-motion-polish`, base PR67/APK0.62 ; code validé `61e8bfb3591ea53ef0781d79388991622773c2c9`. Aucun changement Core/simulation.
- Pelouse RGB2048×1024+mipmaps (~8MiB GPU), détails non répétitifs, UV continus ombre/tonte ; moins de contraste, matières mates. Grain pré-calculé, tampon CPU libéré après upload, pas de passe supplémentaire. Maillots : tissu procédural et panneaux discrets dans la texture partagée.
- Pas chassés : fondu des poids gauche/droite au lieu du basculement instantané ; contrôles30/60/120Hz et pause. Pas de nouvelle mocap.
- Qualité maximale : suppression du plafond commun1600×1000 ; mode maximal jusqu'à2160px par axe/2,6Mpx, aspect/densité préservés, MSAA4× avec repli matériel. Résolution adaptative conservée ; coût GPU accru, aucune mesure Fold.
- Core923/923 : `.validation/visual-polish-core.txt`. Unity313/313 ciblés réussis, aucune erreur/alerte CS trouvée : Actions37975543320, artefact11638527992, `.validation/visual-polish-native-final`. Premier run302/303 : ancien test de rotation corrigé pour respecter le plafond600°/s déjà présent ; tous les nouveaux tests passaient.
- Films jour37976831352 et nuit37979409379 réussis sur61e8bfb ; vues large/rapprochée et deux séquences de neuf images revues. Terrain/équipes/ballon visibles, continuité UV ; gros plans nocturnes encore sombres, public simple. Runtime sans erreur Touchline. Glissement p95=0,40m/s mais pics au contrôle/préparation et maximum12,04m/s en course : non résolus, pas de comparaison appariée avant/après. Preuves `.validation/film-graphics-polish-{day,night}`.
- **Build0.63/code56 en cours37982451316, source9d0246f02304700ff8894dbd7b0511049e2cb27e** (seule différence Assets depuis61e8bfb : version/code/nom APK). Session36004 exécute `.validation/deliver063.ps1` : attend, récupère, copie sans écrasement puis vérifie signature/version/ARM64/hash. **Une seule Unity, Assets figés pendant build.**
- PR68 attachée au chat. Dernier corps PR dans `.validation/pr68-final-review.json`, à actualiser après APK. Aucun changement graphique encore livré en APK.

## Dernière APK conservée

- `artifacts/Touchline-Unity-0.62-preview.apk`, version0.62.0-preview.1/code55,73909010octets, ARM64. Build37957302244, source029274fa50c345808567c6aa6ef986768db87880, PR66.
- SHA256 `76C88DEC6F177C9AEC3AD46FA0CB68702C893C4685AFDBBFA16D2B4BA1588EB7` ; preuve `.validation/apk062-proof.json`.
- Certificat stable `130917e6d2b4ea2dcca487587dcff03b1356b14e7de39c3e01657097ad57ee13`, package `fr.personal.touchline.unity`. Préserver APK0.61 et antérieures, sauvegardes personnelles et preuves.
- Prochaine livraison graphique : nouveau nom0.63/code56 après validation/revue. Aucun essai Fold ; ne pas affirmer fluidité/chauffe Android vérifiées. Main non fusionnée, attente test utilisateur.

## État fonctionnel et limites

- PR62 assemblage Claude : cellule de recrutement, missions/connaissance régionale/intérêt, immersion tactique, causeries, consignes, adjoint. Sauvegarde entraîneur sans emploi corrigée. PR63 rattache les signatures au bon club.
- PR64 : recommandations selon budget/besoins/tactique, comparaison titulaires, explications adjoint ; xG affichés recalibrés seulement. PR65 : trésorerie, réserves, engagements et marges salariales explicités. Huit captures revues, rendu37953810119 ; Unity275/275. Trois libellés11sp préexistants restent à améliorer.
- PR67 : tests neutres corrigés, Core923/923 et Unity277/277 (37962815505). Deux essais de passes retirés pour régressions ; rapports `.validation/through-target-*`, `through-onside-*`. Aucun changement de moteur retenu.
- Calibration baseline200 graine1 : buts2,61, tirs24,72, cadrés7,74, touches49,93, favori52,53% sur99 affiches ; penalties0,19 et sorties13,79 sous cibles. Ligne haute trop avantageuse non résolue. PR64 deux graines : xG1,69/1,72→2,50/2,52 pour2,61/2,69 buts ; statistiques hors xG identiques, pas validation sur données réelles.
- Carrière cinq saisons à scores simplifiés :384 clubs, aucun sous18 joueurs, un au-dessus de40 ; sauvegardes identiques sur27368 joueurs. Salaires médians2450→2977€/semaine, libres273→1122, cinq clubs dépassent70% salaires/recettes. Preuve `.validation/claude-relay-five-seasons.txt`. Pas simulation3D de toutes les rencontres ni validation sur plusieurs décennies.
- Base21815 entrées importées/715 clubs/36 ligues, sources ESPN ; notes/salaires non tous certifiés. **Pas de joueurs fictifs au catalogue initial**. Ancienne extension conservée uniquement pour compatibilité sauvegardes. Newgens à partir2027, aucun U19 fictif initial. Portraits absents du dépôt : initiales.
- Ne pas intégrer PR53 (ligne défensive e1509ee, rejetée). Pile de travail non fusionnée #62→#63→#64→#65→#66→#67→#68.

## Outils et prochaine étape

- Pas de Unity local : Unity6000.3.24f1 via Actions, une instance. `.tools/dotnet/dotnet.exe`, `DOTNET_CLI_HOME=.tools/cli`, CoreTests/Calibration/SeasonSim. Ne pas relancer les tests inchangés sans raison.
- `runtime-tests.yml` : workflow_dispatch, filtre ciblé. `match-film.yml` : push `film/<nom>`, suffixe-night ; vidéos dans branche `films`. Films Mesa à pas fixe ≠ FPS Android. `build-android.yml` : manuel ou release/**, versions/noms dans Editor/ProjectBuilder.cs.
- Helpers ignorés `.validation/` : github-api.ps1, watch-run.ps1 (attente45s), download-artifact.ps1, fetch-film.ps1, verify-apk.ps1. Git réseau/mutations peuvent nécessiter escalade. Dépôt partiel : fetch explicite des branches utiles.
- Exception UnityEditor.Search/QuickSearch préexistante dans les rendus ; distinguer des erreurs Touchline. Shaders runtime dans Resources, postProcessData requis. Qualifier HumanBone/Position en cas de collision. Ne pas utiliser apostrophes typographiques dans les chaînes PowerShell entre apostrophes simples.
- Terminer revue jour/nuit, puis éventuelle APK distincte vérifiée ; demander retour Fold sur nuit, fluidité, après-but, pliage. Ensuite reprendre ligne haute/sorties/penalties avec200 matchs appariés ; management long terme et UI. Automatisation historique PAUSED, ne pas la réactiver sans demande.

Historique détaillé avant condensation : `git show bb8f233:HANDOFF.md`, PR et preuves locales.
