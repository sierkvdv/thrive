@echo off
rem Start direct in de landfase met een testbeest op vier poten
start "" "%~dp0..\godot-4.7.2-mono\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --screen -2 --path "%~dp0." res://src/stage_starters/MacroscopicStageStarter.tscn -- dev-legs dev-land=2
