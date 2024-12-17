#!/bin/bash

git fetch
git reset --hard
git checkout origin/main-ffc-at-beginning-with-1-minute-wait
cd ../Dockerfiles || exit
sudo docker-compose build --no-cache
sudo docker-compose down
sudo docker-compose up --detach