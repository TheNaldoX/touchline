# Simulation de saisons

Fait tourner une carrière complète jour par jour **sans Unity** et résume l'état du monde
à chaque saison : trésorerie du club, taille et force des effectifs, salaires, transferts de l'IA.

```
cd tools/SeasonSim
dotnet run -c Release -- 3            # 3 saisons avec Marseille (id 176), matchs du club tirés au sort
dotnet run -c Release -- 2 176 --engine   # matchs du club joués par le vrai moteur (~1,3 s/match)
dotnet run -c Release -- 6 176 --world    # personne ne dirige : l'IA gère tous les clubs
```

Le club est géré « passivement » : aucune prolongation, aucun recrutement. C'est ce qui a révélé
le blocage corrigé par `SquadSafetyNet` (contrats tous expirés → plus de onze → impossible de
jouer ou d'avancer). Compter ~1 minute par saison.

Options de diagnostic :
- `--report` ajoute les indicateurs de l'IA des clubs, saison par saison : tailles d'effectif (avec et
  sans les partants annoncés), âges, notes, salaires/recettes, dettes, transferts, joueurs générés et
  libres, champions, survie des promus, progression de la note par âge, plus gros effectifs.
  Exemple : `dotnet run -c Release -- 10 176 --world --report --worldseed 77` (~5 min).
- `--worldseed N` change la graine des tirages du monde (résultats, jeunes générés).
- `--dump fichier` écrit transferts IA et état de chaque joueur : deux versions du Core doivent
  produire des fichiers identiques après une optimisation « sans effet » (`cmp`).
- `--savesize` mesure la sauvegarde au format `JsonUtility` (via `tools/CoreTests/UnityShim.cs`).
- `--savecheck` écrit la carrière dans l'ancien format et dans le format compact, recharge les deux
  et vérifie qu'ils donnent exactement la même carrière et les mêmes joueurs ; affiche tailles et temps.
