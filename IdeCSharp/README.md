# IDE en C# — Projet de groupe KEYCE

IDE inspiré de Visual Studio Code, réalisé en 4 jours : éditer, enregistrer, compiler, exécuter et afficher le résultat de programmes C#.

## Décisions techniques (T01)

| Besoin | Choix |
| --- | --- |
| Langage et plateforme | C# sur **.NET 10** |
| Interface | **WPF** (Windows) |
| Composant d'édition | **AvalonEdit** (coloration, numéros de ligne, annuler / rétablir) |
| Exécution des programmes | SDK .NET (`dotnet build` / `dotnet run`) via la classe `Process` |
| Gestion de versions | Git + GitHub, une branche par module |

Framework cible : `net10.0` (`net10.0-windows` pour le projet UI).

## Architecture

La solution `IdeCSharp.sln` contient trois projets :

- **IdeCSharp.UI** : fenêtres, menus, panneaux (ce que l'utilisateur voit).
- **IdeCSharp.Core** : la logique (fichiers, exécution, éditeur), sans dépendance à l'interface.
- **IdeCSharp.Tests** : tests automatiques du Core.

Les modules communiquent par des **interfaces** définies dans `IdeCSharp.Core/Interfaces` :
`IFileService`, `IEditorService`, `IExecutionService`.
Chacun développe son module derrière son interface.

## Responsabilités

| Membre | Responsabilité |
| --- | --- |
| Chef de groupe | Organisation, séances, présence, intégration, tests, démo |
| Essomba | Interface principale, dépôt Git, installateur |
| Valdez | Éditeur, mode gros fichier |
| Raïssa | Fichiers et gros fichiers |
| Kammadeu | Exécution, panneau Sortie, documentation |

## Travail avec Git

- Une branche par module, créée à partir de `main`.
- Fusion vers `main` uniquement après relecture d'une pull request par un autre membre.
- Messages de commit courts et explicites.

## Prérequis sur chaque machine

- SDK .NET 10 (`dotnet --list-sdks` doit afficher une ligne `10.0.x`).
- Visual Studio avec la charge de travail « Développement .NET Desktop », ou VS Code avec C# Dev Kit.
