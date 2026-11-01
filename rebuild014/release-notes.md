# STB Studio 0.1.14 — Atelier 3D, archives et logos GTA

- La voiture bleue en cubes de la démonstration est retirée. L'éditeur démarre sur un écran d'accueil sobre, sans faux véhicule.
- Import de véhicules FiveM directement à partir d'une archive RAR, ZIP ou 7Z, sans installation de WinRAR ou de 7-Zip. Le modèle YFT et ses textures YTD sont pris en charge lorsque les fichiers sont compatibles avec l'éditeur natif existant. Les OBJ/MTL avec textures sont aussi acceptés.
- Extraction sécurisée : protection des chemins, nombre et taille des fichiers limités.
- Nouvelle galerie de logos fictifs de GTA V, à partir de l'image fournie par le créateur de STB Studio : sélection visuelle et recherche par marque.
- Possibilité d'importer un logo personnalisé en PNG, JPG ou BMP et de l'appliquer sur le matériau sélectionné.
- Les réglages existants de position UV, taille, opacité et masquage fonctionnent aussi pour les logos image. L'export PNG/YTD réutilise la texture modifiée.

## Limites connues

Le fichier RAR/7Z doit contenir un YFT avec un YTD associé, ou un OBJ utilisable ; une archive seule ne transforme pas magiquement un modèle incompatible. Les archives protégées par mot de passe et les RAR multi-volumes ne sont pas encore pris en charge. Les collisions, hiérarchies de bones, extras et configurations ELS/sirènes ne sont pas recalculés par le nouveau module.

**Version de base :** STB Studio 0.1.13. **Plateforme :** Windows x64. **Mises à jour :** packages Velopack diffusés sur GitHub Releases.
