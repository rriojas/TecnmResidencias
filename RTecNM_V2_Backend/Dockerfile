FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["TecNM.Residency.csproj", "./"]
RUN dotnet restore "TecNM.Residency.csproj"

COPY . .
RUN dotnet publish "TecNM.Residency.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Crear directorio de uploads para documentos escaneados
RUN mkdir -p /app/uploads/documents

ENV ASPNETCORE_URLS=http://+:5144
EXPOSE 5144

ENTRYPOINT ["dotnet", "TecNM.Residency.dll"]
