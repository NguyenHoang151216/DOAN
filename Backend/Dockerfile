FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY DoAn.HotelParking.sln ./
COPY src/DoAn.HotelParking.Core.Domain/DoAn.HotelParking.Core.Domain.csproj src/DoAn.HotelParking.Core.Domain/
COPY src/DoAn.HotelParking.Core.Application/DoAn.HotelParking.Core.Application.csproj src/DoAn.HotelParking.Core.Application/
COPY src/DoAn.HotelParking.Infrastructure/DoAn.HotelParking.Infrastructure.csproj src/DoAn.HotelParking.Infrastructure/
COPY src/DoAn.HotelParking.Presentation.Api/DoAn.HotelParking.Presentation.Api.csproj src/DoAn.HotelParking.Presentation.Api/
COPY tests/DoAn.HotelParking.UnitTests/DoAn.HotelParking.UnitTests.csproj tests/DoAn.HotelParking.UnitTests/

RUN dotnet restore DoAn.HotelParking.sln

COPY . .
RUN dotnet publish src/DoAn.HotelParking.Presentation.Api/DoAn.HotelParking.Presentation.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DoAn.HotelParking.Presentation.Api.dll"]
