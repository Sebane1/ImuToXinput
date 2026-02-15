# ImuToXInput Config Server

Simple API to store and share game configs so the MAUI app can search and download them.

## Run locally

```bash
dotnet run
```

Runs with HTTPS (default port 7001). The MAUI app is preconfigured to use `https://localhost:7001`. For the Android emulator, use your machine's IP or `http://10.0.2.2:5000` if you run the server with HTTP on port 5000.

## Endpoints

- **POST /api/configs** – Upload a config. Body: `{ "gameName", "configName", "jsonContent" }`.
- **GET /api/configs?search=** – List configs (optional search by game or config name).
- **GET /api/configs/{id}** – Get full config including JSON.

Data is stored under `SharedConfigs/` as JSON files. No database required.

## Deploying

Publish and run on any host (Azure, AWS, VPS, etc.). Set the MAUI app's server URL in `SharedConfigsPage.xaml.cs` (`_service.BaseUrl`) or add a settings screen so users can enter the URL.
