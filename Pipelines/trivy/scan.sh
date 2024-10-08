#!/bin/bash

if [ ${BUILD_SOURCEBRANCHNAME} -eq "master" ]; then
  ACR_NAME="ospriprodregistry"
else
  ACR_NAME="ospriregistry"
fi

IMAGE_TAG="${BUILD_BUILDNUMBER}"

echo "Begining Scan..."

for IMAGE in "$@"
do
  echo "Scaning $IMAGE"
  trivy image --exit-code 1 --severity LOW,MEDIUM,HIGH,CRITICAL $ACR_NAME.azurecr.io/$IMAGE:$IMAGE_TAG
done

echo "Scan complete"