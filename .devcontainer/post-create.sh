#!/bin/bash

echo "post-create start" >> ~/status

# pull the rancher images
docker pull rancher/k3s:v1.21.3-k3s1
docker pull rancher/k3d-proxy:4.4.8

# this runs in background after UI is available

# (optional) upgrade packages
#sudo apt-get update
#sudo apt-get upgrade -y
#sudo apt-get autoremove -y
#sudo apt-get clean -y

# add your commands here

echo "post-create complete" >> ~/status
