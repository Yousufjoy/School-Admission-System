FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
ARG VITE_BACKEND_URL=http://localhost:8080
ARG VITE_API_BASE_URL=
ENV VITE_BACKEND_URL=$VITE_BACKEND_URL
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

RUN npm run build

FROM nginx:alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
COPY 40-runtime-env.sh /docker-entrypoint.d/40-runtime-env.sh

RUN sed -i 's/\r$//' /docker-entrypoint.d/40-runtime-env.sh \
    && chmod +x /docker-entrypoint.d/40-runtime-env.sh

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]

