#!/bin/bash
set -eu
protoc_version=3.11.4
protoc_dir=/tmp/protoc/
install -d ${protoc_dir}
cd ${protoc_dir}
curl -L -o protoc.zip https://github.com/protocolbuffers/protobuf/releases/download/v${protoc_version}/protoc-${protoc_version}-linux-x86_64.zip
unzip protoc.zip
cp ${protoc_dir}/bin/protoc /usr/sbin/
rm -rf ${protoc_dir}
