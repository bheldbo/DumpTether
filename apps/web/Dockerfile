FROM node:24-alpine AS build
WORKDIR /src/apps/web

COPY apps/web/package.json apps/web/package-lock.json ./
RUN npm ci

COPY apps/web/ ./
RUN npm run build

FROM caddy:2-alpine AS runtime
COPY apps/web/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /src/apps/web/dist /srv
EXPOSE 80
