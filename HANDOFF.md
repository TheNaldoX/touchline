# HANDOFF.md — Touchline

Lire AGENTS.md. Claude et Codex alternent, jamais simultanément. Historique dans Git/PR, pas de nouveaux journaux docs.

## Lot courant — préparation des gardiens

- Branche `fix/keeper-claim-readiness`, base PR72/cbfb053. Mecanim conserve sa pose et ses appuis ; correction progressive des bras pour la préparation de prise aérienne. Annulation interpolée jusqu’au retour réel des mains, déplacement racine accompagné. Aucun Core ni ballon modifié.
-19/19 tests KeeperClaimAnticipation réussis, dont6 nouveaux cas statique/course à30/60/120Hz. Trois anciens tests de mains trop basses corrigés. Essai intermédiaire de retour en course échouait de35–48cm, correction validée (`keeper-claim-moving-{before,after}.xml`).
- Capture synthétique AVANT revue : `build/film/keeper-claim-before`,21 images ; mains à hauteur de taille malgré ballon aérien. Capture APRÈS revue (`keeper-claim-after`), mains préparées et appuis visuellement inchangés. Vidéo `artifacts/Touchline-keeper-readiness-before-after-2026-10-10.mp4` : avant à gauche/après à droite, diagnostic0,35s ralenti4×. Suite complète1975/2274,299 échecs préexistants, aucun nouveau, trois anciens corrigés ; `.validation/keeper-claim-comparison.json`. Aucune Unity active. Ne pas revendiquer une prise entière, une nouvelle mocap ou une performance Android.

## Lot recrutement — PR72

- Branche `fix/scouting-report-snapshots`, base `4a7708a` (PR71), code `3688ec0`. Les fiches utilisent les estimations enregistrées ; attributs copiés au début et à la fin de l’observation. Plus de progression cachée découverte en lisant un vieux rapport. Les anciennes sauvegardes gardent leurs estimations ; attributs non archivés masqués, connaissance plafonnée89% pour permettre une actualisation, avertissement dans Rapports.
- Core **926/926** : `.validation/scout-snapshots-core-v3.txt`. Trois nouveaux tests : stabilité après changement caché, anciennes sauvegardes, achèvement/renouvellement. Fixture des tests de fraîcheur explicitement moderne ; assertions conservées.
- Unity complète :1966/2268, mêmes302 échecs préexistants, aucun nouveau ; `.validation/scout-snapshots-comparison.json`. Revue native terminée : `build/film/ui-scout-snapshot-notice`,8 captures plié/déplié ; filtres compacts, actions et avertissement visibles. Parcours filtre/actualisation réussi. Trois titres de navigation préexistants11sp déplié ; aucun nouveau texte<12sp. Aucune Unity active. Ne pas déclarer suite verte.
- Audit20 saisons terminé, base4a7708a, `.validation/world20-seed77.txt` : monde/graine77, scores simplifiés.384 clubs jouables : min23/médiane30/max42, aucun sous18, un>40 ; force médiane+1 point, note moyenne64,3→64,3. Salaires/recettes médiane35→36%,2 clubs>70%,132 endettés, un avec trésorerie nette<−100% recettes. Libres273→1029, promus maintenus57%. Sauvegardes identiques pour47867 joueurs ;149,78Mo complète/30,39Mo compacte. Ce n’est ni un test3D des rencontres ni une mesure Android.

## Moteur : expérience rejetée, aucun changement retenu

- Branche locale `tune/defensive-turn-recovery`, commit0defb19 isolé, non intégré : limitation de course arrière des défenseurs pendant leur rotation. Core927/927 mais calibration défavorable.
-200 matchs graine1 avant/après : buts2,61→2,64 ; touches49,93→51,11 ; penalties0,19→0,21 ; sorties13,79→13,94 ; favori52,53→55,56% sur99 affiches. Fautes19,85 après.
-200 paires ligne basse/haute : surplus de buts haute +0,70→+0,72 ; réduction de buts encaissés−0,43→−0,51. Ne résout pas l’avantage. Preuves `.validation/recovery-{before,after}-{200,line}.txt` et CSV ; conserver la base.
- PR53 (e1509ee) déjà rejetée : atténuation du décalage des attaquants et bonus d’appels. PR67 a retiré d’autres essais de passes. Ne pas refaire ces essais sans hypothèse nouvelle.

## Animations / graphismes déjà validés localement

- PR69 : éclairage nocturne latéral, pas de replacement depuis le pied visible si ancien ancrage inaccessible et écart>6cm.317 tests ciblés ; comparaison complète1943/2253→1947/2253,306 échecs préexistants. Preuves `.validation/night-support-test-comparison.json`. Aucune réduction globale démontrée du glissement.
- PR70 : clip de passe miroir pour gauchers,1953/2259, mêmes306 échecs. Vidéo `artifacts/Touchline-passing-feet-review-2026-10-09.mp4`.
- PR71 : correction IK pied de passe vers impact (.18s, fondu .14s, max35cm).44 tests ciblés, six nouveaux cas échouent avant/passent après ; suite1963/2265,302 échecs, aucun nouveau. `.validation/pass-contact-comparison.json`, vidéo `artifacts/Touchline-pass-contact-before-after-2026-10-10.mp4`. Aucun Core modifié.
- Ces PR restent en brouillon : dette globale d’animations et Fold non testé. Prochaine enquête concrète : `KeeperClaimAnticipationTests`, mains trop basses avant prise aérienne ; le retour anticipé Mecanim dans PlayerView court-circuite la préparation procédurale. Reproduire visuellement avant correction, ne pas simplement imposer l’ancienne pose.

## Données / carrière / systèmes existants

- Vérification publique du10/10 : réponse ESPN OM dans `.validation/espn-marseille-roster-live.json` :30 joueurs contre24 au catalogue,24 communs. Six jeunes manquants (Koum, Clement, Bang Na, Doubal, Slimani, El Kadmiri), présence corroborée sur https://www.om.fr/en/reserve-team . Non importés : consolider postes, dates de naissance et provenance des évaluations ; ne pas traiter des statistiques absentes comme zéro. ESPN indique25juillet2007 pour El Kadmiri, autres sources25juin : divergence à résoudre avant import. Staff Ferrier/Nouri/Lancet/Farrugia déjà présents, ne pas dupliquer.

-21815 joueurs importés,715 clubs,36 ligues. Aucun fictif au catalogue initial ; newgens à partir2027. Portraits non présents dans le dépôt, initiales affichées.
- Audit interne `.validation/catalogue-source-audit.json` :9174 ont provenance d’effectif directement dans database ; le sidecar couvre les21815 identités,12921 URLs de référence de notes. Ne pas confondre absence du champ embarqué et absence de source. Audit de couverture seulement, pas vérification publique récente ni certification des salaires. `.validation/catalogue-integrity-audit.json` : aucun ID dupliqué, club invalide, niveau hors1–99, potentiel inférieur au niveau ni salaire négatif.
- Recrutement déjà doté de besoins tactiques/budget, cellule, missions, comparaisons, intérêt et négociation. Management, conférences, promesses, formations et finances existent : inspecter avant ajout.
- Dernier ancien audit5 saisons :384 clubs, aucun sous18, un au-dessus40, salaires médians2450→2977€/semaine, libres273→1122 ; `.validation/claude-relay-five-seasons.txt`. Ne prouve pas équilibre sur20 saisons.
- Baseline200 : buts2,61, tirs24,72, cadrés7,74, touches49,93, favori52,53%, penalties0,19, sorties13,79. xG affichés calibrés2,50 seulement ; décisions inchangées. Ligne haute encore excessive.

## Dernière APK — préserver

- `artifacts/Touchline-Unity-0.63-preview.apk`,0.63.0-preview.1/code56,73911418octets, ARM64. Source9d0246f, build37982451316, PR68.
- SHA256 `8BBCA02E30C4F106E5984A3FDA40635F5880DC4AC6B8B596F154EB1A27ED46F6`, `.validation/apk063-proof.json`. Certificat `130917e6d2b4ea2dcca487587dcff03b1356b14e7de39c3e01657097ad57ee13`, package `fr.personal.touchline.unity`.
-0.62 et antérieures/sauvegardes préservées. Aucun essai Fold. Main non fusionnée, retour utilisateur attendu. Prochaine APK nouveau nom0.64/code57 minimum, clé stable disponible seulement en CI. Ne pas remplacer par une clé locale.

## Outils / garde-fous

- Unity locale licenciée6000.3.24f1, RTX5070Ti/D3D11. Une seule instance ; processus parfois invisibles au sandbox : `Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"` avec escalade. Helper `.validation/run-local-unity.ps1`, noms de preuves uniques.
- `.tools/dotnet/dotnet.exe`, `DOTNET_CLI_HOME=.tools/cli`, tools/CoreTests, Calibration, SeasonSim. Attendre les programmes existants avant de reconstruire leurs binaires. Python réel `C:/Users/victo/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe`.
- CI Unity bloquée deux fois avant lancement par Docker Hub anonyme ; ne pas relancer en boucle. Compilation/rendus locaux possibles. `.validation/github-api.ps1` utilise Git Credential Manager sans afficher les secrets ; attacher toute PR créée.
- Nettoyage uniquement après dernière Unity : `.validation/cleanup-unity-imports.ps1` restaure packages-lock/GraphicsSettings et retire les73 imports générés connus. Ne jamais supprimer Library ni restaurer des sources de travail.
- `.validation/` et artefacts locaux ignorés ; preuves sélectionnées dans PR si nécessaire. Automatisation PAUSED. Aucun travail planifié réactivé.
