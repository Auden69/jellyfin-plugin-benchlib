# BenchLib — Plugin Jellyfin

Plugin officiel BenchLib pour Jellyfin. Scanne vos bibliothèques localement et envoie des statistiques agrégées vers [benchlib.com](https://benchlib.com) pour obtenir un score de qualité.

> **Confidentialité** — Aucun titre, aucun identifiant de contenu ne quitte votre serveur. Seules des statistiques agrégées sont transmises (ex : nombre de films 4K, ratio de complétude des séries).

---

## Prérequis

- Jellyfin 10.9 ou supérieur
- Un compte BenchLib avec un serveur enregistré
- Une clé API BenchLib (générée depuis votre tableau de bord)

---

## Installation

### Installation manuelle

1. Téléchargez `BenchlibPlugin.dll` depuis la section [Releases](https://github.com/benchlib/benchlib-agent/releases) de ce dépôt
2. Copiez le fichier dans le dossier `plugins/` de votre installation Jellyfin
   - Linux : `/var/lib/jellyfin/plugins/`
   - Windows : `%APPDATA%\Jellyfin\plugins\`
   - Docker : montez le volume `/config/plugins/`
3. Redémarrez Jellyfin
4. Le plugin apparaît dans **Administration > Plugins**

### Compilation depuis les sources

```bash
git clone https://github.com/benchlib/benchlib-agent.git
cd benchlib-agent/jellyfin/BenchlibPlugin
dotnet build -c Release
# Le .dll se trouve dans bin/Release/net8.0/BenchlibPlugin.dll
```

---

## Configuration

1. Dans Jellyfin, allez dans **Administration > Plugins > BenchLib**
2. Collez votre **clé API BenchLib** (disponible dans votre tableau de bord sur benchlib.com)
3. Choisissez les bibliothèques à envoyer (Films, Séries, Musique)
4. Cliquez sur **Enregistrer**
5. Utilisez le bouton **Tester la connexion** pour vérifier

---

## Planification

Le plugin s'exécute automatiquement **tous les jours à 3h du matin**.

Pour modifier la fréquence :

1. Allez dans **Administration > Tâches planifiées**
2. Trouvez **BenchLib — Envoyer les statistiques**
3. Modifiez le déclencheur selon vos préférences

Vous pouvez aussi lancer le scan manuellement en cliquant sur **Exécuter** dans les tâches planifiées.

---

## Données envoyées

| Bibliothèque | Données transmises                                                                                         |
| ------------ | ---------------------------------------------------------------------------------------------------------- |
| Films        | Nombre total, répartition résolution (4K/1080p...), codec vidéo/audio, sous-titres, métadonnées, fraîcheur |
| Séries       | Nombre d'épisodes, qualité vidéo/audio, ratio de complétude (calculé localement)                           |
| Musique      | Nombre de pistes/albums/artistes, qualité audio (FLAC/MP3...)                                              |

**Jamais transmis** : titres, noms de fichiers, identifiants TMDB/TVDB, adresse IP de votre serveur.

---

## Licence

MIT — voir [LICENSE](../../LICENSE)
