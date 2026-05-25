# Recam Real Estate Media Delivery Platform

Recam is a backend platform for managing real-estate media delivery workflows between photography companies and real-estate agents.

Photography companies can create property listing cases, upload media assets, assign agents, and manage delivery status. Agents can access assigned listings, review property information, and work with listing media for final property presentation workflows.

This project was built as an ASP.NET Core Web API backend with role-based authentication, layered architecture, SQL Server persistence, and Azure Blob Storage integration.

## Project Highlights

- Built a role-based backend workflow for photography-company Admin users and real-estate Agent users.
- Implemented JWT authentication with ASP.NET Core Identity and policy-based authorization.
- Designed listing case management with create, update, query, soft delete, status transition, and role-aware access control.
- Integrated Azure Blob Storage for uploading and deleting listing media assets.
- Modeled real-estate listing data, media assets, company-agent relationships, and Identity-based user profiles with EF Core.
- Applied a Controller-Service-Repository architecture to separate API, business logic, and persistence concerns.
- Added request validation, object mapping, shared API response formatting, and unit tests for service-layer behavior.

## Core Features

### Authentication and Authorization

- User registration and login
- JWT-based authentication
- ASP.NET Core Identity integration
- Role seeding for `Admin`, `Agent`, and `User`
- Policy-based access control for protected endpoints

### Listing Case Management

- Create, update, retrieve, and soft-delete property listing cases
- Paginated listing case queries
- Listing case detail retrieval
- Listing status transitions:
  - `Created`
  - `Pending`
  - `Delivered`
- Role-aware listing access for Admin and Agent workflows

### Media Management

- Upload multiple media files for a listing case
- Store uploaded assets in Azure Blob Storage
- Delete media assets
- Retrieve listing media grouped by media type
- Support media-driven property presentation workflows

### User and Agent Workflow

- Create agent accounts
- Assign agents to photography companies
- Retrieve agents under the current company
- Get current user profile information
- Update authenticated user password

## Tech Stack

| Area | Technology |
| --- | --- |
| Backend | ASP.NET Core Web API, .NET 10 |
| Authentication | ASP.NET Core Identity, JWT Bearer |
| Authorization | Role-based and policy-based authorization |
| Database | SQL Server |
| ORM | Entity Framework Core |
| Cloud Storage | Azure Blob Storage |
| Validation | FluentValidation |
| Mapping | AutoMapper |
| Testing | xUnit, Moq, FluentAssertions |
| Architecture | Controller-Service-Repository pattern |

## Architecture

The solution is organized as a layered .NET backend:

```text
Remp.API/          API controllers, authentication, authorization, middleware, DI setup
Remp.Service/      Business logic, DTOs, validators, mapping profiles
Remp.Repository/   Repository interfaces and data access implementations
Remp.DataAccess/   EF Core DbContext, migrations, database configuration
Remp.Models/       Domain entities, Identity entities, enums
Remp.Common/       Shared response models and common utilities
Remp.Tests/        Unit tests
```

This structure keeps the HTTP layer thin, moves business rules into services, and isolates persistence behind repository abstractions.

## Domain Model

The backend uses ASP.NET Core Identity for authentication and extends it with business-specific profile entities.

Main entities:

- `User`: application user based on Identity
- `Role`: Identity role used for authorization
- `PhotographyCompany`: photography-company profile
- `Agent`: real-estate agent profile
- `ListingCase`: property listing case with address, property details, pricing, and delivery status
- `CaseContact`: contact information associated with a listing case
- `MediaAsset`: uploaded media linked to a listing case and uploader

Key relationships:

- `Agent` and `PhotographyCompany` are user profile entities linked to Identity users.
- `Agent` and `PhotographyCompany` have a many-to-many relationship.
- `ListingCase` has many `CaseContact` records.
- `ListingCase` has many `MediaAsset` records.
- `MediaAsset` tracks the user who uploaded it.

## Engineering Decisions

- Used ASP.NET Core Identity instead of custom authentication to rely on a proven user and role management foundation.
- Used JWT Bearer authentication to support stateless API access.
- Applied policy-based authorization so controller endpoints can express role requirements clearly.
- Used EF Core migrations to keep SQL Server schema changes versioned with the codebase.
- Used Azure Blob Storage for media files instead of storing binary data in SQL Server.
- Introduced DTOs, AutoMapper, and FluentValidation to keep API contracts separate from persistence entities.
- Used a shared `ApiResponse<T>` envelope to keep response shape consistent across endpoints.
- Added soft delete support for listing cases and media assets to preserve historical records.

## API Surface

The backend currently exposes endpoints for:

- `Auth`: registration, login, and user listing
- `User`: current user profile, password update, agent creation, and company-agent assignment
- `ListingCase`: listing creation, update, query, detail retrieval, deletion, status update, and listing media retrieval
- `MediaAsset`: media upload and deletion

Responses follow a shared envelope:

```json
{
  "success": true,
  "message": "Operation completed successfully.",
  "errors": [],
  "data": {}
}
```

## Running Locally

Prerequisites:

- .NET SDK 10
- SQL Server or Azure SQL-compatible database
- Azure Blob Storage account/container

Restore dependencies:

```bash
dotnet restore Remp.slnx
```

Configure these settings in `Remp.API/appsettings.json`, user secrets, or environment variables:

```json
{
  "Jwt": {
    "Key": "replace-with-a-long-secret-key",
    "Issuer": "RempServer",
    "Audience": "RempClient"
  },
  "ConnectionStrings": {
    "RempDb": "replace-with-sql-server-connection-string"
  },
  "AzureBlobStorage": {
    "ConnectionString": "replace-with-azure-blob-connection-string",
    "ContainerName": "replace-with-container-name"
  }
}
```

Apply database migrations:

```bash
dotnet ef database update \
  --project Remp.DataAccess/Remp.DataAccess.csproj \
  --startup-project Remp.API/Remp.API.csproj
```

Run the API:

```bash
dotnet run --project Remp.API/Remp.API.csproj
```

Run tests:

```bash
dotnet test Remp.Tests/Remp.Tests.csproj
```

## Planned Extensions

- Agent-driven display media selection
- Final property preview page generation
- Shareable property page links
- Download-all media package generation
- Multi-quality image downloads
- Activity/history tracking with NoSQL storage
- Swagger/OpenAPI documentation
- Docker and CI/CD deployment automation
