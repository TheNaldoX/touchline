# HANDOFF.md — Touchline
Lire AGENTS.md. Claude et Codex alternent, jamais simultanément. Historique dans Git/PR, preuves locales ignorées dans .validation et artifacts.

## État courant —12 octobre2026
- Lot coup franc : passe vers partenaire clairement mieux placé plutôt que frappe automatique ; sécurité/hors-jeu et spécialité tireur considérés. Core1019/1019, UnityRestart17/17. Calibration200/graine1 strictement mêmes moyennes : cas rare, pas rééquilibrage global. Capture central-free-kick-choice-native deux instants revus, ne prouve pas la nouvelle combinaison en match naturel. Prochain : atténuation du lien hauteur de ligne/attaquants, audit apparié en cours.
- Lot ergonomie presse : décor réduit après introduction, trois choix entièrement visibles dans les deux captures ui-press-interview-answer-space ; assertion viewport réussie. Core inchangé1016/1016. Fold matériel toujours non testé. Prochain : choix sur coups francs puis ligne haute.
- Lot presse contextuelle : blessures actuelles distinctes, départ demandé et calendrier chargé pilotent la troisième question/phrases ; transcript exact conservé. Core1016/1016, Unity18/18, captures ui-press-interview-contextual revues. Salle stylisée encore volumineuse en déplié : prochain lot ergonomie, puis décisions/coups francs. Aucun effet médical ou promesse implicite ajouté.
- Lot fix/sale-buyer-needs : besoins au poste, trésorerie et masse salariale des acheteurs vérifiés avant offre/réponse/signature ; projet sportif affiché. Core1012/1012, Unity67/67, deux formats UI revus, calibration200/graine1 identique. APK0.66 inchangée. Prochain : presse contextuelle puis axes tactique/coups francs/Fold encore ouverts.
- Demande en cours : traiter les6axes (contacts, décisions/tactique, coups francs, recrutement/ventes, Fold, management). Ne pas annoncer les6terminés. Lot fix/contact-animation-clock :7cas supplémentaires synchronisés, tête progressive ; relance au sol rejetée pour pénétration du sol. Globale finale2153/2405,252anciens échecs/0nouveau. Ciblés116/134 (18anciens), film contact-clock-final-goal revu sur captures, aucunCoremodifié. APK0.66 inchangée. Prochain lot : besoins sportifs/budget des acheteurs. Questions Fold0.66 en attente utilisateur.
- Lot fix/kick-animation-clock : frappes capturées et correction de contact de passe utilisent le temps interpolé de simulation. Entrée à mi-geste reproduite en3échecs avant ;43/43 ciblés et Core1006/1006 après. Globale kick-clock-global2146/2398,252échecs anciens, aucun nouveau (2 anciens corrigés). Film kick-clock-native/images revues. Pas de modificationCore, pas nouvellemocap, pas Fold testé. APK0.66 inchangée, correction pas encore livrée en APK.
- Branche fix/release066-feedback, PR107 : APK0.66/code59 livrée, source0d5fdc243d568e12f197afc4c77e93a880d505ca sur PR106/ddfb7e7 ; aucun changement de logique depuis la globale validée. Ajout de centres sur coups francs excentrés selon duel aérien, gardien, hors-jeu et tactique. Core1006/1006, Unity73/73. Calibration200matchs sur graines1/2 avant/après dans .validation/wide-free-kick-calibration.md. Film natif wide-free-kick-delivery-native revu partiellement : ne prouve pas toutes les variantes ni la fluidité Fold. Suite globale feedback-all-final :2141/2395,254échecs identiques à throw-plant-all-final.xml, aucun nouveau.
- PR96–105 brouillons empilés, attachés : bilan après délégation ; tactique (banc cliquable, consignes repliées, un scroll) ; résultats/classement bureau ; conférence3questions/phrases/transcript ; sélection des moments clés ; aperçu exact des11discussions ; négociation de vente ; boutons fixes pendant appui ; préparation coups francs accélérée ; formulaire transfert hiérarchisé avec envoi fixe.
- Preuves natives revues : delegation-recap-verified, tactical-selection-native, ui-dashboard-results-final, ui-press-interview-compact, ui-discussion-preview-final, ui-sale-negotiation-reviewed, ui-button-motion-fixed, ui-free-kick-wait-reviewed, ui-transfer-form-labelled. Certaines sont dans artifacts/unity, les autres unity/Touchline/build/film.
- Bouton raté reproduit : ui-button-motion-baseline=0clic car entrée déplacée et active réduite ; fixed=1clic, rectangle stable. Test pointeur synthétique, pas Android. Conférence : décor UI stylisé, pas salle3D. Quatre modes de visionnage donnent le même état final90minutes.
- Dernière globale avant ces lots2107/2361 (254échecs préexistants). ProfessionalTests199/202 : les3échecs déjà présents concernent AiSurplusLeavesAtExpiryWithoutImmediateSalaryCut, LegacyKeeperMigrationPreservesSignedClubAndAddsRecognizedKeeperKeys, TwoGenerationsRetainClubLevelAndSalaryScale. Ne pas déclarer tous les tests verts.

## Livraison
- APK0.66.0-preview.1/code59 : artifacts/Touchline-Unity-0.66-preview.apk,73956606octets ; CI38222681759 réussie. Signature stable, package et ARM64 vérifiés : .validation/apk066/verified.json. SHA25608BF5F0042B4E467D5169A9CEAD50A6DBFDD8FD90D5189F81E90F3B598DA6E4B. Inclut PR96–106. Fold non testé.
- Ancienne APK0.65.0-preview.1/code58 : artifacts/Touchline-Unity-0.65-preview.apk (73937734octets), source71f2b3076c7e4d5db30018ca3288135c527f89f4, PR95/CI38028354263. Ne contient PAS PR96–106.
- Preuve .validation/apk065/verified.json ; SHA256 D8C37FB3E479AF45550F851F13820747FE0F51F57895B39DA174CB81430642E2. Package fr.personal.touchline.unity, cert130917e6d2b4ea2dcca487587dcff03b1356b14e7de39c3e01657097ad57ee13, ARM64.
- Prochaine APK au moins0.67/code60, nom distinct ; clé stable en CI. Préserver anciennes APK et sauvegardes. Main non fusionnée, Fold réel toujours non testé.
- Automatisation PAUSED, anciennes échéances expirées. Quota vérifié12/10 :11% utilisé. Seuil utilisateur : dès92% utilisés, aucun nouveau lot, finir APK et livraison.

## Limites moteur et essais rejetés
- Référence avant dernier lot : graine1/200 buts2,61, tirs24,72, cadrés7,74, corners10,18, fautes20,80, jaunes3,75, hors-jeu4,05, touches49,93, penalties0,19, sorties13,79, favoris52,53%. Après :2,59buts/24,78tirs/49,89touches/.19pen/13,82sorties/favoris52,53%.
- Graine2 favoris62,14→63,11% (une victoire sur103 en plus), tirs24,25 inchangés. Ligne haute trop avantageuse (+1,13but net sur200paires baseline), non résolue. Pas de perfection ni paritéFM démontrée.
- Ne pas intégrer les essais rejetés : through-pass-running-lead364b411, reception-braking-distance df70bbf, blind-interception-contact4c40e01, through-pass-target-reading2e893e6, defensive-flight-reading37b3f57, defensive-turn-recovery0defb19. Défauts locaux corrigés mais calibration globale dégradée. Détails Git/anciennes PR et .validation.

## Carrière et données
- Catalogue21815 joueurs importés,715clubs/36ligues ; aucun fictif initial, newgens dès2027. Portraits absents : initiales. catalogue-source-audit.json couvre21815identités et12921URLs de notes ; pas une validation récente de tous les salaires.
- Audit20saisons source6b5205b/graine77 :384clubs, effectifs23/30/42, un>40 ; note64,3 stable, salaires/recettes35→36%,2clubs>70%,132endettés. Sauvegardes complet/compact identiques47867joueurs+7jours. Scores simplifiés, pas3D. Preuve release064-world20-seed77.txt.
- Recrutement/promesses/presse/formation/finances existent ; inspecter avant ajout. Ventes : contre-offre/réponse lendemain,3tours max, plafond15% offre initiale, échéance et propriété contrôlées. Sélection initiale acheteur et son contrat restent simplifiées.

## Outils
- Unity6000.3.24f1/RTX5070Ti/D3D11. Une seule Unity locale/CI ; AUCUNE édition Assets pendant compilation/test/capture/build. Vérifier Get-CimInstance avec escalade si nécessaire.
- .validation/run-local-unity.ps1 ; .tools/dotnet/dotnet.exe (DOTNET_CLI_HOME=.tools/cli), CoreTests/Calibration/SeasonSim. Lire résultats, shell peut masquer un échec.
- .validation/github-api.ps1 via GCM ; jamais imprimer secrets. PR une tâche/branche, attacher chaquePR. Attente scriptée de CI, pas de polls répétés.
- Après Unity seulement : .validation/cleanup-unity-imports.ps1 enlève73imports connus ; restore scène/ProjectSettings uniquement si modifications générées. Pas de copies de sources, pas de docs journal.
- Prochain : retour Fold sur0.66 (appuis, tactique, clavier mercato, séquences coups francs), puis bugs visibles et déséquilibres moteur encore ouverts. Les252échecs préexistants et le rendu naturel incomplet restent explicitement non résolus.




