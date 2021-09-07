#!/bin/bash

echo "post-start start" >> ~/status

# this runs each time the container starts

# update the base docker images
docker pull mcr.microsoft.com/dotnet/aspnet:5.0-alpine
docker pull mcr.microsoft.com/dotnet/sdk:5.0
docker pull ghcr.io/cse-labs/jumpbox:latest
docker pull postgres:latest
docker pull registry:2
docker pull gogs/gogs:latest

echo "post-start complete" >> ~/status
