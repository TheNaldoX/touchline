# Changelog

Une ligne par pull request, la plus récente en haut.

## À venir
- Gestion : un club dont les contrats ont expiré n'est plus bloqué le jour du match ; le centre de formation complète l'effectif (16 joueurs, gardien d'abord) et une alerte prévient quand l'effectif devient court.
- Attaque : centres évalués comme des duels (≈ 36 centres/match au lieu de 5), défenseurs qui attaquent le ballon centré, déviations de la tête en corner (≈ 7,6 corners au lieu de 1,6), lecture imparfaite du hors-jeu, contrôles ratés ramenés à ≈ 50/match, frappes moins systématiquement dans la lucarne (buts ≈ 3,0 au lieu de 3,4).
- Défense : les défenseurs temporisent au lieu de tacler à chaque contact (≈ 44 tacles réussis/match au lieu de 150) ; fautes sur tacle manqué hors surface (≈ 20 fautes/match au lieu de 5).
- Outil de calibration hors Unity (`tools/Calibration`) : 200 matchs complets, tableau comparé aux fourchettes réelles.

## 0.41
- Import dans Git depuis Google Drive. `MatchDecisions.bak.cs` retiré (doublon compilé de `MatchDecisions.cs`).
