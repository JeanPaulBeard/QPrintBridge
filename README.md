# QPrintBridge 🖨️

Un microservicio local diseñado para solucionar el problema de la impresión térmica directa y silenciosa desde aplicaciones web (SaaS/POS). 

QPrintBridge actúa como un puente (API REST local) que recibe comandos en crudo (ESC/POS) desde el navegador a través de peticiones HTTP estándar y los inyecta directamente en la cola de impresión de Windows (Spooler), evadiendo los cuadros de diálogo del navegador y permitiendo una automatización total.

## ✨ Características

- 🚀 **Impresión Silenciosa:** Sin ventanas emergentes ni confirmaciones del navegador.
- 🔌 **API REST Local:** Expone endpoints HTTP ligeros en el puerto `19100`.
- 🛠️ **CORS Habilitado:** Permite peticiones seguras (`fetch`) desde tu frontend web (HTTPS) hacia `localhost`.
- 📋 **Listado de Hardware:** Endpoint integrado para descubrir las impresoras instaladas en el sistema operativo.
- ⚙️ **Servicio de Windows:** Diseñado como un *Worker Service* de .NET para ejecutarse en segundo plano desde el arranque, sin requerir sesión de usuario.
- 📝 **Log de Errores:** Registro detallado de operaciones y fallos en archivos de texto locales para fácil depuración.

## 📐 Arquitectura

El flujo de trabajo es el siguiente:

1. El Frontend (ej. Vue.js, React, JS Vanilla) genera el ticket en formato de bytes (ESC/POS).
2. Los bytes se codifican en **Base64** para su transporte seguro.
3. El Frontend hace un `POST` a `http://localhost:19100/imprimir`.
4. QPrintBridge recibe el JSON, decodifica el Base64 y utiliza `winspool.drv` (API Nativa de Windows) para enviar el documento en formato `RAW` a la impresora.

## 🚀 Instalación y Despliegue

### Requisitos Previos
* Windows 10 / 11 / Server.
* **No requiere la instalación de .NET Runtime** en la máquina de destino (el ejecutable generado es completamente auto-contenido).
* La impresora térmica debe estar instalada en el Panel de Control de Windows (recomendable usar el controlador *"Generic / Text Only"* si es una impresora genérica ESC/POS).

### Compilación e Instalación Automática

Para compilar e instalar el servicio en Windows:

1. Asegúrate de tener instalado el [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Clona el repositorio y abre una terminal en la carpeta del proyecto.
3. Ejecuta el comando de publicación para generar el ejecutable en un único archivo (`Single-File`):

```bat
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

4. Navega a la carpeta generada: `bin\Release\net8.0-windows\win-x64\publish`. 
Allí encontrarás un único archivo maestro **`QPrintBridge.exe`** (ejecutable auto-contenido con inyección de metadatos), junto a la configuración básica y tu `gestor_servicio.bat`.
5. Ejecuta **como Administrador** el archivo `gestor_servicio.bat` (el cual se copiará automáticamente al compilar).
6. Usa el menú interactivo para instalar (Opción 1) e iniciar (Opción 2) el servicio de Windows `QPrintBridge Service`.

## 📖 Referencia de la API

Por defecto, el servicio escucha en `http://localhost:19100`.

### 1. Listar Impresoras
Devuelve un arreglo con los nombres exactos de las impresoras instaladas en Windows.

* **Endpoint:** `GET /printers`
* **Respuesta Exitosa (200 OK):**

```json
{
  "status": "success",
  "printers": [
    "Impresora_Recepcion",
    "Microsoft Print to PDF",
    "Fax"
  ]
}
```

### 2. Imprimir Ticket
Recibe el payload en Base64 y lo envía a la cola de impresión de Windows.

* **Endpoint:** `POST /imprimir`
* **Headers:** `Content-Type: application/json`
* **Body:**

```json
{
  "impresora": "Impresora_Recepcion",
  "payload": "G0BFWy0... (Cadena Base64 de tus bytes ESC/POS)"
}
```

* **Respuesta Exitosa (200 OK):**

```json
{
  "status": "success",
  "message": "Impreso",
  "printer": "Impresora_Recepcion"
}
```

* **Respuesta de Error Frecuente (500 Error):**

```json
{
  "status": "error",
  "message": "Fallo al enviar a la cola de impresión."
}
```

## 💻 Ejemplo de Uso desde el Frontend (JavaScript)

```javascript
// 1. Generar tus bytes ESC/POS (ej. usando la librería esc-pos-encoder o manualmente)
const ticketBytes = new Uint8Array([0x1B, 0x40, 0x48, 0x6F, 0x6C, 0x61, 0x0A, 0x1D, 0x56, 0x41, 0x10]);

// 2. Convertir a Base64
const base64Payload = btoa(String.fromCharCode.apply(null, ticketBytes));

// 3. Enviar a QPrintBridge
fetch('http://localhost:19100/imprimir', {
    method: 'POST',
    headers: {
        'Content-Type': 'application/json'
    },
    body: JSON.stringify({
        impresora: "Impresora_Recepcion",
        payload: base64Payload
    })
})
.then(response => response.json())
.then(data => console.log(data))
.catch(error => console.error("Error al conectar con QPrintBridge:", error));
```

## 👨‍💻 Autor

**Jean Paul Beard**
* Web: [jeanpaul.pro](https://jeanpaul.pro)

## 📄 Licencia

Este proyecto está bajo la Licencia MIT. Consulta el archivo `LICENSE` para más detalles.