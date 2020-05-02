# Development
FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine AS development

RUN apk update \
  && apk --no-cache add curl procps unzip \
  && wget -qO- https://aka.ms/getvsdbgsh | /bin/sh /dev/stdin -v latest -l /vsdbg

RUN addgroup -g 1000 dotnet \
    && adduser -u 1000 -G dotnet -s /bin/sh -D dotnet

USER dotnet
WORKDIR /home/dotnet

RUN mkdir -p /home/dotnet/PowerLinesWeb/ /home/dotnet/PowerLinesWeb.Tests/
COPY --chown=dotnet:dotnet ./PowerLinesWeb.Tests/*.csproj ./PowerLinesWeb.Tests/
RUN dotnet restore ./PowerLinesWeb.Tests/PowerLinesWeb.Tests.csproj
COPY --chown=dotnet:dotnet ./PowerLinesWeb/*.csproj ./PowerLinesWeb/
RUN dotnet restore ./PowerLinesWeb/PowerLinesWeb.csproj
COPY --chown=dotnet:dotnet ./PowerLinesWeb.Tests/ ./PowerLinesWeb.Tests/
COPY --chown=dotnet:dotnet ./PowerLinesWeb/ ./PowerLinesWeb/
RUN dotnet publish ./PowerLinesWeb/ -c Release -o /home/dotnet/out

ARG PORT=5000
ENV PORT ${PORT}
ENV ASPNETCORE_ENVIRONMENT=development
EXPOSE ${PORT}
# Override entrypoint using shell form so that environment variables are picked up
ENTRYPOINT dotnet watch --project ./PowerLinesWeb run --urls "http://*:${PORT}"

# Production
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-alpine AS production

RUN addgroup -g 1000 dotnet \
    && adduser -u 1000 -G dotnet -s /bin/sh -D dotnet

USER dotnet
WORKDIR /home/dotnet

COPY --from=development /home/dotnet/out/ ./
ARG PORT=5000
ENV ASPNETCORE_URLS http://*:${PORT}
ENV ASPNETCORE_ENVIRONMENT=production
EXPOSE ${PORT}
# Override entrypoint using shell form so that environment variables are picked up
ENTRYPOINT dotnet PowerLinesWeb.dll
