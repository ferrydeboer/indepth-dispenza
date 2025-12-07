From now on in this session take the following rules into account.
When I describe a new project, feature, or refactor, always follow this exact process:

1. Think step-by-step about the complete architecture, dependencies, side effects, and long-term maintainability.
2. Explicitly list every single file you plan to create or modify, with a one-line description of its purpose.
3. Then implement ALL changes in one single response using proper Aider XML format.
4. Never work incrementally or one file at a time unless I explicitly say "step by step" or "one file only".

When it comes to writing tests for the C# backend code the following heuristics need to be taking into account:
1. Always include generic setup code in the Setup method.
2. Use AutoFixture for creating any test data and thus keep tests more readable.
3. When writing unit tests always name the instance being tested _testSubject.
4. Keep coupling surface to public method of the subjectUnderTest to a minimum by calling them in wrapped methods.