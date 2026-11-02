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
- Editor: knop "Pootpaar toevoegen" (`MetaballBodyEditorComponent.OnAddLegPairPressed`, 42 MP, undo via CombinedEditorAction); editorvloer zakt mee onder de poten.
- Eten: `CreatureFood.cs` (gele bollen in water, rode bessen op land); 5 eten = editor open (`MacroscopicCreature.FoodEaten`, `ResetGrowth`).
- Naar het land kan pas met poten (`MacroscopicHUD`, `HasLegChains`).
- Kleur: witte soort krijgt bij overgang naar macroscopic groen + schubben (`GameWorld.ChangeSpeciesToMacroscopic`). Textuurcache `%APPDATA%\Thrive\cache\img` legen na kleur/shader-wijziging (kleur zit niet in de cache-sleutel).
- Hele keten testen: `MacroscopicEditorStarter.tscn -- devshot-dir=... dev-editorlegs=8,2 dev-editorfinish=10 dev-land=19 dev-walk=22 dev-foodahead=..`.
- Volledig spel vanaf de cel: `Start-Thrive.cmd` -> New Game. `Start-Landbeest.cmd` is alleen de testsprong naar het land.
- Headless rooktest: `<godot>_console.exe --headless --path . --quit-after 900`.
