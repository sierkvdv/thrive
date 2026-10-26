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
- `Start-Landbeest.cmd`: direct de landfase in met de testviervoeter (`dev-legs dev-land=2`).
- Onzichtbaar testen met plaatjes (geen venster op Sierks schermen, OBS draait vaak):
  `<godot>_console.exe --path . --position -6000,0 --resolution 1280x720 res://src/stage_starters/MacroscopicStageStarter.tscn -- devshot-dir=C:/pad devshot-times=10,14 dev-legs dev-land=3 dev-camyaw=1.5 dev-walk=12`
  (src/engine/DevShots.cs; devshot-dir forceert windowed).

## Ons werk (deel2)
- Poten: `src/macroscopic_stage/CreatureRig.cs` (bot per metabal + huidgewichten), `CreatureLegs.cs` (pootketens vinden, voeten planten, draf, Godot `Fabrik3D`). Aangehaakt in `MacroscopicCreature` (rig na UV-unwrap, capsule tot de voeten, topsnelheid).
- Bekend: beest is nog wit (textuur komt niet door), poten alleen als de editor-vorm ketens onder het lijf heeft.
- Headless rooktest: `<godot>_console.exe --headless --path . --quit-after 900`.
