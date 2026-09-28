# Build context: src/api
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Romp.Api/Romp.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# SCRUM-93: PO files are written here (a volume in docker-compose.yml). Created before dropping to the
# non-root user, since /app is read-only for it.
RUN mkdir -p /data/po-files && chown -R $APP_UID /data
ENV Vndr__PoFileStorage__RootPath=/data/po-files
USER $APP_UID
ENTRYPOINT ["dotnet", "Romp.Api.dll"]
