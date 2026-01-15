---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Conversation API code example


```csharp
var request = new ConversationRequest
{
    Messages = [
        new Message { Role = "user", Content = "Explain Dapr, the distributed application runtime in simple terms." }
    ]
};

var response = await daprClient.ConversationAsync(
    "myconversation", request);
```
