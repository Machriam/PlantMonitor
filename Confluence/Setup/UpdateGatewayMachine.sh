#!/bin/bash

git fetch
git reset --hard
git checkout origin/main
cd ../Dockerfiles || exit
sudo docker-compose build --no-cache
sudo docker-compose down
sudo docker-compose up --detach