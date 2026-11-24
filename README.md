# Kode

Kode est un IDE simple, fluide et intuitif pour C# (WPF), réalisé en groupe
dans le cadre du projet Keyce.

## Fonctionnalités
- *Fichiers* : ouvrir un dossier, explorateur, nouveau fichier, fichiers récents
- *Éditeur* : onglets, numéros de ligne, coloration (C#, Python, JSON, XML, HTML),
  annuler/rétablir, recherche/remplacement, aller à la ligne, auto-indentation,
  autocomplétion, zoom, mode gros fichier
- *Exécution* : compilation, exécution, panneau SORTIE, saisie, arrêt, délai maximal
  de 5 minutes, autres langages (JavaScript, C, etc.)
- *Barre d'état* et onglets du panneau du bas (Sortie, Terminal, Ports)

## Installation
1. Installer le SDK .NET : https://dotnet.microsoft.com/download
2. Récupérer le projet : git clone https://github.com/Henri-Michel-Essomba-Djomeni/Kode.git
3. Lancer : cd Kode\IdeCSharp puis dotnet run --project IdeCSharp.UI

Un installateur (Inno Setup) est disponible dans le dossier Installateur.

## Raccourcis
| Action | Raccourci |
|---|---|
| Ouvrir un dossier | Ctrl+Maj+O |
| Nouveau fichier | Ctrl+N |
| Enregistrer | Ctrl+S |
| Exécuter | F5 |
| Arrêter | Maj+F5 |

## Guide d'utilisation
1. *Ouvrir un projet* : Fichier → Ouvrir un dossier (Ctrl+Maj+O). L'explorateur
   affiche les fichiers à gauche.
2. *Éditer* : ouvrir un fichier dans un onglet. Un astérisque sur l'onglet
   indique des modifications non enregistrées.
3. *Enregistrer* : Ctrl+S.
4. *Exécuter* : F5. La compilation puis l'exécution s'affichent dans l'onglet SORTIE.
5. *Saisie* : si le programme pose une question, taper la réponse dans le champ
   en bas du panneau, puis cliquer sur Envoyer.
6. *Arrêter* : Maj+F5, ou le menu Exécuter.
7. *Erreurs* : une erreur de compilation s'affiche sous la forme
   Program.cs(ligne,colonne): error CSxxxx. Double-clique sur cette ligne :
   le fichier s'ouvre dans l'éditeur, curseur à la ligne de l'erreur.

## Autres langages
Pour exécuter un fichier hors C# (JavaScript, Python, C, etc.), ouvre-le dans
l'éditeur (double-clic dans l'explorateur) puis appuie sur F5.
L'outil correspondant (Node, Python, gcc, etc.) doit être installé et accessible
dans le PATH : sinon le panneau SORTIE affiche un message d'installation.
Testés : JavaScript, Python, C (message d'installation).
Pour Python, installe-le depuis python.org en cochant « Add python.exe to PATH ».

## Limites connues
- Un script Node qui lit la saisie reste ouvert tant que l'entrée n'est pas fermée
  (terminer le script par process.exit()).
- Panneau SORTIE en texte seul, sans couleurs par type de ligne.