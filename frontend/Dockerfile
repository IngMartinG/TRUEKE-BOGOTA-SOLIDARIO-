# syntax=docker/dockerfile:1
# Front Angular de Trueke Bogotá Solidario.
#   docker build -t trueke-web .
#   docker run -p 8081:8080 -e API_UPSTREAM=http://api:8080 trueke-web
# Etapa 1: compilación (Node solo existe aquí; la imagen final no lleva código fuente ni herramientas).
FROM node:24-alpine AS compilacion
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npx ng build --configuration production

# Etapa 2: servidor estático sin privilegios (usuario nginx, puerto 8080).
FROM nginxinc/nginx-unprivileged:1.27-alpine
# Valores por defecto: API en el mismo origen, servida por el proxy inverso de este nginx.
ENV API_URL="" \
    API_UPSTREAM="http://api:8080" \
    NGINX_RESOLVER="127.0.0.11" \
    CSP_CONNECT_EXTRA="" \
    CSP_IMG_EXTRA="" \
    MAPA_TESELAS="" \
    MAPA_ATRIBUCION=""
COPY docker/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY docker/seguridad.inc.template /etc/nginx/templates/seguridad.inc.template
COPY docker/config.json.template /etc/trueke/config.json.template
COPY --chmod=755 docker/05-trueke-config.envsh /docker-entrypoint.d/05-trueke-config.envsh
# Los archivos servidos pertenecen a root (solo lectura para nginx). config.json se genera en /tmp al arrancar.
COPY --from=compilacion /app/dist/trueke-web/browser /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://127.0.0.1:8080/salud || exit 1
