#!/bin/bash

#
#  "sp_env_name" content example:
# {
#   "appId": "...",
#   "displayName": "dev-prometheus-adx",
#   "name": "http://dev-prometheus-adx",
#   "password": "...",
#   "tenant": "...",
#   "cloud": "AzureCloud"
# }
#

set -e
[ $# -lt 1 ] && echo "Usage: $(basename $0) <sp env var name>" && exit 1

AZURE_SP=${!1} # get content by var name

function get_sp {
    echo $AZURE_SP | jq --raw-output ".${1:-}";
}

echo [*] SP `get_sp displayName`

az cloud set \
    --name `get_sp cloud`

az login \
    --service-principal \
    --tenant   `get_sp tenant` \
    --username `get_sp appId` \
    --password `get_sp password`