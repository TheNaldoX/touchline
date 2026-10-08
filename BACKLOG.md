# Backlog Touchline

Chaque tâche est formulée pour être collée telle quelle dans Codex (ou un autre agent).
Une tâche = une branche = une PR. Les critères d'acceptation font foi.
Outils de mesure sans Unity : `tools/Calibration` (matchs), `tools/CoreTests` (tests),
`tools/SeasonSim` (saisons de gestion). Voir leurs README.

État de référence (200 matchs, graine 1, après la PR #3) : buts 2,97 · xG 2,35 · tirs 23,0 ·
corners 7,6 · fautes 18,4 · jaunes 3,95 · hors-jeu 1,50 · touches 21,8 · passes réussies 83,3 %.

---

## Fait
- ✅ T0.1 Outil de calibration (`tools/Calibration`) — PR #1
- ✅ T0.2 Restes de sessions (`MatchDecisions.bak.cs`) retirés — import initial
- ✅ Tests du Core hors Unity (`tools/CoreTests`) — PR #1
- ✅ Tacles et fautes réalistes — PR #2
- ✅ Centres, duels aériens, corners, lecture du hors-jeu, contrôles — PR #3
- ✅ Carrière bloquée quand l'effectif est trop court — PR #4
- ✅ Journées et changement de saison −33 % de calcul — PR #5

---

## 1. Sauvegarde compacte (priorité n°1, accord de Victor requis)

### T1.1 — Sauvegarde sous 5 Mo
Constat (`tools/SeasonSim 1 --savesize`) : 40 Mo au départ, 87 Mo après une saison.
`world.contracts` (37 Mo) : `JsonUtility` écrit chaque sous-objet vide. `world.rosterChanges`
(46 Mo après une saison) : tous les joueurs y sont copiés au changement de saison.
- [ ] Joueurs : n'enregistrer que l'état qui diffère de `database.json` (club, âge, note, potentiel, salaire, valeur, attributs, développement), jamais les textes de provenance
- [ ] Contrats : conditions de prêt / d'achat / clauses stockées à part, seulement quand elles existent
- [ ] Les sauvegardes actuelles se chargent toujours (test de migration avec une sauvegarde 0.41 réelle)
- [ ] Taille après 1 saison < 5 Mo, mesurée par `--savesize`
- [ ] Écriture hors du fil principal (sérialisation sur le fil principal autorisée, écriture disque en tâche de fond), et plus à chaque changement de page

### T1.2 — Base de données compacte
`database.json` = 59 Mo, lue 2 à 3 fois. Attributs = 43 %, provenance ≈ 35 %.
- [ ] Attributs en tableau de nombres dans un ordre fixe
- [ ] Provenance (sources, évidences) dans un fichier séparé, lu seulement par l'écran Provenance
- [ ] Une seule lecture au démarrage ; les copies « vierges » viennent d'un clone en mémoire
- [ ] Taille < 20 Mo, temps de démarrage mesuré sur le Fold avant / après

---

## 2. Moteur de match et IA

### T2.1 — Touches et hors-jeu dans la fourchette
- [ ] Touches 35–50 : dégagements sous forte pression vers la touche, déviations de tacle et de tête vers l'extérieur
- [ ] Hors-jeu 3–5 : appels en profondeur parfois mal synchronisés par l'attaquant (pas seulement l'erreur du passeur)
- [ ] Les autres statistiques restent dans leur fourchette (rapport avant / après)

### T2.2 — Buts alignés sur les xG
- [ ] Écart buts − xG < 0,2 sur 200 matchs
- [ ] Si la portée du gardien change, vérifier visuellement que la main touche le ballon (pas d'arrêt « à distance »)

### T2.3 — Décisions par valeur attendue
Remplacer l'empilement de bonus du score de passe par : probabilité de réussite × valeur de la position atteinte − risque de perte × danger concédé. Garder les consignes tactiques comme pondérations.
- [ ] Log debug des 3 meilleures options avec leur valeur
- [ ] Tests : joueur seul face au but frappe ; sous pressing, la passe sûre l'emporte ; consignes « centrer », « jouer court », « contre-attaque » ont un effet mesurable
- [ ] Calibration avant / après

---

## 3. Animations

### T3.1 — Prototype Mecanim
Le jeu n'utilise pas le module Animation d'Unity (32 scripts faits main). Prototype à comparer avant toute migration.
- [ ] Modèle FBX Humanoid (export du personnage MakeHuman)
- [ ] Animations capturées (Mixamo ou pack football) : course multi-vitesses, frappe, passe, tête, tacle
- [ ] Animator : arbre de mélange vitesse × direction, couche de gestes, IK des pieds
- [ ] Le moteur reste maître des positions (animations sur place, vitesse de lecture = vitesse du joueur)
- [ ] Vidéo comparée avec l'actuel sur le Fold ; décision de Victor avant de migrer

---

## 4. Gestion

### T4.1 — Économie de l'IA crédible
Constat (`tools/SeasonSim 6 176 --world`) : 0 club sur 715 en trésorerie négative en 6 saisons ; trésorerie médiane 3,4 → 7,7 M€.
- [ ] Des clubs en difficulté apparaissent (ventes forcées, masse salariale réduite)
- [ ] Trésorerie médiane stable à ±30 % sur 10 saisons

### T4.2 — Inflation du niveau
Force médiane des clubs +2,1 en 6 saisons (progression jusqu'à 26 ans, déclin après 31 ans).
- [ ] Force médiane stable à ±1 point sur 10 saisons (`--world`)

### T4.3 — Performance des journées
~60 ms de calcul par jour sur PC. Restes : `Database.Squad` (parcours complet par club), suivi des jeunes, agents libres.
- [ ] Index des effectifs par club tenu à jour
- [ ] < 20 ms par jour, résultats identiques (`tools/SeasonSim --dump` avant / après)

---

## 5. Interface

### T5.1 — Audit des écrans fréquents
Pour : composition, tactique, match en direct, mercato, boîte de réception — compter les touches nécessaires pour les actions courantes et proposer de réduire les parcours de plus de 3 touches.
- [ ] Captures plié / déplié de chaque écran dans la PR
