#!/bin/bash

realpath() { [[ $1 = /* ]] && echo "$1" || echo "$PWD/${1#./}"; }
SCRIPT_DIR=`dirname $(realpath $0)` # without ending /

function generate_proto_file {
    local protofile=${1}
    echo "Generate '${protofile}'"
    protoc -I=${SCRIPT_DIR}/ --csharp_out=${SCRIPT_DIR}/../ ${SCRIPT_DIR}/${protofile}
}

generate_proto_file remote.proto
generate_proto_file types.proto
generate_proto_file gogoproto/gogo.proto

echo "All done"