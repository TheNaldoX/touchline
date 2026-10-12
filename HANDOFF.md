# HANDOFF.md — Touchline
Lire AGENTS.md. Claude et Codex alternent, jamais simultanément. Historique dans Git/PR, preuves locales ignorées dans .validation et artifacts.

## État courant — 12 octobre 2026
- Lot scout-comparison-uncertainty : verdict des comparaisons fondé sur toute la fourchette du rapport, sans lecture de note cachée ; Core1023/1023, Unity29/29. ADB revérifié : aucun appareil. Les cinq points demandés restent en cours.
- Demande : terminer les six axes. NON terminé : ligne haute trop avantageuse, 234 échecs Unity anciens, rendu naturel incomplet et Fold non testé. Ne pas annoncer tous les axes achevés.
- Branche fix/captured-keeper-contact : contact des plongeons Mecanim corrigé par retarget continu des bras/corps, sans changer le ballon simulé. 14/14 ciblés à 30/60/120Hz ; globale six-axes-regression-global 2190/2424, 234 anciens échecs, zéro nouveau, 18 anciens corrigés. Captures keeper-contact-native revues partiellement : arrêt réel graine731 à319,5s ; caméra suivie cadre mal le contact. Quelques pénétrations du sol persistent (jusqu’à3mm aggravées dans cas déjà en échec). Pas de preuve de fluidité Fold.
- PR108–109 : horloges des frappes et autres contacts synchronisées au rendu interpolé. PR110 : acheteurs selon poste/budget/salaires, vérification à chaque étape, Unity67/67. PR111–112 : troisième question de presse contextuelle, réponses entièrement visibles dans deux formats natifs. PR113 : combinaison de coup franc si partenaire nettement mieux placé, Unity17/17, calibration200/graine1 identique (variante rare). PR114 : conseils adjoint sur faible qualité des tirs, action sur tactique, Unity4/4, captures natives revues. Core1019/1019. PR brouillons empilées, toutes attachées ; aucune dans APK0.66.
- Rejetés, non intégrés : a4aa430 ligne découplée (favoris61,62%, touches51,19), c84b5b9 conduite libre (favoris59,60%, avantage ligne1,20), 1445bf2 cohésion ligne (favoris57,58%, avantage ligne1,44 contre1,09). Preuves .validation/line-block-*, carry-space-*, line-cohesion-*.
- PR96–106 dans APK0.66 : bilan après délégation, tactique/banc, résultats et classement bureau, presse/dialogues, moments clés, négociations de vente et transfert, boutons fixes, préparation et centres sur coups francs.
- ADB12/10 : aucun appareil. Questions Fold et connexion USB en attente ; ne pas prétendre tester le téléphone. Prochain : anciens défauts de contact, rééquilibrage ligne haute, puis consolidation et APK distincte.
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





