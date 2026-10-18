# HANDOFF — Touchline
Lire AGENTS.md. Une tâche/branche/PR ; une Unity locale OU CI ; aucun Assets modifié pendant compilation/test/capture/build. Aucun nouvel agent. Préserver sauvegardes/APK/pack privé. Aucun joueur fictif au catalogue initial. Automation PAUSED. Historique détaillé dans Git/PR.

## État courant — 18 octobre
Branche fix/motion-matching-ankle-contact, empilée sur fix/motion-matching-pose-continuity (PR191/dd30cd0), puis PR190/bb11771 et PR189/23747b4. Dernière globale Touchline inchangée :2665/2688,23anciens échecs identiques,0nouveau ; Core1054/1054. Unity arrêtée. Aucun succès Fold revendiqué.

## Dernier lot : orientation des pieds du prototype
Option -FootOrientation inclut corps interpolé et conserve rotation animée cheville pendant IK : les parents déplaçaient l'orteil après calcul du décalage cheville/orteil.4tests géométriques nouveaux (côtés±,yaw0/65) : écart max3.079mm→.000427mm,18contrôles réussis par essai. Preuves .validation/mm-foot-orientation-{30,60,120}/contact-checks.json,pose-metrics.json,result.json,preview-12s.png (revues).22acteurs/20s : P95 glissement120Hz.315→.107m/s,60Hz.148→.085,30Hz.208→.088 ; mêmes nombres d'appuis et pics rotation. Régression appuis PR191 résorbée sur ces trajets, résidu non nul.0erreur animation,CS0618/indexeurEditor préexistants. Aucun AssetsTouchline ni APK modifié. Suite : amortissement du contact/portée, puis branchement trajectoire Touchline sans déplacer Core et vérification FBXfootball/Android ; ne pas prétendre intégré ou Fold validé.

## Dernier lot : articulations du prototype — expérimental uniquement
Option -SubframePose : interpolation corps/vitesses et poses de transition fractionnaires, deux buffers réutilisés, contacts discrets ;8contrôles nouveaux +6racine réussis. Banc aligné2s chauffe/20s mesure à30/60/120Hz, arrêt immédiat en erreur. Preuves mm-pose-reference-{30,60,120}, mm-pose-interpolated-{30,120}, mm-pose-interpolated-60-retry ; captures revues.120Hz articulations immobiles6.379→.303%,pic2412→1218°/s,CPU.883→1.057ms/22acteurs. MAIS P95 glissement appui60Hz.137→.148m/s,30Hz.159→.208 ;120Hz.315m/s (aucun appui verrouillé avant, donc pas comparaison glissement). Ne pas intégrer au jeu : priorité corriger maintien des pieds puis valider trajectoires/FBXfootball/Android.0erreur animation sur essais valides,1erreur indexeurEditor etwarningCS0618amont préexistants. Essai mm-pose-interpolated-60 invalide (conflit accès cache JLData.mmfeatures), arrêté, reprise propre ; mm-pose-before-120 compile banc échouée puis corrigée. Aucun AssetsTouchline modifié, APK inchangée. Détails tableau README/PR.

## Dernier lot : racine du prototype à 120 Hz
PR190 -SubframeRoot : racine et origine de transition interpolées dans le même clip, poses partagées intactes. Preuves .validation/mm-subframe-root-{30,60,120} ;22acteurs/600images :120Hz arrêts50→0%,pasmax5.94→2.97cm. Essai historique plus court, ne pas comparer ses extrêmes au nouveau banc20s. Script refuse modifications externes inconnues ; sans option rétablit contrôleur épinglé. Non intégré à Touchline.

## Lot recrutement validé
CareerAiEconomy : après un achat, marquer le joueur remplacé ET la nouvelle recrue comme besoins satisfaits pour ce mercato estival. Le joueur remplacé conserve ses droits/contrat. Deux besoins distincts au même poste restent possibles. Aucun nouveau champ de sauvegarde.
Reproduction :1/2tests réussis avant (2achats pour1besoin) ;1054/1054Core après, dont2cas nouveaux. Preuves .validation/filled-needs-red.txt, filled-needs-core-fixed.txt, filled-transfer-needs-global.xml/log. Compilation sans diagnostic C# nouveau.
200matchsseed1 : rapport filled-needs-calibration-200-seed1.txt identique à presser-availability-before-200-seed1.txt (buts2.56,tirs25.14,sorties13.92,favoris53.54%). Pas modification moteur.
SeasonSim3saisons/worldseed77 : référence recruitment-reference-before-world77.txt, après filled-needs-world77-report.txt ; effectif médian30inchangé, max40→41 ; ratio médian salaires/recettes36%inchangé, clubs endettés42→41, clubs>70%salaires3→4. Pas preuve équilibre de plusieurs décennies ; un sureffectif41reste à diagnostiquer.

## Évaluation externe — PR188
tools/MotionMatchingProbe : projet amont35fc8ee isolé sous .validation/external-research, aucune dépendance ajoutée à Touchline.1/22personnages,30/60/120Hz,600images après120chauffe ; capturées et revues mm-single-60/mm-team-60.22acteurs60Hz~.97msCPUanimationPC,p95~1.57ms, sans rendu/jeu/Android.120Hz :50%images sans déplacement de racine contre0%60Hz. Ne pas intégrer tel quel. Une erreur indexeur UnityEditor.Search rapportée séparément,0erreur animation. Droits des données/FBX et Android à vérifier avant reprise ; code MIT.
Sources étudiées : Pyrus2D9c5b17f (MIT), interception/temps de récupération ; OpenFootball bfb2273 (Apache2), besoins/titulaire/budget et besoin déjà satisfait ; OpenFootManager référence de conception GPLv3, aucun code importé. Liens/prérequis dans README de l'outil.
Animations :8/21clips du pack actifs. Aucun nouveau pack acheté ni intégré. Priorité gestes manquants : plongeons hauts complets, contrôles en course, pivots/freinages, récupérations après tacle ; vérifier d'abord le stock.

## Essais IA rejetés — à préserver
Presseur au sol sélectionné par distance reproduit4cas. Exclusion stricte :200seed1 favoris53.54→60.61%,sorties13.92→13.28 ; variante coût de récupération6m/s :60.61%,13.44,pens.19. Non retenues, Core revenu à la base. Stash rejected-presser-recovery-seed1-favourites60.61-goalkicks13.44 ; preuves .validation/presser-*. Première globale available-presser-global interrompue dès rejet calibration, ne pas citer comme validation achevée.
Autres stashes rejetés conservés : OpponentCoach fenêtre récente, open-space-carrying, through-pass-run-lead80/target-reading, claims retarget, keeper-high-reach-volume. Ne pas réappliquer sans nouveau diagnostic.

## Match/caméra/contacts restant
PR185/24c0020 : continuité zoom retour replay ; PR186/57e526d : contact dribble capturé run/idle ; PR187/fa2b085 : correction minimale aux bords du cadrage. Portrait×10pasmax8.5→6.3m,×1 .6→.5m ; recul62.9→65.1m, tribunes empiètent encore. Pas confort Fold validé.
Core PR177/1927459 : gardien ignore trajectoire>1m hors poteau.1600matchs graines1–8 : favoris509/831=61.25%. Ligne haute avantageuse :200pairesseed2 butscontre−.53±.20.23échecs connus concernent contacts/récupérations/fixtures, voir XML.
Gardiens hauts : résidu avant warp1.68m,cap1.1m,gants.533/.472m ; essais tilt45/70° et portée Core rejetés. Claims35cmpréparation/1.05mimpact : retarget direct rejeté (sautmain.784m).
Revue clipsRohr15/16 entière déjà faite : build/film/keeper-pack-late-review-20261018,f85/110/152/165. Pas remplaçants évidents du plongeon haut. Piste : comparer Mixamo Goalkeeper Diving Save et variante(1), déjà présentes ; contact à établir visuellement (pas déduit du picmain).

## Données, branches et APK
Base utilisée SeasonSim21815joueurs/715clubs + supplément22jeunes réels OM/Nantes, pas U19complets ; données2025–26/2024 et notes/valeurs provisoires signalées. Départs Forget/Oissa/Khenissi à sourcer, protéger prêts/promotions/contrats utilisateur. Aucun catalogue externe généré importé.
PR184 tactique Claude ouverte : rôles/consignes/plansABC/entraînement individuel ; ne pas dupliquer. Réserves : IA n'utilise pas toutes options, bonus individuel sans coût.
main intègre0.72 viaPR183 ; correctifs suivants restent en PR draft. Dernière APK0.72.0-preview.1/code65 source6fde23a92c8c7acc27e7a732b6277946186fe9d1,CI38414356508,packagefr.personal.touchline.unity,ARM64. SHA256 EDD3E691E5AF230DAAD52E25AB5DD74A539C830726459AC26BCC128242994E45. Preuves .validation/apk072. Lien https://github.com/TheNaldoX/touchline/releases/download/v0.72.0-preview.1/Touchline-Unity-0.72-preview.apk . Prochaine>=0.73/code66 ; aucune APK construite sur ces lots.

## Suite et outils
Priorités : stabiliser23échecs contacts/caméra, poursuivre IA sans dégrader calibration ; management besoins par titulaire/système tactique et sureffectifs sur durée. PrototypeMM à adapter avant intégration, pas remplacement direct.
.validation/run-local-unity.ps1 ; nettoyage après arrêt CIM escaladé via cleanup-unity-imports.ps1, restaurer seulement scène/settings générés. Core .tools/dotnet/dotnet.exe avec DOTNET_CLI_HOME=.tools/cli. API .validation/github-api.ps1 (corpsJSONfichier). Ne jamais imprimer database.json. Pas de journal docs. Quota5%utilisé vérifié début session ; garder marge livraison au seuil95% utilisé demandé par utilisateur.
