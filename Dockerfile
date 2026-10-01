# Site (.NET) + bot do WhatsApp (Node) na mesma imagem: o site sobe o bot sozinho.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/DeliciasDaNilda/DeliciasDaNilda.csproj src/DeliciasDaNilda/
RUN dotnet restore src/DeliciasDaNilda/DeliciasDaNilda.csproj
COPY src/DeliciasDaNilda/ src/DeliciasDaNilda/
RUN dotnet publish src/DeliciasDaNilda/DeliciasDaNilda.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates \
 && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
 && apt-get install -y --no-install-recommends nodejs \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /whatsapp-bot
COPY whatsapp-bot/package*.json ./
RUN npm ci --omit=dev
COPY whatsapp-bot/index.js ./
COPY whatsapp-bot/src ./src

WORKDIR /app
COPY --from=build /app .
RUN mkdir -p wwwroot/uploads/sabores

ENV ASPNETCORE_ENVIRONMENT=Production \
    WhatsAppBot__Pasta=/whatsapp-bot \
    Database__MigrarAoIniciar=true
EXPOSE 10000
# O Render informa a porta em $PORT; o bot fala com o site por localhost nessa porta.
CMD ["sh", "-c", "export PORT=${PORT:-10000}; export ASPNETCORE_URLS=http://0.0.0.0:$PORT; export WhatsAppBot__ApiBaseUrl=http://localhost:$PORT; exec dotnet DeliciasDaNilda.dll"]
