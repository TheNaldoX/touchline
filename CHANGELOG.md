# Changelog

Une ligne par pull request, la plus récente en haut.

## À venir
- Outil : film de match rendu par Unity dans GitHub Actions (branche film/…), avec mesures de fluidité, publié dans la branche films.
- Animation : rotation du corps plafonnée à 600°/s pendant un geste, petits pas d'ajustement à l'arrêt au lieu de pieds qui glissent.
- Animation plus fluide : trajectoire des joueurs et allure du corps à vitesse continue entre les pas de simulation, ballon en vol sur sa vraie parabole.
- Appels en profondeur un peu plus fréquents (0,30 → 0,38 par seconde) : hors-jeu 2,9 → 3,3 par match.
- Pokes perdus plus souvent fautifs (part de base 0,35 → 0,55) : fautes 18,8 → 20,2 par match.
- Une faute en retard dans sa propre surface (dix fois plus rare qu'ailleurs) donne un penalty : penalties 0,15 → 0,23 par match.
- Corrections de relecture : appel en profondeur borné dans le temps, pas de retraite après un précontrat, terrain neutre pour les anciennes sauvegardes, constantes nommées.
- Forme du jour, avantage du terrain et plongeon du gardien plus long : favori (écart 3–6) 50–53 % de victoires, nuls 26 %, domicile 46 %.
- Joueur averti plus prudent (plus de tacle glissé, moitié moins de fautes en retard) : cartons rouges 0,30 → 0,20 par match.
- Monde qui ne vieillit plus : retraites dès 31–36 ans en fin de contrat, contrats d'un an après 31 ans, un jeune du centre par club et par été (âge moyen sur 6 saisons 24,7 → 25,7 au lieu de 27,6).
- Têtes au but sur centre dans la surface : têtes cadrées ou non 1,3 → 4,3 par match, buts 2,34 → 3,0, tirs 20,9 → 23,2.
- Fautes tactiques pour stopper une contre-attaque (jamais par un joueur averti) : fautes 17 → 19,7 par match.
- Frappes de loin quand un défenseur est à 2–3 m : tirs 19,6 → 21,1 par match.
- Économie IA : propriétaires prudents ou dépensiers, recettes liées au classement (±15 %) ; sur 6 saisons, 20 % des clubs jouables endettés (avant 9 %).
- Ballon libre qui roule plus loin (1,65 → 0,95 m/s²), longs ballons trop appuyés, interceptions déviées : touches 29 → 35 par match.
- Appels en profondeur et passeur qui lit la ligne avec retard : hors-jeu 1,6 → 3,2 par match, passes en profondeur 1,1 → 9,5.
- Build de l'APK Android dans GitHub Actions (game-ci), signature stable par secrets ; version 0.42.0-preview.1 (code 35).
- Touches : défenseurs coincés qui dégagent en touche, ballons déviés dans un angle (touches 22 → 29 par match).
- Sauvegarde compacte : 40 Mo → 2,6 Mo au départ, 87 Mo → 5,8 Mo après une saison, rechargement identique à l'ancien format ; les anciennes sauvegardes restent lisibles.
- Performance : simulation des journées et changement de saison ~33 % plus rapides (résultats identiques, vérifiés sur 2 saisons).
- Gestion : un club dont les contrats ont expiré n'est plus bloqué le jour du match ; le centre de formation complète l'effectif (16 joueurs, gardien d'abord) et une alerte prévient quand l'effectif devient court.
- Attaque : centres évalués comme des duels (≈ 36 centres/match au lieu de 5), défenseurs qui attaquent le ballon centré, déviations de la tête en corner (≈ 7,6 corners au lieu de 1,6), lecture imparfaite du hors-jeu, contrôles ratés ramenés à ≈ 50/match, frappes moins systématiquement dans la lucarne (buts ≈ 3,0 au lieu de 3,4).
- Défense : les défenseurs temporisent au lieu de tacler à chaque contact (≈ 44 tacles réussis/match au lieu de 150) ; fautes sur tacle manqué hors surface (≈ 20 fautes/match au lieu de 5).
- Outil de calibration hors Unity (`tools/Calibration`) : 200 matchs complets, tableau comparé aux fourchettes réelles.

## 0.41
- Import dans Git depuis Google Drive. `MatchDecisions.bak.cs` retiré (doublon compilé de `MatchDecisions.cs`).
