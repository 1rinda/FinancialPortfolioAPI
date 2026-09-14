FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet publish src/FinancialPortfolioAPI.API -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
ENV Database__Provider=Sqlite
ENV ConnectionStrings__DefaultConnection="Data Source=/data/portfolio.db;Default Timeout=30"
RUN mkdir /data && chown app:app /data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "FinancialPortfolioAPI.API.dll"]
