# Pokédex API

A REST API that returns Pokémon information, with optional fun translations of descriptions.

---

## Requirements

### Install .NET 9 SDK

**macOS (Homebrew):**
```bash
brew install --cask dotnet-sdk
```

**macOS / Linux (official script):**
```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --channel 9.0
```
After running the script, add `~/.dotnet` to your PATH:
```bash
export PATH=$PATH:~/.dotnet
```

**Windows:**
Download and run the installer from [https://dotnet.microsoft.com/download/dotnet/9.0](https://dotnet.microsoft.com/download/dotnet/9.0)

**Verify the installation:**
```bash
dotnet --version
# expected output: 9.x.xxx
```

---

## How to run

### Option 1 — Terminal

```bash
# 1. Navigate to the project root
cd /path/to/Poke

# 2. Restore dependencies
dotnet restore

# 3. Run the API
dotnet run --project src/Pokedex.Api
```

The API will start on `http://localhost:5000` and open the Scalar UI in the browser automatically.

---

### Option 2 — Visual Studio (Windows/macOS)

1. Open `Pokedex.sln` (double-click or **File → Open → Project/Solution**)
2. In the toolbar, select **`Pokedex.Api`** as the startup project and the **`http`** launch profile
3. Press **F5** (debug) or **Ctrl+F5** (run without debug)

The browser will open automatically on `http://localhost:5000/scalar/v1`.

---

### Option 4 — Docker

Requires [Docker](https://www.docker.com/products/docker-desktop) installed and running.

```bash
# Build the image
docker build -t pokedex-api .

# Run the container (maps host port 5000 to container port 8080)
docker run -p 5000:8080 pokedex-api
```

The API will be available at `http://localhost:5000`.

---

### Option 3 — Visual Studio Code

1. Install the [C# Dev Kit extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)
2. Open the `Poke` folder (**File → Open Folder**)
3. Open the Command Palette (`Cmd+Shift+P` / `Ctrl+Shift+P`) and run **`.NET: Generate Assets for Build and Debug`** (only needed the first time)
4. Press **F5** to run

The browser will open automatically on `http://localhost:5000/scalar/v1`.

---

## Endpoints

### 1. Basic Pokémon information

```
GET /pokemon/{name}
```

Returns standard Pokémon information from PokéAPI.

**Example:**
```bash
curl http://localhost:5000/pokemon/mewtwo
```

**Response:**
```json
{
  "name": "mewtwo",
  "description": "It was created by a scientist after years of horrific gene splicing and DNA engineering experiments.",
  "habitat": "rare",
  "isLegendary": true
}
```

---

### 2. Translated Pokémon description

```
GET /pokemon/translated/{name}
```

Returns Pokémon information with a fun translated description:
- **Cave habitat or legendary** → Yoda translation
- **All others** → Shakespeare translation
- **Translation unavailable** → falls back to standard description

**Example:**
```bash
curl http://localhost:5000/pokemon/translated/mewtwo
```

**Response:**
```json
{
  "name": "mewtwo",
  "description": "Created by a scientist after years of horrific gene splicing and dna engineering experiments, it was.",
  "habitat": "rare",
  "isLegendary": true
}
```

---

### Error responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | Pokémon name not recognised by PokéAPI |
| `500 Internal Server Error` | Unexpected error |

Errors are returned as [RFC 7807 ProblemDetails](https://tools.ietf.org/html/rfc7807):

```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Pokémon 'pippo' not found."
}
```

---

## How to run the tests

```bash
dotnet test
```

The test suite includes:
- **Unit tests** (`Pokedex.Tests/Unit/`) — test `PokemonService` in isolation with mocked HTTP clients
- **Integration tests** (`Pokedex.Tests/Integration/`) — test the full request pipeline with an in-process test server

---

## What I would do differently in production

### Resilience
- Add **Polly** retry policies with exponential backoff on both `IPokeApiClient` and `ITranslationClient`.
- Add a **circuit breaker** on `ITranslationClient` — the FunTranslations API has a strict rate limit of 5 requests/minute. A circuit breaker would stop hammering the API when it is rate-limiting, rather than waiting for each request to fail.

### Caching
- Replace `IMemoryCache` with a **distributed cache** (e.g. Redis via `IDistributedCache`). The current in-memory cache is per-instance: in a horizontally scaled deployment (multiple pods, load balancer), each instance maintains its own cache, which defeats the purpose and may still hit rate limits on FunTranslations.

### Observability
- Add structured logging (e.g. Serilog) with correlation IDs per request.
- Expose a `/health` endpoint (via `Microsoft.AspNetCore.Diagnostics.HealthChecks`) for liveness and readiness probes.
- Add metrics (e.g. OpenTelemetry) to track cache hit rates and external API latency.

### Security
- Add rate limiting on the API itself (via `Microsoft.AspNetCore.RateLimiting`) to protect against abuse.

### .NET version
- For a long-lived production service, target **.NET 8 (LTS)** instead of .NET 9. .NET 8 is supported until November 2026; .NET 9 reaches end of life in May 2026.

Ps: Git History absent, but you can find the workflow in the file DECISIONS.txt - where there's the chat with Claude Code :-)
