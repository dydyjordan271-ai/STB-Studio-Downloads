# Reconstruction reproductible STB Studio 0.1.13

Le code source original complet n'était pas disponible dans ce dépôt. Ce pipeline récupère le paquet public signé par sa somme SHA-256 de la release 0.1.10, le décompile à l'aide d'ILSpy, applique un patch contrôlé à l'interface WPF, puis compile un moteur natif d'édition YFT à partir de ragekit. Les dépendances et l'installeur sont créés dans un runner Windows GitHub Actions.

La publication finale est une **préversion de test** sur GitHub. Les utilisateurs peuvent télécharger l'installeur sans conserver les ZIP temporaires de la conversation.

## Pour relancer

Dans l'onglet Actions, ouvrir « Rebuild STB Studio 0.1.13 for Windows » puis « Run workflow ». Les fichiers sont conservés dans la release `v0.1.13` si toutes les étapes réussissent.

## Limites

Il s'agit d'une reconstruction expérimentale, pas d'un éditeur universel équivalent à Blender. Les modifications ne sont pas promises compatibles avec tous les véhicules et doivent être testées en jeu.
