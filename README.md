# Balatro Save and Load Tool

A simple tool that allows you to save your Balatro game at any point, and load it again later.

## How to use

* Hit "Save" whenever you want to save your current run in the selected profile.
* Check "Auto save" if you want the current profile to be saved every X minutes.
* All Saves show up in the list
* To load a save file:
  * In Balatro: go to the main menu (you need to exit your current run)
  * Select the save file in the list, and click "Load". WARNING! Your current run in Balatro will be overwritten!
  * In Balatro: continue your current run, it will be the one you just loaded.

## Download

Download the latest release here: https://github.com/papauschek/balatro-save-load/releases/tag/v1.2.0

## Screenshot
![Screenshot 2025-05-16 160647](https://github.com/user-attachments/assets/7a75374d-82e9-408c-b9a3-20643780b37b)

## Build Status

[![Build WPF app](https://github.com/papauschek/balatro-save-load/actions/workflows/build.yml/badge.svg)](https://github.com/papauschek/balatro-save-load/actions/workflows/build.yml)

This project is automatically built using GitHub Actions. You can find the latest build artifact by following these steps:

1. Go to the [Actions tab](https://github.com/papauschek/balatro-save-load/actions) in this repository.
2. Click on the latest "Build WPF app" workflow run.
3. Scroll down to the "Artifacts" section.
4. Download the "BalatroSaveAndLoad" artifact.

The artifact contains the latest executable build of the application.

## Development

To build this project locally:

1. Ensure you have .NET 8.0 SDK installed.
2. Clone this repository.
3. Open a terminal in the project directory.
4. Run the following commands:

```bash
dotnet restore BalatroSaveAndLoad/BalatroSaveAndLoad.csproj
dotnet build BalatroSaveAndLoad/BalatroSaveAndLoad.csproj --configuration Release
dotnet publish BalatroSaveAndLoad/BalatroSaveAndLoad.csproj -c Release -o publish --self-contained true -r win-x64 /p:PublishSingleFile=true
```

5. The built executable will be in the `publish` directory.
