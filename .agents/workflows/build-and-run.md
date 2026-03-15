---
description: Cómo compilar y ejecutar la aplicación de Avalonia localmente de forma segura.
---

# Pasos para compilar y ejecutar KontrolSage

Sigue estos pasos para probar la aplicación de forma local. Asegúrate de tener MongoDB ejecutándose según los requerimientos del sistema o la conexión en `DatabaseConfig.cs`.

1.  **Restaurar dependencias**
    ```bash
    dotnet restore
    ```
    // turbo

2.  **Compilar la solución** para verificar que no haya errores de compilación ni de sintaxis `CS...`.
    ```bash
    dotnet build
    ```
    // turbo

3.  **Ejecutar la aplicación**.
    ```bash
    dotnet run
    ```
    // turbo
