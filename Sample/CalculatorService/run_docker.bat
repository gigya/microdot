SET repo=calculatorservice
SET tag=1.0.0.1

dotnet publish -c Release
docker build -t %repo%:%tag% -f Dockerfile .
docker run -it --name CalculatorService --rm -p 12323:12323 %repo%:%tag%