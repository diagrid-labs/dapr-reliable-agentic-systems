---
theme: default
layout: default
---

## Code Example


```csharp
var request = new ConversationRequest
{
    Messages = [
        new Message { Role = "user", Content = prompt }
    ]
};

var response = await daprClient.ConversationAsync(
    "myconversation", request);
```
