# Database

MySQL 8 accessed with **Dapper**. There is no EF Core in this solution and none should be
added — no `DbContext`, no scaffolding, no EF migrations. The schema is owned by the SQL
scripts in `admin/src/OnlinePalengke.Migrations/Scripts/` and applied by a
[DbUp](https://dbup.readthedocs.io/) console runner.

---

## 1. Bring the database up

```bash
docker compose up -d
docker compose ps          # wait until mysql shows (healthy)
```

`docker-compose.yml` at the repo root starts two containers:

| Service   | Image        | Host port | Notes                                     |
| --------- | ------------ | --------- | ----------------------------------------- |
| `mysql`   | `mysql:8.4`  | `3306`    | utf8mb4, server time zone UTC             |
| `adminer` | `adminer:5`  | `8080`    | Browse the schema at <http://localhost:8080> |

Local defaults — **throwaway values, never reuse them anywhere deployed**:

| Variable              | Default          |
| --------------------- | ---------------- |
| `MYSQL_DATABASE`      | `onlinepalengke` |
| `MYSQL_USER`          | `palengke`       |
| `MYSQL_PASSWORD`      | `palengke`       |
| `MYSQL_ROOT_PASSWORD` | `localrootpw`    |
| `MYSQL_HOST_PORT`     | `3306`           |
| `ADMINER_HOST_PORT`   | `8080`           |

Override by exporting them or by dropping a `.env` file next to `docker-compose.yml`
(`.env` is gitignored).

If you already have MySQL installed on the host it will be holding port 3306 and
`docker compose up -d` will fail with `ports are not available`. Either stop the local
service, or move the container's host port and point the runner at the new one:

```bash
echo "MYSQL_HOST_PORT=3307" >> .env
docker compose up -d
```

Remember to change `Port=` in whichever connection string you use to match.

Data lives in the named volume `palengke-mysql-data`. `docker compose down` keeps it;
`docker compose down -v` wipes it, which is the fastest way to rebuild from script 001.

There is **no MinIO or other S3 emulator** in the compose file, on purpose. Object storage
is real AWS S3 in every environment including local development, so that presigned URLs,
bucket policies and lifecycle rules behave the same everywhere. Do not add one.

---

## 2. Run the migrations

```bash
dotnet run --project admin/src/OnlinePalengke.Migrations
```

The runner applies every script in `Scripts/` that is not already recorded in the
`schema_versions` journal table, in ordinal filename order. It is idempotent: run it as
often as you like, a second run reports `No pending scripts.` and exits 0.

### Connection string resolution

Checked in this order; the first one found wins.

**1. `--connection` argument** (highest precedence)

```bash
dotnet run --project admin/src/OnlinePalengke.Migrations -- \
  --connection "Server=127.0.0.1;Port=3306;Database=onlinepalengke;User ID=palengke;Password=palengke;AllowPublicKeyRetrieval=true;SslMode=None;"
```

**2. `PALENGKE_DB_CONNECTION` environment variable** — this is the form CI uses, so the
connection string never appears in a command line or a build log.

```bash
# bash
export PALENGKE_DB_CONNECTION="Server=127.0.0.1;Port=3306;Database=onlinepalengke;User ID=palengke;Password=palengke;AllowPublicKeyRetrieval=true;SslMode=None;"
dotnet run --project admin/src/OnlinePalengke.Migrations
```

```powershell
# PowerShell
$env:PALENGKE_DB_CONNECTION = "Server=127.0.0.1;Port=3306;Database=onlinepalengke;User ID=palengke;Password=palengke;AllowPublicKeyRetrieval=true;SslMode=None;"
dotnet run --project admin/src/OnlinePalengke.Migrations
```

**3. `appsettings.json`** next to the executable, under `ConnectionStrings:Palengke`.
It ships pointing at the local compose defaults above so a fresh clone works with no
setup. Never point it at a deployed database and never put real credentials in it.

If none of the three yields a connection string the runner prints all three options and
exits with code 2.

### Options

| Option                | Effect                                                                    |
| --------------------- | ------------------------------------------------------------------------- |
| `--connection <value>` | Connection string to migrate.                                             |
| `--ensure-database`    | `CREATE DATABASE` if the target does not exist. Needs a privileged user — the `palengke` user only has rights on `onlinepalengke`, so use root, or skip it (compose creates the database for you). |
| `--dry-run`            | List the pending scripts and apply nothing. Requires the database to exist; `--ensure-database` is ignored in this mode. |
| `-h`, `--help`         | Usage.                                                                    |

`--connection=value` works as well as `--connection value`.

### Exit codes

| Code | Meaning                                            |
| ---- | -------------------------------------------------- |
| `0`  | Success, including "nothing to do"                 |
| `1`  | Connection failure or a script failed              |
| `2`  | Bad arguments, or no connection string was found   |

### Checking what would happen

```bash
dotnet run --project admin/src/OnlinePalengke.Migrations -- --dry-run
```

---

## 3. Adding a migration script

1. Create the file in `admin/src/OnlinePalengke.Migrations/Scripts/`.
2. Name it `NNN_snake_case_description.sql` — three-digit zero-padded number, then a short
   description of what the script does. Take the next free number.
   Examples: `001_identity_and_media.sql`, `002_markets_and_categories.sql`.
   The number is what orders execution, so **never renumber or rename a script that has
   already been applied anywhere** — DbUp keys the journal on the resource name and would
   re-run it.
3. Nothing to register. `Scripts\*.sql` is a wildcard `EmbeddedResource` in the `.csproj`,
   so the new file is picked up on the next build.
4. Open the script with a short comment block saying what it covers and why.
5. Run it against a local database, then run the tool a second time to confirm the journal
   caught it.

### Scripts are append-only

An applied script is history. To change something already shipped, add a new script with
an `ALTER`. Editing an applied script only changes what a fresh database gets, silently
diverging it from every existing one.

MySQL DDL is **not transactional** — every `CREATE TABLE` implicitly commits — so the
runner deliberately does not wrap the run in a transaction, because that would only give a
false sense of atomicity. Write each script so it is safe to fix and re-run after a
partial failure.

---

## 4. Schema conventions

These are settled. Follow them rather than inventing an alternative.

**Storage**
- `ENGINE = InnoDB`, `DEFAULT CHARSET = utf8mb4`, `COLLATE = utf8mb4_0900_ai_ci`, stated
  explicitly on every `CREATE TABLE`.

**Naming**
- Columns are `snake_case`. A Dapper type map handles `snake_case` → `PascalCase` on the
  C# side, so there is nothing to configure per query — but it only works if the column
  names actually are snake_case.
- Table names are plural snake_case: `users`, `customer_addresses`.
- Constraints and indexes are named, never left to MySQL:
  `pk` is implicit, `uq_<table>_<cols>`, `idx_<table>_<cols>`, `fk_<table>_<ref>`,
  `chk_<table>_<what>`.

**Keys**
- Primary keys are `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT`.
- Foreign keys are always declared explicitly, with a deliberate `ON DELETE`:
  `CASCADE` for rows that are meaningless without their parent (a user's refresh tokens),
  `RESTRICT` for records that must outlive the parent (`media_assets.uploaded_by` — KYC
  documents and delivery proofs are evidence).
- If a table needs a foreign key to a table that does not exist yet, leave the column
  nullable with a comment and add the FK in the later script that creates the target.
  `partners.market_id` and `partners.category_id` are the current examples.

**Types**
- **Money is `DECIMAL(12,2)`.** Never `FLOAT`, never `DOUBLE`, anywhere, for anything
  denominated in pesos. Maps to `decimal` in C#.
- Coordinates are `DECIMAL(10,7)` — about 1 cm of resolution and exact, which matters
  because delivery fees are derived from distance.
- **All timestamps are `DATETIME(6)` holding UTC.** Never MySQL `TIMESTAMP`: it converts
  to and from the session time zone and will silently corrupt values. Never
  `ON UPDATE CURRENT_TIMESTAMP` and generally no `DEFAULT CURRENT_TIMESTAMP` — the
  application writes `created_at` / `updated_at` explicitly so the stored value is the same
  instant the application already has in hand. Convert to Asia/Manila only for display.
- Enum-ish columns are `VARCHAR(n)` plus a named `CHECK ... IN (...)` constraint, not
  MySQL `ENUM`. Adding a value is then a constraint swap instead of a table rebuild, and
  the allowed set is visible in `information_schema`.

---

## 5. Inspecting the result

Adminer at <http://localhost:8080> (System `MySQL`, Server `mysql`, user `palengke`), or
from the shell:

```bash
docker compose exec mysql mysql -upalengke -ppalengke onlinepalengke -e "SHOW TABLES;"

# Which scripts have been applied. The journal table is created and owned by
# DbUp, so its own columns are not snake_case - ours are.
docker compose exec mysql mysql -upalengke -ppalengke onlinepalengke \
  -e "SELECT schemaversionid, scriptname, applied FROM schema_versions ORDER BY schemaversionid;"

# CHECK constraints on a table
docker compose exec mysql mysql -upalengke -ppalengke onlinepalengke \
  -e "SELECT constraint_name, check_clause FROM information_schema.check_constraints WHERE constraint_schema = 'onlinepalengke';"

# Foreign keys
docker compose exec mysql mysql -upalengke -ppalengke onlinepalengke \
  -e "SELECT constraint_name, table_name, referenced_table_name, delete_rule FROM information_schema.referential_constraints WHERE constraint_schema = 'onlinepalengke';"
```

To start over from an empty database:

```bash
docker compose down -v
docker compose up -d
dotnet run --project admin/src/OnlinePalengke.Migrations
```
