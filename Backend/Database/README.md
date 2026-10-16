# Database and persistence

- `AppDbContext` is in `../DataAccessLayer/AppDbContext.cs` and defines the EF Core schema and relationships.
- Entity classes are in `../Models/Entities.cs`.
- Donation categories are seeded by the EF model configuration in `AppDbContext`.
- The current development configuration uses SQLite. Unless `ConnectionStrings:Default` is configured, the database file is created at `Backend/storage/peniel.sqlite` at runtime.
- The SQLite database and Data Protection key ring are runtime data and must not be committed. Back up both together because the key ring is required to decrypt protected donor and donation fields.
- `EnsureCreated` is currently used for initial schema creation. If the schema changes after deployment, create and review EF Core migrations before applying changes to an existing database. Do not mix `EnsureCreated` with migrations on the same database.

Connection strings and bootstrap administrator credentials belong in .NET user-secrets for local development or environment/secret settings for deployment, never in committed source files.
