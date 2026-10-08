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
