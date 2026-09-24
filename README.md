# TP03 — Unity 3D (ST2OOS)

Unity 6, nouveau Input System. Au premier lancement, `Assets/Editor/TP03Builder.cs` génère automatiquement les 3 scènes.
Pour tout régénérer : menu **TP03 → Build everything**.

| Scène | Exercices |
|---|---|
| `Gameplay` | 1 déplacement · 2 caméra orbitale · 3 jour/nuit · 4 animations · 5 monstre + barres de vie · 8 bonus caméra/obstacles |
| `Bezier` | 6 courbes quadratique / cubique · 9 bonus courbe récursive à n points |
| `Forest` | 7 terrain forêt + caméra qui suit une trajectoire de Bézier |

## Contrôles
| Touche | Action |
|---|---|
| ZQSD / WASD | Se déplacer |
| Espace | Sauter |
| F | Attaquer |
| Clic droit + souris | Tourner la caméra et le joueur |
| Clic gauche + souris | Tourner la caméra seule |
| Molette | Zoom |
| T | Accélérer le temps (24 h = 2 min) |
| 1 / 2 / 3 | Changer de scène |
| H | Cacher l'interface |

Scène Bezier : clic gauche + glisser pour déplacer un point, N pour ajouter un point, Retour arrière pour en retirer un.
Scène Forest : flèches haut / bas pour la vitesse, R pour recommencer.

## Personnage et monstre (Ex 4 et 5)
1. Asset Store (gratuits) : **RPG Tiny Hero Duo PBR Polyart** (joueur) et **RPG Monster Duo PBR Polyart** (monstre), puis *Package Manager → My Assets → Download → Import*.
2. Dans la fenêtre Project, sélectionner le prefab du personnage → menu **TP03 → Use selected model as Player**.
3. Sélectionner le prefab du monstre → **TP03 → Use selected model as Monster**.

Les animations (Idle, Run, Jump, Attack, Hit, Die) sont assignées automatiquement.

## Vidéo (Ex 7)
*Window → Package Manager → Unity Registry → Recorder → Install*, puis *Window → General → Recorder → Recorder Window → Add Recorder → Movie*. Ouvrir la scène `Forest`, appuyer sur H pour cacher l'interface, puis Start Recording.
