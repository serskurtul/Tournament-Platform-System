FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
# Reproducible install from the lock file (npm install may rewrite it and is not deterministic).
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
# nginx:alpine renders /etc/nginx/templates/*.template into conf.d at start (envsubst).
# API_UPSTREAM is the backend base URL: compose uses the service name 'app', Kubernetes uses its own Service.
ENV API_UPSTREAM=http://app:80
COPY nginx.conf /etc/nginx/templates/default.conf.template
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]