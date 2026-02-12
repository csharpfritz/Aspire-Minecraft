### Cylinder & Azure resource detection precedence

**By:** Rocket
**Date:** 2026-02-12

**What:** `GetStructureType()` now checks `IsDatabaseResource()` before `IsAzureResource()`. Resources that are both database AND Azure (e.g., `cosmosdb`, `azure.sql`) get the Cylinder structure shape, with the azure banner added as a post-build overlay via `PlaceAzureBannerAsync()`.

**Why:** The database cylinder icon is a stronger visual signal for data stores than the Azure color palette. Azure identity is communicated additively via the rooftop banner, which works on any structure type. This means a CosmosDB resource looks like a database cylinder with an azure flag on top — both identities are visible.

**Impact:** `IsDatabaseResource` and `IsAzureResource` are both `internal static` methods on `StructureBuilder`, available for other services (e.g., Nebula's tests). The detection lists are intentionally broad — `Contains()` matching catches compound types like `azure.postgres` or `sql-server-2022`.
