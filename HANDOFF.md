# HANDOFF — Touchline
Lire AGENTS.md. Une tâche/branche/PR ; une Unity locale OU CI, aucun Assets modifié pendant compilation/test/capture/build. Aucun nouvel agent. Préserver sauvegardes/APK/pack privé. Ne jamais imprimer database.json (132Mo).

## Demande / état
Six chantiers : caméra, IA/tactique, équilibre multi-graines, contacts/clips adaptés, U19 réels, 196échecs puis consolidation main. Aucun joueur fictif initial. Automation PAUSED. Quota69% utilisé ; réserver livraison à95%. Main non fusionnée, PR146–178 empilées draft. APK0.71 inchangée.
PR178/11c6867 publiée, branche fix/captured-locomotion-context. Branche de diagnostic fix/distant-keeper-contact sans correction retenue.

## Dernier lot validé
Mecanim court-circuitait ContextBody : port des effets accélération/freinage vers torse anatomique et vigilance gardien vers bassin/bras, appuis capturés conservés. Contexte gardien requis, poses spécialisées inchangées. Trois nouveaux tests30/60/120Hz : torse réactif, pieds<3mm du témoin, pause stable, acteur inchangé. Ciblés44/44 ; globale2628/2661,33anciens/0nouveau (.validation/captured-locomotion-context-global.xml). 163/196 initiaux résolus, dont corrections de fixtures, pas163bugs de jeu. Core1052/1052 inchangé PR177. Compilation sans nouveau diagnosticC#. Démarrage Unity initial annulé avant compilation, relance réussie.
Native captured-locomotion-context-native84images : far-own/shot-preparation f60 et context-switch f90 revues, appuis~1cm. captured-locomotion-balance-native422images, control-chest f45/f111 revues. Pas mesure Fold ni perfection des mouvements ; bras gardien encore génériques. Unity arrêtée/imports nettoyés.
PR175/2b03623 tacle debout préparation/retrait .363/.275→.287/.157m à30Hz ; PR176/d6dd212 tests réception tête mesurent semelles/bassin anatomique et récupération, 12/12 ; PR177 IA ci-dessous. Historique dans PR/Git, preuves .validation et artifacts/unity.

## Moteur retenu / réserves
PR177/1927459 : gardien ne plonge pas sur trajectoire passant>1m hors poteau, marge près cadre préservée. Core1052/1052. Baseline actuelle keeper-wide-reading-after-200-seed1..4.txt.
800matchs graines1–4 vsPR154 : buts2.680→2.568,tirs24.948→24.818,cadrés7.800→7.688,corners9.468→9.795,fautes20.432→20.755,jaunes3.950→3.905,penalties.1975→.215,touches50.115→49.858,sorties13.720→13.768,favoris247/413(59.81%)→242/413(58.60%). Rapports arrondis, keeper-wide-four-seed-summary.csv.
Ligne seed2/200paires : surplus tirs1.75→1.77,buts pour+.20→+.22,buts contre−.81±.20→−.53±.20. Avantage défensif persiste ; sorties/favoris hors objectifs.
Stashes rejetés à préserver : OpponentCoach fenêtre récente, open-space-carrying, through-pass-run-lead80/target-reading ; claims retarget direct corrige7contacts mais crée saut main.784m. Ne pas réappliquer.

## Prochain travail
33échecs exacts dans dernièreXML : claims7, dribble2, distant-high gardien2, transitions pivot/redémarrage/tacle, boot160cm, entre autres. Ne pas desserrer contacts pour verdir. Claims : anticipation corporelle nécessaire, cible préparation limitée35cm vs impact1.05m. Distant-high : contrôler éventuel post-traitement KeeperElbowClearance après Reach, pas encore diagnostiqué. Goal celebration fixture impose largeur bras ancienne au lieu du geste capturé.
Caméra PR14637tests/6vues, portrait rapproché mais confort rapide/Fold non validé. 8/21clips actifs, autres nécessitent raccords adaptés. Ne pas fusionner main avec dette restante.

## Données
22jeunes réels12OM/10Nantes, catalogue21837/715clubs. Sources FCN2025–26 et Montaigu2024, pas roster2026–27. Notes45/potentiel65/valeur50k provisoires, salaire0inconnu. U19 incomplets.
Recherche non intégrée : départs Forget/Khenissi/Oissa annoncés TribuneNantaise22mai2026. Oissa contrat30juin2026 confirmé Transfermarkt ; Forget libre1juillet2026 mais fin contrat inconnue ; Khenissi date effective INCONNUE (ne pas attribuer30juin depuis ligne voisine). Initial15juin cohérent mais droits après été à traiter avec mécanisme FreeAgentReference existant, protéger contrats/promotions/prêts utilisateur. Ne pas inventer dates. Source article https://tribunenantaise.fr/actus-fcnantes/formations-feminines/formation-16-jeunes-non-conserves-par-le-fc-nantes-a-lissue-de-la-saison/ ; Forget https://www.transfermarkt.com/quentin-forget/profil/spieler/1449424 ; Oissa https://www.transfermarkt.co.in/fc-nantes-u19/vertragsende/verein/10851 . Zeze/Camara déjàbase, ne pas dupliquer.

## Outils / livraison
.validation/run-local-unity.ps1 -Mode Test -Name unique -Filter Touchline.Tests. Après arrêt CIM escaladé : cleanup-unity-imports.ps1 puis restaurer scène/ProjectSettings générés. API .validation/github-api.ps1 avec corpsJSONfichier. AttachePRlimite100, ne pas supprimer anciennes.
APK https://github.com/TheNaldoX/touchline/releases/download/v0.71.0-preview.1/Touchline-Unity-0.71-preview.apk ; source0cf0a80a61609df4be7d4e16e44766a6fc547ed1, CI38334925329, preuves .validation/apk071. Packagefr.personal.touchline.unity ARM64 signature habituelle. Prochaine>=0.72/code65, pack privé seulement compilé. Aucun tous-tests-verts/Fold/paritéFM revendiqué.
Diagnostic distant-high : .validation/distant-keeper-contact-stages.log prouve gant déjà éloigné après Reach, aucune dérive due au sol/coude. Résidu avant warp1.68m, limite translation1.1m, contact final.533/.472m. Essai rotation autour bassin45 puis70deg réduit à~.27m mais reste hors17cm : retiré, pas intégré. Traces/candidats XML préservés (distant-keeper-high-direction-targeted/expanded). Pas relancer suite complète car runtime revenu exactement PR178. Prochaine piste : clip de plongeon haut réellement adapté ou validation de portée physique, sans desserrer les tests ni augmenter arbitrairement le warp.
