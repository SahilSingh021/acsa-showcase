# ACSA - AssaultCube Secure Arena (Research Showcase)

ACSA (AssaultCube Secure Arena) is a secure remote competition
infrastructure built around a modified FPS engine.
The project explores secure match orchestration, controlled client
participation, and backend-enforced integrity validation in multiplayer
environments.

This repository contains the source-only research showcase of the
system.

------------------------------------------------------------------------

## 1. Research Context

Online multiplayer environments are vulnerable to:

-   Client-side tampering
-   Memory modification
-   Unauthorized server connections
-   Illegitimate match participation
-   Result manipulation

ACSA investigates how a controlled competitive environment can be
enforced by combining:

-   Backend-driven server orchestration
-   Role-based authentication
-   Token-based match authorization
-   Client integrity validation
-   Engine-level integration

The objective is not merely cheat detection, but **architectural control
over competitive execution**.

------------------------------------------------------------------------

## 2. System Architecture

The platform consists of three tightly coupled components.

### 2.1 Web Platform (`acsa-web`)

An ASP.NET Core MVC application responsible for identity, authorization,
and match lifecycle control.

**Technologies** - ASP.NET Core MVC
- Microsoft Identity
- Entity Framework Core (Code-First)
- SignalR
- JWT authentication
- Kestrel hosting

**Responsibilities** - User registration and authentication
- Role-based authorization (Admin / Player)
- Lobby creation and controlled match initiation
- Dynamic game server allocation
- Secure token issuance for match participation
- Backend validation of match state transitions

The backend is the authoritative control layer.

------------------------------------------------------------------------

### 2.2 Native Integrity Module (`ac-anti-tamper`)

A C++ integrity enforcement module integrated into the modified game
client.

**Responsibilities** - Runtime integrity checks
- Detection of suspicious memory modifications
- Controlled validation of match participation
- JWT validation prior to server connection
- Secure reporting of events to backend

The module acts as a bridge between the native engine environment and
the backend trust model.

------------------------------------------------------------------------

### 2.3 Modified AssaultCube Engine (`assaultcube-acsa-fork`)

A fork of AssaultCube modified to support secure orchestration.

**Engine-level modifications include** - Controlled match startup flow
- Backend-authorized server connections
- Port allocation validation
- Anti-tamper integration hooks
- Restricted client execution during competitive sessions

Only engine source code is included in this repository.
Runtime assets and compiled distributions are intentionally excluded.

------------------------------------------------------------------------

## 3. Security Model

ACSA enforces security across multiple layers.

### 3.1 Identity and Authorization

-   ASP.NET Identity manages accounts and roles.
-   JWT tokens are issued per match session.
-   Role-based access control governs match control privileges.

### 3.2 Server-Orchestrated Execution

-   Matches cannot be started directly by clients.
-   Game servers are spawned dynamically by backend command.
-   Ports are allocated and tracked centrally.
-   Clients must present valid authorization tokens before joining.

### 3.3 Integrity Enforcement

-   Client-side runtime checks detect tampering attempts.
-   Match participation is gated behind backend-issued credentials.
-   Match results are reported and validated server-side.

This layered approach shifts authority away from the client and toward a
centrally validated trust boundary.

------------------------------------------------------------------------

## 4. Repository Structure

    src/
    ├── acsa-web                # ASP.NET Core web platform
    ├── ac-anti-tamper          # C++ integrity enforcement module
    └── assaultcube-acsa-fork   # Modified AssaultCube engine source

The repository is structured as a monorepo to preserve architectural
context and cross-component traceability.

------------------------------------------------------------------------

## 5. Configuration

Sensitive configuration is intentionally excluded from this public
repository.

The included `appsettings.json` contains placeholder values only.

The following must be supplied securely via:

-   .NET User Secrets (recommended for development)
-   Environment variables
-   Secure production configuration

**Excluded configuration includes** - Database connection strings
- JWT signing keys
- SMTP credentials
- TLS certificate passwords

### Local Development Example

``` bash
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "YOUR_SECRET"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_CONNECTION_STRING"
```

------------------------------------------------------------------------

## 6. Technology Stack

### Backend

-   ASP.NET Core MVC
-   Entity Framework Core
-   Microsoft Identity
-   SignalR
-   JWT authentication

### Native

-   C++
-   MSVC toolchain
-   Engine-level integration hooks

### Database

-   SQL Server

------------------------------------------------------------------------

## 7. Research Focus

This project explores:

-   Secure orchestration of multiplayer systems
-   Authority separation between backend and client
-   Practical integrity enforcement in native environments
-   Hybrid C# / C++ system design
-   Engine-level security integration

It is intended as a systems security engineering showcase.

------------------------------------------------------------------------

## 8. Disclaimer

This repository contains source code only.

Runtime distributions, compiled binaries, and game assets are excluded.

All sensitive credentials have been removed.
Any required configuration must be supplied securely.

------------------------------------------------------------------------

## Author

Sahilpreet Singh

BSc (Hons) Computer Science

Secure Systems and Multiplayer Integrity Engineering
