# Daily HTTP Request Worker
This worker is designed to run daily at 3 AM and perform an HTTP request to an external API. 
It's a simple background process built with .NET, ideal for automating daily integrations or data pulls.

## How it works

```mermaid
flowchart TD
    A[Start Worker] --> B{DelayTimeToStart > 0?}
    B -->|Yes| C[Wait initial delay]
    B -->|No| D[Worker started]
    C --> D
    D --> E{DailyTime valid?}
    E -->|Yes| F[Wait until next DailyTime]
    E -->|No| G[GET External API]
    F --> G
    G --> H[Log success / warning / error]
    H --> I{Cancelled?}
    I -->|Yes| J[Stop Worker]
    I -->|No| K{Schedule mode}
    K -->|DailyTime| F
    K -->|Interval| L[Wait TimeBetweenExecution]
    L --> G
```

## How to run locally
1. Clone this repository
2. Restore dependencies

```bash
dotnet restore
```
3. Run the application:
```bash
dotnet run
```

The worker will execute the request and then finish.
1. Clone this repository
2. Restore dependencies

```bash
dotnet restore
```
3. Run the application:
```bash
dotnet run
```

The worker will execute the request and then finish.
