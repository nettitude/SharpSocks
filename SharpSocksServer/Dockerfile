FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["SharpSocksServer.csproj", "SharpSocksServer/"]
RUN dotnet restore "SharpSocksServer/SharpSocksServer.csproj"
COPY . "SharpSocksServer/"
WORKDIR "/src/SharpSocksServer"
RUN dotnet build "SharpSocksServer.csproj" -c "Release Linux" -o /app/build

FROM build AS publish
RUN dotnet publish "SharpSocksServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 43334
EXPOSE 8081
ENTRYPOINT ["dotnet", "SharpSocksServer.dll"]
