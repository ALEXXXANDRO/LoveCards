#!/bin/bash

dotnet publish -c Release
rm -rf docs
mkdir -p docs
cp -R bin/Release/*/publish/wwwroot/* docs/
touch docs/.nojekyll
echo "eto-proydet.ru" > docs/CNAME

git add .
git commit -m "Deploy update"
git push

echo "Готово 💛"