#!/bin/sh
set -eu

dotnet CityQuest.WebApi.dll --initialize-database
exec dotnet CityQuest.WebApi.dll "$@"
