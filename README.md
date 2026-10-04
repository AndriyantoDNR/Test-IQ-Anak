# SiapSD Cognitive

Internal cognitive-performance development app. Not a clinical diagnosis; never produces standardized IQ.

## Start

1. Start local PostgreSQL on `127.0.0.1:5432` and create database/user supplied by team.
2. Set password outside repo: `dotnet user-secrets set "ConnectionStrings:SiapSD" "Host=127.0.0.1;Port=5432;Database=siapsd_cognitive;Username=siapsd_dev;Password=YOUR_PASSWORD" --project src/SiapSD.Cognitive.Api`
3. Apply migration: `dotnet tool run dotnet-ef database update --project src/SiapSD.Cognitive.Infrastructure --startup-project src/SiapSD.Cognitive.Api`
4. API: `dotnet run --project src/SiapSD.Cognitive.Api --urls http://localhost:5000`
5. Web: `cd src/siap-sd-cognitive-web; npm install; npm run dev`

Local EF Core 8 tool manifest: `dotnet-tools.json`.
