#!/bin/bash

if test -f isdocker; then
  return
fi

apt-get update
apt-get install -y unzip ffmpeg nano

wget https://github.com/immisterio/Lampac/releases/latest/download/publish.zip
unzip -o publish.zip && rm -f publish.zip

touch isdocker

dotnet Lampac.dll
