# Mod Dropper

Drag and drop installer for MSFS add-ons. Drop a zip, 7z, rar, tar or a plain folder on the window and it gets unpacked and put where it belongs.

Not affiliated with or endorsed by Addon Linker.

## What it does

- Handles .zip, .7z, .rar, .tar, .tar.gz/.tgz, .tar.bz2 and .tar.xz without needing WinRAR or 7-Zip
- Finds the manifest.json so nested folders and multi-package downloads end up in the right place
- Installs straight into your Community folder, or into a separate add-ons folder and links it into Community
- Saves a list of destination folders you can switch between
- Asks before replacing a mod that is already installed

## Privacy

No network access and no data collection. It only touches the folders you pick, plus its settings file at `%AppData%\ModDropper\settings.json`.

## Known limits

- Replacing a mod deletes the old folder first, files are not merged
- Password-protected and split multi-part archives are not supported
- Links made in add-ons folder mode may not show up in the Addon Linker app itself
- The exe is not code signed, so Windows SmartScreen will warn on first run (More info, then Run anyway)

## Building

Open the solution in Visual Studio and build. Needs .NET Framework 4.7.2. Archive support uses the SharpCompress NuGet package.

## Credits

[SharpCompress](https://github.com/adamhathcock/sharpcompress) for reading rar, 7z and tar archives.