#!/bin/bash

echo "on-create start" >> ~/status

# run dotnet restore
dotnet restore kap/kap.csproj

# copy grafana.db to /grafana
sudo cp deploy/grafanadata/grafana.db /grafana
sudo chown -R 472:0 /grafana

# create local registry
docker network create k3d
k3d registry create registry.localhost --port 5000
docker network connect k3d k3d-registry.localhost

# build KubeApps
cd kap
make build
cd ..

# clone repos
git clone https://github.com/retaildevcrews/ngsa-dapr /workspaces/ngsa
git clone https://github.com/cse-labs/webvalidate /workspaces/webvalidate

# configure git
sudo git config --system core.whitespace blank-at-eol,blank-at-eof,space-before-tab
sudo git config --system pull.rebase false
sudo git config --system init.defaultbranch main
sudo git config --system fetch.prune true
sudo git config --system core.pager more

echo "on-create complete" >> ~/status

### spike using gogs as a local git repo
# install postgres
# docker run -d \
# --name postgres \
# --restart always \
# -e POSTGRES_PASSWORD=akdc-512 \
# -v /gogs/postgresdata:/var/lib/postgresql/data \
# --network k3d \
# -p 5432:5432 \
# postgres

# create gogs user
# docker exec postgres psql -U postgres -c "create user gogs with password 'akdc-512';"

# create gogs db
# docker exec postgres psql -U postgres -c "create database gogs owner gogs;"

# run local git instance
# docker run -d \
# --name gogs \
# --restart always \
# --network k3d \
# -p 3000:3000 -p 22:22 \
# -v /gogs/gogsdata:/data \
# gogs/gogs

# configure git to use local repo
# echo "http://vscode:akdc-512@localhost:3000" >> ~/.git-credentials
# git config --global credential.helper store
# sudo git config --system credential.helper store
# git config --global url.http://localhost:3000/vscode/.insteadOf v://
