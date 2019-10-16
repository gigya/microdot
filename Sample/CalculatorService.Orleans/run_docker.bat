SET repo=calculatorservice_orleans
SET tag=1.0.0.1

dotnet publish -c Release
docker build -t %repo%:%tag% -f Dockerfile .
docker run -it --name CalculatorServiceOrleans --rm -p 12323:12323 %repo%:%tag%