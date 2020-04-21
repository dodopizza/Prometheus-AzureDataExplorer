#!/bin/bash
set -eo pipefail

[ $# -lt 1 ] && echo "Usage: $(basename $0) <cluster>" && exit 1

cluster_name=${1}
subscription_id=`az account show | jq '.id' --raw-output`
cloud_name=`az account show | jq '.environmentName' --raw-output`

az ad sp create-for-rbac \
    --skip-assignment \
    --name "prometheus-adx-${cluster_name}" \
| jq \
    --arg subscription_id "${subscription_id}" \
    --arg cloud_name "${cloud_name}" \
    '. + { subscription_id: $subscription_id, cloud: $cloud_name }'
