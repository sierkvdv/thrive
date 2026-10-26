# Thrive-fork: "deel 2 van Spore" (Sierk)

Fork van Revolutionary-Games/Thrive (GPL-3; wat we erop bouwen blijft open-source).
Doel: een Spore-achtig evolutiespel. Cel -> meercellig -> vis -> land, jij ontwerpt en paart je soort door de eeuwen.
Werkwijze: niets zelf uitvinden wat al bestaat; Thrive als basis, losse open-source stukken erbij (Quaternius/Kenney CC0, IK-poten).
Geen neurale netwerken (daar is Oerbeest in vastgelopen; dat project blijft apart en onaangeroerd).

## Branches
- `master` = upstream Thrive (alleen `git pull` van upstream). Ons werk op `deel2`.

## Opzet op deze pc
- Godot 4.7.2 .NET: `..\godot-4.7.2-mono\` (Thrive eist exact deze versie, zie `Thrive.csproj`).
- .NET 10 SDK via winget. Native libs: `dotnet run --project Scripts -- native Fetch Install`.
- Bouwen: `dotnet build Thrive.csproj`. Assets importeren: `<godot>_console.exe --headless --import --path .` (eerste keer 2x draaien; eerste run crasht soms halverwege).
- Spelen: `Start-Thrive.cmd`. Start standaard exclusive fullscreen; F11 wisselt.
- Saves/logs/instellingen: `%APPDATA%\Thrive` (logs\log.txt).
- Starten vanuit de Claude-app: via `Invoke-CimMethod Win32_Process Create` (MSIX-schaduwmap).
- Headless rooktest: `<godot>_console.exe --headless --path . --quit-after 900`.
