### fs-lab-assignment-sportsstore-71757 

## Student Name: Raheem Folarin

## Student ID: 71757

# SportsStore Modernization (NET 10 + Serilog + Stripe + CI)

This repository contains the upgraded SportsStore application with structured logging, Stripe test payments, and CI validation.

## Requirements

- .NET SDK `10.0.103` or later
- Stripe test API key (`sk_test_...`)

## Upgrade Notes

- Target framework updated to `net10.0`.
- NuGet packages updated to current net10-compatible versions.
- SQLite is used for local data storage.

## Logging (Serilog)

Serilog is configured via `appsettings.json` with:

- Console sink
- Rolling file sink at `logs/sportsstore-.log`
- Structured properties for checkout and order creation

Key files:

- `SportsStore/Program.cs`
- `SportsStore/appsettings.json`

## Stripe Test Payments

Stripe Checkout is integrated with test keys only.

Setup the secret key using user-secrets:

```powershell
dotnet user-secrets --project "SportsStore/SportsStore.csproj" set "Stripe:SecretKey" "sk_test_..."
```

Verify:

```powershell
dotnet user-secrets --project "SportsStore/SportsStore.csproj" list
```

Test card:

- `4242 4242 4242 4242`
- Any future expiry date, any CVC, any ZIP

Payment flow:

1. Submit checkout form.
2. Redirect to Stripe Checkout.
3. On success, order is confirmed and marked `Paid`.
4. On cancellation or failure, order is marked accordingly.

Key files:

- `SportsStore/Controllers/OrderController.cs`
- `SportsStore/Models/Payments/StripePaymentService.cs`

## Run Locally

From the `SportsSln` directory:

```powershell
dotnet restore "SportsSln.sln"
dotnet build "SportsSln.sln"
dotnet run --project "SportsStore/SportsStore.csproj"
```

Then open the app at the URL printed in the console.

## Tests

```powershell
dotnet test "SportsSln.sln"
```

## CI (GitHub Actions)

Workflow file:

- `.github/workflows/ci.yml`


