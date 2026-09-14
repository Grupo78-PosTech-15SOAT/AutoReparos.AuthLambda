# Multi-stage Dockerfile for AutoReparos.AuthLambda (.NET 10 Serverless Function)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/AutoReparos.AuthLambda.csproj", "src/"]
RUN dotnet restore "src/AutoReparos.AuthLambda.csproj"

COPY . .
WORKDIR "/src/src"
RUN dotnet publish "AutoReparos.AuthLambda.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "AutoReparos.AuthLambda.dll"]
