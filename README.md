# StayDesk

Plataforma de reservas de hotel con microservicios en ASP.NET Core 8: control de sobreventa
bajo concurrencia, motor de tarifas por temporada/fin de semana, maquina de estados de la
reserva con politica de cancelacion, y un frontend Razor Pages que consume la API.

## Que resuelve

Un hotel necesita vender habitaciones de distintos tipos (sencilla, doble, suite) sin
sobrevenderlas, cobrar tarifas distintas segun temporada o fin de semana, y llevar el ciclo
de vida completo de cada reserva (pendiente, confirmada, con check-in, con check-out,
cancelada o no-show) aplicando una politica de penalizacion por cancelacion tardia.

StayDesk expone esa logica como un microservicio REST (`StayDesk.Api`) y un sitio web
(`StayDesk.Web`) donde un huesped busca disponibilidad, reserva y administra sus reservas, y
el personal de recepcion (rol `FrontDesk`) confirma, hace check-in/check-out, cancela o marca
no-show.

## Arquitectura

```
┌─────────────────┐        HTTP (IHttpClientFactory)        ┌──────────────────┐
│  StayDesk.Web    │ ───────────────────────────────────────▶│   StayDesk.Api    │
│  Razor Pages     │        JWT Bearer (guardado en          │   ASP.NET Core    │
│  + cookie de      │        cookie de sesion)                │   Web API          │
│  sesion propia    │ ◀─────────────────────────────────────── │                    │
└─────────────────┘            JSON / ProblemDetails          └─────────┬────────┘
                                                                          │ EF Core
                                                                          ▼
                                                                 ┌──────────────────┐
                                                                 │   SQL Server      │
                                                                 │  (SQLite en tests) │
                                                                 └──────────────────┘
```

- **StayDesk.Api**: microservicio Web API con las reglas de negocio (disponibilidad,
  precios, estados de reserva), EF Core + SQL Server, JWT, rate limiting y Swagger.
- **StayDesk.Web**: frontend Razor Pages (HTML/CSS, sin JS pesado) que llama a la API via
  `IHttpClientFactory`. No comparte código ni base de datos con la API: son dos servicios
  desplegables por separado, cada uno con sus propios contratos (DTOs duplicados a propósito
  en `StayDesk.Web/ApiClient`).
- **StayDesk.Api.Tests**: pruebas xUnit del backend contra SQLite en memoria, sin SQL Server.

## Decisiones tecnicas

### Control de sobreventa sin hints T-SQL

Cada tipo de habitacion tiene una fila de inventario por dia (`RoomTypeDailyInventory`,
`RoomTypeId` + `Date` + `RoomsBooked`). Reservar una estancia incrementa esa fila para cada
noche dentro de una misma transaccion con un `UPDATE` atomico:

```sql
UPDATE RoomTypeDailyInventories
SET RoomsBooked = RoomsBooked + 1
WHERE RoomTypeId = @id AND Date = @date AND RoomsBooked < @totalRooms
```

Un `UPDATE` con condicion en el `WHERE` es seguro bajo concurrencia sin necesitar hints como
`UPDLOCK`/`HOLDLOCK`: el motor toma el lock de la fila antes de evaluar el predicado, asi que
dos transacciones concurrentes nunca pueden incrementar mas alla del limite. Se evito
deliberadamente la sintaxis T-SQL de hints porque **no es portable a SQLite**, el motor que
usan las pruebas automatizadas; este patron sí corre igual en ambos motores y se verifica con
una prueba de concurrencia real (`ConcurrencyTests`) que lanza 8 reservas simultaneas contra
una habitacion con cupo para 2 y comprueba que exactamente 2 se crean y las demas reciben
`409 Conflict`. Si la fila del dia aun no existe se inserta bajo la misma transaccion, con
reintento si otra transaccion gano la carrera de creacion (violacion del indice unico
`RoomTypeId + Date`).

### Motor de tarifas

El precio se calcula noche por noche: si existe una tarifa de temporada (`SeasonalRate`)
vigente para esa fecha se usa esa tarifa fija; si no, se usa el precio base del tipo de
habitacion con un recargo porcentual configurable para noches de viernes/sabado.

### Maquina de estados de la reserva

`Pending → Confirmed → CheckedIn → CheckedOut`, con salidas a `Cancelled` (desde `Pending` o
`Confirmed`) y a `NoShow` (desde `Confirmed`, solo despues de que paso la fecha de entrada).
Cada transicion revisa el estado actual y regenera un `ConcurrencyStamp` (Guid) como token de
concurrencia optimista — se eligio un Guid regenerado en código en vez de `rowversion` de SQL
Server porque `rowversion` no es portable a SQLite, usado en las pruebas.

La cancelacion aplica una politica basada en dias de anticipacion: 3 dias o mas antes del
check-in no hay penalizacion, 1-2 dias antes se cobra 50%, el mismo dia del check-in o despues
se cobra el 100% (sin reembolso); ademas libera el inventario reservado.

### Seguridad

- JWT firmado con HMAC-SHA256, emitido por `StayDesk.Api`.
- Contrasenas con `PasswordHasher<T>` (PBKDF2) del propio framework ASP.NET Core.
- Rate limiting particionado por usuario/IP en el endpoint de creacion de reservas (5
  solicitudes por minuto) para frenar abuso sin penalizar a otros usuarios.
- CORS configurado por lista blanca de origenes (pensado para un eventual cliente JS que
  llame a la API directamente; el frontend actual la llama desde el servidor, por lo que no
  depende de CORS para funcionar).
- El frontend guarda el JWT como claim dentro de una cookie de autenticacion propia
  (`CookieAuthenticationDefaults`), no en `localStorage`.

### Pruebas sin servicios externos

`StayDesk.Api.Tests` reemplaza SQL Server por SQLite en memoria con cache compartido
(`Cache=Shared`, `DataSource=...;Mode=Memory`): cada `DbContext` abre su propia conexion (como
en produccion) pero todas ven la misma base, lo que permite probar concurrencia real entre
requests simultaneas via `WebApplicationFactory`. `Default Timeout` configura el
`busy_timeout` de SQLite para que los escritores concurrentes esperen en vez de fallar de
inmediato con "database is locked".

## Estructura

```
StayDesk.Api/          Microservicio backend (Web API)
StayDesk.Web/           Frontend Razor Pages
StayDesk.Api.Tests/     Pruebas xUnit del backend
docker-compose.yml      Orquesta API + Web + SQL Server
.github/workflows/      CI (build + test)
```

## Como correrlo

### Con Docker (recomendado)

```bash
docker compose up --build
```

- API: http://localhost:5080/swagger
- Web: http://localhost:5100

La contrasena de SQL Server usada en `docker-compose.yml` y `appsettings.json`
(`StayDesk#2026_Dev`) es solo para desarrollo local; en un despliegue real se reemplaza por un
secreto inyectado via variables de entorno o un gestor de secretos.

### En local sin Docker

Requiere .NET 8 SDK y una instancia de SQL Server accesible en `localhost:1433`.

```bash
# Backend
dotnet run --project StayDesk.Api

# Frontend (en otra terminal)
dotnet run --project StayDesk.Web
```

Las migraciones de EF Core se aplican automaticamente al iniciar `StayDesk.Api` (excepto en
el entorno `Testing`, usado por las pruebas).

Usuario de recepcion sembrado por defecto: `frontdesk@staydesk.local` / `FrontDesk#2026`.

## Como probarlo

```bash
dotnet test StayDesk.Api.Tests/StayDesk.Api.Tests.csproj
```

No requiere SQL Server ni Docker: usa SQLite en memoria. Cubre precios (temporada/fin de
semana), disponibilidad y paginacion, autenticacion y autorizacion por rol, la maquina de
estados completa de la reserva, la politica de cancelacion por dias de anticipacion, y una
prueba de concurrencia que lanza reservas simultaneas para comprobar que nunca se sobrevende
una habitacion.

También se puede explorar la API manualmente desde Swagger UI (`/swagger` en modo
Development) una vez iniciado `StayDesk.Api`.
