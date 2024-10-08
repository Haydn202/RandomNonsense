#!/bin/bash

if [ ${BUILD_SOURCEBRANCHNAME} -eq "master" ]; then
  ACR_NAME="ospriprodregistry"
  AZURE_SERVICE_PRINCIPAL_ID=$ProdPrincipalId
  AZURE_TENANT_ID=$ProdTenantId
  AZURE_SERVICE_PRINCIPAL_PASSWORD=$ProdPassword
else
  ACR_NAME="ospriregistry"
  AZURE_SERVICE_PRINCIPAL_ID=$PrincipalId
  AZURE_TENANT_ID=$TenantId
  AZURE_SERVICE_PRINCIPAL_PASSWORD=$Password
fi

IMAGE_TAG="${BUILD_BUILDNUMBER}"

echo "Installing Trivy..."

sudo apt-get install rpm
wget https://github.com/aquasecurity/trivy/releases/download/v0.32.0/trivy_0.32.0_Linux-64bit.deb
sudo dpkg -i trivy_0.32.0_Linux-64bit.deb
trivy -v

echo "Trivy Installed"

echo "Logging into Azure..."
az login --service-principal --username $AZURE_SERVICE_PRINCIPAL_ID --password $AZURE_SERVICE_PRINCIPAL_PASSWORD --tenant $AZURE_TENANT_ID

echo "Logging into Azure Container Registry..."
az acr login --name $ACR_NAME

echo "Login successful."

for IMAGE in "$@"
do
  echo "Pulling Docker image $IMAGE, from $ACR_NAME"
  docker pull $ACR_NAME.azurecr.io/$IMAGE:$IMAGE_TAG
done

echo "Docker image pulled successfully."