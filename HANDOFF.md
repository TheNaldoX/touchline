# HANDOFF.md — Touchline

Lire AGENTS.md. Claude et Codex alternent, jamais simultanément. Historique dans Git/PR, pas de nouveaux journaux docs.

## Lot courant — finances et billetterie

- Branche `feat/finance-ticket-preview`, base PR79/661ece9. Quatre onglets avec défilement séparé, estimation avant confirmation incluant la réaction des supporters, délai hebdomadaire explicite. Flux comptables30 jours et état vide ; statuts sponsors français ; alerte salariale ouvre Budget directement.
- Core941/941 (`ticket-preview-core-final.txt`), quatre nouveaux tests read-only/appliqué/délai/plage ; Unity192/195 ciblés, trois échecs déjà présents dans PR79, aucun nouveau. Captures natives des quatre onglets aux deux formats revues (`ui-finance-preview-links`), parcours aperçu/confirmation/délai/alerte réussi. Exception préexistante UnityEditor.Search au démarrage, aucune exception du jeu. Badge notifications préexistant11sp ; aucun bouton<48dp dans ce parcours.
- Suite globale de référence PR79 :2007/2288,281 échecs ; pas de nouvelle APK ni Fold physique. Pas de modification de la demande ou des tarifs appliqués, seulement un aperçu utilisant leur formule. Restent moteur/ligne haute, contacts, données et profondeur de carrière.

## Appuis Mecanim — PR79

- Branche `fix/mecanim-ground-contact`, base PR78/7ab45e0. Réutilise la correction de semelle au sol après les couches Mecanim, en conservant rotation du pied et plan du genou. Aucun moteur/ballon modifié.
- Trois nouveaux tests de récupération de touche échouent avant (5,5–6,6cm sous le sol), passent après à30/60/120Hz. Core937/937 ; Unity2007/2288,281 échecs restants,18 anciens corrigés, aucun nouveau (`ground-contact-comparison.json`). Captures natives avant/après revues, vidéo `artifacts/Touchline-throw-support-before-after-2026-10-10.mp4` (avant gauche/après droite, ralenti2×). Ballon de référence immobile ; ne prouve pas le contact mains/ballon. Pas de mesure Android.
- Prochain lot : finances en onglets et aperçu billetterie sans modifier la carrière avant confirmation. Dette d'animations et ligne haute toujours ouvertes.

## Contrats après sauvegarde — PR78

- Branche `fix/loan-state-after-save`, base PR77/b2a8c3d. `Employment.IsLoan` distingue un vrai club prêteur d'une chaîne vide après JsonUtility ; usages unifiés dans retours, salaires, fins de contrat, ventes, formation et UI. Pas de changement du format enregistré ni du moteur de match.
- Régression avant10/13, après Core937/937 (`loan-state-core.txt`), trois nouveaux scénarios de restauration : joueurs permanents, vente et partage salarial/retour d'un vrai prêt. Unity complète1986/2285, mêmes299 échecs, aucun nouveau (`loan-state-comparison.json`), aucun diagnostic C# nouveau. Captures natives réussies et revues plié/déplié (`ui-loan-save-final`) : option prêt activée avec champ prêteur vide. Pas de Fold physique ni de nouvelle APK. Prochaine priorité : dette d'animations (contacts/sol), puis ligne haute avec hypothèse nouvelle et calibration appariée.
- `loan-state-two-seasons-final.txt` : deux saisons monde/graine77,384 clubs jouables, effectifs23/29/37 (min/médiane/max), aucun sous18 ou au-dessus40 ; salaires/recettes médiane36%, trois clubs>70%. Sauvegardes complète/compacte identiques pour23682 joueurs, puis sept jours repris identiques avec clubs valides. Premier contrôle rejetait à tort les identifiants `academy-`, corrigé dans l'outil. Scores simplifiés, pas de preuve Android ni d'équilibre sur des décennies.

## Relances des joueurs — PR77

- Branche `fix/departure-followup`, base PR76/d24adb7. Après un refus, relance après28 jours de préoccupation persistante ; indisponibilité/amélioration réinitialise le délai. Seule la dernière demande est actionnable. Champ de club prêteur vide accepté comme absence de prêt après sérialisation.
- Régression avant0/1, après Core934/934 (`departure-followup-core-save.txt`) et Unity12/12 (`departure-followup-save-tests.xml`). Trois nouveaux cas, dont reprise après sauvegarde/blessure. Capture native `ui-departure-followup-native` réussie, message plié et discussion dépliée revus. Pas de nouvelle APK. À vérifier ensuite : autres comparaisons `parent!=null` dans prêts/finances/carrière ; elles peuvent confondre chaîne vide et prêt après chargement.

## Bilan des occasions — PR76

- Branche `feat/match-chance-review`, base PR75/5375b7a. Carte dans Analyse/Adjoint : tirs, cadrés, xG, qualité moyenne, conseil prudent après six tirs ; explication dépliable. Bilan final distingue consignes finales et statistiques cumulées. Aucun calcul de tir, décision ou aléatoire modifié.
- Core931/931 (`chance-review-core.txt`), dont5 nouveaux cas ; Unity22/22 MatchImmersionTests (`chance-review-tests.xml`). Texte compact ensuite revérifié4/4 Core et compilation/captures natives `ui-chance-review-final`, deux formats revus, pas de petit texte ni bouton<48dp dans ce parcours. Fixture synthétique10tirs/0,50xG, pas une preuve d’équilibrage. Premier rendu échouait à rechercher une arène inactive dans la fixture ; corrigé.
- Dernière suite globale reste celle de PR73 :1975/2274 et299 anciens échecs ; non relancée pour ces écrans. Aucun test Fold physique ou nouvelle APK. Restent ouverts : équilibre ligne haute/penalties/sorties, contacts d’animation, profondeur management, sources financières et présentation. Ne pas annoncer tous les chantiers terminés.

## Navigation et marché — PR75

- Branche `fix/workspace-scroll-memory`, base PR74/ad52d6a. Positions séparées par onglet (recrutement, calendrier, tactique) et format. Titres latéraux12sp ; marché déplié : lignes104 unités pour éviter le chevauchement du nom/salaire/rapport, recherche sur une ligne dédiée et filtres visibles.
- Régression native `BuildScrollMemorySteps` : échec avant dans les deux formats (`scroll-memory-repro.log`), réussite après (`scroll-memory-final.log`). Six captures ; retour marché revu plié/déplié. Assertions position, contenu dans sa ligne et largeur des filtres. Première fixture sans monde corrigée ; essai de barre qui masquait les filtres corrigé avant livraison. Aucun changement Core,926/926 hérités ; suite Unity globale reste1975/2274,299 anciens échecs. Pas de nouvelle APK ni validation physique Fold.

## Recrutement après changement tactique — PR74

- Branche `fix/recruitment-tactic-refresh`, base PR73/c6c666b, code a0fb3a9. Invalidation du cache des besoins à chaque reconstruction d’écran ; réutilisation conservée entre les cartes du même écran. Corrige les postes modifiés sans changement de nom de formation ni de jour. Budget déjà recalculé correctement avant ce lot.
- Régression native `BuildRecruitmentCacheSteps` : échec avant dans les deux résolutions, réussite après ; quatre captures après revues (`build/film/ui-recruitment-cache-{before,after}`). Aucun avertissement/erreur C# ; exception préexistante de l’indexation UnityEditor.Search au démarrage, distincte du jeu. Pas de test Fold physique. Trois titres latéraux préexistants à11sp.
- Core inchangé,926/926 hérités. Dernière suite complète de la base PR73 :1975/2274,299 échecs préexistants ; ne pas déclarer la suite verte. Prochaine priorité : diagnostiquer la ligne haute avec une nouvelle hypothèse, puis calibration appariée200 matchs. Pas de nouvelle APK.

## Lot gardiens — PR73

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
- Ces PR restent en brouillon : dette globale d’animations et Fold non testé. Préparation aérienne ensuite corrigée dans PR73 ; autres défauts de contacts et réception à traiter séparément.

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
