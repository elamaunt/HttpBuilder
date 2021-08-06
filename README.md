# HttpBuilder
Http helper to build and observe http requests.

## Get started

#### 0. Installation from Nuget console.

```
Install-Package HttpBuilder -Version 1.0.0
```

#### 1. Create you service.

```csharp
using HttpBuilder;

// Service to create requests and observe them
class MyHttpService : HttpService
{
        protected override void ConfigureHandler(HttpClientHandler handler)
        {
          /// Configure your handler
        }

        protected override void ConfigureClient(HttpClient client)
        {
          /// Configure your client
        }
        
        // Many additional overrides
}
```

#### 2. Use service as start point.

```csharp
var service = new MyHttpService();

// Sample
var result = await service.Build($"api/v3/insurances") // Creates a relative request
  .WithParameter("parameter", parameter) // Custom query parameter
  .Get()
  .Send(token) // Sends the built request
  .ValidateSuccessStatusCode()
  .Json<InsuranceDTO[]>(JsonHelper.Serializer) // Parse json
  .Continue(x => x.FirstOrDefault(y => y.Id == id)) // Additional conversion. May be async
  .Task;
                
// Supports uploading
await service.Build($"api/v3/insurances")
  .Get()
  .Send(HttpCompletionOption.ResponseHeadersRead)
   // Process the loading progress for big files
  .ProcessLoading(token, destinationStream, 4096, (totalBytes, readedBytes, percentage) => { /* reporting */ })
  .ValidateSuccessStatusCode()
  .Task;
```
