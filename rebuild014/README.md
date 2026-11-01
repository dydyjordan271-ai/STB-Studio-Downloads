# STB Studio 0.1.14 — publication via GitHub Actions

Ce ZIP contient exclusivement les nouveaux fichiers de la version 0.1.14.
Il ne contient pas encore l'installateur `.exe` : GitHub Actions doit le compiler sous Windows.

## Installation dans ton dépôt

1. Extrais le ZIP sur ton PC.
2. Dans `https://github.com/dydyjordan271-ai/STB-Studio-Downloads`, ouvre l'onglet **Code**.
3. Clique **Add file** → **Upload files**, puis dépose les dossiers `rebuild014` et `.github` depuis le dossier extrait, en conservant exactement leur arborescence.
4. Clique **Commit changes**.
5. Dans **Actions**, ouvre **Rebuild STB Studio 0.1.14 for Windows**. Le build démarre automatiquement sur un commit qui ajoute `rebuild014/` (ou utilise **Run workflow**).
6. Attends un ✅ vert. La nouvelle release `v0.1.14` publiera l'installateur Windows `STB.Studio-win-Setup.exe` et les fichiers Velopack. Tu pourras l'installer pour tester.

### Vérifier la structure

```text
STB-Studio-Downloads/
├── .github/
│   └── workflows/
│       └── rebuild-v014.yml
└── rebuild014/
    ├── patch.py
    ├── overlays/
    │   └── Studio3DEditorWindow.ArchiveAndBrands.cs
    ├── assets/
    │   └── brands-atlas.png
    └── release-notes.md
```

**Important :** le workflow précédent 0.1.13 reste inchangé. La nouvelle version utilise `rebuild014/` pour ne pas déclencher l'ancien workflow automatiquement.

**Sécurité :** le workflow vérifie le SHA-256 du package 0.1.13 avant de reconstruire et ne requiert aucun token personnel GitHub. Garde seulement le dépôt **public** si tu veux que tous les utilisateurs téléchargent et mettent STB Studio à jour sans compte.
