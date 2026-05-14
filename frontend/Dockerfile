# Stage 1: Build the Angular application
FROM node:22-alpine AS build
WORKDIR /app

# Copy necessary files for dependency installation
COPY package.json package-lock.json ./
RUN npm ci

# Copy the rest of the application files and build
COPY . .
RUN npm run build --configuration=production

# Stage 2: Serve the application using Nginx
FROM nginx:alpine AS production

# Sécurité : On crée un utilisateur non-root pour Nginx
RUN touch /var/run/nginx.pid && \
    chown -R nginx:nginx /var/run/nginx.pid /var/cache/nginx /var/log/nginx /etc/nginx/conf.d

USER nginx

# Copy the custom Nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy the built application from the build stage.
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html

# Expose port 8080 (non-root)
EXPOSE 8080

# Start Nginx
CMD ["nginx", "-g", "daemon off;"]
