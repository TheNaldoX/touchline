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

Deux secondes de chauffe puis vingt secondes mesurées à chaque cadence ; pas de simulation fixé à 1/Hz. La moyenne et le percentile 95 mesurent le temps CPU du contrôleur d'animation, avec instrumentation, **sans** le rendu, le moteur de match, le skinning complet et les autres coûts Android. Trois captures PNG vérifient les poses à 5, 12 et 19 secondes. Les déplacements des racines sont mesurés en mètres par image. Une faible valeur ne prouve ni un bon contact au sol ni la synchronisation avec un ballon. Les anciens essais PR188/190 utilisaient 120 images de chauffe puis 600 mesurées : ne pas comparer leurs extrêmes directement aux nouveaux essais plus longs.

## Conditions avant intégration

### Adaptation expérimentale de la racine

Ajouter `-SubframeRoot` à la commande pour interpoler la position et la rotation de la racine entre deux poses du même clip. L'origine des transitions utilise la même fraction pour éviter un décalage lors d'une recherche. Les tableaux de poses amont ne sont jamais modifiés. Sans ce paramètre, le contrôleur épinglé est rétabli ; le script refuse d'écraser des modifications externes inconnues. Six vérifications déterministes couvrent les milieux, rotations, frames entières, fins de clip, bornes et données partagées avant chaque essai.

Mesure historique PR190, 22 acteurs, 600 images à 120 Hz : images sans déplacement 50 % → 0 %, pas maximal 5,94 → 2,97 cm. À 60 Hz : 0 % d'arrêts et pas maximal 5,92 cm après adaptation. Ce sont des mesures de racine sur un trajet synthétique ; l'option racine seule laisse les articulations échantillonnées par l'amont et n'est **pas** une solution complète de fluidité. Une erreur d'indexation `UnityEditor.Search` persiste, distincte des erreurs d'animation. Aucune intégration ni validation Android.

### Adaptation expérimentale du corps

`-SubframePose` inclut la racine, interpole les articulations, le bassin et les vitesses, et fournit les poses fractionnaires aux transitions amorties. Deux tampons réutilisables par contrôleur évitent de modifier les poses partagées et d'allouer des tableaux à chaque image. Les contacts restent discrets ; aucune interpolation entre clips. Huit contrôles supplémentaires couvrent le bassin, la rotation, les vitesses, les contacts, la fin de clip, la réutilisation, l'immutabilité et l'indépendance des tampons.

`pose-metrics.json` mesure les rotations locales (seuil d'immobilité 0,0001 degré/image) et la vitesse horizontale des orteils seulement lorsqu'ils sont déclarés verrouillés sur deux images consécutives. **Zéro échantillon verrouillé ne signifie pas zéro glissement.** Ces mesures concernent le squelette amont, pas une semelle de chaussure ni un contact football. Les articulations naturellement immobiles sont comptées : le taux n'est pas un score absolu de qualité. Les coûts de diagnostic Editor ne sont pas inclus dans le chronométrage du contrôleur.

Résultats du trajet de vingt secondes, 22 acteurs, racine seule → corps interpolé :

| Hz | Articulations immobiles (%) | Pic rotation (°/s) | P95 vitesse orteil verrouillé (m/s) |
|---|---:|---:|---:|
| 120 | 6,379 → 0,303 | 2412 → 1218 | non mesurable avant (0 appui) → 0,315 |
| 60 | 0,443 → 0,389 | 1191 → 1109 | 0,137 → 0,148 |
| 30 | 0,645 → 0,601 | 1064 → 1055 | 0,159 → 0,208 |

Le corps progresse à 120 Hz mais les pieds glissent encore, avec une régression à 30/60 Hz : **adaptation non prête pour Touchline**. Sur PC, le coût moyen contrôleur passe de 0,883 à 1,057 ms à 120 Hz (22 acteurs), pas un gain de performances. Les durées sont alignées, pas les recherches de poses ni chaque appui : les populations de contacts varient. Les captures statiques ne constituent pas une validation du confort en mouvement. Les essais valides ont 0 erreur animation et 1 erreur d'indexeur Editor préexistante chacun ; un essai à 60 Hz a été exclu après conflit d'accès au cache amont, puis repris sous un nom distinct. Le banc s'arrête désormais dès une erreur d'exécution.

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
