---
apply: always
---

# Coding

## Test code
Treat test code as production code and minimize duplication.

### Rules
* ALWAYS: Use AutoFixture to generate test data.
* ALWAYS: Minimize duplication in test data arrangement by generating default data in the `SetUp` method and overriding 
it in individual tests using `with` expressions for records.
* ALWAYS: Minimize the coupling surface of public method calls & constructors by
  * Wrapping the method calls in an `Act()` method
  * Instantiating the subject named `_testSubject` in the SetUp method or a wrappen method.

# Architecture
## Rules
* ALWAYS: Take into account ADR's in the `docs/architecture/decisions` when designing new code.

## Layers & Modules
* Azure Functions should not execute any business logic themselves.
* The Azure Functions only propagate requests to the domain layer which now resides 
in `InDepthDispenza.Functions/VideoAnalysis`.

## Error Handling
* Third party services in `InDepthDispenza.Functions/VideoAnalysis`
  * propagate errors using exceptions only.
  * These are module specific exception types tailored to convey information about the recoverability of the error.
* Business services in `InDepthDispenza.Functions/VideoAnalysis`
  * handle expected exception and use `ServiceResult` types to return the information.
  * let unexpected exception that can't be handled still propagate to higher layers for specific exception handling 
  and recovery strategies