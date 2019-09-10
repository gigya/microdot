FROM mcr.microsoft.com/dotnet/core/runtime:2.2
COPY ./bin/Release/netcoreapp2.0/publish/ app/
WORKDIR /app
ENTRYPOINT ["dotnet", "CalculatorService.Orleans.dll"]