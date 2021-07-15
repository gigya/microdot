#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["CalculatorService.Orleans/CalculatorService.Orleans.csproj", "CalculatorService.Orleans/"]
RUN dotnet restore "CalculatorService.Orleans/CalculatorService.Orleans.csproj"
COPY . .
    WORKDIR "/src/CalculatorService.Orleans"
RUN dotnet build "CalculatorService.Orleans.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CalculatorService.Orleans.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
    ENTRYPOINT ["dotnet", "CalculatorService.Orleans.dll"]