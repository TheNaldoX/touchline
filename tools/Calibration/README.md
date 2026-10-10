# Calibration du moteur

Simule des matchs complets (2 × 45 min, règles professionnelles) **sans Unity**, en compilant
directement `unity/Touchline/Assets/Touchline/Core`, puis compare les moyennes par match aux
fourchettes de `AGENTS.md`.

```
cd tools/Calibration
dotnet run -c Release -- 200 1
```

Arguments : nombre de matchs (200), graine (1), chemin de `database.json` (trouvé automatiquement).
Les affiches sont tirées au hasard entre clubs jouables d'un même championnat de 1re division.
Même graine = mêmes matchs : comparer toujours avant/après avec la même graine.

Prérequis : SDK .NET 8. Environ 1,3 s par match et par cœur.

## Audit des consignes

`dotnet run -c Release -- --tactics 200 1 tactical-audit.csv`

Compare sept consignes sur les mêmes 200 affiches et graines, soit 2 800 matchs.
Une seule consigne change à la fois (0,2 contre 0,8 ; missions des milieux défense/attaque).
Formation, onze et adversaire sont identiques ; l'IA adverse reste active. Mesures du club
dirigé à domicile : largeur, ligne, pressing, passes, précision, tirs, xG, condition et buts.
Le CSV conserve chaque résultat, sans écraser un fichier existant. La sortie console donne
la moyenne des différences appariées et un intervalle approximatif à 95 %. « Non concluant »
ne signifie pas absence d'effet. Intervalles exploratoires, sans correction des comparaisons
multiples ; ce n'est ni une promesse de victoire ni une validation sur toutes les formations.
Une erreur de simulation interrompt l'audit au lieu d'exclure silencieusement un match.

Les colonnes « Profondeur adverse » suivent chaque passe en profondeur de l’adversaire
jusqu’à sa première issue : contrôle, hors-jeu, interception contrôlée, déviation,
contrôle manqué, sortie, ballon non maîtrisé, interruption ou vol inachevé à la pause/fin.
Elles sont exprimées par match, pas en pourcentage. Leur somme doit correspondre au
nombre de passes en profondeur adverses ; sinon l’audit échoue. Le suivi lit chaque
pas physique sans modifier le moteur ni consommer de hasard. Un contrôle réussi ne
prouve ni un tir ultérieur ni une occasion dangereuse : ce sont des issues immédiates,
pas une attribution causale des buts à une consigne.

Les colonnes « Après réception » prolongent chaque contrôle pendant cinq secondes
au maximum, jusqu’au premier tir, nouvelle passe, possession adverse, ballon contesté,
arrêt de jeu ou fin de période. « Sans action après 5s » ne signifie pas joueur immobile :
il peut conduire ou préparer un geste. La somme de ces issues doit égaler les contrôles.
Deux compteurs décrivent la position au contrôle : distance au but supérieure à25m,
et au moins deux adversaires de champ plus avancés vers leur propre but. Ce dernier
compteur est longitudinal, pas une mesure de la fermeture effective du couloir de tir.

Une sélection facultative évite de rejouer les consignes inchangées :
`dotnet run -c Release -- --tactics 200 1 ligne.csv Ligne`
ou `"Ligne,Rythme"`. Les affiches et graines restent identiques à l'audit complet.
Le rapport général distingue les favoris à domicile/à l'extérieur, donne un intervalle
binomial de Wilson à 95 % et mesure l'écart buts–xG apparié. Ces intervalles décrivent
l'échantillon simulé, pas l'exactitude des données ni la validité du moteur dans le football réel.
