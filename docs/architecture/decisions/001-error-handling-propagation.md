# Architecture Decision Record: Error Handling and Propagation

## Status
Approved

## Context
The previous architecture overused a generic `ServiceResult<T>` wrapper across all layers (Integration, Domain, and Presentation). This led to:
- **Obfuscated Failures**: Technical exceptions were swallowed and converted to failure results, preventing Azure Functions from detecting failures for retries or monitoring.
- **Log Noise**: Every layer manually logged the same error, creating redundant entries.
- **Boilerplate**: "Railway Oriented Programming" (if-checks on results) obscured the core business logic.

## Decision
We adopt a **Hybrid Error Handling Strategy** that distinguishes between **Technical Exceptions** and **Domain Outcomes**.

### 1. Integration/Infrastructure Layer (Exceptions)
Integration components (Repositories, API Clients, Cloud Services) must **not** use `ServiceResult`. 
- They should throw exceptions for failures.
- **Exception Wrapping**: Raw technical exceptions (e.g., `HttpRequestException`, `CosmosException`) should be wrapped in Domain-Specific Exceptions (e.g., `QueueTransientException`) to provide context and decouple the interface from specific vendors.
- Always include the original exception as `InnerException`.

### 2. Domain/Service Layer (Hybrid)
The Service layer orchestrates logic and decides the flow based on the nature of errors.
- **Total Failures**: For fatal errors where the process cannot continue, let exceptions bubble up.
- **Partial Failures**: Use `try-catch` for specific, expected domain exceptions to allow for "Continue on Error" logic (e.g., skipping a single item in a loop).
- **Domain Outcomes**: Use `ServiceResult` or dedicated response objects only when a "failure" is a valid, expected business outcome that the caller must handle (e.g., `ValidationResult`).

### 3. Presentation/Azure Function Layer (Boundary)
The entry point acts as the final boundary before the outside world or the Azure runtime.
- **Let it Bubble**: For technical or fatal failures that the service layer has already logged or decided not to catch, the Azure Function should generally let the exception bubble up to the runtime.
- **Runtime Benefits**: By letting exceptions reach the Azure runtime unhandled, it can:
    - Automatically trigger **Retry Policies** configured in `host.json`.
    - Accurately track "Failures" in **Application Insights** without custom logging.
- **Input Validation**: The Function still handles immediate boundary concerns like input validation, returning `400 BadRequest` before calling the underlying services.
- **Specific Mapping (Optional)**: If a specific HTTP status code is required for a known domain exception that escaped the service layer, a `try-catch` can be used to map it, but it should be the exception rather than the rule.

## Implementation Rules
1. **Fail-Fast**: Do not catch an exception if you cannot recover from it at that level.
2. **Categorize by Behavior**: Create custom exceptions based on how the caller should react (e.g., `Transient` vs. `Permanent`) rather than just mirroring the technical cause.
3. **Log Once**: Only log an exception if you are handling it (e.g., `LogWarning` before skipping an item) or if it's the final boundary and you aren't letting it bubble to a runtime that logs it for you.

## Consequences
- **Improved Monitoring**: Azure Function failure rates will accurately reflect system health.
- **Cleaner Code**: Business logic becomes the "Happy Path," unburdened by repetitive error-checking.
- **Better Contract**: Interfaces clearly define their failure modes through specific exception types.
