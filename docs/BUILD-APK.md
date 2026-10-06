# Construire l'APK sans PC (GitHub Actions)

Le workflow `.github/workflows/build-android.yml` compile le projet Unity
(6000.3.24f1, IL2CPP ARM64) sur les serveurs de GitHub et publie l'APK.

## 1. Secrets à créer une seule fois
GitHub → dépôt `touchline` → **Settings → Secrets and variables → Actions →
New repository secret**.

| Nom | Valeur |
|-----|--------|
| `UNITY_LICENSE` | Tout le contenu du fichier `C:\ProgramData\Unity\Unity_lic.ulf` (l'ouvrir avec le Bloc-notes, tout copier) |
| `UNITY_EMAIL` | E-mail du compte Unity |
| `UNITY_PASSWORD` | Mot de passe du compte Unity |
| `ANDROID_KEYSTORE_BASE64` | Contenu du fichier `keystore-base64.txt` (clé de signature) |
| `ANDROID_KEYSTORE_PASS` | Contenu du fichier `keystore-pass.txt` |

Les deux derniers sont facultatifs mais **fortement conseillés** : sans eux,
chaque build est signé avec une clé différente, et Android refuse la mise à
jour (il faut désinstaller, ce qui efface la carrière). Garder le fichier
`touchline.keystore` et son mot de passe en lieu sûr (Drive privé) : les
perdre oblige à désinstaller une dernière fois.

## 2. Lancer un build
- Onglet **Actions → Build Android APK → Run workflow** (branche au choix), ou
- pousser un tag `apk-<version>` : l'APK est alors aussi publié dans **Releases**.

Durée : 30 à 60 min la première fois (import complet), moins ensuite grâce au cache.

## 3. Récupérer l'APK
- Build par tag : page **Releases**, fichier `.apk`, téléchargeable directement sur le téléphone.
- Build manuel : page du run → section **Artifacts** → `Touchline-APK` (zip contenant l'APK).

## Version
Numéro et `versionCode` : `Editor/ProjectBuilder.cs` (`Configure`). Augmenter
`bundleVersionCode` à chaque APK installé, sinon Android refuse la mise à jour.
