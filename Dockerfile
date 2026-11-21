FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY CityQuest.Domain/CityQuest.Domain.csproj CityQuest.Domain/
COPY CityQuest.Application/CityQuest.Application.csproj CityQuest.Application/
COPY CityQuest.DataAccess/CityQuest.DataAccess.csproj CityQuest.DataAccess/
COPY CityQuest.WebApi/CityQuest.WebApi.csproj CityQuest.WebApi/
RUN dotnet restore CityQuest.WebApi/CityQuest.WebApi.csproj
COPY CityQuest.Domain/ CityQuest.Domain/
COPY CityQuest.Application/ CityQuest.Application/
COPY CityQuest.DataAccess/ CityQuest.DataAccess/
COPY CityQuest.WebApi/ CityQuest.WebApi/
RUN dotnet publish CityQuest.WebApi/CityQuest.WebApi.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
COPY docker/entrypoint.sh /app/entrypoint.sh
RUN sed -i 's/\r$//' /app/entrypoint.sh && mkdir -p /app/keys && chown app:app /app/keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["sh", "/app/entrypoint.sh"]
