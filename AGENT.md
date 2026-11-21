# AGENT.md — Passation du projet Kode (IDE en C#)

Document destiné à un agent IA (ou à un humain) qui reprend le travail en cours de route.
Rédigé le 21 novembre 2026. Tout ce qui est écrit ici vient de la conversation précédente ;
ce qui n'a pas été vérifié est signalé comme tel.

## 1. Utilisateur et façon de collaborer

- **Utilisateur** : Essomba (se fait appeler Sperline), étudiant en première année à KEYCE (Informatique & IA), Yaoundé.
- **Langue** : toujours répondre en **français**.
- **Niveau** : à l'aise avec Git et un éditeur de code, mais il veut **comprendre** ce qu'il ajoute. Ne pas le traiter en débutant, ne pas survoler.
- **Préférences exprimées** :
  - Ne plus envoyer de fichier zip pour le code : montrer le code dans le chat, avec le **chemin exact** de chaque fichier et l'endroit précis de la modification.
  - Être précis sur l'emplacement des fichiers dans l'arborescence.
  - Commentaires de code écrits de façon naturelle, en français.
  - Les icônes sont dessinées en SVG par l'assistant, pas récupérées ailleurs.
  - Workflow direct : appliquer les changements plutôt que de demander une validation à chaque étape.
- **Méthode qui marche** : demander le texte exact de la sortie de `dotnet build` ou de l'onglet Problems, et diagnostiquer à partir de là. L'assistant précédent **n'a pas pu compiler** le code : tout a été validé par l'utilisateur sur sa machine.

## 2. Le projet

- **Nom** : Kode, un IDE académique type VS Code, écrit en C#, utilisable et fonctionnel, à construire **en 4 jours**. Le professeur le testera avec des programmes.
- **Dépôt** : https://github.com/Henri-Michel-Essomba-Djomeni/Kode
- **Techno** : **.NET 10**, **WPF** (projet `net10.0-windows`), namespace **`IdeCSharp.UI`**. Composant d'édition prévu : AvalonEdit (compatibilité avec .NET 10 **non vérifiée**, c'est à tester lors de l'intégration de l'éditeur).
- **Groupe de 5** : le chef de groupe (prénom non communiqué, ce n'est pas Essomba), Essomba, Valdez, Raïssa, Kammadeu.
- **Notation du professeur** : 50 % sur la présence aux séances de travail (en présentiel ou en ligne, selon le nombre de séances organisées par le chef) ; le reste sur les tâches (hypothèse du groupe, à confirmer). Seul le chef modifie la présence.

## 3. Structure du dépôt (vue sur capture d'écran)

```
Kode/
├─ .gitignore
├─ README.md
└─ IdeCSharp/
   ├─ IdeCSharp.slnx
   ├─ IdeCSharp.Core/
   │  ├─ Interfaces/   IEditorService.cs, IExecutionService.cs, IFileService.cs
   │  └─ Models/       BuildResult.cs, FileNode.cs
   ├─ IdeCSharp.Tests/ UnitTest1.cs
   └─ IdeCSharp.UI/
      ├─ App.xaml, App.xaml.cs
      ├─ AppCommands.cs
      ├─ MainWindow.xaml, MainWindow.xaml.cs
      ├─ ThemeManager.cs
      └─ Themes/      Light.xaml, Dark.xaml
```

Règle d'architecture : l'interface vit dans `IdeCSharp.UI`, la logique dans `IdeCSharp.Core`, les tests dans `IdeCSharp.Tests`. Les modules communiquent par des interfaces (`Core/Interfaces`).

## 4. Rôles et tâches

| Membre | Responsabilité principale |
|---|---|
| Chef de groupe | Organisation, séances, présence, interfaces entre modules, terminal, intégration, tests, démo |
| **Essomba** | Dépôt Git, interface principale, thème, installateur |
| Valdez | Éditeur de code, mode gros fichier |
| Raïssa | Fichiers, gros fichiers |
| Kammadeu | Exécution, panneau Sortie, documentation |

**Tâches d'Essomba** (identifiants du fichier Excel de suivi) :
T02 dépôt Git · T07 fenêtre principale · T08 panneaux redimensionnables · T09 menus · T10 raccourcis · T11 barre d'état · T12 thème clair/sombre · T13 icône et fenêtre « À propos » · T48 choix de l'outil d'installation · T49 publication autonome · T50 installateur · T51 test sur machine propre · T54 correction des bugs interface et éditeur.

## 5. État d'avancement

### Fait (code écrit et compilé par l'utilisateur ; l'exécution complète est à confirmer)

- **T07 à T11** : fenêtre principale avec menus, panneaux redimensionnables (GridSplitter), raccourcis clavier, barre d'état.
- **T12** : thème clair/sombre/système.
- Le build de `Core` et de `Tests` passe. Celui de `UI` échouait à cause d'un caractère parasite en tête de `MainWindow.xaml` ; l'utilisateur l'a corrigé et indique que « c'est okay ».
- Branche de travail : `feature/interface-principale` (déjà poussée une première fois). Un deuxième commit (thème, namespace) était prévu.

### Statuts à cocher dans le fichier Excel de suivi

- **Terminé** (après test d'exécution réussi) : T07, T08, T10, T11, T12.
- **En cours** : T09 (les menus existent mais ne sont pas encore branchés aux modules), T02 (en attente de l'ajout des membres et de la protection de la branche principale).

### Architecture de la fenêtre principale (pour brancher les autres modules)

`MainWindow` ne contient **aucune logique métier**. Elle offre :

- **Zones d'accueil** : `SetExplorerContent(UIElement)`, `SetEditorContent(UIElement)`, `SetBottomPanelContent(UIElement)`.
- **Barre d'état** : `SetStatus(string)`, `SetCursorPosition(int line, int column)`, `SetLanguage(string)`, `SetEncoding(string)`.
- **Événement** `CommandInvoked` (`Action<ICommand>`) levé pour toute commande non gérée par la fenêtre. Les modules s'y abonnent. Sans abonné, la barre d'état affiche « La commande … n'est pas encore branchée ».
- **Commandes** (`AppCommands.cs` et `ApplicationCommands`) : Nouveau Ctrl+N, Ouvrir Ctrl+O, Ouvrir un dossier Ctrl+Maj+O, Enregistrer Ctrl+S, Enregistrer sous Ctrl+Maj+S, Rechercher Ctrl+F, Remplacer Ctrl+H, Aller à la ligne Ctrl+G, Exécuter F5, Arrêter Maj+F5, Explorateur Ctrl+B, Panneau du bas Ctrl+J, SetTheme (paramètre `Light`, `Dark` ou `System`), À propos, Quitter. Annuler, Rétablir, Couper, Copier, Coller sont des commandes standard WPF routées vers l'élément qui a le focus.
- La fenêtre gère elle-même : afficher/masquer l'explorateur et le panneau du bas (avec mémorisation des tailles), le thème, À propos, Quitter.

### Thème (T12) : choix techniques

- `ThemeManager.Apply(AppTheme)` (`Light`, `Dark`, `System`).
- Deux mécanismes : (1) `Application.ThemeMode` du thème Fluent de WPF (.NET 9+), qui stylise les contrôles standard ; c'est une **API expérimentale** (`WPF0001`), d'où un `#pragma warning disable WPF0001` localisé dans `ThemeManager.cs` ; (2) deux dictionnaires `Themes/Light.xaml` et `Themes/Dark.xaml` qui définissent quatre pinceaux utilisés par le XAML de Kode : `PanelBackgroundBrush`, `PanelForegroundBrush`, `MutedForegroundBrush`, `SplitterBrush`.
- Le mode « Système » lit la valeur de registre `AppsUseLightTheme` (HKCU, `Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`).
- `App.xaml.cs` appelle `ThemeManager.Apply(AppTheme.System)` dans `OnStartup`, **avant** `base.OnStartup(e)`.
- `App.xaml` doit contenir **un seul** bloc `<Application.Resources>` qui fusionne `Themes/Light.xaml`. Un doublon a déjà provoqué l'avertissement « Application.Resources has already been set ».
- Limite connue : le choix du thème n'est pas mémorisé entre deux lancements (à faire avec une sauvegarde de configuration).
- Tout nouveau contrôle dessiné par Kode doit utiliser `{DynamicResource ...}` avec ces clés, jamais de couleur en dur.

### Incidents rencontrés (utile pour le diagnostic)

- 30 erreurs `CS0103` (`InitializeComponent`, `ExplorerHost`, etc.) : conséquence d'un XAML invalide qui empêchait la génération de la partie cachée de `MainWindow`. Cause réelle : erreur MC3000 « Name cannot begin with the '<' character » à la ligne 1.
- Message VS Code « Cannot open project file ..._wpftmp.csproj » : inoffensif (projet temporaire créé puis supprimé) ; `Developer: Reload Window` le fait disparaître.

## 6. Prochaines étapes pour Essomba

1. **Vérifier et pousser** : `dotnet build IdeCSharp/IdeCSharp.UI`, `dotnet run --project IdeCSharp/IdeCSharp.UI`, tester Affichage > Thème (Clair, Sombre, Comme Windows), vérifier qu'`App.xaml` n'a qu'un bloc de ressources.
2. **Fusion dans `main`** : voir la section 7.
3. **T13** : dessiner l'icône de Kode en SVG (préférence de l'utilisateur), la convertir pour l'application (le SVG n'est pas utilisable directement comme icône de fenêtre WPF : prévoir un `.ico` ou un `DrawingImage` XAML), et créer une vraie fenêtre « À propos » (la version actuelle est un simple `MessageBox`).
4. **T09** : brancher les menus aux services dès que les modules des autres membres existent, via `CommandInvoked` et les interfaces de `Core`.
5. **T48 à T51** (installateur) : choisir l'outil (MSIX, Inno Setup ou WiX, selon le temps), `dotnet publish` autonome, créer l'installateur (raccourci bureau, menu Démarrer, désinstallation propre), tester sur une machine sans Visual Studio. Le SDK .NET est nécessaire pour compiler du C# avec l'IDE : l'IDE doit le détecter et expliquer clairement son absence.
6. **Mémorisation du thème** quand la sauvegarde de configuration existera.

## 7. Workflow Git

- **Décision de l'utilisateur** : tout doit aller dans la branche **`main`**.
- Règle du groupe : une branche par module, fusion dans `main` via **pull request relue** par un autre membre (le chef de préférence), messages de commit explicites, fusions fréquentes.
- Sur la page GitHub, seule la branche `dev_Bel` (2 commits, dossier `IdeCSharp` et `.gitignore`) était visible ; l'existence de `main` n'a pas pu être confirmée (GitHub bloque la lecture automatisée). **À vérifier** avec `git branch -a`.
- Procédure : depuis `feature/interface-principale` → `git add .` → `git commit` → `git push` → sur GitHub, ouvrir la pull request avec **base = `main`** et **compare = `feature/interface-principale`** → faire relire → fusionner. Si `main` n'existe pas encore, le chef doit la créer avant.
- Vérifier avec `git status` qu'aucun dossier `bin` ou `obj` n'est suivi.

## 8. Fichiers de suivi et documentation déjà produits

Dans cette conversation ont été créés (les fichiers Excel sont à demander à l'utilisateur, il les a) :

- **IDE_CSharp_Todo_List_Projet_v1.xlsx** : 58 tâches sur 4 jours (J1 à J4) pour l'IDE, avec priorités Must/Should/Could, points, dépendances, assignation, présence protégée par mot de passe (réservée au chef), tableau de bord qui calcule la note de chacun, checklist de tests, registre des risques, roadmap. Les points d'une tâche réalisée par un autre membre sont transférés à celui qui l'a faite.
- **Documentation du projet — IDE en C#** (document Claude Docs). Elle mentionne encore **.NET 8** alors que le groupe utilise **.NET 10** : à corriger, ainsi que la tâche T01 du fichier Excel.
- Le nom du chef de groupe reste « Chef de groupe » dans les fichiers tant que l'utilisateur ne l'a pas donné.

## 9. Second projet : agent antivirus IA (analysé, pas encore commencé)

- **Consigne du professeur** : pas un antivirus classique de type Avast ; un agent qui répond aux **attaques menées par IA**. Il doit faire tout ce que fait un antivirus, ajouter ce volet IA comme plus-value, se comporter comme **un ami qui donne des conseils de sécurité** de temps en temps, avoir des **options** activables, et **surveiller les ports et le réseau**.
- **Quatre blocs** : socle antivirus classique ; surveillance réseau et ports ; volet anti-attaques IA (détection comportementale, anti-phishing expliqué, protection contre l'injection de prompt, anti-fraude en bonus) ; ami conseiller.
- **Techno proposée** (non validée) : Python, psutil, watchdog, SQLite, scikit-learn, Windows.
- **Livrés** : fichier **Agent_Antivirus_IA_Todo_List_Projet_v1.xlsx** (84 tâches en 4 étapes E1 à E4, équipe supposée identique à celle de l'IDE) et **Documentation du projet — Agent antivirus IA**.
- **Inconnues** : délai et taille réelle du groupe, prénom du chef.
- **Règle de sécurité** : aucun vrai malware ; tests avec le fichier EICAR et des simulateurs, dans une machine virtuelle.

## 10. Questions ouvertes

1. Quel est le prénom du chef de groupe ?
2. La branche `main` existe-t-elle déjà et qui la protège ?
3. Le partage 50 % présence / 50 % tâches a-t-il été confirmé par le professeur ?
4. Quel est le délai pour l'agent antivirus ?
5. AvalonEdit s'installe-t-il sans problème sous .NET 10 ?