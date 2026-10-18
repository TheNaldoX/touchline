# HANDOFF — Touchline
Lire AGENTS.md. Une tâche/branche/PR ; une Unity locale OU CI. Aucun Assets modifié pendant compilation/test/capture/build. Aucun nouvel agent. Préserver sauvegardes/APK/pack privé. Pas de copie de sources ni journal docs. Automation PAUSED.

## Priorités utilisateur (18 octobre)
Caméra/contacts → décisions/équilibrage → tactique/recrutement → carrière/données → finition. Fold0.72 : mouvements et zooms brusques. Aucun joueur fictif initial. Tout n'est pas terminé ; main non fusionnée, PR empilées draft. Quota vérifié0% utilisé le18octobre ; garder marge livraison à95% selon instruction antérieure.

## Base et APK
PR182/3ac8915 : zoom boutons/pincement progressif, reproduction saut7.96m ; ciblés43/43,24captures natives. PR181/ac904b0 corrige6fixtures pivots (compteur procédural remplacé par rotation visible),9/9 dont3pauses. PR180/63639ed limite rotation genoux540deg/s, appuis préservés, globale2636/2667 :31anciens/0nouveau.25attendus aprèsPR181, à confirmer.
APK0.72.0-preview.1/code65 publiée : source6fde23a92c8c7acc27e7a732b6277946186fe9d1, CI38414356508, packagefr.personal.touchline.unity,ARM64,signature habituelle. SHA256EDD3E691E5AF230DAAD52E25AB5DD74A539C830726459AC26BCC128242994E45. Preuves .validation/apk072. Lien https://github.com/TheNaldoX/touchline/releases/download/v0.72.0-preview.1/Touchline-Unity-0.72-preview.apk . PR180–182 non incluses. Prochaine>=0.73/code66. Fold non validé.

## Lot en cours
Branche fix/replay-live-camera-continuity, non committée : supprimer reset de distance caméra après replay. Replay restaure déjà pose du direct ; reset provoquait un second saut au passage layout suivant. Reproduction2échecs/9,19cm portrait ; après56/56ciblés, Core1052/1052 (.validation/replay-return-after.xml et replay-return-core.txt). Nouveaux cas même format, Passer et fin naturelle, zoom en cours ; préférence zoom isolée/restaurée.
Native GoalReplayUISmoke réussie : artifacts/unity/replay-return-native, revue replay-portrait/skipped-landscape. Reprise, bouton et état simulé vérifiés. Cadrage ultra-large encore éloigné, portrait replay beaucoup de ciel ; pas perfection revendiquée.
Globale replay-return-global terminée :2649/2674,25anciens échecs/0nouveau ;6fixtures pivots PR181 confirmées résolues. Unity arrêtée, imports/scène/settings nettoyés. Publier PR sur fix/smooth-manual-match-zoom.

## Prochain diagnostic contacts
PlayerView.Render retourne après Mecanim sans FinalBallContacts (appel seulement chemin procédural). Deux ActionCoverage.CloseDribbleTouchMeetsBallAndDoesNotMoveActor échouent. Raccorder uniquement touches run/idle, ne pas écraser passes capturées. Phase Core MatchBall.UpdateOwnedBall=stride*2.4 ; minimum ballon .30m, maximum .56m. Vérifier continuité, pieds gauche/droit,30/60/120Hz, pause et acteur immuable avant retenir.
Suivi caméra encore .5/sqrt(Speed), clamp focus et confinement immédiats ; pas corrigés par zoom/replay.

## Réserves moteur et contacts
Core retenu PR177/1927459 : gardien ignore trajectoire>1m hors poteau. Core1052.800matchs graines1–4 : buts2.568,tirs24.818,cadrés7.688,corners9.795,fautes20.755,jaunes3.905,pens.215,touches49.858,sorties13.768,favoris242/413=58.60%. Ajout graines5–8 : favoris267/418 ; total1600matchs509/831=61.25%. Rapports keeper-wide-reading-after-200-seed1..4 / keeper-wide-extension-200-seed5..8. Ligne haute garde avantage défensif :200pairesseed2 butscontre−.53±.20. Ne pas déclarer équilibre résolu.
Distant-high : .validation/distant-keeper-contact-stages.log montre résidu avant warp1.68m, limite1.1m, contact.533/.472m ; pas causé par coude/sol. Tilt45/70deg reste~.27m, retiré. Essai portée Core en hauteur rejeté : gants.36/.29m toujours>17cm ;200seed1 favoris53.54→61.62%,buts2.56→2.71. Stashes conservés ; Calibration.dll restauré CorePR177.
Claims préparation35cm vs impact1.05m : retarget direct corrige7 mais saut main.784m, rejeté. Nécessite anticipation corporelle.8/21clips actifs. Autres échecs : dribble2, boot160, récupérations gardien/tacle, appuis frappe et locomotion, célébration fixture.
Stashes rejetés à préserver : OpponentCoach fenêtre récente, open-space-carrying, through-pass-run-lead80/target-reading, claims retarget, keeper-high-reach-volume. Ne pas réappliquer sans nouveau diagnostic.

## Données
Catalogue21837joueurs/715clubs ; supplément22jeunes réels12OM/10Nantes, pas U19 complets. Sources FCN2025–26 et Montaigu2024, pas roster2026–27. Notes45/potentiel65/valeur50k provisoires,salaire0inconnu.
Départs à traiter sans inventer dates : Forget libre1juillet2026 TM mais expiration inconnue ; Oissa contrat30juin2026 TM ; Khenissi date effective inconnue. Article TribuneNantaise22mai2026 annonce fin saison. Utiliser FreeAgentReference existant, protéger contrats/promotions/prêts utilisateur. Zeze/Camara déjàbase. Rumeur Forget Cagliari non confirmée, ne pas importer. Recherche détaillée dans historique Git/PR, aucune donnée modifiée sur lots caméra.

## Outils
.validation/run-local-unity.ps1 -Mode Test -Name unique -Filter Touchline.Tests ; preuves longues .validation/artifacts seulement. Après arrêt CIM escaladé, cleanup-unity-imports.ps1 puis restaurer Touchline.unity et ProjectSettings.asset générés. Core via .tools/dotnet/dotnet.exe, DOTNET_CLI_HOME=.tools/cli. Nouveaux tests Runtime à exclure dans tools/CoreTests/excluded-tests.props.
API .validation/github-api.ps1 avec corpsJSONfichier ; sortie sélectionner number/html_url. AttachePR limite100, ne pas supprimer anciennes. PR factual template, changelog1ligne. Ne jamais imprimer database.json132Mo.
