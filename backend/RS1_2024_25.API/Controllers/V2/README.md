# 808 Music API V2

The existing legacy API remains available for backward compatibility under its current routes, such as `/api/tracks`, `/api/artists`, and `/api/products`.

V2 is the new Clean Architecture surface. New and refactored modules are exposed under `/api/v2/...`, and controllers should stay thin: extract route values/current user, map requests into Application commands or queries, call Application handlers, and return HTTP responses.

Migration should happen module by module. Simple legacy CRUD endpoints can remain in the current endpoint structure until they are intentionally refactored.

Both API versions use the same database initially; Infrastructure can swap storage, AI, and processing adapters behind Application interfaces without changing controller contracts.
