---
description: Pasos para agregar una nueva vista y su respectivo ViewModel (MVVM).
---

# Agregar un nuevo Feature o Funcionalidad

1.  **Crear el ViewModel**: 
    - En el directorio `ViewModels/`, crea un archivo `MiNuevaFuncionalidadViewModel.cs`.
    - Declara las dependencias si utilizará _Service_ e inyéctalas en el constructor.
    - Asegúrate que la variable privada que inicializas en el constructor no levante warning de nulo (Ej. `[ObservableProperty] private object? _myVar;`).

2.  **Crear la Vista (View)**:
    - En el directorio `Views/`, crea un archivo `MiNuevaFuncionalidadView.axaml` (y su respectivo _code-behind_ `.cs`).
    - Agrega el namespace del VM predeterminado: `xmlns:vm="clr-namespace:KontrolSage.ViewModels"`.
    - Establece `x:DataType="vm:MiNuevaFuncionalidadViewModel"` en el XML.

3.  **Registro y Navegación**:
    - Registra el View y/o ViewModel en Inyección de Dependencias, típicamente en `App.axaml.cs` (`IServiceCollection`), para instanciar su uso.

4.  **Actualizar el ViewLocator** (si lo utilizas):
    - Verifica que el `ViewLocator.cs` (si está configurado por convenciones de nombre en Avalonia) enlace tu nuevo `[Nombre]ViewModel` con su Vista `[Nombre]View` correspondiente correctamente instanciado.
