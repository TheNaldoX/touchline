# Tests du Core hors Unity

Exécute les tests EditMode qui ne dépendent que de `Touchline.Core` (620 tests à ce jour),
sans lancer Unity, en quelques secondes :

```
cd tools/CoreTests
dotnet run -c Release                 # tous les tests
dotnet run -c Release -- StandingDuel # filtre sur le nom
```

- `NUnitShim.cs` : sous-ensemble de NUnit utilisé par les tests.
- `UnityShim.cs` : `JsonUtility` (mêmes règles que Unity : champs publics, null → "" / [] / instance),
  `Resources.Load<TextAsset>`, `Mathf`, `Application.dataPath`.
- `excluded-tests.props` : tests qui ont besoin de `Runtime`, `Editor` ou de la scène Unity.
  Ceux-là ne tournent que dans Unity (Test Runner).

Ce n'est pas un remplacement du Test Runner Unity : c'est une vérification rapide avant de pousser,
utilisable aussi par les agents IA qui n'ont pas Unity.
