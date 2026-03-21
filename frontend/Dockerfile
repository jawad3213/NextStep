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
FROM nginx:alpine

# Copy the custom Nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy the built application from the build stage.
# Note: The Angular application builder outputs client assets to the "browser" subfolder.
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html

# Expose port 80
EXPOSE 80

# Start Nginx
CMD ["nginx", "-g", "daemon off;"]
