## ☁️ Despliegue en Producción & CI/CD

### Despliegue Local en Producción con Docker Compose

Para empaquetar y levantar la versión ligera de producción compilada:

```bash
cd docker
docker compose --profile prod up app-prod --build -d
```

### CI/CD en Azure mediante GitHub Actions

El repositorio está configurado para realizar compilaciones e integraciones continuas. Al hacer un push a la rama `main` o `develop`, GitHub Actions compila la imagen Docker y la despliega automáticamente en Azure Container Registry (ACR) y Azure Container Apps:

Agrega las siguientes credenciales en los Secrets de GitHub (Settings > Secrets and variables > Actions):

- **AZURE_CREDENTIALS**: JSON del Service Principal de Azure.
- **REGISTRY_LOGIN_SERVER**: tusitio.azurecr.io
- **REGISTRY_USERNAME**: Usuario de ACR.
- **REGISTRY_PASSWORD**: Clave de ACR.

El flujo `.github/workflows/deploy.yml` ejecutará la compilación multi-stage y el push automáticamente a tu infraestructura en la nube.