#!/bin/bash

echo "on-create start" >> ~/status

# run dotnet restore
dotnet restore kap/kap.csproj 

# clone repos
git clone https://github.com/retaildevcrews/ngsa-app /workspaces/ngsa-app
git clone https://github.com/microsoft/webvalidate /workspaces/webvalidate

# copy grafana.db to /grafana
sudo cp deploy/grafanadata/grafana.db /grafana
sudo chown -R 472:0 /grafana

# create local registry
docker network create k3d
k3d registry create registry.localhost --port 5000
docker network connect k3d k3d-registry.localhost

# install postgres
docker run -d \
--name postgres \
--restart always \
-e POSTGRES_PASSWORD=akdc-512 \
-v /ago/postgres/data:/var/lib/postgresql/data \
--network k3d \
-p 5432:5432 \
postgres

sleep 20

# create git db and user
docker exec -it postgres psql -U postgres -c "create user gogs with password 'akdc-512';"
docker exec -it postgres psql -U postgres -c "create database gogs owner gogs;"

# run local git instance
docker run -d \
--name gogs \
--restart always \
--network k3d \
-p 3000:3000 -p 22:22 \
-v /ago/gogs/data:/data \
gogs/gogs

echo "on-create complete" >> ~/status
