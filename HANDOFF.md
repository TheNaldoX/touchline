# HANDOFF.md — Touchline
Lire AGENTS.md. Claude et Codex alternent, jamais simultanément. Historique dans Git/PR, preuves locales ignorées dans .validation et artifacts.

## État courant —12 octobre2026
- Branche tune/wide-free-kick-delivery, empilée sur PR105/0daec3b. Ajout de centres sur coups francs excentrés selon duel aérien, gardien, hors-jeu et tactique. Core1006/1006, Unity73/73. Calibration200matchs sur graines1/2 avant/après dans .validation/wide-free-kick-calibration.md. Film natif wide-free-kick-delivery-native revu partiellement : ne prouve pas toutes les variantes ni la fluidité Fold. Suite globale feedback-all-final :2141/2395,254échecs identiques à throw-plant-all-final.xml, aucun nouveau.
- PR96–105 brouillons empilés, attachés : bilan après délégation ; tactique (banc cliquable, consignes repliées, un scroll) ; résultats/classement bureau ; conférence3questions/phrases/transcript ; sélection des moments clés ; aperçu exact des11discussions ; négociation de vente ; boutons fixes pendant appui ; préparation coups francs accélérée ; formulaire transfert hiérarchisé avec envoi fixe.
- Preuves natives revues : delegation-recap-verified, tactical-selection-native, ui-dashboard-results-final, ui-press-interview-compact, ui-discussion-preview-final, ui-sale-negotiation-reviewed, ui-button-motion-fixed, ui-free-kick-wait-reviewed, ui-transfer-form-labelled. Certaines sont dans artifacts/unity, les autres unity/Touchline/build/film.
- Bouton raté reproduit : ui-button-motion-baseline=0clic car entrée déplacée et active réduite ; fixed=1clic, rectangle stable. Test pointeur synthétique, pas Android. Conférence : décor UI stylisé, pas salle3D. Quatre modes de visionnage donnent le même état final90minutes.
- Dernière globale avant ces lots2107/2361 (254échecs préexistants). ProfessionalTests199/202 : les3échecs déjà présents concernent AiSurplusLeavesAtExpiryWithoutImmediateSalaryCut, LegacyKeeperMigrationPreservesSignedClubAndAddsRecognizedKeeperKeys, TwoGenerationsRetainClubLevelAndSalaryScale. Ne pas déclarer tous les tests verts.

## Livraison
- APK actuelle0.65.0-preview.1/code58 : artifacts/Touchline-Unity-0.65-preview.apk (73937734octets), source71f2b3076c7e4d5db30018ca3288135c527f89f4, PR95/CI38028354263. Ne contient PAS PR96–105.
- Preuve .validation/apk065/verified.json ; SHA256 D8C37FB3E479AF45550F851F13820747FE0F51F57895B39DA174CB81430642E2. Package fr.personal.touchline.unity, cert130917e6d2b4ea2dcca487587dcff03b1356b14e7de39c3e01657097ad57ee13, ARM64.
- Prochaine APK au moins0.66/code59, nom distinct ; clé stable en CI. Préserver anciennes APK et sauvegardes. Main non fusionnée, Fold réel toujours non testé.
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
- Prochain : résultat globale et comparaison échecs, finaliser PR coups francs, versionner/build APK distincte vérifiée. Les limites Fold et moteur restantes doivent être explicitement annoncées.

