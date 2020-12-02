FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS builder
WORKDIR /app
COPY ./*.csproj ./
RUN dotnet restore
COPY ./ ./
RUN dotnet publish -c Release -o out
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 as runtime
COPY --from=builder ["/app/out", "./"]
ENTRYPOINT ["dotnet", "EfCoreBug.dll"]