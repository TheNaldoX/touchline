# HANDOFF.md — Touchline

Lire AGENTS.md. Claude et Codex alternent, jamais simultanément. Historique détaillé dans Git/PR ; aucun nouveau journal docs.

## État courant

- Session autorisée jusqu’au10/10/2026 à10h Paris (08:00UTC). Quota vérifié05h33 :73% utilisé,27% restant. Automatisation réactivée avec arrêt à10h ; dès92% utilisés, aucun nouveau lot, build/livraison dernière source validée (clé CI stable, version au moins0.65/code58). Dès09h15 privilégier consolidation/livraison. Ce créneau remplace les anciennes échéances.

- Lot courant : `fix/agent-playing-time-feedback`, avis agent/fourchettes selon rôle promis, sans écraser offre ; précontrat affiche indemnité nulle, prêt conserve salaire parent. Core947/947, ciblés Unity22/22 ; six captures `ui-agent-role-compact` et parcours des deux résolutions revus. PR83 étoiles relatives, PR84 pose gardien : Core944/944, globale2032/2306 (274 anciens échecs, aucun nouveau). Ces lots ne sont pas dans APK0.64. Essais moteur isolés, non intégrés.

- Branche `feat/apk-064-consolidated-preview`, PR82, source6b5205b ; base PR81/620b43f. APK0.64/code57 construite, signature/manifest/ARM64 vérifiés. Correction mains gardien : Core941/941, ciblés30/30, globale2020/2295,275 anciens échecs ; six corrigés, aucun nouveau (`keeper-hold-comparison.json`). Captures natives `keeper-hold-native` revues ; vidéo en artifacts. Pas de validation Fold.
- Finances : quatre onglets, défilement séparé, aperçu tarif sans mutation incluant réaction des supporters et délai hebdomadaire ; flux comptables30 jours ; statuts sponsors français ; alerte salariale ouvre Budget directement.
- Core941/941 (`.validation/ticket-preview-core-final.txt`). Quatre nouveaux cas. Unity192/195 ciblés (`ticket-preview-unity.xml`) : trois échecs ProfessionalTests déjà présents, aucun nouveau. Captures natives `ui-finance-preview-links` réussies/revues : aperçu, confirmation, délai, destination de l’alerte. Badge préexistant11sp, aucun bouton<48dp. Exception préexistante UnityEditor.Search au démarrage, distincte du jeu.
- La suite Unity reste rouge : ne pas annoncer tous les chantiers terminés. Nouvelle correction limitée au ballon tenu ; plongeons/prises/placements ne sont pas tous résolus.

## Lots retenus récents

- Lot validé : `fix/keeper-distribution-clips`, raccordement des deux captures de relance existantes, contact et récupération continus. Core947/947 ; ciblés37/37. Globale2067/2321 :254 échecs anciens,20 anciens corrigés,aucun nouveau (`keeper-distribution-final-comparison.json`). Douze nouveaux tests. Flexion proportionnelle à la taille, bras libre préservé. Captures roll-final et throw-native revues, vidéo `artifacts/Touchline-keeper-roll-final-2026-10-10.mp4`. Pas de nouvelle mocap, pas de test Fold ; simulation inchangée. Prochaine étape : aperçu financier des offres avant envoi, puis diagnostic moteur ligne haute distinct des essais rejetés.

| PR | Changement et preuves locales |
|---|---|
|79,661ece9|Correction des semelles au sol après Mecanim, rotation du pied et plan du genou conservés. Trois nouveaux tests30/60/120Hz échouent avant, passent après. Core937/937 ; globale2007/2288. Captures `throw-ground-before/after` revues ; vidéo `artifacts/Touchline-throw-support-before-after-2026-10-10.mp4`, avant gauche/après droite, ralenti2×. Ballon de référence immobile : pas une preuve de contact mains/ballon.|
|78,7ab45e0|Employment.IsLoan distingue null/vide d’un vrai prêteur après JsonUtility, dans contrats/finances/mercato/formation/UI. Trois scénarios permanents/vente/vrai prêt ; Core937/937, globale1986/2285,299 anciens échecs. `loan-state-comparison.json`, `ui-loan-save-final` revu.|
|77,b2a8c3d|Relance de départ après28 jours de conflit persistant ; blessure/amélioration réinitialise le délai, seul dernier message actionnable. Core934/934, Unity12/12, captures `ui-departure-followup-native`.|
|76,d24adb7|Bilan occasions et explication prudente des xG ; consignes finales distinctes des mesures cumulées. Aucun moteur/aléatoire modifié. Core931/931, Unity22/22, `ui-chance-review-final` revu.|
|75,5375b7a|Défilement par onglet/layout ; marché lisible, filtres visibles, lignes104 unités, navigation12sp. Régression native avant/après, `ui-scroll-memory-final` revu.|
|74,a0fb3a9|Cache besoins recrutement invalidé après modifications manuelles de postes ; budget déjà correct. `ui-recruitment-cache-before/after` revus.|
|73,c6c666b|Mains du gardien préparées progressivement avant prise aérienne, annulation continue en course.19/19 ciblés,6 nouveaux ; globale1975/2274. Vidéo `Touchline-keeper-readiness-before-after-2026-10-10.mp4`. Pas une prise complète.|
|72,cbfb053|Rapports archivés : niveaux/attributs observés plutôt que valeurs cachées en direct. Anciennes observations sans attributs signalées et actualisables. Core926/926, `ui-scout-snapshot-notice` revu.|
|69–71|Éclairage nocturne, reprise d’appui si ancrage inaccessible, passe gauchère miroir, IK passe autour du contact. Preuves `night-support-test-comparison.json`, `pass-contact-comparison.json`, vidéos dans artifacts. Aucun gain global de glissement ou performance Android démontré.|

## Moteur — essais rejetés, ne pas intégrer

- Essai `fix/through-pass-target-reading`,2e893e6 (origine ddeffa1/aed2c72), rejeté et non intégré. Ne considérer que la ligne projetée pour les appels licites corrige8 scénarios mais graine2/200 matchs : favoris62,14→68,93%, touches49,44→50,27, penalties0,26→0,20, buts2,69→2,82. Graine1 : favoris52,53→53,54%, touches49,93→51,20 ; avantage net ligne haute+1,13→+1,12 inchangé sur200 paires. Preuves through-onside-{before,after}-seed2.txt, through-onside-after-{200,line}.txt.955 tests Core expérimentaux,947 dans la branche retenue. Ne pas réintégrer sans nouveau diagnostic.

- Dernier essai local `tune/defensive-flight-reading`,37b3f57, isolé de PR80 : lecture progressive du point d’arrivée sur0,3s. Core944/944 expérimental.200 matchs graine1 avant/après : buts2,61→2,70 ; favoris52,53→67,68% ; touches49,93→50,53 ; penalties0,19→0,18 ; sorties13,79→13,52.200 paires ligne basse/haute : effet buts pour+0,70→+0,68, buts contre−0,43→−0,75. Rejeté, source rétablie. Preuves `.validation/flight-reading-{before,after}-{200,line}.txt` et CSV. Ne pas confondre944 expérimentaux et941 retenus.
- Ancien essai local `tune/defensive-turn-recovery`,0defb19 : limitation course arrière pendant rotation. Core927/927 mais touches49,93→51,11 ; favoris52,53→55,56% ; avantage ligne haute non réduit. Preuves `recovery-{before,after}-{200,line}`.
- PR53/e1509ee déjà rejetée : déplacement attaquants/bonus d’appels ; PR67 a retiré d’autres essais de passes. Ne pas répéter sans hypothèse nouvelle.
- Référence actuelle200/graine1 : buts2,61, tirs24,72, cadrés7,74, corners10,18, fautes20,80, jaunes3,75, hors-jeu4,05, touches49,93, penalties0,19, sorties13,79, favoris52,53%. xG2,50 = estimation affichée calibrée, pas décision. Ligne haute reste trop avantageuse.

## Carrière et données

- PR78, deux saisons monde/graine77 puis sauvegarde/reprise :384 clubs jouables, effectifs min23/médiane29/max37, aucun<18 ou>40 ; salaires/recettes médiane36%,3 clubs>70%. Formats complet/compact identiques pour23682 joueurs puis sept journées reprises identiques avec clubs valides. `loan-state-two-seasons-final.txt`. Premier contrôle oubliait `academy-`, corrigé dans l’outil.
- Audit20 saisons source6b5205b, monde/graine77, scores simplifiés :384 clubs, effectifs23/30/42, aucun<18, un>40 ; note moyenne64,3→64,3, salaires/recettes35→36%,2 clubs>70%,132 endettés, un avec trésorerie nette<−100% recettes. Promus maintenus57%. Formats complet/compact identiques47867 joueurs, puis sept journées reprises identiques et tous les clubs joueurs valides. `.validation/release064-world20-seed77.txt` (496s). Pas de test3D/Android ; aucun appareil ADB connecté.
- Catalogue21815 joueurs importés,715 clubs,36 ligues ; aucun fictif initial, newgens dès2027. Portraits absents du dépôt : initiales. Sources : `catalogue-source-audit.json` (sidecar couvre21815 identités,12921 URLs de notes), `catalogue-integrity-audit.json` sans ID dupliqué/club invalide/note hors limites. Ce n’est pas une vérification récente de tous les salaires.
- OM : ESPN consulté10/10,30 joueurs contre24 au catalogue ; six jeunes manquants Koum/Clement/Bang Na/Doubal/Slimani/El Kadmiri, présence corroborée https://www.om.fr/en/reserve-team . Non importés : postes/dates/provenance à consolider ; DOB El Kadmiri discordante juillet/juin. Staff Ferrier/Nouri/Lancet/Farrugia déjà présents. `.validation/espn-marseille-roster-live.json`.
- Recrutement, promesses, presse, formation, finances existent : inspecter avant ajout. Priorités ouvertes : décisions/ligne haute, contacts gardiens et sol, profondeur de carrière, données sourcées, UI homogène, Fold réel.

## APK — préserver

- Nouvelle `artifacts/Touchline-Unity-0.64-preview.apk`,0.64.0-preview.1/code57,73920554octets,source6b5205b,build38014332408 réussi. SHA256 `EA4D0E632C1C7F42A8A75B92353D495231B72218C6710FD712F11CEB0560C0BA` ; preuve `.validation/apk064/verified.json`. Même package et certificat que0.63. PR82 brouillon,275 tests anciens rouges ; Fold non testé.
- `artifacts/Touchline-Unity-0.63-preview.apk`,0.63.0-preview.1/code56,73911418octets,ARM64,source9d0246f,build37982451316,PR68.
- SHA256 `8BBCA02E30C4F106E5984A3FDA40635F5880DC4AC6B8B596F154EB1A27ED46F6`, preuve `.validation/apk063-proof.json` ; certificat `130917e6d2b4ea2dcca487587dcff03b1356b14e7de39c3e01657097ad57ee13`, package `fr.personal.touchline.unity`.
- Préserver APK/sauvegardes. Prochaine version au moins0.65/code58, nom distinct, clé stable seulement en CI. Main non fusionnée, retour Fold attendu.

## Outils et garde-fous

- Unity6000.3.24f1 locale licenciée,RTX5070Ti/D3D11. Une seule instance, aucune modification Assets pendant test/capture/build. Processus parfois invisibles au sandbox : Get-CimInstance Win32_Process avec escalade. `.validation/run-local-unity.ps1`, noms de preuves uniques.
- `.tools/dotnet/dotnet.exe`, DOTNET_CLI_HOME=.tools/cli ; CoreTests/Calibration/SeasonSim. Ne pas reconstruire un outil pendant son exécution. Python : `C:/Users/victo/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe` ; ffmpeg `C:/ffmpeg-8.1.2-essentials_build/bin/ffmpeg.exe`.
- CI Unity bloquée deux fois par Docker Hub anonyme : pas de relance en boucle. GitHub via `.validation/github-api.ps1`, secrets jamais imprimés ; attacher chaque PR créée.
- Nettoyage après dernière Unity seulement : `.validation/cleanup-unity-imports.ps1`,73 imports générés connus ; préserver nouveaux .meta intentionnels, Library et sources. Restaurer scène/ProjectSettings seulement si modifications générées connues.
- Preuves locales ignorées dans `.validation/` et artifacts ; détails dans PR. Automatisation ACTIVE jusqu’au10/10 à10h Paris ; dès92% utilisés, geler et livrer une APK validée.


