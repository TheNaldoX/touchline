# HANDOFF.md — Touchline

Lire AGENTS.md. État courant uniquement ; historique dans Git et les PR. Claude et Codex alternent, jamais simultanément.

## Lot courant : visibilité nocturne et appuis — PR #69

- Session autorisée le9octobre22h12–23h42 Paris (fin21h42UTC), graphismes/animation. Automatisation réactivée jusqu'à cette échéance ; la désactiver au bilan. Aucun agent supplémentaire. À23h20 privilégier validation/passation, aucun nouveau lot après23h42.
- Branche `fix/night-fill-foot-release`, base PR68/0.63, code4602c9c726ded01f97a44e5cf65649467c8d0acf. Lumière ambiante latérale nuit .30/.31/.34→.46/.48/.53, rebond sol .10/.16/.10→.14/.20/.14 ; ciel/projecteur inchangés, aucune lumière supplémentaire.
- Appuis : FootCanPlant distingue un pied descendant/posé d'un pied déjà montant ; évite sa capture en début de balancement, y compris après pas de replacement. Tests30/60/120Hz, tailles, pause. Aucune modification Core.
- Core923/923 `.validation/night-support-core.txt`. Unity37985773041 en cours. **Session39006**, script `.validation/validate-night-support.ps1`, attend/tests/téléchargement puis film `night-support-night` sur4602c9c, récupération. Une seule Unity ; **Assets figés** pendant tests/rendu.
- Revoir le film et comparer aux mêmes images/métriques de `.validation/film-graphics-polish-night` : graine731,départ95s,durée15s,30fps. Rejeter la correction d'appuis si régression visible ou mesures dégradées. Pas de succès présumé, pas de FPS Android revendiqué. PR69 attachée, corps initial `.validation/pr-night-support.json`.
- 0.63 précédente : Core923/923, Unity313/313 (37975543320), films jour37976831352/nuit37979409379 revus. Sources61e8bfb, release9d0246f. Glissement p95=.40m/s,max12.04m/s ; nuit sombre. Pelouse/matières/résolution améliorées et pas chassés interpolés dans PR68. Preuves `.validation/visual-polish-native-final`, `film-graphics-polish-day`, `film-graphics-polish-night`.

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

- Pas de Unity local : Unity6000.3.24f1 via Actions, une instance. `.tools/dotnet/dotnet.exe`, `DOTNET_CLI_HOME=.tools/cli`, CoreTests/Calibration/SeasonSim. Ne pas relancer les tests inchangés sans raison.
- `runtime-tests.yml` : workflow_dispatch, filtre ciblé. `match-film.yml` : push `film/<nom>`, suffixe-night ; vidéos dans branche `films`. Films Mesa à pas fixe ≠ FPS Android. `build-android.yml` : manuel ou release/**, versions/noms dans Editor/ProjectBuilder.cs.
- Helpers ignorés `.validation/` : github-api.ps1, watch-run.ps1 (attente45s), download-artifact.ps1, fetch-film.ps1, verify-apk.ps1. Git réseau/mutations peuvent nécessiter escalade. Dépôt partiel : fetch explicite des branches utiles.
- Exception UnityEditor.Search/QuickSearch préexistante dans les rendus ; distinguer des erreurs Touchline. Shaders runtime dans Resources, postProcessData requis. Qualifier HumanBone/Position en cas de collision. Ne pas utiliser apostrophes typographiques dans les chaînes PowerShell entre apostrophes simples.
- Demander retour Fold0.63 : option Rendu3D/Qualité, nuit, fluidité, après-but, pliage. Limites graphiques : gros plans nocturnes sombres, public simple, pics de glissement résiduels. Ensuite reprendre ligne haute/sorties/penalties avec200 matchs appariés ; management long terme et UI. Automatisation ACTIVE pour cette session uniquement ; désactiver à la fin.

Historique détaillé avant condensation : `git show bb8f233:HANDOFF.md`, PR et preuves locales.
