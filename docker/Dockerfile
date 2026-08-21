# ==========================================
# Stage 1: build (Restore + Publish)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY core/*.csproj ./core/
RUN dotnet restore ./core/*.csproj
COPY core/ ./core/
WORKDIR /src/core
RUN dotnet publish -c Release -o /app/publish

# ==========================================
# Stage 2: final (Runtime ligero)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "openclient.dll"]