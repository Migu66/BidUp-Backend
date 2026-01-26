# 🎯 BidUp - Backend API

**Plataforma de subastas en tiempo real con .NET 9.0**

---

## 📋 Descripción

BidUp es una API backend para un sistema de subastas en tiempo real. Permite a los usuarios crear subastas, realizar pujas y recibir actualizaciones instantáneas mediante WebSocket.

---

## ✨ Características

- 🔐 Autenticación con JWT
- ⚡ Subastas en tiempo real con SignalR
- 📱 API RESTful bien documentada
- 🔄 Gestión de usuarios, subastas y pujas
- 📊 Swagger/OpenAPI integrado

---

## 🛠 Requisitos

- .NET 9.0 SDK
- Visual Studio 2022 o Visual Studio Code
- SQL Server (opcional)

---

## 🚀 Inicio Rápido

```bash
# Clonar repositorio
git clone <repo-url>
cd "Backend - BidUp"

# Restaurar dependencias
dotnet restore

# Ejecutar
dotnet run --project BidUp.Api
```

La API estará disponible en `https://localhost:5001`

---

## 📂 Estructura

```
BidUp.Api/
├── Application/      # Servicios y DTOs
├── Controllers/      # Endpoints API
├── Domain/          # Entidades y lógica de negocio
├── Hubs/            # SignalR para tiempo real
└── Configuration/   # Configuración
```

---

## 📡 Configuración

Edita `appsettings.json` con tu conexión a base de datos y claves JWT.

---
