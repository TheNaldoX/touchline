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
