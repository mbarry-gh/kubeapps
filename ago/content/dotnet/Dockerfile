### Build and Test the App
FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build

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
RUN addgroup -S ago && \
    adduser -S ago -G ago && \
    mkdir -p /home/ago && \
    chown -R ago:ago /home/ago

### run as ago user
USER ago
ENV ASPNETCORE_URLS=http://+:{{gitops.port}}

### copy the app
COPY --from=build /app .

ENTRYPOINT [ "dotnet",  "{{gitops.name}}.dll" ]
