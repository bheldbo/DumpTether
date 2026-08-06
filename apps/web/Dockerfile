FROM node:24-alpine AS build
WORKDIR /src/apps/web

COPY apps/web/package.json apps/web/package-lock.json ./
RUN npm ci

COPY apps/web/ ./
RUN npm run build

FROM caddy:2-alpine AS runtime
ARG IMAGE_VERSION=development
ARG SOURCE_REVISION=unknown
LABEL org.opencontainers.image.title="DumpTether Web" \
      org.opencontainers.image.description="DumpTether React web client served by Caddy" \
      org.opencontainers.image.source="https://github.com/bheldbo/DumpTether" \
      org.opencontainers.image.url="https://dumptether.bheldbo.dk" \
      org.opencontainers.image.documentation="https://github.com/bheldbo/DumpTether/blob/main/README.md" \
      org.opencontainers.image.licenses="NOASSERTION" \
      org.opencontainers.image.vendor="bheldbo" \
      org.opencontainers.image.version="${IMAGE_VERSION}" \
      org.opencontainers.image.revision="${SOURCE_REVISION}"
COPY apps/web/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /src/apps/web/dist /srv
EXPOSE 80
