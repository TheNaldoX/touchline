# HANDOFF.md — Touchline, relais Claude / Codex

Lire AGENTS.md. Ce fichier contient uniquement l’état courant ; l’historique détaillé est dans Git et les PR. Mettre à jour en fin de lot, sans accumuler les comptes rendus anciens.

## Session autonome autorisée — 17 h 19 à 18 h 49 Paris

- Demande utilisateur : améliorer IA/décisions, graphismes/animations, UI, événements aléatoires à choix, finances/budgets et contenu original cohérent. Fin le9 octobre à16:49UTC ; réserver après18h25Paris aux validations. Automatisation touchline-am-liorations-jusqu-au-6-octobre réactivée toutes les10min, avec cette échéance et désactivation finale.
- Unity actuelle : rendu37950676810. Session26334 attend puis déclenche runtime-tests ; ne pas lancer une seconde Unity, ne pas toucher Assets avant la fin de ces jobs.
- Diagnostic finances commencé : ProfessionalManagement.TransferBudget déduit achats engagés, réserve de trésorerie, plafond12% des recettes et obligations des prêts ; WageBudget séparé. Examiner leur présentation et les engagements avant de changer les règles. CareerFinancialTransactions assure les contreparties des transferts : ne pas créer de recettes sans paiement.
- Événements : recherche des classes Event/Incident non concluante ; inspecter CareerManagement et les messages/décisions existants avant de créer un système en doublon. Nouveaux choix à coûts proportionnés au club, effets bornés, aléatoire à graine et persistance testée.

## Reprise immédiate — 9 octobre 2026

- Branche : `feat/recruitment-tactical-decisions`, PR #64, code62eb8a4 ; base PR #63 (`fix/signing-club-history`c022b2c), puis #62/#61.
- Claude et Codex alternent, jamais simultanément. Victor a réinitialisé ses limites et demandé de poursuivre. Il demande aussi une méthode économe : contexte court, lectures ciblées, lots stabilisés avant Unity, attentes scriptées.
- Claude a laissé deux lots, maintenant assemblés : `film/ui-recruitment-cell-2`9983765 (cellule de recrutement) et `film/ui-immersion-focus-1`b3cbf11 (immersion tactique). Un conflit CiUiScreens résolu en conservant les deux parcours.
- Fonctionnalités : missions par recruteur, connaissance régionale, intérêt du joueur, personnalité simulée, adaptation des recrues ; préparation collective, causeries, cris, consignes individuelles et observations de l’adjoint.
- Corrections de reprise : recommandations devenues inaccessibles filtrées ; Resize et ScrollTo séparés dans le scénario de captures ; carrière sans emploi rechargeable même si un ancien titulaire a quitté le club ou pris sa retraite. Les contrôles des matchs actifs et joueurs introuvables restent stricts.
- Validation native1944eef réussie246/246 (Actions37943332562). Puis correction visuelle : boutons de causerie sur deux colonnes, retour à la ligne et hauteur automatique. Rendu ciblé réussi, Actions37944380106 / `film/ui-immersion-focus-talks-relay` /5feca04 : huit captures revues, boutons lisibles, réactions visibles, cibles≥48dp et textes≥12sp. Exception QuickSearch éditeur préexistante seulement. Pas de nouvelle APK ni fusion main.

- Lot courant : arrivées rattachées au club recruteur ; bilans/progression et notifications filtrés, historique conservé. Trois régressions (offre de changement de club + sauvegarde, double arrivée, notification). 916/916 Core. Unity249/249 réussi Actions37947890765 /6a435d8, artefact11624124627 récupéré dans .validation/signing-club-native. Anciennes données sans club rattachées au club courant avant départ ; origine de données déjà mélangées non reconstructible.

## Lot recrutement / conseils / calibration — en validation

- Recommandations triées par budget puis besoins du système, comparaison au titulaire/référent et adéquation tactique connue. Conseils avec essai, compromis et indicateurs ; tempo formulé comme hypothèse.
- Core919/919. Deux graines ×200 avant/après, rapports recruitment-context-* dans tools/Calibration/Reports. Statistiques hors xG identiques ; xG1,69/1,72→2,50/2,52 pour2,61/2,69 buts. Correction de l’estimation seulement, coefficient interne1,65 ; pas validé sur données réelles.
- Ligne haute NON résolue : essai de déplacement différencié retiré (buts/penalties dégradés). Retard de lecture défensive retiré après régressions ; runs flight-read invalides, conflit de compilation .NET, ne pas les citer. Penalties0,19/0,26 ; sorties13,79/13,46 restent inchangés.
- Rendu UI en cours Actions37950676810 /62eb8a4, film/ui-recruitment-focus-context. Attendre, récupérer les captures et les revoir. Monitor séquentiel prévu pour déclencher les tests Unity seulement après réussite du rendu. Ne pas modifier Assets pendant ces jobs. Aucun APK nouveau.

## Preuves disponibles

| Contrôle | Résultat / référence |
|---|---|
| Core assemblage + correctif sauvegarde |913/913 ; `.validation/claude-relay-core-savefix.txt` |
| Unity assemblage avant correctif sauvegarde |235/235 ciblés, Actions37941735253, source8c59424, artefact11622731251 ; `.validation/claude-relay-native` |
| Unity correctif sauvegarde |246/246 ciblés, Actions37943332562, source1944eef, artefact11622083022 ; `.validation/claude-relay-savefix-native` |
| Carrière 1 saison, monde, graine77 |384 clubs jouables, aucun sous18 joueurs ; restauration complète/compacte identique sur22888 joueurs (87,43/5,99Mo). `.validation/claude-relay-season-fixed.txt` |
| Carrière 5 saisons, monde, graine77 |384 clubs : aucun sous18 joueurs, un au-dessus de40 ; sauvegardes identiques sur27368 joueurs. Salaire médian2450→2977 €/semaine, libres273→1122, cinq clubs au-dessus de70% de salaires/recettes. .validation/claude-relay-five-seasons.txt |
| Défaut reproduit avant correction |152270 retraité dans l’ancien onze d’un entraîneur sans emploi ; restauration refusée. `.validation/claude-relay-season.txt` |
| Base sans joueurs fictifs, lot précédent |881 Core et210 Unity ; Actions37926637983, source40e063c, artefact11615140142 ; `.validation/real-catalogue-native` |
| Rendus séparés Claude |Recrutement37939096762 ; immersion37935969487. Succès technique, revue partielle : causerie hors champ et recommandations obsolètes détectées puis corrigées. |

Calibration :200 matchs/graine1, même catalogue. Avant `tools/Calibration/Reports/pressure-penalties-after-200-seed1.txt`, après `claude-relay-after-200-seed1.txt` dans le même dossier.
- Buts2,58→2,61 ; tirs24,60→24,72 ; cadrés7,69→7,74 ; touches49,98→49,93 ; favori54,55→52,53% sur99 affiches.
- Penalties.19 et sorties13,79 inchangés, sous cibles ; xG1,69 pour2,61 buts. Ligne haute encore trop avantageuse. État neutre pas strictement identique à l’ancien moteur ; ne pas affirmer le contraire sur la foi des commentaires de Claude.
- Carrière de contrôle à scores simplifiés, pas une saison de matchs3D. Contrôle cinq saisons effectué, pas de validation sur plusieurs décennies ni test matériel Fold. Le compteur des transferts IA est un historique plafonné à3000, pas un cumul total.

## Données et sauvegardes

- Victor refuse les joueurs fictifs pour élargir la base de départ.21815 entrées importées,715 clubs,36 ligues ; liens source ESPN, sans certification de chaque salaire, note ou effectif actuel.
- Extension fictive2784 joueurs/116 clubs/8 ligues désactivée dans les nouvelles carrières. Son JSON figé reste pour reprendre les anciennes sauvegardes sans supprimer leur progression. Détection aussi par contrats/comptes hérités.
- Aucun jeune inventé au démarrage des nouvelles carrières : `youthGenerationFromYear=2027`. Les newgens futurs restent autorisés selon la demande précédente, clarification utilisateur toujours sans réponse. Centre initial sans promotion fictive ; aucun effectif U19 réel ajouté dans ce lot.
- Les libres sourcés restent importés. Les traits de personnalité et évaluations restent des simulations, pas des faits biographiques. Portraits absents du dépôt : initiales.
- Préserver les fichiers personnels et toutes les APK/preuves existantes. Ne jamais présenter une lecture ou une simulation CI comme un essai sur le téléphone.

## Dernière APK livrée

- `artifacts/Touchline-Unity-0.61-preview.apk`,0.61.0-preview.1/code54,73774374octets.
- Source62579583792280aa8ee7c996d1b47a1e0f88f838 ; Actions37921391496 ; artefact11613111834 ; PR #60.
- SHA256 :503A848CD608C1B01CAB236CC7F4A8E88E5F45582AE01F29BDE0E59C5CF8817D.
- Certificat :130917e6d2b4ea2dcca487587dcff03b1356b14e7de39c3e01657097ad57ee13. Package fr.personal.touchline.unity, ARM64 IL2CPP vérifié.
- **Cette APK contient encore l’extension fictive** et aucun des nouveaux lots de Claude.0.60 et versions antérieures conservées. Installer en mise à jour sans désinstallation. Test Fold toujours attendu.
- Prochaine APK : nouveau nom/version/code (au moins55), après validations et revue visuelle. Ne pas écraser0.61.

## Infrastructure / commandes

- Pas de Unity local. Unity6000.3.24f1 Android via GitHub Actions. Un seul Unity à la fois ; sources figées pendant sa compilation/validation/build.
- `git fetch origin` peut ne récupérer que la branche courante : récupérer explicitement les branches utiles. Le dépôt est partiel ; `git show/diff` peut demander le réseau pour les blobs absents.
- Windows : `.tools/dotnet/dotnet.exe`, `DOTNET_CLI_HOME=.tools/cli`. Outils `tools/CoreTests`, `tools/Calibration`, `tools/SeasonSim`. SeasonSim utilise la base importée ; `--legacy-generated` sert seulement aux anciens diagnostics.
- `.github/workflows/runtime-tests.yml` : workflow_dispatch. Le filtre doit inclure les nouvelles classes testées ; ne pas confondre tests ciblés et toute la suite EditMode.
- `build-android.yml` : workflow_dispatch ou push `release/**` / tag apk-*. Version, code et nom dans Editor/ProjectBuilder.cs ; artefact Touchline-APK. Secrets déjà configurés, ne jamais les afficher.
- `match-film.yml` : push `film/<nom>`. ui* lance CiUiScreens ; proto* lance MecanimPrototypeFilm ; sinon CiMatchFilm. Suffixe-night pour la nuit, goal pour la séquence de but. Résultats dans branche films/dossier films/<nom>.
- Les films utilisent Mesa à pas fixe, pas une mesure FPS Android. Une réussite ne dispense pas de regarder les images ; une carte peut exister dans le DOM sans être visible dans la capture.
- Helpers locaux ignorés `.validation/` : github-api.ps1, watch-run.ps1, download-artifact.ps1, fetch-film.ps1, verify-apk.ps1. Ne pas écraser les sorties existantes. Git/ réseau peuvent nécessiter une escalade.

## Pièges et prochaines étapes

1. Terminer rendu37950676810 et tests natifs PR #64, revue visuelle. Rééquilibrage physique encore ouvert : ne pas présenter les trois demandes comme entièrement terminées.
2. Test Fold réel : fluidité, gels, après-but, nuit, navigation et pliage. Main n’est pas fusionnée ; ne pas contourner ce point de validation.
3. Calibration : ligne haute, sorties, penalties, xG ; audit des consignes sur graines appariées, pas de réglage pour satisfaire une seule graine.
4. Revoir adaptation/recrutement après changement de club et équilibre sur plusieurs saisons. Puis lisibilité portrait et petits libellés11sp.
- PR #53 (expérience ligne défensive e1509ee) rejetée : **ne jamais intégrer**. Pile utile antérieure : #49/50/51/52/54/55→#56→#57→#58→#59→#60→#61→#62.
- Conserver les fins de ligne ; Core ne dépend jamais de Runtime. Qualifier UnityEngine.HumanBone et UnityEngine.UIElements.Position en cas de masquage.
- Shaders runtime référencés dans Resources ; postProcessData requis. Changer GetVersion() de MixamoImportSettings si imports modifiés. MSAA pipeline2×, films4×.
- Exception UnityEditor.Search/QuickSearch au démarrage des rendus existants : pas de pile Touchline ; ne pas la confondre avec une erreur CS du jeu.

Historique complet de la passation avant condensation : `git show 8c59424:HANDOFF.md`. Détails et preuves des lots précédents dans leurs PR ; ne relire que le sujet nécessaire.
