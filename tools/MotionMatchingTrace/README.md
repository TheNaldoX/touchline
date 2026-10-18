# Trajectoires Touchline pour le prototype

`dotnet run --project tools/MotionMatchingTrace -c Release -- chemin/database.json sortie.json`

Outil hors Unity qui référence les mêmes sources Core que Calibration. Il simule Marseille et un adversaire du même championnat, graine 1, règles professionnelles, puis exporte les 22 acteurs entre 45 et 71 secondes. Les échantillons sont pris au **pas natif de 0,1 seconde**, sans inventer de frames à 60 Hz. Le lecteur interpole ensuite positions et angles. Les actions sont conservées dans le fichier pour identifier les gestes non couverts par la démonstration locomotion.

La sortie ne doit pas exister. Aucun catalogue ni sauvegarde n'est modifié. Deux exécutions indépendantes doivent donner le même SHA256 ; le fichier contient aussi le SHA256 de la base utilisée. Ce n'est ni une calibration de match complète ni une preuve de qualité des données.

Utiliser ensuite `tools/MotionMatchingProbe/Run.ps1 -Actors 22 -Hz 60 -Name preuve-distincte -MatchTrace sortie.json`. Les contrôleurs restent hors du jeu. Ils lisent aussi le futur enregistré pour sélectionner les poses : cet avantage est réservé au diagnostic, ce n'est pas une prédiction temps réel utilisable telle quelle.
