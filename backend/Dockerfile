# Stage 1: Build the .NET Application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["NextStep.csproj", "./"]
RUN dotnet restore "NextStep.csproj"

# Copy the remaining source code and build the application
COPY . .
RUN dotnet build "NextStep.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "NextStep.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS production
WORKDIR /app
COPY --from=publish /app/publish .

# Configure environment variables
# The application will listen on port 5000 as defined by docker-compose
EXPOSE 5000
ENV ASPNETCORE_HTTP_PORTS=5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Start the application
ENTRYPOINT ["dotnet", "NextStep.dll"]
