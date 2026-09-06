@echo off
set DEST=%LOCALAPPDATA%\ClothSimLab
if not exist "%DEST%\ClothSimLab.exe" (
  mkdir "%DEST%" 2>nul
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Expand-Archive -LiteralPath 'ClothSimLab-Windows.zip' -DestinationPath $env:LOCALAPPDATA\ClothSimLab -Force"
  powershell -NoProfile -ExecutionPolicy Bypass -Command "$dir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'; $s = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $dir 'Cloth Simulation.lnk')); $s.TargetPath = Join-Path $env:LOCALAPPDATA 'ClothSimLab\ClothSimLab.exe'; $s.WorkingDirectory = Join-Path $env:LOCALAPPDATA 'ClothSimLab'; $s.Save()"
)
start "" "%DEST%\ClothSimLab.exe"
