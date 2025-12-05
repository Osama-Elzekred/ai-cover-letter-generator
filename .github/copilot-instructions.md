# Copilot Instructions

## Rules
- Be **proactive**: anticipate issues (duplication, middleware order, consistency)
- Check existing code patterns before suggesting new code
- Apply expert/production-ready solutions by default
- Trace full request flow before making changes
- Never duplicate logic (logging, error handling, validation)

## Stack
- .NET 10, C# 14, Minimal APIs, MediatR, FluentValidation, Refit, Serilog
- Onion Architecture: Domain → Application → Infrastructure → Api
- Result<T> pattern for business errors, exceptions for unexpected errors

## Logging
- Log **once at the boundary** (GlobalExceptionHandler for errors, Serilog for requests)
- Validation = Debug level (expected), Unexpected errors = Error level
- No duplicate logs across layers

## Middleware Order
```csharp
app.UseSerilogRequestLogging(); // FIRST - sees final status
app.UseExceptionHandler();       // Converts exceptions to responses
```

## Error Handling
- FluentValidation → ValidationException → GlobalExceptionHandler → 400
- Business errors → Result<T> → ResultExtensions → appropriate status
- Unexpected → GlobalExceptionHandler → 500 with logging

## DO NOT
- Duplicate logging across layers
- Log validation at Error level
- Add try-catch when GlobalExceptionHandler handles it
- Suggest code without checking existing patterns
- Use .Result or .Wait()

