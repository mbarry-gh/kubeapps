### Build and Test the App
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build

### copy the source and tests
COPY . /src

WORKDIR /src

# build the app
RUN dotnet publish -c Release -o /app

###########################################################


### Build the runtime container
FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine AS release

### if port is changed, also update value in Config
EXPOSE {{gitops.port}}
WORKDIR /app

### create a user
### dotnet needs a home directory
RUN addgroup -S kap && \
    adduser -S kap -G kap && \
    mkdir -p /home/kap && \
    chown -R kap:kap /home/kap

### run as kap user
USER kap
ENV ASPNETCORE_URLS=http://+:{{gitops.port}}

### copy the app
COPY --from=build /app .

ENTRYPOINT [ "dotnet",  "{{gitops.name}}.dll" ]
