# Multi-stage build for the React (Vite) Admin UI: compile the static bundle with Node, then
# serve it with nginx. Build context is this directory.
FROM node:23-alpine AS build
WORKDIR /app
# Vite has no runtime env — the API base URL and admin key must be baked in at build time via
# these build args (see docker-compose.yml's config-ui build.args), otherwise the bundle falls
# back to the dotnet-run dev port / an empty key and every request from the container fails.
ARG VITE_API_BASE_URL
ARG VITE_ADMIN_API_KEY
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_ADMIN_API_KEY=$VITE_ADMIN_API_KEY
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
