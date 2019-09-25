#!/bin/sh
DIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
cd "${DIR}"

VIEWER_BINARY_PATH="$1"
./optool install -c load -p "@executable_path/../Frameworks/ReflectCustomUri.framework/Versions/A/ReflectCustomUri" -t "${VIEWER_BINARY_PATH}"

