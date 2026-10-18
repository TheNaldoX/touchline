# Évaluation MotionMatching isolée

Ce banc d'essai utilise le projet minimal de [JLPM22/MotionMatching](https://github.com/JLPM22/MotionMatching), révision `35fc8ee9a0fd97f12de458ea81eaef7c8219d87a`, sous licence MIT. Il n'ajoute aucun package à Touchline. Les animations de démonstration restent dans le dossier ignoré `.validation` ; leur redistribution n'est pas nécessaire pour cet essai.

## Exécution

Cloner le dépôt dans `.validation/external-research/MotionMatching` et sélectionner la révision ci-dessus. Installer Unity `6000.3.24f1`. Fermer toute autre instance Unity, y compris les validations Touchline, puis lancer depuis la racine :

```powershell
pwsh -File tools/MotionMatchingProbe/Run.ps1 -Actors 1 -Hz 60 -Name mm-one-60
pwsh -File tools/MotionMatchingProbe/Run.ps1 -Actors 22 -Hz 60 -Name mm-team-60
pwsh -File tools/MotionMatchingProbe/Run.ps1 -Actors 22 -Hz 120 -Name mm-team-120
```

Les commandes sont séquentielles. Le script refuse un dossier de preuves existant. Il importe les exemples dans le projet **externe**, active l'instrumentation amont et ajoute deux compteurs cumulatifs (durée/nombre). Les sources et sauvegardes Touchline ne sont pas modifiées.

120 images de chauffe puis 600 images mesurées ; pas de simulation fixé à 1/Hz. La moyenne et le percentile 95 mesurent le temps CPU du contrôleur d'animation, avec instrumentation, **sans** le rendu, le moteur de match, le skinning complet et les autres coûts Android. La capture PNG vérifie que le test anime réellement le modèle. Les déplacements des racines sont mesurés en mètres par image. Une faible valeur ne prouve ni un bon contact au sol ni la synchronisation avec un ballon.

## Conditions avant intégration

- Vérifier le résultat à 30, 60 et 120 Hz : le contrôleur amont avertit explicitement d'un problème possible au-dessus de la cadence de la base d'animations.
- Adapter une trajectoire issue de Touchline sans laisser le contrôleur modifier les positions du Core.
- Mesurer les appuis et les transitions avec les clips de football autorisés ; les démonstrations BVH ne prouvent pas la compatibilité de nos FBX.
- Vérifier compilation Android, mémoire et temps de frame sur Fold, puis contacts pied/ballon et gardien/ballon.
- Conserver les notices de licence du code repris et vérifier séparément les droits des données d'animation avant redistribution.

## Références pour les autres lots

- [Pyrus2D](https://github.com/Cyrus2D/Pyrus2D/tree/9c5b17f0a830e200265a60a654fc2c0bda46363f), MIT : `base/generator_pass.py` compare les cycles d'arrivée du receveur, du ballon et des adversaires ; `base/bhv_move.py` compare les temps d'interception des partenaires. Dans Touchline, `Safety` connaît déjà les temps de récupération, mais le classement des presseurs dans `Move` utilise la distance. Première adaptation à tester : un joueur au sol ne doit pas monopoliser le rôle de premier presseur.
- [Open Football](https://github.com/ZOXEXIVO/open-football/tree/bfb2273053aab5825b5f58e9f588211c2d7794cf), Apache-2.0 : `src/core/src/transfers/squad/plan.rs` organise les besoins selon le titulaire, l'objectif et le budget ; `deal/negotiation.rs` prévoit qu'un poste soit pourvu pendant une négociation. Dans Touchline, vérifier la réévaluation après chaque arrivée : le plus faible remplaçant reste actuellement la référence des achats successifs.
- [OpenFootManager](https://github.com/openfootmanager/openfootmanager), GPLv3 : référence de conception pour les parcours de gestion. Aucun code ni catalogue importé. La reprise de code demanderait une analyse de compatibilité de licence.

Ces lectures sont des pistes de conception, pas une preuve que leurs moteurs sont plus réalistes. Les règles adaptées au match doivent conserver l'aléatoire à graine et passer les comparaisons appariées de 200 matchs. Aucun catalogue externe n'est importé : des bases mélangent données réelles et joueurs générés.

## Animations à rechercher après l'essai

Priorité aux plongeons hauts gauche/droite avec réception et relevé, puis contrôles orientés en course, freinages/pivots et récupération après tacle. Exiger un aperçu du geste entier, une licence adaptée, le format/squelette, l'échelle, les variantes gauche/droite et un contact identifiable. Ne pas sélectionner un clip sur son nom seul : les deux prises du pack déjà revues ne remplacent pas directement un plongeon haut.
